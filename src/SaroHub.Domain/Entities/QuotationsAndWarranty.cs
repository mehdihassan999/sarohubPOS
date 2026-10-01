// src/SaroHub.Domain/Entities/QuotationsAndWarranty.cs
using SaroHub.Domain.Common;

namespace SaroHub.Domain.Entities;

public class Quotation : Entity
{
    public string QuotationNo { get; set; } = "";
    public DateTime DateUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiryDateUtc { get; set; } = DateTime.Today.AddDays(14);
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string CustomerName { get; set; } = "";
    public string CustomerPhone { get; set; } = "";
    public long SubtotalPaisa { get; set; }
    public long DiscountPaisa { get; set; }
    public long TotalPaisa { get; set; }
    public string Status { get; set; } = "Draft"; // Draft, Sent, Converted, Expired
    public string? Note { get; set; }
    public List<QuotationItem> Items { get; set; } = new();
}

public class QuotationItem : Entity
{
    public int QuotationId { get; set; }
    public Quotation? Quotation { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public string ProductName { get; set; } = "";
    public long QtyRaw { get; set; }
    public string UnitSymbol { get; set; } = "";
    public long UnitPricePaisa { get; set; }
    public long LineTotalPaisa { get; set; }
}

public class WarrantyRecord : Entity
{
    public string SerialNumber { get; set; } = "";
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public string ProductName { get; set; } = "";
    public int? SaleId { get; set; }
    public Sale? Sale { get; set; }
    public string InvoiceNo { get; set; } = "";
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string CustomerName { get; set; } = "";
    public string CustomerPhone { get; set; } = "";
    public DateTime PurchaseDateUtc { get; set; } = DateTime.UtcNow;
    public int WarrantyMonths { get; set; }
    public DateTime ExpiryDateUtc { get; set; }
    public string Status { get; set; } = "Active"; // Active, Claimed, Repaired, Replaced, Expired
    public string? Notes { get; set; }
}
