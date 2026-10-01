// src/SaroHub.Core/Services/ProductService.cs
using Microsoft.EntityFrameworkCore;
using SaroHub.Core.Abstractions;
using SaroHub.Core.Dtos;
using SaroHub.Domain.Common;
using SaroHub.Domain.Entities;

namespace SaroHub.Core.Services;

public sealed class ProductService
{
    private readonly IAppDbFactory _factory;
    private readonly AuditService _audit;
    private readonly ICurrentUser _user;
    private readonly IAppLogger _log;

    public ProductService(IAppDbFactory factory, AuditService audit, ICurrentUser user, IAppLogger log)
    { _factory = factory; _audit = audit; _user = user; _log = log; }

    public async Task<List<ProductSearchItem>> SearchAsync(string? term, int take = 60, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        var q = db.Products.AsNoTracking().Include(p => p.BaseUnit).Include(p => p.Brand)
                           .Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(term))
        {
            var t = term.Trim();
            var barcodeProductIds = db.ProductBarcodes.Where(b => b.Code == t).Select(b => b.ProductId);
            q = q.Where(p => p.Name.Contains(t)
                          || (p.Sku != null && p.Sku.Contains(t))
                          || (p.Model != null && p.Model.Contains(t))
                          || (p.Brand != null && p.Brand.Name.Contains(t))
                          || barcodeProductIds.Contains(p.Id));
        }

        return await q.OrderBy(p => p.Name).Take(take)
            .Select(p => new ProductSearchItem(
                p.Id, p.Name, p.Sku, p.Brand!.Name, p.BaseUnit!.Symbol,
                p.SalePricePaisa, p.StockQtyRaw, p.MinStockQtyRaw))
            .ToListAsync(ct);
    }

    public async Task<Product?> FindByBarcodeAsync(string code, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        var id = await db.ProductBarcodes.AsNoTracking()
                         .Where(b => b.Code == code).Select(b => b.ProductId).FirstOrDefaultAsync(ct);
        if (id == 0)
            id = await db.Products.AsNoTracking().Where(p => p.Sku == code).Select(p => p.Id).FirstOrDefaultAsync(ct);
        if (id == 0) return null;
        return await GetAsync(id, ct);
    }

    public async Task<Product?> GetAsync(int id, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        return await db.Products.AsNoTracking()
            .Include(p => p.BaseUnit).Include(p => p.Brand).Include(p => p.Category)
            .Include(p => p.Units).ThenInclude(u => u.Unit)
            .Include(p => p.Barcodes)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<List<Unit>> GetUnitsAsync(CancellationToken ct = default)
    {
        using var db = _factory.Create();
        return await db.Units.AsNoTracking().OrderBy(u => u.Name).ToListAsync(ct);
    }

    public async Task<List<Category>> GetCategoriesAsync(CancellationToken ct = default)
    {
        using var db = _factory.Create();
        return await db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);
    }

    public async Task<List<Brand>> GetBrandsAsync(CancellationToken ct = default)
    {
        using var db = _factory.Create();
        return await db.Brands.AsNoTracking().OrderBy(b => b.Name).ToListAsync(ct);
    }

    public async Task<Result<int>> SaveAsync(Product edited, IEnumerable<string> barcodes,
                                             IEnumerable<(int unitId, decimal factor)> extraUnits,
                                             CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(edited.Name)) return Result<int>.Fail("Product name is required.");
        if (edited.BaseUnitId == 0) return Result<int>.Fail("Select a unit.");
        if (edited.SalePricePaisa < 0) return Result<int>.Fail("Sale price cannot be negative.");

        using var db = _factory.Create();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            Product entity;
            string action;

            if (edited.Id == 0)
            {
                entity = new Product { CreatedByUserId = _user.UserId };
                db.Products.Add(entity);
                action = "ProductCreated";
            }
            else
            {
                entity = await db.Products.Include(p => p.Units).Include(p => p.Barcodes)
                                          .FirstAsync(p => p.Id == edited.Id, ct);
                action = "ProductUpdated";
            }

            var oldPrice = entity.SalePricePaisa;

            entity.Name = edited.Name.Trim();
            entity.Sku = string.IsNullOrWhiteSpace(edited.Sku) ? null : edited.Sku.Trim();
            entity.CategoryId = edited.CategoryId;
            entity.BrandId = edited.BrandId;
            entity.Model = edited.Model;
            entity.BaseUnitId = edited.BaseUnitId;
            entity.PurchasePricePaisa = edited.PurchasePricePaisa;
            entity.SalePricePaisa = edited.SalePricePaisa;
            entity.WholesalePricePaisa = edited.WholesalePricePaisa;
            entity.MinStockQtyRaw = edited.MinStockQtyRaw;
            entity.DefaultSupplierId = edited.DefaultSupplierId;
            entity.WarrantyMonths = edited.WarrantyMonths;
            entity.TracksSerial = edited.TracksSerial;
            entity.Notes = edited.Notes;
            entity.AttributesJson = edited.AttributesJson;
            entity.IsActive = edited.IsActive;

            await db.SaveChangesAsync(ct);

            // Units: base + extras
            db.ProductUnits.RemoveRange(db.ProductUnits.Where(u => u.ProductId == entity.Id));
            await db.SaveChangesAsync(ct);

            db.ProductUnits.Add(new ProductUnit
            {
                ProductId = entity.Id,
                UnitId = entity.BaseUnitId,
                FactorToBaseRaw = Qty.Scale,
                IsBase = true
            });

            foreach (var (unitId, factor) in extraUnits.Where(u => u.unitId != entity.BaseUnitId && u.factor > 0))
                db.ProductUnits.Add(new ProductUnit
                {
                    ProductId = entity.Id,
                    UnitId = unitId,
                    FactorToBaseRaw = Qty.From(factor),
                    IsBase = false
                });

            // Barcodes
            db.ProductBarcodes.RemoveRange(db.ProductBarcodes.Where(b => b.ProductId == entity.Id));
            await db.SaveChangesAsync(ct);

            foreach (var code in barcodes.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct())
                db.ProductBarcodes.Add(new ProductBarcode { ProductId = entity.Id, Code = code });

            if (action == "ProductUpdated" && oldPrice != entity.SalePricePaisa)
                _audit.Add(db, _user, "ProductPriceChanged", nameof(Product), entity.Name,
                           Money.Format(oldPrice), Money.Format(entity.SalePricePaisa));
            else
                _audit.Add(db, _user, action, nameof(Product), entity.Name);

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result<int>.Ok(entity.Id);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _log.Error("Product save failed", ex);
            return Result<int>.Fail("Could not save the product. Your data is safe.");
        }
    }
}