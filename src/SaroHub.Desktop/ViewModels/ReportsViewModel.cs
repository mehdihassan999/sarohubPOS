// src/SaroHub.Desktop/ViewModels/ReportsViewModel.cs
using System.Collections.ObjectModel;
using System.Windows.Input;
using SaroHub.Core.Dtos;
using SaroHub.Core.Services;
using SaroHub.Desktop.Mvvm;
using SaroHub.Domain.Common;

namespace SaroHub.Desktop.ViewModels;

public sealed class TrialBalanceLineVm
{
    public string Code       { get; init; } = "";
    public string Name       { get; init; } = "";
    public string Debit      { get; init; } = "";
    public string Credit     { get; init; } = "";
}

public sealed class ReportsViewModel : ViewModelBase, ILoadable
{
    private readonly ReportService _reports;

    // ── P&L ───────────────────────────────────────────────────────────────
    private string _salesFmt     = "";
    private string _discFmt      = "";
    private string _cogsFmt      = "";
    private string _grossFmt     = "";
    private string _expFmt       = "";
    private string _netFmt       = "";
    public string SalesFmt   { get => _salesFmt;  private set => Set(ref _salesFmt, value); }
    public string DiscFmt    { get => _discFmt;   private set => Set(ref _discFmt, value); }
    public string CogsFmt    { get => _cogsFmt;   private set => Set(ref _cogsFmt, value); }
    public string GrossFmt   { get => _grossFmt;  private set => Set(ref _grossFmt, value); }
    public string ExpFmt     { get => _expFmt;    private set => Set(ref _expFmt, value); }
    public string NetFmt     { get => _netFmt;    private set => Set(ref _netFmt, value); }

    // ── Trial Balance ─────────────────────────────────────────────────────
    public ObservableCollection<TrialBalanceLineVm> TrialBalance { get; } = new();

    // ── Tabs ─────────────────────────────────────────────────────────────
    private int _tabIndex;
    public  int TabIndex { get => _tabIndex; set { Set(ref _tabIndex, value); _ = LoadCurrentTabAsync(); } }

    // ── Date filter ──────────────────────────────────────────────────────
    private DateTime _from = DateTime.Today.AddDays(-30);
    private DateTime _to   = DateTime.Today;
    public DateTime FromDate { get => _from; set { Set(ref _from, value); _ = LoadCurrentTabAsync(); } }
    public DateTime ToDate   { get => _to;   set { Set(ref _to, value);   _ = LoadCurrentTabAsync(); } }

    // ── Customer Khata report ─────────────────────────────────────────────
    public ObservableCollection<KhataRow> KhataReport { get; } = new();

    public ICommand RefreshCommand { get; }

    public ReportsViewModel(ReportService reports)
    {
        _reports       = reports;
        RefreshCommand = AsyncCommand(LoadCurrentTabAsync);
    }

    public Task LoadAsync() => LoadCurrentTabAsync();

    private async Task LoadCurrentTabAsync()
    {
        await RunAsync(async () =>
        {
            switch (_tabIndex)
            {
                case 0: await LoadPnLAsync(); break;
                case 1: await LoadTrialBalanceAsync(); break;
                case 2: await LoadKhataAsync(); break;
            }
        });
    }

    private async Task LoadPnLAsync()
    {
        var r = await _reports.ProfitAndLossAsync(_from, _to);
        SalesFmt = Money.Format(r.SalesPaisa);
        DiscFmt  = Money.Format(r.DiscountPaisa);
        CogsFmt  = Money.Format(r.CogsPaisa);
        GrossFmt = Money.Format(r.GrossProfitPaisa);
        ExpFmt   = Money.Format(r.TotalExpensesPaisa);
        NetFmt   = Money.Format(r.NetProfitPaisa);
    }

    private async Task LoadTrialBalanceAsync()
    {
        var rows = await _reports.TrialBalanceAsync(_to);
        TrialBalance.Clear();
        foreach (var r in rows)
            TrialBalance.Add(new TrialBalanceLineVm
            {
                Code   = r.Code,
                Name   = r.Name,
                Debit  = r.DebitPaisa  > 0 ? Money.Format(r.DebitPaisa)  : "",
                Credit = r.CreditPaisa > 0 ? Money.Format(r.CreditPaisa) : ""
            });
    }

    private async Task LoadKhataAsync()
    {
        var rows = await _reports.CustomersWhoOweAsync();
        KhataReport.Clear(); foreach (var r in rows) KhataReport.Add(r);
    }
}
