// src/SaroHub.Desktop/Composition.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SaroHub.Core.Abstractions;
using SaroHub.Core.Accounting;
using SaroHub.Core.Services;
using SaroHub.Desktop.Services;
using SaroHub.Desktop.ViewModels;
using SaroHub.Infrastructure;
using SaroHub.Infrastructure.Persistence;
using SaroHub.Infrastructure.Services;

namespace SaroHub.Desktop;

public static class Composition
{
    public static IServiceProvider BuildServices()
    {
        var sc = new ServiceCollection();

        // ── EF Core ────────────────────────────────────────────────────────────
        sc.AddDbContextFactory<AppDbContext>(opt =>
            opt.UseSqlite(AppPaths.ConnectionString));

        // ── Infrastructure ─────────────────────────────────────────────────────
        sc.AddSingleton<IAppLogger>(_ => new FileAppLogger(AppPaths.LogFolder));
        sc.AddSingleton<IClock, SystemClock>();
        sc.AddSingleton<IPasswordHasher, PasswordHasher>();
        sc.AddSingleton<IAppDbFactory, AppDbFactory>();
        sc.AddSingleton<IBackupService, SqliteBackupService>();
        sc.AddSingleton<DatabaseInitializer>();

        // ── Auth (singleton = session-scoped current user) ─────────────────────
        sc.AddSingleton<AuthService>();
        sc.AddSingleton<ICurrentUser>(sp => sp.GetRequiredService<AuthService>());

        // ── Core services ──────────────────────────────────────────────────────
        sc.AddSingleton<NumberService>();
        sc.AddSingleton<PostingEngine>();
        sc.AddSingleton<AuditService>();
        sc.AddSingleton<SettingsService>();
        sc.AddSingleton<SetupService>();
        sc.AddSingleton<CashSessionService>();
        sc.AddSingleton<ProductService>();
        sc.AddSingleton<PartyService>();
        sc.AddSingleton<SalesService>();
        sc.AddSingleton<PaymentService>();
        sc.AddSingleton<StockService>();
        sc.AddSingleton<ExpenseService>();
        sc.AddSingleton<ReportService>();

        // ── Desktop services ───────────────────────────────────────────────────
        sc.AddSingleton<NavigationService>();
        sc.AddSingleton<WpfDialogService>();
        sc.AddSingleton<IReceiptPrinter, ReceiptPrinterService>();

        sc.AddTransient<Func<IAppDbContext>>(sp => () => sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

        // ── ViewModels ─────────────────────────────────────────────────────────
        sc.AddTransient<LoginViewModel>();
        sc.AddTransient<FirstRunViewModel>();
        sc.AddSingleton<MainViewModel>();
        sc.AddTransient<DashboardViewModel>();
        sc.AddTransient<PosViewModel>();
        sc.AddTransient<KhataViewModel>();
        sc.AddTransient<ProductsViewModel>();
        sc.AddTransient<ProductEditViewModel>();
        sc.AddTransient<StockViewModel>();
        sc.AddTransient<PurchasesViewModel>();
        sc.AddTransient<CustomersViewModel>();
        sc.AddTransient<SuppliersViewModel>();
        sc.AddTransient<ExpensesViewModel>();
        sc.AddTransient<ReportsViewModel>();
        sc.AddTransient<AccountingViewModel>();
        sc.AddTransient<WarrantyViewModel>();
        sc.AddTransient<QuotationsViewModel>();
        sc.AddTransient<BackupViewModel>();
        sc.AddTransient<SettingsViewModel>();
        sc.AddTransient<UsersViewModel>();
        sc.AddTransient<DayCloseViewModel>();

        return sc.BuildServiceProvider();
    }
}
