// src/SaroHub.Core/Services/StockService.cs
using Microsoft.EntityFrameworkCore;
using SaroHub.Core.Abstractions;
using SaroHub.Core.Accounting;
using SaroHub.Core.Dtos;
using SaroHub.Domain;
using SaroHub.Domain.Accounting;
using SaroHub.Domain.Common;
using SaroHub.Domain.Entities;

namespace SaroHub.Core.Services;

public sealed class StockService
{
    private readonly IAppDbFactory _factory;
    private readonly NumberService _numbers;
    private readonly PostingEngine _posting;
    private readonly AuditService _audit;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAppLogger _log;
    private readonly CashSessionService _sessions;

    public StockService(IAppDbFactory factory, NumberService numbers, PostingEngine posting, AuditService audit,
                        ICurrentUser user, IClock clock, IAppLogger log, CashSessionService sessions)
    { _factory = factory; _numbers = numbers; _posting = posting; _audit = audit; _user = user; _clock = clock; _log = log; _sessions = sessions; }

    /// <summary>Applies perpetual weighted-average costing to a receipt of stock.</summary>
    internal static (long newQtyRaw, long newAvgCost) ApplyReceipt(long qtyRaw, long avgCost, long inQtyRaw, long inUnitCost)
    {
        var newQty = qtyRaw + inQtyRaw;
        if (newQty <= 0) return (newQty, inUnitCost);

        var currentValue = Money.Multiply(avgCost, Qty.To(qtyRaw));
        var incomingValue = Money.Multiply(inUnitCost, Qty.To(inQtyRaw));
        var totalValue = currentValue + incomingValue;
        var newAvg = (long)Math.Round((decimal)totalValue / Qty.To(newQty), MidpointRounding.AwayFromZero);
        return (newQty, newAvg);
    }

    public async Task<Result<int>> ReceiveStockAsync(NewStockInRequest req, CancellationToken ct = default)
    {
        if (req.Lines.Count == 0) return Result<int>.Fail("Add at least one product.");

        var session = await _sessions.GetOrOpenAsync(ct);

        using var db = _factory.Create();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var existing = await db.StockIns.FirstOrDefaultAsync(s => s.ClientOperationId == req.ClientOperationId, ct);
            if (existing is not null) return Result<int>.Ok(existing.Id);

            var productIds = req.Lines.Select(l => l.ProductId).Distinct().ToList();
            var products = await db.Products.Where(p => productIds.Contains(p.Id)).ToListAsync(ct);
            var productUnits = await db.ProductUnits.Include(u => u.Unit)
                                       .Where(u => productIds.Contains(u.ProductId)).ToListAsync(ct);

            var doc = new StockIn
            {
                DocumentNo = await _numbers.NextAsync(db, "StockIn", ct),
                ClientOperationId = req.ClientOperationId,
                AtUtc = _clock.UtcNow,
                SupplierId = req.SupplierId,
                SupplierInvoiceNo = req.SupplierInvoiceNo,
                IsOpening = req.IsOpening,
                UserId = _user.UserId,
                CashSessionId = session.Id,
                PaidMethod = req.PaidMethod,
                CreatedByUserId = _user.UserId
            };

            long subTotal = 0;

            foreach (var line in req.Lines)
            {
                if (line.Qty <= 0) return Result<int>.Fail("Quantity must be more than zero.");
                var product = products.FirstOrDefault(p => p.Id == line.ProductId);
                if (product is null) return Result<int>.Fail("Product not found.");

                var pu = productUnits.FirstOrDefault(u => u.ProductId == product.Id && u.UnitId == line.UnitId)
                         ?? productUnits.First(u => u.ProductId == product.Id && u.IsBase);

                var qtyRaw = Qty.From(line.Qty);
                var qtyBaseRaw = Qty.From(line.Qty * Qty.To(pu.FactorToBaseRaw));
                var lineTotal = Money.Multiply(line.UnitCostPaisa, line.Qty);
                subTotal += lineTotal;

                doc.Items.Add(new StockInItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    UnitId = pu.UnitId,
                    UnitSymbol = pu.Unit?.Symbol ?? "",
                    FactorToBaseRaw = pu.FactorToBaseRaw,
                    QtyRaw = qtyRaw,
                    QtyBaseRaw = qtyBaseRaw,
                    UnitCostPaisa = line.UnitCostPaisa,
                    LineTotalPaisa = lineTotal
                });
            }

