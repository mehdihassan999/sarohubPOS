// src/SaroHub.Desktop/Views/FirstRunView.xaml.cs
using System.Windows;
using SaroHub.Desktop.ViewModels;

namespace SaroHub.Desktop.Views;

public partial class FirstRunView : Window
{
    public FirstRunView() => InitializeComponent();

    private void PwBox_Changed(object sender, RoutedEventArgs e)
    {
        if (DataContext is FirstRunViewModel vm)
            vm.Password = PwBox.Password;
    }
}
