// src/SaroHub.Desktop/ViewModels/PosViewModel.cs
using System.Collections.ObjectModel;
using System.Windows.Input;
using SaroHub.Core.Abstractions;
using SaroHub.Core.Dtos;
using SaroHub.Core.Services;
using SaroHub.Desktop.Mvvm;
using SaroHub.Desktop.Services;
using SaroHub.Domain;
using SaroHub.Domain.Common;
using SaroHub.Domain.Entities;

namespace SaroHub.Desktop.ViewModels;

/// <summary>One line on the current bill.</summary>
public sealed class BillLine : ViewModelBase
{
    public int     ProductId   { get; init; }
    public int     UnitId      { get; init; }
    public string  ProductName { get; init; } = "";
    public string  UnitSymbol  { get; init; } = "";

    private decimal _qty = 1;
    private long    _unitPrice;
    private long    _discount;

    public decimal Qty
    {
        get => _qty;
        set { if (Set(ref _qty, value > 0 ? value : 1)) { OnPropertyChanged(nameof(LineTotal)); } }
    }
    public long UnitPricePaisa
    {
        get => _unitPrice;
        set { Set(ref _unitPrice, value); OnPropertyChanged(nameof(LineTotal)); OnPropertyChanged(nameof(UnitPriceFormatted)); }
    }
    public long DiscountPaisa
    {
        get => _discount;
        set { Set(ref _discount, value); OnPropertyChanged(nameof(LineTotal)); }
    }

    public long    LineTotal          => Money.Multiply(UnitPricePaisa, Qty) - DiscountPaisa;
    public string  UnitPriceFormatted => Money.Format(UnitPricePaisa);
    public string  LineTotalFormatted => Money.Format(LineTotal);
    public decimal AvailableQty       { get; init; }
    public long    UnitCostPaisa      { get; init; }
}

public sealed class PosViewModel : ViewModelBase, ILoadable
{
    private readonly ProductService   _products;
    private readonly SalesService     _sales;
    private readonly PartyService     _parties;
    private readonly IReceiptPrinter  _printer;
    private readonly WpfDialogService _dialog;

    // ── Bill ─────────────────────────────────────────────────────────────
    public ObservableCollection<BillLine> BillLines { get; } = new();

    private long _invoiceDiscount;
    private long _taxPaisa;
    private string _note = "";

    public long    InvoiceDiscountPaisa { get => _invoiceDiscount; set { Set(ref _invoiceDiscount, value); RaiseTotals(); } }
    public long    TaxPaisa             { get => _taxPaisa;        set { Set(ref _taxPaisa, value);        RaiseTotals(); } }
    public string  Note                 { get => _note;            set => Set(ref _note, value); }

    public long    SubtotalPaisa   => BillLines.Sum(l => Money.Multiply(l.UnitPricePaisa, l.Qty));
    public long    LineDiscounts   => BillLines.Sum(l => l.DiscountPaisa);
    public long    TotalPaisa      => SubtotalPaisa - LineDiscounts - InvoiceDiscountPaisa + TaxPaisa;
    public string  SubtotalFormatted   => Money.Format(SubtotalPaisa);
    public string  TotalFormatted      => Money.Format(TotalPaisa);
    public string  DiscountsFormatted  => Money.Format(LineDiscounts + InvoiceDiscountPaisa);

    // ── Payment ───────────────────────────────────────────────────────────
    private long   _cashTendered;
    private long   _khataPaisa;
    private long   _bankPaisa;
    private long   _mobilePaisa;

    public long    CashTendered { get => _cashTendered; set { Set(ref _cashTendered, value); RaiseChange(); } }
    public long    KhataPaisa   { get => _khataPaisa;   set { Set(ref _khataPaisa, value);   RaiseChange(); } }
    public long    BankPaisa    { get => _bankPaisa;    set { Set(ref _bankPaisa, value);     RaiseChange(); } }
    public long    MobilePaisa  { get => _mobilePaisa;  set { Set(ref _mobilePaisa, value);   RaiseChange(); } }

