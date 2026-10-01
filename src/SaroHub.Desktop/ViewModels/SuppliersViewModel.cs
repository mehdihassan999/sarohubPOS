// src/SaroHub.Desktop/ViewModels/SuppliersViewModel.cs
using System.Collections.ObjectModel;
using System.Windows.Input;
using SaroHub.Core.Services;
using SaroHub.Desktop.Mvvm;
using SaroHub.Domain;
using SaroHub.Domain.Common;
using SaroHub.Domain.Entities;

namespace SaroHub.Desktop.ViewModels;

public sealed class SuppliersViewModel : ViewModelBase, ILoadable
{
    private readonly PartyService   _parties;
    private readonly PaymentService _payments;

    private string _search = "";
    public  string Search { get => _search; set { Set(ref _search, value); _ = LoadAsync(); } }

    public ObservableCollection<Supplier> Items { get; } = new();

    private Supplier? _selected;
    public  Supplier? Selected { get => _selected; set => Set(ref _selected, value); }

    // ── Pay panel ──────────────────────────────────────────────────────────
    private bool   _showPay;
    private string _payAmount = "";
    private PaymentMethod _payMethod = PaymentMethod.Cash;

    public bool   ShowPay   { get => _showPay;    set => Set(ref _showPay, value); }
    public string PayAmount { get => _payAmount;  set => Set(ref _payAmount, value); }
    public PaymentMethod PayMethod { get => _payMethod; set => Set(ref _payMethod, value); }
    public IReadOnlyList<PaymentMethod> PayMethods { get; } =
        new[] { PaymentMethod.Cash, PaymentMethod.Bank, PaymentMethod.Easypaisa, PaymentMethod.JazzCash, PaymentMethod.Raast };

    public ICommand NewCommand       { get; }
    public ICommand EditCommand      { get; }
    public ICommand PayCommand       { get; }
    public ICommand ConfirmPayCommand{ get; }
    public ICommand CancelPayCommand { get; }
    public ICommand RefreshCommand   { get; }

    // ── Edit inline ─────────────────────────────────────────────────────
    private bool   _showEdit;
    private int    _editId;
    private string _editName = "";
    private string _editPhone = "";
    public bool   ShowEdit   { get => _showEdit;  set => Set(ref _showEdit, value); }
    public string EditName   { get => _editName;  set => Set(ref _editName, value); }
    public string EditPhone  { get => _editPhone; set => Set(ref _editPhone, value); }
    public ICommand SaveCommand   { get; }
    public ICommand CancelCommand { get; }

    public SuppliersViewModel(PartyService parties, PaymentService payments)
    {
        _parties  = parties;
        _payments = payments;

        NewCommand        = Command(OpenNew);
        EditCommand       = Command(OpenEdit,  () => Selected is not null);
        PayCommand        = Command(OpenPay,   () => Selected is not null);
        ConfirmPayCommand = AsyncCommand(ConfirmPayAsync, () => !IsBusy);
        CancelPayCommand  = Command(() => ShowPay = false);
        SaveCommand       = AsyncCommand(SaveAsync, () => !IsBusy);
        CancelCommand     = Command(() => ShowEdit = false);
        RefreshCommand    = AsyncCommand(LoadAsync);
    }

    public async Task LoadAsync()
    {
        var list = await _parties.SearchSuppliersAsync(string.IsNullOrWhiteSpace(Search) ? null : Search);
        Items.Clear(); foreach (var s in list) Items.Add(s);
    }

    private void OpenNew()  { _editId = 0; EditName = ""; EditPhone = ""; ShowEdit = true; }
    private void OpenEdit() { if (Selected is null) return; _editId = Selected.Id; EditName = Selected.Name; EditPhone = Selected.Phone ?? ""; ShowEdit = true; }
    private void OpenPay()  { PayAmount = ""; ShowPay = true; }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(EditName)) { ErrorMessage = "Name required."; return; }
        await RunAsync(async () =>
        {
            var s = new Supplier { Id = _editId, Name = EditName.Trim(), Phone = EditPhone.Trim(), IsActive = true };
            var r = await _parties.SaveSupplierAsync(s);
            if (!r.IsSuccess) { ErrorMessage = r.Error; return; }
            ShowEdit = false; await LoadAsync();
        });
    }

    private async Task ConfirmPayAsync()
    {
        if (!decimal.TryParse(PayAmount, out var v) || v <= 0) { ErrorMessage = "Enter a valid amount."; return; }
        await RunAsync(async () =>
        {
            var r = await _payments.PaySupplierAsync(Selected!.Id, Money.From(v), PayMethod, null, null, Guid.NewGuid());
            if (!r.IsSuccess) { ErrorMessage = r.Error; return; }
            ShowPay = false; await LoadAsync();
        });
    }
}
