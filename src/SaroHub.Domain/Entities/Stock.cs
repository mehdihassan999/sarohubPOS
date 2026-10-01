// src/SaroHub.Domain/Entities/Stock.cs
using SaroHub.Domain.Common;

namespace SaroHub.Domain.Entities;

public class StockTransaction : Entity
{
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public StockMovementType MovementType { get; set; }
    /// <summary>Signed quantity in base units (scaled).</summary>
    public long QtyBaseRaw { get; set; }
    public long UnitCostPaisa { get; set; }
    public long ValuePaisa { get; set; }
    public long BalanceAfterRaw { get; set; }
    public long AvgCostAfterPaisa { get; set; }
    public string SourceType { get; set; } = "";
    public int? SourceId { get; set; }
    public string? DocumentNo { get; set; }
    public int UserId { get; set; }
    public string? Note { get; set; }
}

/// <summary>Simple purchase / stock receipt (supports cash and supplier credit).</summary>
public class StockIn : Entity
{
    public string DocumentNo { get; set; } = "";
    public Guid ClientOperationId { get; set; }
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public string? SupplierInvoiceNo { get; set; }
    public long SubTotalPaisa { get; set; }
    public long DiscountPaisa { get; set; }
    public long TotalPaisa { get; set; }
    public long PaidPaisa { get; set; }
    public PaymentMethod PaidMethod { get; set; } = PaymentMethod.Cash;
    public bool IsOpening { get; set; }
    public int UserId { get; set; }
    public int? CashSessionId { get; set; }
    public List<StockInItem> Items { get; set; } = new();
}

public class StockInItem : Entity
{
    public int StockInId { get; set; }
    public StockIn? StockIn { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public string ProductName { get; set; } = "";
    public int UnitId { get; set; }
    public string UnitSymbol { get; set; } = "";
    public long FactorToBaseRaw { get; set; }
    public long QtyRaw { get; set; }
    public long QtyBaseRaw { get; set; }
    public long UnitCostPaisa { get; set; }   // per chosen unit
    public long LineTotalPaisa { get; set; }
}

public class StockAdjustment : Entity
{
    public string DocumentNo { get; set; } = "";
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public StockMovementType MovementType { get; set; }
    public long QtyBaseRaw { get; set; }
    public long ValuePaisa { get; set; }
    public string Reason { get; set; } = "";
    public int UserId { get; set; }
}

public class CashSession : Entity
{
    public string SessionNo { get; set; } = "";
    public DateTime OpenedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAtUtc { get; set; }
    public long OpeningCashPaisa { get; set; }
    public long? ExpectedCashPaisa { get; set; }
    public long? CountedCashPaisa { get; set; }
    public long? DifferencePaisa { get; set; }
    public CashSessionStatus Status { get; set; } = CashSessionStatus.Open;
    public int OpenedByUserId { get; set; }
    public int? ClosedByUserId { get; set; }
    public string? Note { get; set; }
}