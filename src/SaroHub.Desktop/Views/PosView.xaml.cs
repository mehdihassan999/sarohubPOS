// src/SaroHub.Desktop/Views/PosView.xaml.cs
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SaroHub.Desktop.ViewModels;

namespace SaroHub.Desktop.Views;

public partial class PosView : UserControl
{
    public PosView() => InitializeComponent();

    // Barcode scanner feeds into the search box on KeyDown
    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not PosViewModel vm) return;
        var term = SearchBox.Text.Trim();
        if (string.IsNullOrEmpty(term)) return;
        _ = vm.HandleBarcodeAsync(term);
        SearchBox.Clear();
        e.Handled = true;
    }

    private void ProductGrid_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not PosViewModel vm) return;
        if (ProductList.SelectedItem is SaroHub.Core.Dtos.ProductSearchItem item)
            vm.AddProductByItem(item);
    }

    // Money text boxes: convert formatted to long paisa
    private void CashBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PosViewModel vm) return;
        if (decimal.TryParse(CashBox.Text.Replace(",",""), out var v))
            vm.CashTendered = SaroHub.Domain.Common.Money.From(v);
    }
    private void KhataBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PosViewModel vm) return;
        if (decimal.TryParse(KhataBox.Text.Replace(",",""), out var v))
            vm.KhataPaisa = SaroHub.Domain.Common.Money.From(v);
    }
    private void BankBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PosViewModel vm) return;
        if (decimal.TryParse(BankBox.Text.Replace(",",""), out var v))
            vm.BankPaisa = SaroHub.Domain.Common.Money.From(v);
    }
}