            doc.SubTotalPaisa = subTotal;
            doc.DiscountPaisa = req.DiscountPaisa;
            doc.TotalPaisa = subTotal - req.DiscountPaisa;
            if (doc.TotalPaisa < 0) return Result<int>.Fail("Discount is more than the total.");

            doc.PaidPaisa = req.IsOpening ? doc.TotalPaisa : Math.Min(req.PaidPaisa, doc.TotalPaisa);
            var credit = doc.TotalPaisa - doc.PaidPaisa;
            if (credit > 0 && req.SupplierId is null)
                return Result<int>.Fail("Select a supplier for a credit purchase.");

            db.StockIns.Add(doc);
            await db.SaveChangesAsync(ct);

            // Inventory value must match the amount posted to the Inventory account.
            var discountFactor = subTotal == 0 ? 1m : (decimal)doc.TotalPaisa / subTotal;
            long inventoryValue = 0;

            foreach (var item in doc.Items)
            {
                var product = products.First(p => p.Id == item.ProductId);
                var lineValue = (long)Math.Round(item.LineTotalPaisa * discountFactor, MidpointRounding.AwayFromZero);
                inventoryValue += lineValue;

                var unitCostPerBase = item.QtyBaseRaw == 0 ? 0
                    : (long)Math.Round((decimal)lineValue / Qty.To(item.QtyBaseRaw), MidpointRounding.AwayFromZero);

                var (newQty, newAvg) = ApplyReceipt(product.StockQtyRaw, product.AvgCostPaisa, item.QtyBaseRaw, unitCostPerBase);
                product.StockQtyRaw = newQty;
                product.AvgCostPaisa = newAvg;
                product.PurchasePricePaisa = unitCostPerBase;

                db.StockTransactions.Add(new StockTransaction
                {
                    AtUtc = doc.AtUtc,
                    ProductId = product.Id,
                    MovementType = req.IsOpening ? StockMovementType.Opening : StockMovementType.PurchaseIn,
                    QtyBaseRaw = item.QtyBaseRaw,
                    UnitCostPaisa = unitCostPerBase,
                    ValuePaisa = lineValue,
                    BalanceAfterRaw = newQty,
                    AvgCostAfterPaisa = newAvg,
                    SourceType = nameof(StockIn),
                    SourceId = doc.Id,
                    DocumentNo = doc.DocumentNo,
                    UserId = _user.UserId
                });
            }

            var draft = new JournalDraft
            {
                EntryDateUtc = doc.AtUtc,
                Description = req.IsOpening ? "Opening stock" : $"Purchase {doc.DocumentNo}",
                SourceType = nameof(StockIn),
                SourceId = doc.Id,
                DocumentNo = doc.DocumentNo
            };

            draft.Debit(AccountCodes.Inventory, inventoryValue, memo: "Stock received");

            if (req.IsOpening)
            {
                draft.Credit(AccountCodes.OpeningBalanceEq, inventoryValue);
            }
            else
            {
                if (doc.PaidPaisa > 0)
                    draft.Credit(PostingEngine.AccountCodeForPaymentMethod(req.PaidMethod), doc.PaidPaisa,
                                 supplierId: req.SupplierId, memo: "Paid on purchase");
                if (credit > 0)
                    draft.Credit(AccountCodes.AccountsPayable, credit, supplierId: req.SupplierId, memo: "Purchase on credit");
            }

            await _posting.PostAsync(db, draft, _user.UserId, ct);

            if (credit > 0 && req.SupplierId is not null)
            {
                var supplier = await db.Suppliers.FirstAsync(s => s.Id == req.SupplierId, ct);
                supplier.BalancePaisa += credit;
            }

