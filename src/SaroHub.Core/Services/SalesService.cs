// src/SaroHub.Core/Services/SalesService.cs
using Microsoft.EntityFrameworkCore;
using SaroHub.Core.Abstractions;
using SaroHub.Core.Accounting;
using SaroHub.Core.Dtos;
using SaroHub.Domain;
using SaroHub.Domain.Accounting;
using SaroHub.Domain.Common;
using SaroHub.Domain.Entities;

namespace SaroHub.Core.Services;

public sealed class SalesService
{
    private readonly IAppDbFactory _factory;
    private readonly NumberService _numbers;
    private readonly PostingEngine _posting;
    private readonly AuditService _audit;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAppLogger _log;
    private readonly SettingsService _settings;
    private readonly CashSessionService _sessions;

    public SalesService(IAppDbFactory factory, NumberService numbers, PostingEngine posting, AuditService audit,
                        ICurrentUser user, IClock clock, IAppLogger log, SettingsService settings, CashSessionService sessions)
    { _factory = factory; _numbers = numbers; _posting = posting; _audit = audit; _user = user; _clock = clock; _log = log; _settings = settings; _sessions = sessions; }

    /// <summary>
    /// Saves invoice, items, payments, stock movements, COGS, journal entries and audit in ONE transaction.
    /// </summary>
    public async Task<Result<SaleResult>> CreateSaleAsync(NewSaleRequest req, CancellationToken ct = default)
    {
        if (!_user.Has(Domain.Security.Permissions.SaleCreate))
            return Result<SaleResult>.Fail("You do not have permission to make a sale.");
        if (req.Lines.Count == 0)
            return Result<SaleResult>.Fail("Add at least one product.");

        var session = await _sessions.GetOrOpenAsync(ct);
        var allowNegative = _settings.GetBool(SettingsService.AllowNegativeStock);

        using var db = _factory.Create();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // 1. Idempotency: retrying a failed-looking save must not create a second invoice.
            var existing = await db.Sales.AsNoTracking()
                .FirstOrDefaultAsync(s => s.ClientOperationId == req.ClientOperationId, ct);
            if (existing is not null)
                return Result<SaleResult>.Ok(new SaleResult(existing.Id, existing.InvoiceNo, existing.TotalPaisa, 0, existing.KhataPaisa));

            // 2. Load and validate
            var productIds = req.Lines.Select(l => l.ProductId).Distinct().ToList();
            var products = await db.Products.Where(p => productIds.Contains(p.Id)).ToListAsync(ct);
            var productUnits = await db.ProductUnits.Include(u => u.Unit)
                                       .Where(u => productIds.Contains(u.ProductId)).ToListAsync(ct);

            var sale = new Sale
            {
                InvoiceNo = await _numbers.NextAsync(db, "Sale", ct),
                ClientOperationId = req.ClientOperationId,
                SaleAtUtc = _clock.UtcNow,
                CustomerId = req.CustomerId,
                UserId = _user.UserId,
                CashSessionId = session.Id,
                TaxPaisa = req.TaxPaisa,
                Note = req.Note,
                CreatedByUserId = _user.UserId
            };

            long gross = 0, lineDiscounts = 0, cogs = 0;

            foreach (var line in req.Lines)
            {
                if (line.Qty <= 0) return Result<SaleResult>.Fail("Quantity must be more than zero.");

                var product = products.FirstOrDefault(p => p.Id == line.ProductId);
                if (product is null) return Result<SaleResult>.Fail("Product not found.");

                // UnitId == 0 means "use base unit" (sent from POS when no explicit unit selected)
                var pu = (line.UnitId > 0
                             ? productUnits.FirstOrDefault(u => u.ProductId == product.Id && u.UnitId == line.UnitId)
                             : null)
                         ?? productUnits.First(u => u.ProductId == product.Id && u.IsBase);

                var qtyBaseRaw = Qty.From(line.Qty * Qty.To(pu.FactorToBaseRaw));

                if (!allowNegative && product.StockQtyRaw < qtyBaseRaw)
                    return Result<SaleResult>.Fail($"Not enough stock for {product.Name}. Available: {Qty.Format(product.StockQtyRaw)}");

                var lineGross = Money.Multiply(line.UnitPricePaisa, line.Qty);
                if (line.DiscountPaisa > lineGross) return Result<SaleResult>.Fail("Line discount is too large.");

                var lineTotal = lineGross - line.DiscountPaisa;
                var lineCost = Money.Multiply(product.AvgCostPaisa, Qty.To(qtyBaseRaw));

                gross += lineGross;
                lineDiscounts += line.DiscountPaisa;
                cogs += lineCost;

                sale.Items.Add(new SaleItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    UnitId = pu.UnitId,
                    UnitSymbol = pu.Unit?.Symbol ?? "",
                    FactorToBaseRaw = pu.FactorToBaseRaw,
                    QtyRaw = Qty.From(line.Qty),
                    QtyBaseRaw = qtyBaseRaw,
                    UnitPricePaisa = line.UnitPricePaisa,
                    DiscountPaisa = line.DiscountPaisa,
                    LineTotalPaisa = lineTotal,
                    UnitCostPaisa = product.AvgCostPaisa,
                    LineCostPaisa = lineCost
                });
            }

