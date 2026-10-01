// src/SaroHub.Desktop/ViewModels/DashboardViewModel.cs
using System.Collections.ObjectModel;
using System.Windows.Input;
using SaroHub.Core.Dtos;
using SaroHub.Core.Services;
using SaroHub.Desktop.Mvvm;
using SaroHub.Domain.Common;

namespace SaroHub.Desktop.ViewModels;

public sealed class DashboardViewModel : ViewModelBase, ILoadable
{
    private readonly ReportService  _reports;
    private readonly StockService   _stock;
    private readonly SalesService   _sales;

    // ── KPI cards ─────────────────────────────────────────────────────────
    private long _salesPaisa;
    private long _profitPaisa;
    private long _cashPaisa;
    private long _receivablePaisa;
    private long _payablePaisa;
    private long _expensesPaisa;
    private int  _billCount;

    public long SalesPaisa      { get => _salesPaisa;      private set { Set(ref _salesPaisa, value);      OnPropertyChanged(nameof(SalesFormatted)); } }
    public long ProfitPaisa     { get => _profitPaisa;     private set { Set(ref _profitPaisa, value);     OnPropertyChanged(nameof(ProfitFormatted)); } }
    public long CashPaisa       { get => _cashPaisa;       private set { Set(ref _cashPaisa, value);       OnPropertyChanged(nameof(CashFormatted)); } }
    public long ReceivablePaisa { get => _receivablePaisa; private set { Set(ref _receivablePaisa, value); OnPropertyChanged(nameof(KhataFormatted)); } }
    public long PayablePaisa    { get => _payablePaisa;    private set { Set(ref _payablePaisa, value);    OnPropertyChanged(nameof(PayableFormatted)); } }
    public long ExpensesPaisa   { get => _expensesPaisa;   private set { Set(ref _expensesPaisa, value);   OnPropertyChanged(nameof(ExpensesFormatted)); } }
    public int  BillCount       { get => _billCount;       private set => Set(ref _billCount, value); }

    public string SalesFormatted    => Money.Format(SalesPaisa);
    public string ProfitFormatted   => Money.Format(ProfitPaisa);
    public string CashFormatted     => Money.Format(CashPaisa);
    public string KhataFormatted    => Money.Format(ReceivablePaisa);
    public string PayableFormatted  => Money.Format(PayablePaisa);
    public string ExpensesFormatted => Money.Format(ExpensesPaisa);

    // ── Date filter ───────────────────────────────────────────────────────
    private string _filter = "Today";
    public string Filter
    {
        get => _filter;
        set { if (Set(ref _filter, value)) _ = LoadAsync(); }
    }
    public IReadOnlyList<string> Filters { get; } = new[] { "Today", "This Week", "This Month" };

    // ── Collections ───────────────────────────────────────────────────────
    public ObservableCollection<LowStockRow>     LowStock      { get; } = new();
    public ObservableCollection<KhataRow>        WhoOwe        { get; } = new();
    public ObservableCollection<RecentSaleRow>   RecentSales   { get; } = new();
    public ObservableCollection<SalesByDayPoint> ChartPoints   { get; } = new();

    public ICommand RefreshCommand { get; }

    public DashboardViewModel(ReportService reports, StockService stock, SalesService sales)
    {
        _reports = reports;
        _stock   = stock;
        _sales   = sales;
        RefreshCommand = AsyncCommand(LoadAsync);
    }

    public async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var (from, to) = FilterRange();

            var snap     = await _reports.DashboardAsync(from, to);
            var recent   = await _sales.RecentAsync(8);
            var lowStock = await _stock.LowStockAsync(8);
            var whoOwe   = await _reports.CustomersWhoOweAsync();
            var chart    = await _reports.SalesByDayAsync(from, to);

            SalesPaisa      = snap.SalesPaisa;
            ProfitPaisa     = snap.ProfitPaisa;
            CashPaisa       = snap.CashPaisa;
            ReceivablePaisa = snap.ReceivablePaisa;
            PayablePaisa    = snap.PayablePaisa;
            ExpensesPaisa   = snap.ExpensesPaisa;
            BillCount       = snap.BillCount;

            LowStock.Clear();
            foreach (var r in lowStock)    LowStock.Add(r);

            WhoOwe.Clear();
            foreach (var r in whoOwe.Take(8)) WhoOwe.Add(r);

            RecentSales.Clear();
            foreach (var r in recent)      RecentSales.Add(r);

            ChartPoints.Clear();
            foreach (var (day, total) in chart)
                ChartPoints.Add(new SalesByDayPoint(day.ToString("dd/MM"), Money.To(total)));
        });
    }

    private (DateTime from, DateTime to) FilterRange() => _filter switch
    {
        "This Week"  => (DateTime.Now.AddDays(-(int)DateTime.Now.DayOfWeek), DateTime.Now),
        "This Month" => (new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1), DateTime.Now),
        _            => (DateTime.Today, DateTime.Today)
    };
}

public sealed record SalesByDayPoint(string Day, decimal Total);
