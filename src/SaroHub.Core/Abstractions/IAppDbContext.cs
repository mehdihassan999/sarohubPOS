// src/SaroHub.Core/Abstractions/IAppDbContext.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SaroHub.Domain.Entities;

namespace SaroHub.Core.Abstractions;

public interface IAppDbContext : IDisposable, IAsyncDisposable
{
    DbSet<Role> Roles { get; }
    DbSet<User> Users { get; }
    DbSet<Setting> Settings { get; }
    DbSet<NumberSequence> NumberSequences { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<BackupRecord> Backups { get; }

    DbSet<Category> Categories { get; }
    DbSet<Brand> Brands { get; }
    DbSet<Unit> Units { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductUnit> ProductUnits { get; }
    DbSet<ProductBarcode> ProductBarcodes { get; }

    DbSet<Customer> Customers { get; }
    DbSet<Supplier> Suppliers { get; }

    DbSet<Sale> Sales { get; }
    DbSet<SaleItem> SaleItems { get; }
    DbSet<SalePayment> SalePayments { get; }
    DbSet<PartyPayment> PartyPayments { get; }

    DbSet<ExpenseCategory> ExpenseCategories { get; }
    DbSet<Expense> Expenses { get; }

    DbSet<StockTransaction> StockTransactions { get; }
    DbSet<StockIn> StockIns { get; }
    DbSet<StockInItem> StockInItems { get; }
    DbSet<StockAdjustment> StockAdjustments { get; }
    DbSet<CashSession> CashSessions { get; }

    DbSet<Account> Accounts { get; }
    DbSet<JournalEntry> JournalEntries { get; }
    DbSet<JournalEntryLine> JournalEntryLines { get; }

    DbSet<Quotation> Quotations { get; }
    DbSet<QuotationItem> QuotationItems { get; }
    DbSet<WarrantyRecord> WarrantyRecords { get; }

    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}