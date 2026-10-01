// src/SaroHub.Infrastructure/Persistence/DatabaseInitializer.cs
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SaroHub.Domain;
using SaroHub.Domain.Accounting;
using SaroHub.Domain.Entities;
using SaroHub.Domain.Security;

namespace SaroHub.Infrastructure.Persistence;

public sealed class DatabaseInitializer
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    public DatabaseInitializer(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        if (db.Database.GetMigrations().Any())
            await db.Database.MigrateAsync(ct);
        else
            await db.Database.EnsureCreatedAsync(ct);

        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);
        await db.Database.ExecuteSqlRawAsync("PRAGMA synchronous=FULL;", ct);
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;", ct);

        await SeedRolesAsync(db, ct);
        await SeedUnitsAsync(db, ct);
        await SeedChartOfAccountsAsync(db, ct);
        await SeedExpenseCategoriesAsync(db, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> IntegrityOkAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(AppPaths.ConnectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA integrity_check;";
        var result = (string?)await cmd.ExecuteScalarAsync(ct);
        return string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task SeedRolesAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.Roles.AnyAsync(ct)) return;

        db.Roles.AddRange(
            new Role { Name = "Owner", PermissionsCsv = string.Join(',', Permissions.All), IsSystem = true },
            new Role { Name = "Manager", PermissionsCsv = string.Join(',', Permissions.Manager), IsSystem = true },
            new Role { Name = "Cashier", PermissionsCsv = string.Join(',', Permissions.Cashier), IsSystem = true },
            new Role { Name = "Accountant", PermissionsCsv = string.Join(',', Permissions.Accountant), IsSystem = true },
            new Role
            {
                Name = "Inventory Staff",
                PermissionsCsv = string.Join(',',
                new[] { Permissions.ProductView, Permissions.ProductEdit, Permissions.StockEdit, Permissions.SupplierView }),
                IsSystem = true
            },
            new Role
            {
                Name = "Sales Staff",
                PermissionsCsv = string.Join(',',
                new[] { Permissions.SaleCreate, Permissions.ProductView, Permissions.CustomerView, Permissions.CustomerEdit }),
                IsSystem = true
            }
        );
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedUnitsAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.Units.AnyAsync(ct)) return;

        db.Units.AddRange(
            new Unit { Name = "Piece", Symbol = "pc" },
            new Unit { Name = "Meter", Symbol = "m" },
            new Unit { Name = "Foot", Symbol = "ft" },
            new Unit { Name = "Roll", Symbol = "roll" },
            new Unit { Name = "Box", Symbol = "box" },
            new Unit { Name = "Packet", Symbol = "pkt" },
            new Unit { Name = "Carton", Symbol = "ctn" },
            new Unit { Name = "Dozen", Symbol = "dzn" },
            new Unit { Name = "Kg", Symbol = "kg" },
            new Unit { Name = "Gram", Symbol = "g" },
            new Unit { Name = "Liter", Symbol = "L" }
        );
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedExpenseCategoriesAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.ExpenseCategories.AnyAsync(ct)) return;

        db.ExpenseCategories.AddRange(
            new ExpenseCategory { Name = "Rent", AccountCode = "5200" },
            new ExpenseCategory { Name = "Electricity", AccountCode = "5210" },
            new ExpenseCategory { Name = "Salary", AccountCode = "5220" },
            new ExpenseCategory { Name = "Transport", AccountCode = "5230" },
            new ExpenseCategory { Name = "Internet", AccountCode = "5240" },
            new ExpenseCategory { Name = "Repair", AccountCode = "5250" },
            new ExpenseCategory { Name = "Food", AccountCode = "5260" },
            new ExpenseCategory { Name = "Other", AccountCode = "5290" }
        );
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedChartOfAccountsAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.Accounts.AnyAsync(ct)) return;

        var defs = new (string Code, string Name, AccountType Type, bool Postable)[]
        {
            ("1000",                            "Assets",              AccountType.Asset,     false),
            (AccountCodes.CashInHand,           "Cash in Hand",        AccountType.Asset,     true),
            (AccountCodes.BankAccount,          "Bank Account",        AccountType.Asset,     true),
            (AccountCodes.MobileWallet,         "Mobile Wallet",       AccountType.Asset,     true),
            (AccountCodes.CardClearing,         "Card Clearing",       AccountType.Asset,     true),
            (AccountCodes.AccountsReceivable,   "Customer Khata",      AccountType.Asset,     true),
            (AccountCodes.Inventory,            "Inventory",           AccountType.Asset,     true),

            ("2000",                            "Liabilities",         AccountType.Liability, false),
            (AccountCodes.AccountsPayable,      "Supplier Khata",      AccountType.Liability, true),
            (AccountCodes.CustomerAdvances,     "Customer Advances",   AccountType.Liability, true),
            (AccountCodes.TaxPayable,           "Tax Payable",         AccountType.Liability, true),

            ("3000",                            "Equity",              AccountType.Equity,    false),
            (AccountCodes.OwnerCapital,         "Owner Capital",       AccountType.Equity,    true),
            (AccountCodes.OwnerDrawings,        "Owner Withdrawals",   AccountType.Equity,    true),
            (AccountCodes.OpeningBalanceEq,     "Opening Balance",     AccountType.Equity,    true),

            ("4000",                            "Income",              AccountType.Income,    false),
            (AccountCodes.Sales,                "Sales",               AccountType.Income,    true),
            (AccountCodes.SalesDiscount,        "Sales Discount",      AccountType.Income,    true),
            (AccountCodes.CashOverage,          "Cash Overage",        AccountType.Income,    true),

            ("5000",                            "Expenses",            AccountType.Expense,   false),
            (AccountCodes.CostOfGoodsSold,      "Cost of Goods Sold",  AccountType.Expense,   true),
            ("5200",                            "Rent",                AccountType.Expense,   true),
            ("5210",                            "Electricity",         AccountType.Expense,   true),
            ("5220",                            "Salary",              AccountType.Expense,   true),
            ("5230",                            "Transport",           AccountType.Expense,   true),
            ("5240",                            "Internet",            AccountType.Expense,   true),
            ("5250",                            "Repair",              AccountType.Expense,   true),
            ("5260",                            "Food",                AccountType.Expense,   true),
            ("5290",                            "Other Expense",       AccountType.Expense,   true),
            (AccountCodes.StockWriteOff,        "Stock Write-off",     AccountType.Expense,   true),
            (AccountCodes.CashShortage,         "Cash Shortage",       AccountType.Expense,   true),
        };

        var accounts = defs
            .Where(d => d.Code is not null)
            .Select(d => new Account
            {
                Code       = d.Code,
                Name       = d.Name,
                Type       = d.Type,
                IsPostable = d.Postable,
                IsSystem   = true
            })
            .ToList();

        db.Accounts.AddRange(accounts);
        await db.SaveChangesAsync(ct);
    }
}