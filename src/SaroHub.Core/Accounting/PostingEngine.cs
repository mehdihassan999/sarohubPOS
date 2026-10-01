// src/SaroHub.Core/Accounting/PostingEngine.cs
using Microsoft.EntityFrameworkCore;
using SaroHub.Core.Abstractions;
using SaroHub.Domain;
using SaroHub.Domain.Accounting;
using SaroHub.Domain.Entities;

namespace SaroHub.Core.Accounting;

public sealed class JournalLineDraft
{
    public string AccountCode { get; init; } = "";
    public long Debit { get; init; }
    public long Credit { get; init; }
    public int? CustomerId { get; init; }
    public int? SupplierId { get; init; }
    public string? Memo { get; init; }
}

public sealed class JournalDraft
{
    public DateTime EntryDateUtc { get; init; } = DateTime.UtcNow;
    public string Description { get; init; } = "";
    public string SourceType { get; init; } = "";
    public int? SourceId { get; set; }
    public string? DocumentNo { get; init; }
    public List<JournalLineDraft> Lines { get; } = new();

    public JournalDraft Debit(string code, long amount, int? customerId = null, int? supplierId = null, string? memo = null)
    {
        if (amount > 0) Lines.Add(new JournalLineDraft { AccountCode = code, Debit = amount, CustomerId = customerId, SupplierId = supplierId, Memo = memo });
        return this;
    }

    public JournalDraft Credit(string code, long amount, int? customerId = null, int? supplierId = null, string? memo = null)
    {
        if (amount > 0) Lines.Add(new JournalLineDraft { AccountCode = code, Credit = amount, CustomerId = customerId, SupplierId = supplierId, Memo = memo });
        return this;
    }
}

/// <summary>
/// Single place where journal entries are created. Refuses to persist an unbalanced entry.
/// </summary>
public sealed class PostingEngine
{
    private readonly NumberService _numbers;

    public PostingEngine(NumberService numbers) => _numbers = numbers;

    public static string AccountCodeForPaymentMethod(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => AccountCodes.CashInHand,
        PaymentMethod.Bank or PaymentMethod.Raast => AccountCodes.BankAccount,
        PaymentMethod.Easypaisa or PaymentMethod.JazzCash => AccountCodes.MobileWallet,
        PaymentMethod.Card => AccountCodes.CardClearing,
        _ => throw new InvalidOperationException($"Payment method {method} does not post to a cash account.")
    };

    public async Task<JournalEntry> PostAsync(IAppDbContext db, JournalDraft draft, int userId, CancellationToken ct = default)
    {
        if (draft.Lines.Count < 2)
            throw new InvalidOperationException("A journal entry needs at least two lines.");

        var debit = draft.Lines.Sum(l => l.Debit);
        var credit = draft.Lines.Sum(l => l.Credit);

        if (debit != credit)
            throw new InvalidOperationException($"Unbalanced journal entry. Debit {debit} != Credit {credit}.");
        if (debit == 0)
            throw new InvalidOperationException("A journal entry cannot be zero.");
        if (draft.Lines.Any(l => l.Debit < 0 || l.Credit < 0 || (l.Debit > 0 && l.Credit > 0)))
            throw new InvalidOperationException("Invalid journal line amounts.");

        var codes = draft.Lines.Select(l => l.AccountCode).Distinct().ToList();
        var accounts = await db.Accounts.Where(a => codes.Contains(a.Code)).ToListAsync(ct);

        var missing = codes.Except(accounts.Select(a => a.Code)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException("Missing accounts: " + string.Join(", ", missing));

        var entry = new JournalEntry
        {
            EntryNo = await _numbers.NextAsync(db, "Journal", ct),
            EntryDateUtc = draft.EntryDateUtc,
            Description = draft.Description,
            SourceType = draft.SourceType,
            SourceId = draft.SourceId,
            DocumentNo = draft.DocumentNo,
            UserId = userId,
            IsPosted = true,
            CreatedByUserId = userId
        };

        foreach (var l in draft.Lines)
        {
            var acc = accounts.First(a => a.Code == l.AccountCode);
            if (!acc.IsPostable) throw new InvalidOperationException($"Account {acc.Code} is not postable.");
            entry.Lines.Add(new JournalEntryLine
            {
                AccountId = acc.Id,
                DebitPaisa = l.Debit,
                CreditPaisa = l.Credit,
                CustomerId = l.CustomerId,
                SupplierId = l.SupplierId,
                Memo = l.Memo
            });
        }

        db.JournalEntries.Add(entry);
        return entry;
    }
}