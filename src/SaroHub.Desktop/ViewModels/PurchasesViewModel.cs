// src/SaroHub.Desktop/ViewModels/PurchasesViewModel.cs
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

public sealed class PurchaseLine : ViewModelBase
{
    public int    ProductId   { get; init; }
    public string ProductName { get; init; } = "";
    public string UnitSymbol  { get; init; } = "";
    private decimal _qty = 1;
    private long    _cost;
    public decimal Qty      { get => _qty;  set { Set(ref _qty, value  > 0 ? value : 1); OnPropertyChanged(nameof(LineTotal)); } }
    public long    CostPaisa{ get => _cost; set { Set(ref _cost, value);                 OnPropertyChanged(nameof(LineTotal)); } }
    public long    LineTotal => Money.Multiply(CostPaisa, Qty);
    public string  LineTotalFormatted => Money.Format(LineTotal);
    public string  CostFormatted => Money.Format(CostPaisa);
}

public sealed class PurchasesViewModel : ViewModelBase, ILoadable
{
    private readonly StockService     _stock;
    private readonly ProductService   _products;
    private readonly PartyService     _parties;
    private readonly WpfDialogService _dialog;

    // ── New purchase ──────────────────────────────────────────────────────
    private bool   _showNew;
    public  bool   ShowNew { get => _showNew; set => Set(ref _showNew, value); }

    public ObservableCollection<PurchaseLine> Lines    { get; } = new();
    public ObservableCollection<Supplier>     Suppliers{ get; } = new();
    public ObservableCollection<ProductSearchItem> Products { get; } = new();

    private Supplier? _supplier;
    private string    _suppInvNo     = "";
    private string    _discountStr   = "0";
    private string    _paidStr       = "0";
    private PaymentMethod _paidMethod = PaymentMethod.Cash;
    private string    _productSearch = "";

    public Supplier? SelectedSupplier { get => _supplier;    set => Set(ref _supplier, value); }
    public string    SuppInvNo        { get => _suppInvNo;   set => Set(ref _suppInvNo, value); }
    public string    Discount         { get => _discountStr; set => Set(ref _discountStr, value); }
    public string    Paid             { get => _paidStr;     set => Set(ref _paidStr, value); }
    public PaymentMethod PaidMethod   { get => _paidMethod;  set => Set(ref _paidMethod, value); }
    public string    ProductSearch    { get => _productSearch; set { Set(ref _productSearch, value); _ = SearchProductsAsync(); } }

    public IReadOnlyList<PaymentMethod> PayMethods { get; } =
        new[] { PaymentMethod.Cash, PaymentMethod.Bank, PaymentMethod.Easypaisa, PaymentMethod.JazzCash };

    public long TotalPaisa    => Lines.Sum(l => l.LineTotal) - (decimal.TryParse(Discount, out var d) ? Money.From(d) : 0);
    public string TotalFormatted => Money.Format(TotalPaisa);

    // ── History (recent stock ins) ─────────────────────────────────────────
    public ObservableCollection<StockIn> History { get; } = new();

    public ICommand NewPurchaseCommand   { get; }
    public ICommand SaveCommand          { get; }
    public ICommand CancelCommand        { get; }
    public ICommand AddLineCommand       { get; }
    public ICommand RemoveLineCommand    { get; }
    public ICommand RefreshCommand       { get; }

    public PurchasesViewModel(StockService stock, ProductService products, PartyService parties, WpfDialogService dialog)
    {
        _stock    = stock;
        _products = products;
        _parties  = parties;
        _dialog   = dialog;
        Lines.CollectionChanged += (_, _) => OnPropertyChanged(nameof(TotalPaisa));

        NewPurchaseCommand = Command(OpenNew);
        SaveCommand        = AsyncCommand(SaveAsync, () => !IsBusy);
        CancelCommand      = Command(() => ShowNew = false);
        AddLineCommand     = new RelayCommand<ProductSearchItem>(AddLine);
        RemoveLineCommand  = new RelayCommand<PurchaseLine>(l => { if (l is not null) { Lines.Remove(l); OnPropertyChanged(nameof(TotalPaisa)); } });
        RefreshCommand     = AsyncCommand(LoadAsync);
    }

    public async Task LoadAsync()
    {
        var sups = await _parties.SearchSuppliersAsync(null);
        Suppliers.Clear(); foreach (var s in sups) Suppliers.Add(s);
    }

    private async Task SearchProductsAsync()
    {
        var list = await _products.SearchAsync(string.IsNullOrWhiteSpace(ProductSearch) ? null : ProductSearch, 40);
        Products.Clear(); foreach (var p in list) Products.Add(p);
    }

    private void OpenNew() { Lines.Clear(); ProductSearch = ""; ShowNew = true; }

    private void AddLine(ProductSearchItem? item)
    {
        if (item is null) return;
        var ex = Lines.FirstOrDefault(l => l.ProductId == item.Id);
        if (ex is not null) { ex.Qty++; return; }
        Lines.Add(new PurchaseLine { ProductId = item.Id, ProductName = item.Name, UnitSymbol = item.UnitSymbol, CostPaisa = item.SalePricePaisa });
        OnPropertyChanged(nameof(TotalPaisa));
    }

    private async Task SaveAsync()
    {
        if (Lines.Count == 0) { ErrorMessage = "Add at least one product."; return; }
        var discount = decimal.TryParse(Discount, out var d) ? Money.From(d) : 0L;
        var paid     = decimal.TryParse(Paid,     out var p) ? Money.From(p) : TotalPaisa;

        var req = new NewStockInRequest(
            Guid.NewGuid(), SelectedSupplier?.Id,
            SuppInvNo,
            Lines.Select(l => new NewStockInLine(l.ProductId, 0, l.Qty, l.CostPaisa)).ToList(),
            discount, paid, PaidMethod, false);

        await RunAsync(async () =>
        {
            var result = await _stock.ReceiveStockAsync(req);
            if (!result.IsSuccess) { ErrorMessage = result.Error; return; }
            ShowNew = false;
            ErrorMessage = "";
            await LoadAsync();
        });
    }
}