    public long    NonCashTotal => KhataPaisa + BankPaisa + MobilePaisa;
    public long    DueForCash   => Math.Max(0, TotalPaisa - NonCashTotal);
    public long    Change       => Math.Max(0, CashTendered - DueForCash);
    public string  ChangeFormatted => Money.Format(Change);
    public string  DueFormatted    => Money.Format(DueForCash);

    // ── Customer ──────────────────────────────────────────────────────────
    private Customer? _customer;
    public  Customer? SelectedCustomer { get => _customer; set { Set(ref _customer, value); OnPropertyChanged(nameof(CustomerName)); } }
    public  string    CustomerName     => _customer?.Name ?? "Walk-in";

    // ── Product search ─────────────────────────────────────────────────
    private string _searchTerm = "";
    public  string SearchTerm
    {
        get => _searchTerm;
        set { Set(ref _searchTerm, value); _ = SearchProductsAsync(); }
    }
    public ObservableCollection<ProductSearchItem> SearchResults { get; } = new();

    // ── Last sale result ──────────────────────────────────────────────
    private string _lastInvoice = "";
    private bool   _showSuccess;
    public  string LastInvoice  { get => _lastInvoice;  private set => Set(ref _lastInvoice, value); }
    public  bool   ShowSuccess  { get => _showSuccess;  private set => Set(ref _showSuccess, value); }

    // ── Commands ─────────────────────────────────────────────────────────
    public ICommand AddProductCommand    { get; }
    public ICommand RemoveLineCommand    { get; }
    public ICommand IncrQtyCommand       { get; }
    public ICommand DecrQtyCommand       { get; }
    public ICommand ChargeCommand        { get; }
    public ICommand ClearBillCommand     { get; }
    public ICommand QuickCashCommand     { get; }

    public PosViewModel(ProductService products, SalesService sales,
                        PartyService parties, IReceiptPrinter printer, WpfDialogService dialog)
    {
        _products = products;
        _sales    = sales;
        _parties  = parties;
        _printer  = printer;
        _dialog   = dialog;

        BillLines.CollectionChanged += (_, _) => RaiseTotals();

        AddProductCommand = new RelayCommand<ProductSearchItem>(AddProduct);
        RemoveLineCommand = new RelayCommand<BillLine>(RemoveLine);
        IncrQtyCommand    = new RelayCommand<BillLine>(l => { if (l is not null) l.Qty++; RaiseTotals(); });
        DecrQtyCommand    = new RelayCommand<BillLine>(l => { if (l is not null && l.Qty > 1) l.Qty--; RaiseTotals(); });
        ChargeCommand     = AsyncCommand(ChargeAsync, () => !IsBusy && BillLines.Count > 0);
        ClearBillCommand  = Command(ClearBill, () => BillLines.Count > 0);
        QuickCashCommand  = Command(() => CashTendered = TotalPaisa);
    }

    public Task LoadAsync() => SearchProductsAsync();

    private async Task SearchProductsAsync()
    {
        var results = await _products.SearchAsync(_searchTerm, 40);
        SearchResults.Clear();
        foreach (var r in results) SearchResults.Add(r);
    }

    public void AddProductByItem(ProductSearchItem item) => AddProduct(item);

    private void AddProduct(ProductSearchItem? item)
    {
        if (item is null) return;
        var existing = BillLines.FirstOrDefault(l => l.ProductId == item.Id);
        if (existing is not null) { existing.Qty++; RaiseTotals(); return; }
        BillLines.Add(new BillLine
        {
            ProductId    = item.Id,
            UnitId       = 0, // base unit; resolved in service
            ProductName  = item.Name,
            UnitSymbol   = item.UnitSymbol,
            UnitPricePaisa = item.SalePricePaisa,
            AvailableQty = Qty.To(item.StockQtyRaw)
        });
        SearchTerm = "";
        RaiseTotals();
    }

