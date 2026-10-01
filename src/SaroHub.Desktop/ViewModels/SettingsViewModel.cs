// src/SaroHub.Desktop/ViewModels/SettingsViewModel.cs
using System.Windows.Input;
using SaroHub.Core.Services;
using SaroHub.Desktop.Mvvm;
using SaroHub.Desktop.Services;

namespace SaroHub.Desktop.ViewModels;

public sealed class SettingsViewModel : ViewModelBase, ILoadable
{
    private readonly SettingsService  _settings;
    private readonly WpfDialogService _dialog;

    private string _shopName        = "";
    private string _ownerName       = "";
    private string _shopPhone       = "";
    private string _shopAddress     = "";
    private string _currency        = "PKR";
    private string _language        = "en";
    private string _receiptFooter   = "";
    private string _backupFolder    = "";
    private string _backupFreqHours = "24";
    private bool   _allowNegative;

    public string ShopName        { get => _shopName;        set => Set(ref _shopName, value); }
    public string OwnerName       { get => _ownerName;       set => Set(ref _ownerName, value); }
    public string ShopPhone       { get => _shopPhone;       set => Set(ref _shopPhone, value); }
    public string ShopAddress     { get => _shopAddress;     set => Set(ref _shopAddress, value); }
    public string Currency        { get => _currency;        set => Set(ref _currency, value); }
    public string Language        { get => _language;        set => Set(ref _language, value); }
    public string ReceiptFooter   { get => _receiptFooter;   set => Set(ref _receiptFooter, value); }
    public string BackupFolder    { get => _backupFolder;    set => Set(ref _backupFolder, value); }
    public string BackupFreqHours { get => _backupFreqHours; set => Set(ref _backupFreqHours, value); }
    public bool   AllowNegativeStock { get => _allowNegative; set => Set(ref _allowNegative, value); }

    public IReadOnlyList<string> Languages { get; } = new[] { "en", "ur" };

    public ICommand SaveCommand         { get; }
    public ICommand BrowseFolderCommand { get; }

    public SettingsViewModel(SettingsService settings, WpfDialogService dialog)
    {
        _settings = settings;
        _dialog   = dialog;
        SaveCommand         = AsyncCommand(SaveAsync, () => !IsBusy);
        BrowseFolderCommand = Command(BrowseFolder);
    }

    public Task LoadAsync()
    {
        ShopName          = _settings.Get(SettingsService.ShopName);
        OwnerName         = _settings.Get(SettingsService.OwnerName);
        ShopPhone         = _settings.Get(SettingsService.ShopPhone);
        ShopAddress       = _settings.Get(SettingsService.ShopAddress);
        Currency          = _settings.Get(SettingsService.Currency, "PKR");
        Language          = _settings.Get(SettingsService.Language, "en");
        ReceiptFooter     = _settings.Get(SettingsService.ReceiptFooter);
        BackupFolder      = _settings.Get(SettingsService.BackupFolder, Infrastructure.AppPaths.BackupFolder);
        BackupFreqHours   = _settings.Get(SettingsService.BackupFrequencyHours, "24");
        AllowNegativeStock= _settings.GetBool(SettingsService.AllowNegativeStock);
        return Task.CompletedTask;
    }

    private void BrowseFolder()
    {
        var f = _dialog.PickFolder("Select backup folder");
        if (f is not null) BackupFolder = f;
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(ShopName)) { ErrorMessage = "Shop name is required."; return; }
        await RunAsync(async () =>
        {
            await _settings.SetManyAsync(new Dictionary<string, string>
            {
                [SettingsService.ShopName]            = ShopName.Trim(),
                [SettingsService.OwnerName]           = OwnerName.Trim(),
                [SettingsService.ShopPhone]           = ShopPhone.Trim(),
                [SettingsService.ShopAddress]         = ShopAddress,
                [SettingsService.Currency]            = Currency.Trim(),
                [SettingsService.Language]            = Language,
                [SettingsService.ReceiptFooter]       = ReceiptFooter,
                [SettingsService.BackupFolder]        = BackupFolder,
                [SettingsService.BackupFrequencyHours]= BackupFreqHours,
                [SettingsService.AllowNegativeStock]  = AllowNegativeStock.ToString()
            });
            ErrorMessage = "";
        });
    }
}
