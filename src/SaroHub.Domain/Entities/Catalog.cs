// src/SaroHub.Domain/Entities/Catalog.cs
using SaroHub.Domain.Common;

namespace SaroHub.Domain.Entities;

public class Category : Entity { public string Name { get; set; } = ""; }
public class Brand : Entity { public string Name { get; set; } = ""; }

public class Unit : Entity
{
    public string Name { get; set; } = "";
    public string Symbol { get; set; } = "";
}

public class Product : Entity
{
    public string Name { get; set; } = "";
    public string? Sku { get; set; }
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    public int? BrandId { get; set; }
    public Brand? Brand { get; set; }
    public string? Model { get; set; }

    /// <summary>All stock is held in this unit.</summary>
    public int BaseUnitId { get; set; }
    public Unit? BaseUnit { get; set; }

    public long PurchasePricePaisa { get; set; }   // per base unit (reference only)
    public long SalePricePaisa { get; set; }       // per base unit
    public long WholesalePricePaisa { get; set; }  // per base unit

    public long StockQtyRaw { get; set; }          // base units, scaled
    public long AvgCostPaisa { get; set; }         // weighted average, per base unit
    public long MinStockQtyRaw { get; set; }

    public int? DefaultSupplierId { get; set; }
    public Supplier? DefaultSupplier { get; set; }

    public int WarrantyMonths { get; set; }
    public bool TracksSerial { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    /// <summary>Industry-specific attributes (volts, watt, gauge, ...) stored as JSON.</summary>
    public string? AttributesJson { get; set; }

    public List<ProductUnit> Units { get; set; } = new();
    public List<ProductBarcode> Barcodes { get; set; } = new();
}

/// <summary>Sellable / purchasable unit with a conversion factor to the product base unit.</summary>
public class ProductUnit : Entity
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int UnitId { get; set; }
    public Unit? Unit { get; set; }
    /// <summary>How many base units are in one of this unit. Scaled by Qty.Scale.</summary>
    public long FactorToBaseRaw { get; set; } = Qty.Scale;
    public bool IsBase { get; set; }
    public long? PriceOverridePaisa { get; set; }
}

public class ProductBarcode : Entity
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public string Code { get; set; } = "";
}