// src/SaroHub.Desktop/ViewModels/CustomersViewModel.cs
using System.Collections.ObjectModel;
using System.Windows.Input;
using SaroHub.Core.Services;
using SaroHub.Desktop.Mvvm;
using SaroHub.Domain.Entities;

namespace SaroHub.Desktop.ViewModels;

public sealed class CustomersViewModel : ViewModelBase, ILoadable
{
    private readonly PartyService _parties;

    private string    _search = "";
    public  string    Search { get => _search; set { Set(ref _search, value); _ = LoadAsync(); } }

    public ObservableCollection<Customer> Items { get; } = new();

    private Customer? _selected;
    public  Customer? Selected { get => _selected; set => Set(ref _selected, value); }

    // ── Edit panel ────────────────────────────────────────────────────────
    private bool   _showEdit;
    private int    _editId;
    private string _editName    = "";
    private string _editPhone   = "";
    private string _editAddress = "";

    public bool   ShowEdit   { get => _showEdit;    set => Set(ref _showEdit, value); }
    public string EditName   { get => _editName;    set => Set(ref _editName, value); }
    public string EditPhone  { get => _editPhone;   set => Set(ref _editPhone, value); }
    public string EditAddress{ get => _editAddress; set => Set(ref _editAddress, value); }

    public ICommand NewCommand     { get; }
    public ICommand EditCommand    { get; }
    public ICommand SaveCommand    { get; }
    public ICommand CancelCommand  { get; }
    public ICommand RefreshCommand { get; }

    public CustomersViewModel(PartyService parties)
    {
        _parties = parties;
        NewCommand     = Command(OpenNew);
        EditCommand    = Command(OpenEdit, () => Selected is not null);
        SaveCommand    = AsyncCommand(SaveAsync, () => !IsBusy);
        CancelCommand  = Command(() => ShowEdit = false);
        RefreshCommand = AsyncCommand(LoadAsync);
    }

    public async Task LoadAsync()
    {
        var list = await _parties.SearchCustomersAsync(string.IsNullOrWhiteSpace(Search) ? null : Search);
        Items.Clear(); foreach (var c in list) Items.Add(c);
    }

    private void OpenNew() { _editId = 0; EditName = ""; EditPhone = ""; EditAddress = ""; ShowEdit = true; }

    private void OpenEdit()
    {
        if (Selected is null) return;
        _editId = Selected.Id;
        EditName = Selected.Name; EditPhone = Selected.Phone ?? ""; EditAddress = Selected.Address ?? "";
        ShowEdit = true;
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(EditName)) { ErrorMessage = "Name is required."; return; }
        await RunAsync(async () =>
        {
            var c = new Customer { Id = _editId, Name = EditName.Trim(), Phone = EditPhone.Trim(), Address = EditAddress, IsActive = true };
            var r = await _parties.SaveCustomerAsync(c);
            if (!r.IsSuccess) { ErrorMessage = r.Error; return; }
            ShowEdit = false; ErrorMessage = "";
            await LoadAsync();
        });
    }
}
