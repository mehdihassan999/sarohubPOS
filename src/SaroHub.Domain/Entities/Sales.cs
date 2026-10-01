// src/SaroHub.Domain/Entities/Sales.cs
using SaroHub.Domain.Common;

namespace SaroHub.Domain.Entities;

public class Sale : Entity
{
    public string InvoiceNo { get; set; } = "";
    public Guid ClientOperationId { get; set; }
    public DateTime SaleAtUtc { get; set; } = DateTime.UtcNow;
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int UserId { get; set; }
    public int? CashSessionId { get; set; }

    public long GrossPaisa { get; set; }       // sum(qty * unit price), before discounts
    public long DiscountPaisa { get; set; }    // line + invoice discounts
    public long TaxPaisa { get; set; }
    public long TotalPaisa { get; set; }       // gross - discount + tax
    public long PaidPaisa { get; set; }        // non-khata payments
    public long KhataPaisa { get; set; }       // credit portion
    public long CostPaisa { get; set; }        // COGS at time of sale

    public SaleStatus Status { get; set; } = SaleStatus.Posted;
    public string? Note { get; set; }

    public List<SaleItem> Items { get; set; } = new();
    public List<SalePayment> Payments { get; set; } = new();
}

public class SaleItem : Entity
{
    public int SaleId { get; set; }
    public Sale? Sale { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public string ProductName { get; set; } = "";

    public int UnitId { get; set; }
    public string UnitSymbol { get; set; } = "";
    /// <summary>Snapshot of the conversion factor so history stays correct.</summary>
    public long FactorToBaseRaw { get; set; }

    public long QtyRaw { get; set; }        // in the chosen unit
    public long QtyBaseRaw { get; set; }    // in base units
    public long UnitPricePaisa { get; set; }
    public long DiscountPaisa { get; set; }
    public long LineTotalPaisa { get; set; }
    public long UnitCostPaisa { get; set; } // avg cost per base unit at sale time
    public long LineCostPaisa { get; set; }
}

public class SalePayment : Entity
{
    public int SaleId { get; set; }
    public Sale? Sale { get; set; }
    public PaymentMethod Method { get; set; }
    public long AmountPaisa { get; set; }
    public string? Reference { get; set; }
}

/// <summary>Khata payment received from a customer or paid to a supplier.</summary>
public class PartyPayment : Entity
{
    public string VoucherNo { get; set; } = "";
    public Guid ClientOperationId { get; set; }
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
    public PartyType PartyType { get; set; }
    public int PartyId { get; set; }
    public string PartyName { get; set; } = "";
    public PaymentMethod Method { get; set; }
    public long AmountPaisa { get; set; }
    public string? Reference { get; set; }
    public string? Note { get; set; }
    public int UserId { get; set; }
    public int? CashSessionId { get; set; }
}

public class ExpenseCategory : Entity
{
    public string Name { get; set; } = "";
    public string AccountCode { get; set; } = "";
}

public class Expense : Entity
{
    public string VoucherNo { get; set; } = "";
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
    public int ExpenseCategoryId { get; set; }
    public ExpenseCategory? ExpenseCategory { get; set; }
    public long AmountPaisa { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Note { get; set; }
    public int UserId { get; set; }
    public int? CashSessionId { get; set; }
}