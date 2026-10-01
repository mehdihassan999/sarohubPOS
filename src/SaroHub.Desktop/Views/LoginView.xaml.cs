// src/SaroHub.Desktop/Views/LoginView.xaml.cs
using System.Windows;
using System.Windows.Input;
using SaroHub.Desktop.ViewModels;

namespace SaroHub.Desktop.Views;

public partial class LoginView : Window
{
    public LoginView() => InitializeComponent();

    // Bridge password box (WPF PasswordBox doesn't support binding)
    private void LoginBtn_Click(object sender, RoutedEventArgs e)
        => SetPassword();

    private void PwBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { SetPassword(); (DataContext as LoginViewModel)?.LoginCommand.Execute(null); }
    }

    private void SetPassword()
    {
        if (DataContext is LoginViewModel vm)
            vm.Password = PwBox.Password;
    }
}
