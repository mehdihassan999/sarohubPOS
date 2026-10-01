// src/SaroHub.Desktop/ViewModels/FirstRunViewModel.cs
using System.Windows.Input;
using SaroHub.Core.Services;
using SaroHub.Desktop.Mvvm;
using SaroHub.Desktop.Services;

namespace SaroHub.Desktop.ViewModels;

public sealed class FirstRunViewModel : ViewModelBase
{
    private readonly SetupService     _setup;
    private readonly WpfDialogService _dialog;

    // ── Step state ────────────────────────────────────────────────────────────
    private int _step = 1;
    public  int Step { get => _step; private set { Set(ref _step, value); OnPropertyChanged(nameof(StepLabel)); } }
    public  string StepLabel => $"Step {_step} of 8";

    // ── Fields ────────────────────────────────────────────────────────────────
    private string _shopName   = "";
    private string _ownerName  = "";
    private string _phone      = "";
    private string _address    = "";
    private string _shopType   = "Electrical";
    private string _currency   = "PKR";
    private string _language   = "en";
    private string _openingCash = "0";
    private string _username   = "";
    private string _password   = "";
    private string _pin        = "";
    private string _backupFolder = "";

    public string ShopName    { get => _shopName;    set => Set(ref _shopName, value); }
    public string OwnerName   { get => _ownerName;   set => Set(ref _ownerName, value); }
    public string Phone       { get => _phone;       set => Set(ref _phone, value); }
    public string Address     { get => _address;     set => Set(ref _address, value); }
    public string ShopType    { get => _shopType;    set => Set(ref _shopType, value); }
    public string Currency    { get => _currency;    set => Set(ref _currency, value); }
    public string Language    { get => _language;    set => Set(ref _language, value); }
    public string OpeningCash { get => _openingCash; set => Set(ref _openingCash, value); }
    public string Username    { get => _username;    set => Set(ref _username, value); }
    public string Password    { get => _password;    set => Set(ref _password, value); }
    public string Pin         { get => _pin;         set => Set(ref _pin, value); }
    public string BackupFolder { get => _backupFolder; set => Set(ref _backupFolder, value); }

    public IReadOnlyList<string> ShopTypes { get; } = new[]
        { "Electrical", "Hardware", "Electronics", "Computer", "Sanitary/Plumbing", "Auto Parts", "Building Materials", "General Retail" };
    public IReadOnlyList<string> Languages { get; } = new[] { "en", "ur" };

    public ICommand NextCommand     { get; }
    public ICommand BackCommand     { get; }
    public ICommand BrowseCommand   { get; }
    public ICommand FinishCommand   { get; }

    public event Action? Completed;

    public FirstRunViewModel(SetupService setup, WpfDialogService dialog)
    {
        _setup  = setup;
        _dialog = dialog;
        _backupFolder = Infrastructure.AppPaths.BackupFolder;

        NextCommand   = Command(Next,   () => _step < 8);
        BackCommand   = Command(Back,   () => _step > 1);
        BrowseCommand = Command(Browse);
        FinishCommand = AsyncCommand(FinishAsync, () => !IsBusy);
    }

    private void Next()
    {
        if (!ValidateCurrentStep()) return;
        Step++;
    }

    private void Back() => Step--;

    private void Browse()
    {
        var folder = _dialog.PickFolder("Select backup folder");
        if (folder is not null) BackupFolder = folder;
    }

    private bool ValidateCurrentStep()
    {
        ErrorMessage = "";
        return _step switch
        {
            1 when string.IsNullOrWhiteSpace(ShopName)
                => Err("Shop name is required."),
            3 when string.IsNullOrWhiteSpace(Username)
                => Err("Username is required."),
            3 when Password.Length < 4
                => Err("Password must be at least 4 characters."),
            _ => true
        };
    }

    private bool Err(string msg) { ErrorMessage = msg; return false; }

    private async Task FinishAsync()
    {
        ErrorMessage = "";
        var openingCash = decimal.TryParse(OpeningCash, out var v) ? v : 0m;

        var data = new FirstRunData(
            ShopName.Trim(), OwnerName.Trim(), Phone.Trim(), Address.Trim(),
            ShopType, Currency, Language,
            SaroHub.Domain.Common.Money.From(openingCash),
            Username.Trim(), Password,
            string.IsNullOrWhiteSpace(Pin) ? null : Pin.Trim(),
            BackupFolder);

        await RunAsync(async () =>
        {
            var result = await _setup.CompleteAsync(data);
            if (!result.IsSuccess) { ErrorMessage = result.Error; return; }
            Completed?.Invoke();
        });
    }
}
