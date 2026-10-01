// src/SaroHub.Desktop/ViewModels/DayCloseViewModel.cs
using System.Windows.Input;
using SaroHub.Core.Dtos;
using SaroHub.Core.Services;
using SaroHub.Desktop.Mvvm;
using SaroHub.Desktop.Services;
using SaroHub.Domain.Common;

namespace SaroHub.Desktop.ViewModels;

public sealed class DayCloseViewModel : ViewModelBase, ILoadable
{
    private readonly CashSessionService _sessions;
    private readonly WpfDialogService   _dialog;

    private DayCloseSummary? _summary;
    private string _countedCash = "";
    private bool   _closed;
    private string _note = "";

    public string OpenedAt       => _summary is null ? "" : _summary.OpenedAtLocal.ToString("dd/MM/yyyy HH:mm");
    public string OpeningCash    => _summary is null ? "" : Money.Format(_summary.OpeningCashPaisa);
    public string CashSales      => _summary is null ? "" : Money.Format(_summary.CashSalesPaisa);
    public string CreditSales    => _summary is null ? "" : Money.Format(_summary.CreditSalesPaisa);
    public string KhataReceipts  => _summary is null ? "" : Money.Format(_summary.KhataReceiptsPaisa);
    public string CashExpenses   => _summary is null ? "" : Money.Format(_summary.CashExpensesPaisa);
    public string TotalSales     => _summary is null ? "" : Money.Format(_summary.TotalSalesPaisa);
    public string ExpectedCash   => _summary is null ? "" : Money.Format(_summary.ExpectedCashPaisa);
    public string Profit         => _summary is null ? "" : Money.Format(_summary.ProfitPaisa);

    public string CountedCash { get => _countedCash; set => Set(ref _countedCash, value); }
    public string Note        { get => _note;        set => Set(ref _note, value); }
    public bool   IsClosed    { get => _closed;      private set => Set(ref _closed, value); }

    public bool HasOpenSession => _summary is not null;

    public string DifferenceFormatted
    {
        get
        {
            if (_summary is null || !decimal.TryParse(CountedCash, out var v)) return "";
            var counted   = Money.From(v);
            var diff      = counted - _summary.ExpectedCashPaisa;
            return diff == 0 ? "Rs 0.00" : Money.Format(diff);
        }
    }

    public ICommand CloseCommand   { get; }
    public ICommand RefreshCommand { get; }

    public DayCloseViewModel(CashSessionService sessions, WpfDialogService dialog)
    {
        _sessions = sessions;
        _dialog   = dialog;
        CloseCommand   = AsyncCommand(CloseAsync, () => !IsBusy && HasOpenSession);
        RefreshCommand = AsyncCommand(LoadAsync);
    }

    public async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            _summary = await _sessions.SummaryAsync();
            RaiseAll();
        });
    }

    private async Task CloseAsync()
    {
        if (!decimal.TryParse(CountedCash, out var v))
        { ErrorMessage = "Enter the counted cash amount."; return; }

        if (!_dialog.Confirm(
            $"Close today?\n\nExpected: {ExpectedCash}\nCounted: {Money.Format(Money.From(v))}\n\nThis will lock today's transactions.",
            "Close Day"))
            return;

        await RunAsync(async () =>
        {
            var r = await _sessions.CloseAsync(Money.From(v), string.IsNullOrWhiteSpace(Note) ? null : Note.Trim());
            if (!r.IsSuccess) { ErrorMessage = r.Error; return; }
            IsClosed = true;
            _dialog.Alert("Day closed successfully.", "Day Closed");
        });
    }

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(OpenedAt));
        OnPropertyChanged(nameof(OpeningCash));
        OnPropertyChanged(nameof(CashSales));
        OnPropertyChanged(nameof(CreditSales));
        OnPropertyChanged(nameof(KhataReceipts));
        OnPropertyChanged(nameof(CashExpenses));
        OnPropertyChanged(nameof(TotalSales));
        OnPropertyChanged(nameof(ExpectedCash));
        OnPropertyChanged(nameof(Profit));
        OnPropertyChanged(nameof(HasOpenSession));
        OnPropertyChanged(nameof(DifferenceFormatted));
    }
}
