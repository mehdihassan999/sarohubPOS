// src/SaroHub.Desktop/ViewModels/ProductsViewModel.cs
using System.Collections.ObjectModel;
using System.Windows.Input;
using SaroHub.Core.Dtos;
using SaroHub.Core.Services;
using SaroHub.Desktop.Mvvm;
using SaroHub.Desktop.Services;
using SaroHub.Desktop.Views;
using SaroHub.Domain.Common;
using SaroHub.Domain.Entities;

namespace SaroHub.Desktop.ViewModels;

public sealed class ProductsViewModel : ViewModelBase, ILoadable
{
    private readonly ProductService   _products;
    private readonly WpfDialogService _dialog;
    private readonly IServiceProvider _sp;

    private string _search = "";
    public  string Search { get => _search; set { Set(ref _search, value); _ = LoadAsync(); } }

    public ObservableCollection<ProductSearchItem> Items { get; } = new();

    private ProductSearchItem? _selected;
    public  ProductSearchItem? Selected { get => _selected; set => Set(ref _selected, value); }

    public ICommand NewCommand    { get; }
    public ICommand EditCommand   { get; }
    public ICommand RefreshCommand { get; }

    public ProductsViewModel(ProductService products, WpfDialogService dialog, IServiceProvider sp)
    {
        _products = products;
        _dialog   = dialog;
        _sp       = sp;

        NewCommand     = Command(OpenNew);
        EditCommand    = Command(OpenEdit, () => Selected is not null);
        RefreshCommand = AsyncCommand(LoadAsync);
    }

    public async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var list = await _products.SearchAsync(string.IsNullOrWhiteSpace(Search) ? null : Search, 200);
            Items.Clear();
            foreach (var i in list) Items.Add(i);
        });
    }

    private void OpenNew()
    {
        var vm  = _sp.GetService(typeof(ProductEditViewModel)) as ProductEditViewModel ?? new ProductEditViewModel(_products);
        vm.LoadNew();
        var win = new ProductEditView { DataContext = vm };
        vm.Saved += () => { win.Close(); _ = LoadAsync(); };
        win.ShowDialog();
    }

    private async void OpenEdit()
    {
        if (Selected is null) return;
        var product = await _products.GetAsync(Selected.Id);
        if (product is null) return;
        var vm  = _sp.GetService(typeof(ProductEditViewModel)) as ProductEditViewModel ?? new ProductEditViewModel(_products);
        vm.LoadProduct(product);
        var win = new ProductEditView { DataContext = vm };
        vm.Saved += () => { win.Close(); _ = LoadAsync(); };
        win.ShowDialog();
    }
}

// ── ProductEditViewModel ────────────────────────────────────────────────────
public sealed class ProductEditViewModel : ViewModelBase
{
    private readonly ProductService _products;

    public ICommand SaveCommand   { get; }
    public ICommand CancelCommand { get; }
    public event Action? Saved;

    private int    _id;
    private string _name          = "";
    private string _sku           = "";
    private string _model         = "";
    private string _barcode        = "";
    private string _notes         = "";
    private bool   _isActive      = true;
    private bool   _tracksSerial;
    private int    _warrantyMonths;
    private string _salePriceStr  = "0";
    private string _purchPriceStr = "0";
    private string _wholePriceStr = "0";
    private string _minStockStr   = "0";

    public string Name          { get => _name;          set => Set(ref _name, value); }
    public string Sku           { get => _sku;           set => Set(ref _sku, value); }
    public string Model         { get => _model;         set => Set(ref _model, value); }
    public string Barcode       { get => _barcode;       set => Set(ref _barcode, value); }
    public string Notes         { get => _notes;         set => Set(ref _notes, value); }
    public bool   IsActive      { get => _isActive;      set => Set(ref _isActive, value); }
    public bool   TracksSerial  { get => _tracksSerial;  set => Set(ref _tracksSerial, value); }
    public int    WarrantyMonths{ get => _warrantyMonths;set => Set(ref _warrantyMonths, value); }
    public string SalePrice     { get => _salePriceStr;  set => Set(ref _salePriceStr, value); }
    public string PurchasePrice { get => _purchPriceStr; set => Set(ref _purchPriceStr, value); }
    public string WholesalePrice{ get => _wholePriceStr; set => Set(ref _wholePriceStr, value); }
    public string MinStock      { get => _minStockStr;   set => Set(ref _minStockStr, value); }

