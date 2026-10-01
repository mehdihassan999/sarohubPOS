// src/SaroHub.Desktop/Services/ReceiptPrinterService.cs
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using SaroHub.Core.Abstractions;
using SaroHub.Core.Services;
using SaroHub.Domain.Common;
using SaroHub.Infrastructure;
using SaroHub.Infrastructure.Persistence;

namespace SaroHub.Desktop.Services;

public sealed class ReceiptPrinterService : IReceiptPrinter
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly SettingsService _settings;
    private readonly IAppLogger _log;

    public ReceiptPrinterService(IDbContextFactory<AppDbContext> factory,
                                 SettingsService settings, IAppLogger log)
    { _factory = factory; _settings = settings; _log = log; }

    public async Task<bool> PrintSaleAsync(int saleId, bool showDialog)
    {
        try
        {
            var text = await SaveSaleTextAsync(saleId);
            PrintText(text, showDialog);
            return true;
        }
        catch (Exception ex)
        {
            _log.Error("Print failed", ex);
            return false;
        }
    }

    public async Task<string> SaveSaleTextAsync(int saleId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var sale = await db.Sales.AsNoTracking()
            .Include(s => s.Items).Include(s => s.Payments).Include(s => s.Customer)
            .FirstOrDefaultAsync(s => s.Id == saleId);

        if (sale is null) return "";

        var shop    = _settings.Get(SettingsService.ShopName, "SaroHub POS");
        var address = _settings.Get(SettingsService.ShopAddress);
        var phone   = _settings.Get(SettingsService.ShopPhone);
        var footer  = _settings.Get(SettingsService.ReceiptFooter, "Thank you for your business.");

        var sb = new StringBuilder();
        int w = 40;
        sb.AppendLine(Center(shop, w));
        if (!string.IsNullOrWhiteSpace(address)) sb.AppendLine(Center(address, w));
        if (!string.IsNullOrWhiteSpace(phone))   sb.AppendLine(Center(phone, w));
        sb.AppendLine(new string('-', w));
        sb.AppendLine($"Invoice : {sale.InvoiceNo}");
        sb.AppendLine($"Date    : {sale.SaleAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}");
        if (sale.Customer is not null)
            sb.AppendLine($"Customer: {sale.Customer.Name}");
        sb.AppendLine(new string('-', w));
        foreach (var i in sale.Items)
        {
            var qty   = Qty.Format(i.QtyRaw);
            var price = Money.Format(i.UnitPricePaisa);
            var total = Money.Format(i.LineTotalPaisa);
            sb.AppendLine(i.ProductName);
            sb.AppendLine($"  {qty} {i.UnitSymbol} x {price,-14} {total,10}");
        }
        sb.AppendLine(new string('-', w));
        if (sale.DiscountPaisa > 0)
            sb.AppendLine($"{"Discount",-28}{Money.Format(sale.DiscountPaisa),12}");
        sb.AppendLine($"{"TOTAL",-28}{Money.Format(sale.TotalPaisa),12}");
        sb.AppendLine(new string('-', w));
        foreach (var p in sale.Payments)
            sb.AppendLine($"{p.Method,-28}{Money.Format(p.AmountPaisa),12}");
        if (sale.KhataPaisa > 0)
            sb.AppendLine($"{"Balance (Khata)",-28}{Money.Format(sale.KhataPaisa),12}");
        sb.AppendLine(new string('=', w));
        sb.AppendLine(Center(footer, w));
        sb.AppendLine();

        var text = sb.ToString();

        // Save to receipts folder
        var path = Path.Combine(AppPaths.ReceiptFolder, $"{sale.InvoiceNo}.txt");
        await File.WriteAllTextAsync(path, text, Encoding.UTF8);

        return text;
    }

    private static void PrintText(string text, bool showDialog)
    {
        var doc = new FlowDocument(new Paragraph(new Run(text))
        {
            FontFamily = new FontFamily("Courier New"),
            FontSize   = 11
        })
        { PageWidth = 300, PagePadding = new Thickness(10) };

        var dlg = new System.Windows.Controls.PrintDialog();
        if (!showDialog || dlg.ShowDialog() == true)
        {
            dlg.PrintDocument(
                ((IDocumentPaginatorSource)doc).DocumentPaginator,
                "SaroHub Receipt");
        }
    }

    private static string Center(string s, int width)
    {
        if (s.Length >= width) return s;
        var pad = (width - s.Length) / 2;
        return s.PadLeft(s.Length + pad).PadRight(width);
    }
}
