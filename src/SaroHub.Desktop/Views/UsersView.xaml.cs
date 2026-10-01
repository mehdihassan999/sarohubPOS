// src/SaroHub.Desktop/Views/UsersView.xaml.cs
using System.Windows.Controls;
using SaroHub.Desktop.ViewModels;

namespace SaroHub.Desktop.Views;

public partial class UsersView : UserControl
{
    public UsersView() => InitializeComponent();

    private void PwBox_Changed(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is UsersViewModel vm)
            vm.EditPassword = PwBox.Password;
    }
}