    private void RemoveLine(BillLine? line)
    {
        if (line is null) return;
        BillLines.Remove(line);
        RaiseTotals();
    }

    public async Task HandleBarcodeAsync(string barcode)
    {
        var product = await _products.FindByBarcodeAsync(barcode);
        if (product is null) { ErrorMessage = $"Product not found: {barcode}"; return; }
        AddProduct(new ProductSearchItem(
            product.Id, product.Name, product.Sku,
            product.Brand?.Name, product.BaseUnit?.Symbol ?? "",
            product.SalePricePaisa, product.StockQtyRaw, product.MinStockQtyRaw));
    }

    private void ClearBill()
    {
        BillLines.Clear();
        CashTendered = 0; KhataPaisa = 0; BankPaisa = 0; MobilePaisa = 0;
        InvoiceDiscountPaisa = 0; TaxPaisa = 0; Note = "";
        SelectedCustomer = null;
        ShowSuccess = false;
        RaiseTotals();
    }

    private async Task ChargeAsync()
    {
        if (BillLines.Count == 0) { ErrorMessage = "Add products first."; return; }
        if (CashTendered < DueForCash) { ErrorMessage = "Cash is not enough."; return; }
        if (KhataPaisa > 0 && SelectedCustomer is null)
        { ErrorMessage = "Select a customer for Khata."; return; }

        var lines = BillLines.Select(l => new NewSaleLine(
            l.ProductId, l.UnitId == 0
                ? GetBaseUnitId(l.ProductId)
                : l.UnitId,
            l.Qty, l.UnitPricePaisa, l.DiscountPaisa)).ToList();

        var payments = new List<NewSalePayment>();
        if (CashTendered > 0)  payments.Add(new(PaymentMethod.Cash,  CashTendered,  null));
        if (BankPaisa > 0)     payments.Add(new(PaymentMethod.Bank,   BankPaisa,    null));
        if (MobilePaisa > 0)   payments.Add(new(PaymentMethod.Easypaisa, MobilePaisa, null));
        if (KhataPaisa > 0)    payments.Add(new(PaymentMethod.Khata,  KhataPaisa,   null));
        if (payments.Count == 0) payments.Add(new(PaymentMethod.Cash, TotalPaisa, null));

        var req = new NewSaleRequest(
            Guid.NewGuid(), SelectedCustomer?.Id,
            lines, payments,
            InvoiceDiscountPaisa, TaxPaisa, Note);

        await RunAsync(async () =>
        {
            var result = await _sales.CreateSaleAsync(req);
            if (!result.IsSuccess) { ErrorMessage = result.Error; return; }
            LastInvoice = result.Value!.InvoiceNo;
            ShowSuccess = true;
            _ = _printer.PrintSaleAsync(result.Value.SaleId, false);
            ClearBill();
        });
    }

    // When no explicit unit is set on the line (UnitId==0), the sales service picks the base unit
    // automatically via the fallback: productUnits.First(u => u.IsBase). Passing 0 is intentional.
    private static int GetBaseUnitId(int _) => 0; // service falls back to base unit

    private void RaiseTotals()
    {
        OnPropertyChanged(nameof(SubtotalPaisa));
        OnPropertyChanged(nameof(SubtotalFormatted));
        OnPropertyChanged(nameof(LineDiscounts));
        OnPropertyChanged(nameof(DiscountsFormatted));
        OnPropertyChanged(nameof(TotalPaisa));
        OnPropertyChanged(nameof(TotalFormatted));
        OnPropertyChanged(nameof(DueForCash));
        OnPropertyChanged(nameof(DueFormatted));
        RaiseChange();
    }

    private void RaiseChange()
    {
        OnPropertyChanged(nameof(NonCashTotal));
        OnPropertyChanged(nameof(DueForCash));
        OnPropertyChanged(nameof(DueFormatted));
        OnPropertyChanged(nameof(Change));
        OnPropertyChanged(nameof(ChangeFormatted));
    }
}
