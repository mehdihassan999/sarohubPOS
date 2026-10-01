// src/SaroHub.Desktop/Views/ProductEditView.xaml.cs
using System.Windows;

namespace SaroHub.Desktop.Views;

public partial class ProductEditView : Window
{
    public ProductEditView() => InitializeComponent();

    private void CancelBtn_Click(object sender, RoutedEventArgs e) => Close();
}
