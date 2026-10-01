// src/SaroHub.Core/Services/PaymentService.cs
using Microsoft.EntityFrameworkCore;
using SaroHub.Core.Abstractions;
using SaroHub.Core.Accounting;
using SaroHub.Domain;
using SaroHub.Domain.Accounting;
using SaroHub.Domain.Common;
using SaroHub.Domain.Entities;

namespace SaroHub.Core.Services;

public sealed class PaymentService
{
    private readonly IAppDbFactory _factory;
    private readonly NumberService _numbers;
    private readonly PostingEngine _posting;
    private readonly AuditService _audit;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAppLogger _log;
    private readonly CashSessionService _sessions;

    public PaymentService(IAppDbFactory factory, NumberService numbers, PostingEngine posting, AuditService audit,
                          ICurrentUser user, IClock clock, IAppLogger log, CashSessionService sessions)
    { _factory = factory; _numbers = numbers; _posting = posting; _audit = audit; _user = user; _clock = clock; _log = log; _sessions = sessions; }

    /// <summary>Money received from a customer against khata.</summary>
    public async Task<Result<string>> ReceiveFromCustomerAsync(int customerId, long amountPaisa, PaymentMethod method,
                                                               string? reference, string? note, Guid opId, CancellationToken ct = default)
    {
        if (!_user.Has(Domain.Security.Permissions.PaymentReceive))
            return Result<string>.Fail("You do not have permission to receive payments.");
        if (amountPaisa <= 0) return Result<string>.Fail("Enter an amount.");
        if (method == PaymentMethod.Khata) return Result<string>.Fail("Choose a real payment method.");

        var session = await _sessions.GetOrOpenAsync(ct);

        using var db = _factory.Create();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var existing = await db.PartyPayments.FirstOrDefaultAsync(p => p.ClientOperationId == opId, ct);
            if (existing is not null) return Result<string>.Ok(existing.VoucherNo);

            var customer = await db.Customers.FirstAsync(c => c.Id == customerId, ct);

            var payment = new PartyPayment
            {
                VoucherNo = await _numbers.NextAsync(db, "Payment", ct),
                ClientOperationId = opId,
                AtUtc = _clock.UtcNow,
                PartyType = PartyType.Customer,
                PartyId = customerId,
                PartyName = customer.Name,
                Method = method,
                AmountPaisa = amountPaisa,
                Reference = reference,
                Note = note,
                UserId = _user.UserId,
                CashSessionId = session.Id
            };
            db.PartyPayments.Add(payment);
            await db.SaveChangesAsync(ct);

            var draft = new JournalDraft
            {
                EntryDateUtc = payment.AtUtc,
                Description = $"Payment received from {customer.Name}",
                SourceType = nameof(PartyPayment),
                SourceId = payment.Id,
                DocumentNo = payment.VoucherNo
            };

            draft.Debit(PostingEngine.AccountCodeForPaymentMethod(method), amountPaisa, memo: method.ToString());

            // Paying more than owed becomes a customer advance (a liability), never a negative receivable.
            var applied = Math.Min(amountPaisa, Math.Max(customer.BalancePaisa, 0));
            var advance = amountPaisa - applied;

            if (applied > 0) draft.Credit(AccountCodes.AccountsReceivable, applied, customerId: customerId, memo: "Khata payment");
            if (advance > 0) draft.Credit(AccountCodes.CustomerAdvances, advance, customerId: customerId, memo: "Advance");

            await _posting.PostAsync(db, draft, _user.UserId, ct);

            customer.BalancePaisa -= applied;

            _audit.Add(db, _user, "PaymentReceived", nameof(Customer), customer.Name, null, Money.Format(amountPaisa));

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result<string>.Ok(payment.VoucherNo);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _log.Error("Receive payment failed", ex);
            return Result<string>.Fail("Could not save the payment. Your data is safe.");
        }
    }

    /// <summary>Money paid to a supplier.</summary>
    public async Task<Result<string>> PaySupplierAsync(int supplierId, long amountPaisa, PaymentMethod method,
                                                       string? reference, string? note, Guid opId, CancellationToken ct = default)
    {
        if (!_user.Has(Domain.Security.Permissions.PaymentSend))
            return Result<string>.Fail("You do not have permission to pay suppliers.");
        if (amountPaisa <= 0) return Result<string>.Fail("Enter an amount.");
        if (method == PaymentMethod.Khata) return Result<string>.Fail("Choose a real payment method.");

        var session = await _sessions.GetOrOpenAsync(ct);

        using var db = _factory.Create();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var existing = await db.PartyPayments.FirstOrDefaultAsync(p => p.ClientOperationId == opId, ct);
            if (existing is not null) return Result<string>.Ok(existing.VoucherNo);

            var supplier = await db.Suppliers.FirstAsync(s => s.Id == supplierId, ct);

            var payment = new PartyPayment
            {
                VoucherNo = await _numbers.NextAsync(db, "Payment", ct),
                ClientOperationId = opId,
                AtUtc = _clock.UtcNow,
                PartyType = PartyType.Supplier,
                PartyId = supplierId,
                PartyName = supplier.Name,
                Method = method,
                AmountPaisa = amountPaisa,
                Reference = reference,
                Note = note,
                UserId = _user.UserId,
                CashSessionId = session.Id
            };
            db.PartyPayments.Add(payment);
            await db.SaveChangesAsync(ct);

            var draft = new JournalDraft
            {
                EntryDateUtc = payment.AtUtc,
                Description = $"Payment to {supplier.Name}",
                SourceType = nameof(PartyPayment),
                SourceId = payment.Id,
                DocumentNo = payment.VoucherNo
            };

            draft.Debit(AccountCodes.AccountsPayable, amountPaisa, supplierId: supplierId, memo: "Supplier payment")
                 .Credit(PostingEngine.AccountCodeForPaymentMethod(method), amountPaisa, memo: method.ToString());

            await _posting.PostAsync(db, draft, _user.UserId, ct);

            supplier.BalancePaisa -= amountPaisa;
            _audit.Add(db, _user, "SupplierPaid", nameof(Supplier), supplier.Name, null, Money.Format(amountPaisa));

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result<string>.Ok(payment.VoucherNo);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _log.Error("Supplier payment failed", ex);
            return Result<string>.Fail("Could not save the payment. Your data is safe.");
        }
    }
}