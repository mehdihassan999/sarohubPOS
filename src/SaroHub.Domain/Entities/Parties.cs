// src/SaroHub.Domain/Entities/Parties.cs
using SaroHub.Domain.Common;

namespace SaroHub.Domain.Entities;

public class Customer : Entity
{
    public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public long CreditLimitPaisa { get; set; }
    /// <summary>Cache only. The ledger is the source of truth.</summary>
    public long BalancePaisa { get; set; }
    public bool IsWalkIn { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Supplier : Entity
{
    public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    /// <summary>Cache only. Positive = we owe the supplier.</summary>
    public long BalancePaisa { get; set; }
    public bool IsActive { get; set; } = true;
}