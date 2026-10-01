// src/SaroHub.Desktop/ViewModels/StockViewModel.cs
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

public sealed class StockViewModel : ViewModelBase, ILoadable
{
    private readonly StockService     _stock;
    private readonly ProductService   _products;
    private readonly WpfDialogService _dialog;

    private string _search = "";
    public  string Search { get => _search; set { Set(ref _search, value); _ = LoadAsync(); } }

    public ObservableCollection<ProductSearchItem> Items { get; } = new();

    private ProductSearchItem? _selected;
    public ProductSearchItem? Selected { get => _selected; set => Set(ref _selected, value); }

    // ── Adjustment panel ──────────────────────────────────────────────────
    private bool   _showAdjust;
    private string _adjQty    = "";
    private string _adjReason = "";
    private StockMovementType _adjType = StockMovementType.AdjustmentIn;

    public bool   ShowAdjust { get => _showAdjust; set => Set(ref _showAdjust, value); }
    public string AdjQty    { get => _adjQty;    set => Set(ref _adjQty, value); }
    public string AdjReason { get => _adjReason; set => Set(ref _adjReason, value); }
    public StockMovementType AdjType { get => _adjType; set => Set(ref _adjType, value); }
    public IReadOnlyList<StockMovementType> AdjTypes { get; } =
        new[] { StockMovementType.AdjustmentIn, StockMovementType.AdjustmentOut, StockMovementType.Damaged, StockMovementType.Lost };

    public ObservableCollection<LowStockRow> LowStock { get; } = new();

    public ICommand AdjustCommand        { get; }
    public ICommand ConfirmAdjCommand    { get; }
    public ICommand CancelAdjCommand     { get; }
    public ICommand RefreshCommand       { get; }

    public StockViewModel(StockService stock, ProductService products, WpfDialogService dialog)
    {
        _stock    = stock;
        _products = products;
        _dialog   = dialog;

        AdjustCommand     = Command(OpenAdjust, () => Selected is not null);
        ConfirmAdjCommand = AsyncCommand(ConfirmAdjAsync, () => !IsBusy);
        CancelAdjCommand  = Command(() => ShowAdjust = false);
        RefreshCommand    = AsyncCommand(LoadAsync);
    }

    public async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var list = await _products.SearchAsync(string.IsNullOrWhiteSpace(Search) ? null : Search, 200);
            Items.Clear();
            foreach (var i in list) Items.Add(i);
            var low = await _stock.LowStockAsync(20);
            LowStock.Clear();
            foreach (var r in low) LowStock.Add(r);
        });
    }

    private void OpenAdjust() { AdjQty = ""; AdjReason = ""; ShowAdjust = true; }

    private async Task ConfirmAdjAsync()
    {
        if (Selected is null) return;
        if (!decimal.TryParse(AdjQty, out var qty) || qty <= 0) { ErrorMessage = "Enter a valid quantity."; return; }
        if (string.IsNullOrWhiteSpace(AdjReason)) { ErrorMessage = "Reason is required."; return; }

        await RunAsync(async () =>
        {
            var result = await _stock.AdjustAsync(Selected.Id, qty, AdjType, AdjReason.Trim());
            if (!result.IsSuccess) { ErrorMessage = result.Error; return; }
            ShowAdjust = false;
            ErrorMessage = "";
            await LoadAsync();
        });
    }
}
