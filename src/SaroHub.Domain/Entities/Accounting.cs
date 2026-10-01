// src/SaroHub.Domain/Entities/Accounting.cs
using SaroHub.Domain.Common;

namespace SaroHub.Domain.Entities;

public class Account : Entity
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public AccountType Type { get; set; }
    public int? ParentId { get; set; }
    public Account? Parent { get; set; }
    public bool IsPostable { get; set; } = true;
    public bool IsSystem { get; set; }

    public bool IsDebitNormal => Type is AccountType.Asset or AccountType.Expense;
}

public class JournalEntry : Entity
{
    public string EntryNo { get; set; } = "";
    public DateTime EntryDateUtc { get; set; } = DateTime.UtcNow;
    public string Description { get; set; } = "";
    public string SourceType { get; set; } = "";
    public int? SourceId { get; set; }
    public string? DocumentNo { get; set; }
    public int UserId { get; set; }
    public bool IsPosted { get; set; } = true;
    public int? ReversesEntryId { get; set; }

    public List<JournalEntryLine> Lines { get; set; } = new();

    public long TotalDebit => Lines.Sum(l => l.DebitPaisa);
    public long TotalCredit => Lines.Sum(l => l.CreditPaisa);
}

public class JournalEntryLine : Entity
{
    public int JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }
    public int AccountId { get; set; }
    public Account? Account { get; set; }
    public long DebitPaisa { get; set; }
    public long CreditPaisa { get; set; }
    public int? CustomerId { get; set; }
    public int? SupplierId { get; set; }
    public string? Memo { get; set; }
}