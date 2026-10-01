// src/SaroHub.Infrastructure/Persistence/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using SaroHub.Core.Abstractions;
using SaroHub.Domain.Entities;

namespace SaroHub.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<BackupRecord> Backups => Set<BackupRecord>();

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductUnit> ProductUnits => Set<ProductUnit>();
    public DbSet<ProductBarcode> ProductBarcodes => Set<ProductBarcode>();

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<SalePayment> SalePayments => Set<SalePayment>();
    public DbSet<PartyPayment> PartyPayments => Set<PartyPayment>();

    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<Expense> Expenses => Set<Expense>();

    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();
    public DbSet<StockIn> StockIns => Set<StockIn>();
    public DbSet<StockInItem> StockInItems => Set<StockInItem>();
    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();
    public DbSet<CashSession> CashSessions => Set<CashSession>();

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalEntryLine> JournalEntryLines => Set<JournalEntryLine>();

    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<QuotationItem> QuotationItems => Set<QuotationItem>();
    public DbSet<WarrantyRecord> WarrantyRecords => Set<WarrantyRecord>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Role>().HasIndex(x => x.Name).IsUnique();
        b.Entity<User>().HasIndex(x => x.Username).IsUnique();
        b.Entity<Setting>().HasIndex(x => x.Key).IsUnique();
        b.Entity<NumberSequence>().HasIndex(x => x.Name).IsUnique();
        b.Entity<AuditLog>().HasIndex(x => x.AtUtc);

        b.Entity<Category>().HasIndex(x => x.Name).IsUnique();
        b.Entity<Brand>().HasIndex(x => x.Name).IsUnique();
        b.Entity<Unit>().HasIndex(x => x.Name).IsUnique();

        b.Entity<Product>(e =>
        {
            e.HasIndex(x => x.Name);
            e.HasIndex(x => x.Sku);
            e.HasOne(x => x.BaseUnit).WithMany().HasForeignKey(x => x.BaseUnitId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Brand).WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.DefaultSupplier).WithMany().HasForeignKey(x => x.DefaultSupplierId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Units).WithOne(x => x.Product!).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Barcodes).WithOne(x => x.Product!).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ProductBarcode>().HasIndex(x => x.Code).IsUnique();
        b.Entity<ProductUnit>().HasIndex(x => new { x.ProductId, x.UnitId }).IsUnique();

        b.Entity<Customer>().HasIndex(x => x.Name);
        b.Entity<Customer>().HasIndex(x => x.Phone);
        b.Entity<Supplier>().HasIndex(x => x.Name);

        b.Entity<Sale>(e =>
        {
            e.HasIndex(x => x.InvoiceNo).IsUnique();
            e.HasIndex(x => x.ClientOperationId).IsUnique();
            e.HasIndex(x => x.SaleAtUtc);
            e.HasMany(x => x.Items).WithOne(x => x.Sale!).HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Payments).WithOne(x => x.Sale!).HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<SaleItem>().HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<PartyPayment>(e =>
        {
            e.HasIndex(x => x.VoucherNo).IsUnique();
            e.HasIndex(x => x.ClientOperationId).IsUnique();
            e.HasIndex(x => new { x.PartyType, x.PartyId });
        });

        b.Entity<Expense>().HasIndex(x => x.VoucherNo).IsUnique();
        b.Entity<Expense>().HasOne(x => x.ExpenseCategory).WithMany().HasForeignKey(x => x.ExpenseCategoryId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ExpenseCategory>().HasIndex(x => x.Name).IsUnique();

        b.Entity<StockTransaction>(e =>
        {
            e.HasIndex(x => new { x.ProductId, x.AtUtc });
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<StockIn>(e =>
        {
            e.HasIndex(x => x.DocumentNo).IsUnique();
            e.HasIndex(x => x.ClientOperationId).IsUnique();
            e.HasMany(x => x.Items).WithOne(x => x.StockIn!).HasForeignKey(x => x.StockInId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<StockInItem>().HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<StockAdjustment>().HasIndex(x => x.DocumentNo).IsUnique();
        b.Entity<CashSession>().HasIndex(x => x.SessionNo).IsUnique();

        b.Entity<Account>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.HasOne(x => x.Parent).WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<JournalEntry>(e =>
        {
            e.HasIndex(x => x.EntryNo).IsUnique();
            e.HasIndex(x => x.EntryDateUtc);
            e.HasIndex(x => new { x.SourceType, x.SourceId });
            e.HasMany(x => x.Lines).WithOne(x => x.JournalEntry!).HasForeignKey(x => x.JournalEntryId).OnDelete(DeleteBehavior.Cascade);
            e.Ignore(x => x.TotalDebit);
            e.Ignore(x => x.TotalCredit);
        });

        b.Entity<JournalEntryLine>(e =>
        {
            e.HasIndex(x => x.AccountId);
            e.HasIndex(x => x.CustomerId);
            e.HasIndex(x => x.SupplierId);
            e.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_JournalLine_OneSide",
                "(DebitPaisa >= 0 AND CreditPaisa >= 0 AND NOT (DebitPaisa > 0 AND CreditPaisa > 0))"));
        });

        b.Entity<Account>().Ignore(x => x.IsDebitNormal);
    }

    /// <summary>Last-line defence: an unbalanced entry can never reach the disk.</summary>
    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var e in ChangeTracker.Entries<JournalEntry>()
                     .Where(x => x.State is EntityState.Added or EntityState.Modified))
        {
            var entry = e.Entity;
            if (entry.Lines.Count > 0 && entry.TotalDebit != entry.TotalCredit)
                throw new InvalidOperationException(
                    $"Journal entry {entry.EntryNo} is unbalanced ({entry.TotalDebit} vs {entry.TotalCredit}).");
        }
        return await base.SaveChangesAsync(ct);
    }
}