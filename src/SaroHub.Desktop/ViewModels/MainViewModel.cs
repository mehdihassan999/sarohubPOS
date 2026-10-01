// src/SaroHub.Desktop/ViewModels/MainViewModel.cs
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using SaroHub.Core.Abstractions;
using SaroHub.Core.Services;
using SaroHub.Desktop.Mvvm;
using SaroHub.Desktop.Services;
using SaroHub.Desktop.Views;

namespace SaroHub.Desktop.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly IServiceProvider  _sp;
    private readonly NavigationService _nav;
    private readonly SettingsService   _settings;

    private string       _activeSection = "";
    private UserControl? _currentView;
    private string       _shopName = "SaroHub POS";
    private string       _userName = "";
    private string       _statusMessage = "";

    public string       ShopName      { get => _shopName;      private set => Set(ref _shopName, value); }
    public string       UserName      { get => _userName;      private set => Set(ref _userName, value); }
    public string       StatusMessage { get => _statusMessage; set        => Set(ref _statusMessage, value); }
    public UserControl? CurrentView   { get => _currentView;   private set => Set(ref _currentView, value); }
    public string       ActiveSection { get => _activeSection; private set { Set(ref _activeSection, value); RaiseAllIsActive(); } }

    // ── IsActive helpers for sidebar highlighting ──────────────────────────
    public bool IsDashboard  => _activeSection == "Dashboard";
    public bool IsPos        => _activeSection == "POS";
    public bool IsKhata      => _activeSection == "Khata";
    public bool IsProducts   => _activeSection == "Products";
    public bool IsStock      => _activeSection == "Stock";
    public bool IsPurchases  => _activeSection == "Purchases";
    public bool IsCustomers  => _activeSection == "Customers";
    public bool IsSuppliers  => _activeSection == "Suppliers";
    public bool IsExpenses   => _activeSection == "Expenses";
    public bool IsReports    => _activeSection == "Reports";
    public bool IsAccounting => _activeSection == "Accounting";
    public bool IsWarranty   => _activeSection == "Warranty";
    public bool IsQuotations => _activeSection == "Quotations";
    public bool IsBackup     => _activeSection == "Backup";
    public bool IsSettings   => _activeSection == "Settings";
    public bool IsUsers      => _activeSection == "Users";

    // ── Commands ───────────────────────────────────────────────────────────
    public ICommand NavDashboardCommand  { get; }
    public ICommand NavPosCommand        { get; }
    public ICommand NavKhataCommand      { get; }
    public ICommand NavProductsCommand   { get; }
    public ICommand NavStockCommand      { get; }
    public ICommand NavPurchasesCommand  { get; }
    public ICommand NavCustomersCommand  { get; }
    public ICommand NavSuppliersCommand  { get; }
    public ICommand NavExpensesCommand   { get; }
    public ICommand NavReportsCommand    { get; }
    public ICommand NavAccountingCommand { get; }
    public ICommand NavWarrantyCommand   { get; }
    public ICommand NavQuotationsCommand { get; }
    public ICommand NavBackupCommand     { get; }
    public ICommand NavSettingsCommand   { get; }
    public ICommand NavUsersCommand      { get; }
    public ICommand DayCloseCommand      { get; }

    public MainViewModel(IServiceProvider sp, NavigationService nav,
                         SettingsService settings, ICurrentUser user)
    {
        _sp       = sp;
        _nav      = nav;
        _settings = settings;

        NavDashboardCommand  = Command(() => Navigate("Dashboard"));
        NavPosCommand        = Command(() => Navigate("POS"));
        NavKhataCommand      = Command(() => Navigate("Khata"));
        NavProductsCommand   = Command(() => Navigate("Products"));
        NavStockCommand      = Command(() => Navigate("Stock"));
        NavPurchasesCommand  = Command(() => Navigate("Purchases"));
        NavCustomersCommand  = Command(() => Navigate("Customers"));
        NavSuppliersCommand  = Command(() => Navigate("Suppliers"));
        NavExpensesCommand   = Command(() => Navigate("Expenses"));
        NavReportsCommand    = Command(() => Navigate("Reports"));
        NavAccountingCommand = Command(() => Navigate("Accounting"));
        NavWarrantyCommand   = Command(() => Navigate("Warranty"));
        NavQuotationsCommand = Command(() => Navigate("Quotations"));
        NavBackupCommand     = Command(() => Navigate("Backup"));
        NavSettingsCommand   = Command(() => Navigate("Settings"));
        NavUsersCommand      = Command(() => Navigate("Users"));
        DayCloseCommand      = Command(() => Navigate("DayClose"));
    }

    public async Task InitAsync()
    {
        ShopName = _settings.Get(SettingsService.ShopName, "SaroHub POS");
        UserName = (_sp.GetRequiredService<ICurrentUser>() as SaroHub.Core.Services.AuthService)?.UserName ?? "";
        Navigate("Dashboard");
        await Task.CompletedTask;
    }

    private void Navigate(string section)
    {
        ActiveSection = section;
        UserControl view = section switch
        {
            "Dashboard"  => new DashboardView  { DataContext = _sp.GetRequiredService<DashboardViewModel>()  },
            "POS"        => new PosView        { DataContext = _sp.GetRequiredService<PosViewModel>()        },
            "Khata"      => new KhataView      { DataContext = _sp.GetRequiredService<KhataViewModel>()      },
            "Products"   => new ProductsView   { DataContext = _sp.GetRequiredService<ProductsViewModel>()   },
            "Stock"      => new StockView      { DataContext = _sp.GetRequiredService<StockViewModel>()      },
            "Purchases"  => new PurchasesView  { DataContext = _sp.GetRequiredService<PurchasesViewModel>()  },
            "Customers"  => new CustomersView  { DataContext = _sp.GetRequiredService<CustomersViewModel>()  },
            "Suppliers"  => new SuppliersView  { DataContext = _sp.GetRequiredService<SuppliersViewModel>()  },
            "Expenses"   => new ExpensesView   { DataContext = _sp.GetRequiredService<ExpensesViewModel>()   },
            "Reports"    => new ReportsView    { DataContext = _sp.GetRequiredService<ReportsViewModel>()    },
            "Accounting" => new AccountingView { DataContext = _sp.GetRequiredService<AccountingViewModel>() },
            "Warranty"   => new WarrantyView   { DataContext = _sp.GetRequiredService<WarrantyViewModel>()   },
            "Quotations" => new QuotationsView { DataContext = _sp.GetRequiredService<QuotationsViewModel>() },
            "Backup"     => new BackupView     { DataContext = _sp.GetRequiredService<BackupViewModel>()     },
            "Settings"   => new SettingsView   { DataContext = _sp.GetRequiredService<SettingsViewModel>()   },
            "Users"      => new UsersView      { DataContext = _sp.GetRequiredService<UsersViewModel>()      },
            "DayClose"   => new DayCloseView   { DataContext = _sp.GetRequiredService<DayCloseViewModel>()   },
            _            => new DashboardView  { DataContext = _sp.GetRequiredService<DashboardViewModel>()  }
        };

        // Fire LoadAsync on the new VM if it has one
        if (view.DataContext is ILoadable loadable)
            _ = loadable.LoadAsync();

        CurrentView = view;
    }

    private void RaiseAllIsActive()
    {
        OnPropertyChanged(nameof(IsDashboard));
        OnPropertyChanged(nameof(IsPos));
        OnPropertyChanged(nameof(IsKhata));
        OnPropertyChanged(nameof(IsProducts));
        OnPropertyChanged(nameof(IsStock));
        OnPropertyChanged(nameof(IsPurchases));
        OnPropertyChanged(nameof(IsCustomers));
        OnPropertyChanged(nameof(IsSuppliers));
        OnPropertyChanged(nameof(IsExpenses));
        OnPropertyChanged(nameof(IsReports));
        OnPropertyChanged(nameof(IsAccounting));
        OnPropertyChanged(nameof(IsWarranty));
        OnPropertyChanged(nameof(IsQuotations));
        OnPropertyChanged(nameof(IsBackup));
        OnPropertyChanged(nameof(IsSettings));
        OnPropertyChanged(nameof(IsUsers));
    }
}

/// <summary>VMs that auto-load data when navigated to implement this.</summary>
public interface ILoadable
{
    Task LoadAsync();
}