            _audit.Add(db, _user, req.IsOpening ? "OpeningStockRecorded" : "PurchaseCreated",
                       nameof(StockIn), doc.DocumentNo, null, Money.Format(doc.TotalPaisa));

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result<int>.Ok(doc.Id);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _log.Error("Stock receive failed", ex);
            return Result<int>.Fail("Could not save. Your data is safe.");
        }
    }

    public async Task<Result> AdjustAsync(int productId, decimal qtyBase, StockMovementType type, string reason, CancellationToken ct = default)
    {
        if (qtyBase <= 0) return Result.Fail("Quantity must be more than zero.");
        if (string.IsNullOrWhiteSpace(reason)) return Result.Fail("Reason is required.");

        using var db = _factory.Create();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var product = await db.Products.FirstAsync(p => p.Id == productId, ct);
            var qtyRaw = Qty.From(qtyBase);
            var isIncrease = type == StockMovementType.AdjustmentIn;
            var signed = isIncrease ? qtyRaw : -qtyRaw;

            if (!isIncrease && product.StockQtyRaw < qtyRaw)
                return Result.Fail("Not enough stock to remove.");

            long value;
            long newQty, newAvg;

            if (isIncrease)
            {
                (newQty, newAvg) = ApplyReceipt(product.StockQtyRaw, product.AvgCostPaisa, qtyRaw, product.AvgCostPaisa);
                value = Money.Multiply(product.AvgCostPaisa, qtyBase);
            }
            else
            {
                value = Money.Multiply(product.AvgCostPaisa, qtyBase);
                newQty = product.StockQtyRaw - qtyRaw;
                newAvg = product.AvgCostPaisa;
            }

            product.StockQtyRaw = newQty;
            product.AvgCostPaisa = newAvg;

            var docNo = await _numbers.NextAsync(db, "Adjustment", ct);

            db.StockAdjustments.Add(new StockAdjustment
            {
                DocumentNo = docNo,
                AtUtc = _clock.UtcNow,
                ProductId = productId,
                MovementType = type,
                QtyBaseRaw = signed,
                ValuePaisa = value,
                Reason = reason,
                UserId = _user.UserId
            });

            db.StockTransactions.Add(new StockTransaction
            {
                AtUtc = _clock.UtcNow,
                ProductId = productId,
                MovementType = type,
                QtyBaseRaw = signed,
                UnitCostPaisa = product.AvgCostPaisa,
                ValuePaisa = value,
                BalanceAfterRaw = newQty,
                AvgCostAfterPaisa = newAvg,
                SourceType = nameof(StockAdjustment),
                DocumentNo = docNo,
                UserId = _user.UserId,
                Note = reason
            });

            var draft = new JournalDraft
            {
                EntryDateUtc = _clock.UtcNow,
                Description = $"Stock {type} - {product.Name}: {reason}",
                SourceType = nameof(StockAdjustment),
                DocumentNo = docNo
            };

            if (isIncrease)
                draft.Debit(AccountCodes.Inventory, value).Credit(AccountCodes.OpeningBalanceEq, value);
            else
                draft.Debit(AccountCodes.StockWriteOff, value).Credit(AccountCodes.Inventory, value);

            await _posting.PostAsync(db, draft, _user.UserId, ct);
            _audit.Add(db, _user, "StockAdjusted", nameof(Product), product.Name, null, $"{type} {qtyBase} ({reason})");

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _log.Error("Stock adjust failed", ex);
            return Result.Fail("Could not adjust stock. Your data is safe.");
        }
    }

    public async Task<List<LowStockRow>> LowStockAsync(int take = 20, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        return await db.Products.AsNoTracking().Include(p => p.BaseUnit)
            .Where(p => p.IsActive && p.MinStockQtyRaw > 0 && p.StockQtyRaw <= p.MinStockQtyRaw)
            .OrderBy(p => p.StockQtyRaw).Take(take)
            .Select(p => new LowStockRow(p.Id, p.Name, p.StockQtyRaw, p.MinStockQtyRaw, p.BaseUnit!.Symbol))
            .ToListAsync(ct);
    }

    public async Task<List<StockTransaction>> HistoryAsync(int productId, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        return await db.StockTransactions.AsNoTracking()
            .Where(t => t.ProductId == productId)
            .OrderByDescending(t => t.Id).Take(300).ToListAsync(ct);
    }
}