            var totalDiscount = lineDiscounts + req.InvoiceDiscountPaisa;
            var total = gross - totalDiscount + req.TaxPaisa;
            if (total < 0) return Result<SaleResult>.Fail("Discount is more than the bill.");

            sale.GrossPaisa = gross;
            sale.DiscountPaisa = totalDiscount;
            sale.TotalPaisa = total;
            sale.CostPaisa = cogs;

            // 3. Payments
            var khata = req.Payments.Where(p => p.Method == PaymentMethod.Khata).Sum(p => p.AmountPaisa);
            var cashTendered = req.Payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.AmountPaisa);
            var nonCashNonKhata = req.Payments.Where(p => p.Method != PaymentMethod.Cash && p.Method != PaymentMethod.Khata)
                                              .Sum(p => p.AmountPaisa);

            if (req.Payments.Any(p => p.AmountPaisa <= 0))
                return Result<SaleResult>.Fail("Payment amounts must be more than zero.");

            var due = total - khata - nonCashNonKhata;
            if (due < 0) return Result<SaleResult>.Fail("Payment is more than the bill total.");
            if (cashTendered < due) return Result<SaleResult>.Fail("Payment is less than the bill total.");

            var change = cashTendered - due;
            var cashApplied = due;

            if (khata > 0)
            {
                if (req.CustomerId is null || req.CustomerId == 0)
                    return Result<SaleResult>.Fail("Select a customer for a khata sale.");

                var customer = await db.Customers.FirstAsync(c => c.Id == req.CustomerId, ct);
                if (customer.CreditLimitPaisa > 0 && customer.BalancePaisa + khata > customer.CreditLimitPaisa)
                    return Result<SaleResult>.Fail(
                        $"Credit limit reached for {customer.Name}. Limit: {Money.Format(customer.CreditLimitPaisa)}");
            }

            sale.KhataPaisa = khata;
            sale.PaidPaisa = cashApplied + nonCashNonKhata;

            if (cashApplied > 0)
                sale.Payments.Add(new SalePayment { Method = PaymentMethod.Cash, AmountPaisa = cashApplied });
            foreach (var p in req.Payments.Where(p => p.Method != PaymentMethod.Cash && p.Method != PaymentMethod.Khata))
                sale.Payments.Add(new SalePayment { Method = p.Method, AmountPaisa = p.AmountPaisa, Reference = p.Reference });
            if (khata > 0)
                sale.Payments.Add(new SalePayment { Method = PaymentMethod.Khata, AmountPaisa = khata });

            db.Sales.Add(sale);
            await db.SaveChangesAsync(ct);   // need Sale.Id for stock + journal links

            // 4. Stock out + product balances
            foreach (var item in sale.Items)
            {
                var product = products.First(p => p.Id == item.ProductId);
                product.StockQtyRaw -= item.QtyBaseRaw;

                db.StockTransactions.Add(new StockTransaction
                {
                    AtUtc = sale.SaleAtUtc,
                    ProductId = product.Id,
                    MovementType = StockMovementType.SaleOut,
                    QtyBaseRaw = -item.QtyBaseRaw,
                    UnitCostPaisa = item.UnitCostPaisa,
                    ValuePaisa = item.LineCostPaisa,
                    BalanceAfterRaw = product.StockQtyRaw,
                    AvgCostAfterPaisa = product.AvgCostPaisa,
                    SourceType = nameof(Sale),
                    SourceId = sale.Id,
                    DocumentNo = sale.InvoiceNo,
                    UserId = _user.UserId
                });
            }

            // 5a. Revenue entry
            var revenue = new JournalDraft
            {
                EntryDateUtc = sale.SaleAtUtc,
                Description = $"Sale {sale.InvoiceNo}",
                SourceType = nameof(Sale),
                SourceId = sale.Id,
                DocumentNo = sale.InvoiceNo
            };

            if (cashApplied > 0) revenue.Debit(AccountCodes.CashInHand, cashApplied, memo: "Cash received");
            foreach (var p in req.Payments.Where(p => p.Method != PaymentMethod.Cash && p.Method != PaymentMethod.Khata))
                revenue.Debit(PostingEngine.AccountCodeForPaymentMethod(p.Method), p.AmountPaisa, memo: p.Method.ToString());
            if (khata > 0) revenue.Debit(AccountCodes.AccountsReceivable, khata, customerId: req.CustomerId, memo: "Khata sale");
            if (totalDiscount > 0) revenue.Debit(AccountCodes.SalesDiscount, totalDiscount, memo: "Discount given");

            revenue.Credit(AccountCodes.Sales, gross, memo: "Sales");
            if (req.TaxPaisa > 0) revenue.Credit(AccountCodes.TaxPayable, req.TaxPaisa, memo: "Tax");

            await _posting.PostAsync(db, revenue, _user.UserId, ct);

            // 5b. Cost of goods sold entry
            if (cogs > 0)
            {
                var costEntry = new JournalDraft
                {
                    EntryDateUtc = sale.SaleAtUtc,
                    Description = $"Cost of goods sold {sale.InvoiceNo}",
                    SourceType = nameof(Sale),
                    SourceId = sale.Id,
                    DocumentNo = sale.InvoiceNo
                };
                costEntry.Debit(AccountCodes.CostOfGoodsSold, cogs).Credit(AccountCodes.Inventory, cogs);
                await _posting.PostAsync(db, costEntry, _user.UserId, ct);
            }

            // 6. Customer balance cache
            if (khata > 0 && req.CustomerId is not null)
            {
                var customer = await db.Customers.FirstAsync(c => c.Id == req.CustomerId, ct);
                customer.BalancePaisa += khata;
            }

            _audit.Add(db, _user, "SaleCreated", nameof(Sale), sale.InvoiceNo, null,
                       $"Total {Money.Format(total)} / Khata {Money.Format(khata)}");

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            _log.Info($"Sale {sale.InvoiceNo} posted. Total={Money.Format(total)}");
            return Result<SaleResult>.Ok(new SaleResult(sale.Id, sale.InvoiceNo, total, change, khata));
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _log.Error("Sale failed", ex);
            return Result<SaleResult>.Fail("Something went wrong. Your data is safe. Please try again.");
        }
    }

    public async Task<Sale?> GetSaleAsync(int saleId, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        return await db.Sales.AsNoTracking()
            .Include(s => s.Items).Include(s => s.Payments).Include(s => s.Customer)
            .FirstOrDefaultAsync(s => s.Id == saleId, ct);
    }

    public async Task<List<RecentSaleRow>> RecentAsync(int take = 10, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        return await db.Sales.AsNoTracking().Include(s => s.Customer)
            .OrderByDescending(s => s.Id).Take(take)
            .Select(s => new RecentSaleRow(s.Id, s.InvoiceNo, s.SaleAtUtc.ToLocalTime(),
                                           s.Customer != null ? s.Customer.Name : "Walk-in", s.TotalPaisa))
            .ToListAsync(ct);
    }

    public async Task<List<Sale>> ListAsync(DateTime fromLocal, DateTime toLocal, CancellationToken ct = default)
    {
        var from = fromLocal.Date.ToUniversalTime();
        var to = toLocal.Date.AddDays(1).AddTicks(-1).ToUniversalTime();

        using var db = _factory.Create();
        return await db.Sales.AsNoTracking().Include(s => s.Customer)
            .Where(s => s.SaleAtUtc >= from && s.SaleAtUtc <= to)
            .OrderByDescending(s => s.Id).ToListAsync(ct);
    }
}