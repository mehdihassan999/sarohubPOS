// src/SaroHub.Desktop/ViewModels/KhataViewModel.cs
using System.Collections.ObjectModel;
using System.Windows.Input;
using SaroHub.Core.Dtos;
using SaroHub.Core.Services;
using SaroHub.Desktop.Mvvm;
using SaroHub.Desktop.Services;
using SaroHub.Domain;
using SaroHub.Domain.Common;
using SaroHub.Domain.Entities;

namespace SaroHub.Desktop.ViewModels;

public sealed class KhataViewModel : ViewModelBase, ILoadable
{
    private readonly PartyService   _parties;
    private readonly PaymentService _payments;
    private readonly WpfDialogService? _dialog;

    // ── Search ────────────────────────────────────────────────────────────
    private string _search = "";
    public  string Search { get => _search; set { Set(ref _search, value); _ = LoadAsync(); } }

    public ObservableCollection<Customer> Customers { get; } = new();

    // ── Selected customer + ledger ────────────────────────────────────────
    private Customer? _selected;
    public  Customer? Selected { get => _selected; set { Set(ref _selected, value); _ = LoadLedgerAsync(); OnPropertyChanged(nameof(BalanceLabel)); } }

    public ObservableCollection<LedgerRow> Ledger { get; } = new();
    public string BalanceLabel => _selected is null ? "" :
        _selected.BalancePaisa >= 0
            ? $"You receive: {Money.Format(_selected.BalancePaisa)}"
            : $"You owe: {Money.Format(-_selected.BalancePaisa)}";

    // ── Payment panel ─────────────────────────────────────────────────────
    private bool   _showPayment;
    private string _payAmount  = "";
    private PaymentMethod _payMethod = PaymentMethod.Cash;

    public bool   ShowPayment { get => _showPayment; set => Set(ref _showPayment, value); }
    public string PayAmount   { get => _payAmount;   set => Set(ref _payAmount, value); }
    public PaymentMethod PayMethod { get => _payMethod; set => Set(ref _payMethod, value); }
    public IReadOnlyList<PaymentMethod> PayMethods { get; } =
        new[] { PaymentMethod.Cash, PaymentMethod.Bank, PaymentMethod.Easypaisa, PaymentMethod.JazzCash, PaymentMethod.Raast };

    // ── Commands ─────────────────────────────────────────────────────────
    public ICommand ReceiveCommand     { get; }
    public ICommand ConfirmPayCommand  { get; }
    public ICommand CancelPayCommand   { get; }
    public ICommand RefreshCommand     { get; }

    public KhataViewModel(PartyService parties, PaymentService payments, WpfDialogService dialog)
    {
        _parties  = parties;
        _payments = payments;
        _dialog   = dialog;

        ReceiveCommand    = Command(OpenPayment, () => Selected is not null);
        ConfirmPayCommand = AsyncCommand(ConfirmPayAsync, () => !IsBusy && Selected is not null);
        CancelPayCommand  = Command(() => ShowPayment = false);
        RefreshCommand    = AsyncCommand(LoadAsync);
    }

    public async Task LoadAsync()
    {
        var list = await _parties.SearchCustomersAsync(string.IsNullOrWhiteSpace(Search) ? null : Search);
        Customers.Clear();
        foreach (var c in list) Customers.Add(c);
    }

    private async Task LoadLedgerAsync()
    {
        if (_selected is null) { Ledger.Clear(); return; }
        var rows = await _parties.CustomerLedgerAsync(_selected.Id);
        Ledger.Clear();
        foreach (var r in rows) Ledger.Add(r);
    }

    private void OpenPayment()
    {
        PayAmount  = "";
        ShowPayment = true;
    }

    private async Task ConfirmPayAsync()
    {
        if (!decimal.TryParse(PayAmount, out var v) || v <= 0)
        { ErrorMessage = "Enter a valid amount."; return; }

        var amount = Money.From(v);
        await RunAsync(async () =>
        {
            var result = await _payments.ReceiveFromCustomerAsync(
                Selected!.Id, amount, PayMethod, null, null, Guid.NewGuid());
            if (!result.IsSuccess) { ErrorMessage = result.Error; return; }
            ShowPayment = false;
            ErrorMessage = "";
            await LoadAsync();
            await LoadLedgerAsync();
        });
    }
}
