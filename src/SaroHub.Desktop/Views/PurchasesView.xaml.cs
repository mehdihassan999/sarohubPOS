// src/SaroHub.Desktop/Views/PurchasesView.xaml.cs
using System.Windows.Controls;
using SaroHub.Core.Dtos;
using SaroHub.Desktop.ViewModels;

namespace SaroHub.Desktop.Views;

public partial class PurchasesView : UserControl
{
    public PurchasesView() => InitializeComponent();

    private void ProductList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not PurchasesViewModel vm) return;
        if ((sender as ListBox)?.SelectedItem is ProductSearchItem item)
            vm.AddLineCommand.Execute(item);
    }
}