    public ObservableCollection<Unit>     Units      { get; } = new();
    public ObservableCollection<Category> Categories { get; } = new();
    public ObservableCollection<Brand>    Brands     { get; } = new();

    private Unit?     _baseUnit;
    private Category? _category;
    private Brand?    _brand;
    public Unit?     BaseUnit  { get => _baseUnit;  set => Set(ref _baseUnit, value); }
    public Category? Category  { get => _category;  set => Set(ref _category, value); }
    public Brand?    Brand     { get => _brand;     set => Set(ref _brand, value); }

    public string Title => _id == 0 ? "New Product" : "Edit Product";

    public ProductEditViewModel(ProductService products)
    {
        _products  = products;
        SaveCommand   = AsyncCommand(SaveAsync, () => !IsBusy);
        CancelCommand = Command(() => { /* window closed by view */ });
        _ = LoadLookupsAsync();
    }

    private async Task LoadLookupsAsync()
    {
        var units = await _products.GetUnitsAsync();
        var cats  = await _products.GetCategoriesAsync();
        var brands= await _products.GetBrandsAsync();
        Units.Clear();      foreach (var u in units)  Units.Add(u);
        Categories.Clear(); foreach (var c in cats)   Categories.Add(c);
        Brands.Clear();     foreach (var b in brands) Brands.Add(b);
    }

    public void LoadNew() { _id = 0; OnPropertyChanged(nameof(Title)); }

    public void LoadProduct(Product p)
    {
        _id            = p.Id;
        Name           = p.Name;
        Sku            = p.Sku ?? "";
        Model          = p.Model ?? "";
        Notes          = p.Notes ?? "";
        IsActive       = p.IsActive;
        TracksSerial   = p.TracksSerial;
        WarrantyMonths = p.WarrantyMonths;
        SalePrice      = Money.To(p.SalePricePaisa).ToString("0.00");
        PurchasePrice  = Money.To(p.PurchasePricePaisa).ToString("0.00");
        WholesalePrice = Money.To(p.WholesalePricePaisa).ToString("0.00");
        MinStock       = Qty.Format(p.MinStockQtyRaw);
        Barcode        = p.Barcodes.FirstOrDefault()?.Code ?? "";
        BaseUnit       = Units.FirstOrDefault(u => u.Id == p.BaseUnitId);
        Category       = Categories.FirstOrDefault(c => c.Id == p.CategoryId);
        Brand          = Brands.FirstOrDefault(b => b.Id == p.BrandId);
        OnPropertyChanged(nameof(Title));
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name)) { ErrorMessage = "Product name is required."; return; }
        if (BaseUnit is null) { ErrorMessage = "Select a unit."; return; }

        var edited = new Product
        {
            Id                = _id,
            Name              = Name.Trim(),
            Sku               = string.IsNullOrWhiteSpace(Sku) ? null : Sku.Trim(),
            Model             = Model,
            Notes             = Notes,
            IsActive          = IsActive,
            TracksSerial      = TracksSerial,
            WarrantyMonths    = WarrantyMonths,
            SalePricePaisa    = decimal.TryParse(SalePrice, out var sp) ? Money.From(sp) : 0,
            PurchasePricePaisa= decimal.TryParse(PurchasePrice, out var pp) ? Money.From(pp) : 0,
            WholesalePricePaisa= decimal.TryParse(WholesalePrice, out var wp) ? Money.From(wp) : 0,
            MinStockQtyRaw    = decimal.TryParse(MinStock, out var ms) ? Qty.From(ms) : 0,
            BaseUnitId        = BaseUnit!.Id,
            CategoryId        = Category?.Id,
            BrandId           = Brand?.Id
        };

        await RunAsync(async () =>
        {
            var barcodes = string.IsNullOrWhiteSpace(Barcode)
                ? Array.Empty<string>() : new[] { Barcode.Trim() };
            var result = await _products.SaveAsync(edited, barcodes, Array.Empty<(int, decimal)>());
            if (!result.IsSuccess) { ErrorMessage = result.Error; return; }
            Saved?.Invoke();
        });
    }
}
