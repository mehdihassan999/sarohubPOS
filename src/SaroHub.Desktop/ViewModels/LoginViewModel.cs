// src/SaroHub.Desktop/ViewModels/LoginViewModel.cs
using System.Windows.Input;
using SaroHub.Core.Services;
using SaroHub.Desktop.Mvvm;

namespace SaroHub.Desktop.ViewModels;

public sealed class LoginViewModel : ViewModelBase
{
    private readonly AuthService _auth;

    private string _username = "";
    private string _password = "";

    public string Username { get => _username; set => Set(ref _username, value); }
    public string Password { get => _password; set => Set(ref _password, value); }

    public ICommand LoginCommand { get; }

    /// <summary>Raised on successful login so App.xaml.cs can swap windows.</summary>
    public event Action? LoginSucceeded;

    public LoginViewModel(AuthService auth)
    {
        _auth = auth;
        LoginCommand = AsyncCommand(DoLoginAsync, () => !IsBusy);
    }

    private async Task DoLoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username))
        { ErrorMessage = "Enter your username."; return; }
        if (string.IsNullOrWhiteSpace(Password))
        { ErrorMessage = "Enter your password."; return; }

        await RunAsync(async () =>
        {
            var result = await _auth.LoginAsync(Username.Trim(), Password);
            if (!result.IsSuccess)
                ErrorMessage = result.Error;
            else
                LoginSucceeded?.Invoke();
        });
    }
}
