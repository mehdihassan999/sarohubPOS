// src/SaroHub.Desktop/ViewModels/BackupViewModel.cs
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using SaroHub.Core.Abstractions;
using SaroHub.Core.Services;
using SaroHub.Desktop.Mvvm;
using SaroHub.Desktop.Services;
using SaroHub.Domain.Common;
using SaroHub.Domain.Entities;
using SaroHub.Infrastructure.Persistence;

namespace SaroHub.Desktop.ViewModels;

public sealed class BackupViewModel : ViewModelBase, ILoadable
{
    private readonly IBackupService              _backup;
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly WpfDialogService            _dialog;
    private readonly SettingsService             _settings;
    private readonly IAppLogger                  _log;

    private string _lastBackupInfo = "No backups yet.";
    private string _backupFolder   = "";
    public  string LastBackupInfo { get => _lastBackupInfo; private set => Set(ref _lastBackupInfo, value); }
    public  string BackupFolder   { get => _backupFolder;   private set => Set(ref _backupFolder, value); }

    public ObservableCollection<BackupRecord> History { get; } = new();

    public ICommand BackupNowCommand       { get; }
    public ICommand BackupToUsbCommand     { get; }
    public ICommand RestoreCommand         { get; }
    public ICommand OpenFolderCommand      { get; }
    public ICommand RefreshCommand         { get; }

    public BackupViewModel(IBackupService backup, IDbContextFactory<AppDbContext> factory,
                           WpfDialogService dialog, SettingsService settings, IAppLogger log)
    {
        _backup   = backup;
        _factory  = factory;
        _dialog   = dialog;
        _settings = settings;
        _log      = log;

        BackupNowCommand   = AsyncCommand(BackupNowAsync,   () => !IsBusy);
        BackupToUsbCommand = AsyncCommand(BackupToUsbAsync, () => !IsBusy);
        RestoreCommand     = AsyncCommand(RestoreAsync,     () => !IsBusy);
        OpenFolderCommand  = Command(OpenFolder);
        RefreshCommand     = AsyncCommand(LoadAsync);
    }

    public async Task LoadAsync()
    {
        BackupFolder = _settings.Get(SettingsService.BackupFolder, _backup.DefaultBackupFolder);

        await using var db = await _factory.CreateDbContextAsync();
        var records = await db.Backups.AsNoTracking()
                               .OrderByDescending(b => b.Id).Take(20).ToListAsync();
        History.Clear();
        foreach (var r in records) History.Add(r);

        if (History.Count > 0)
            LastBackupInfo = $"{History[0].AtUtc.ToLocalTime():dd/MM/yyyy HH:mm}  ({History[0].SizeBytes / 1024:N0} KB)";
        else
            LastBackupInfo = "No backups yet.";
    }

    private async Task BackupNowAsync()
    {
        await RunAsync(async () =>
        {
            var r = await _backup.BackupAsync(null, "Manual backup");
            if (!r.Verified)
                ErrorMessage = "Backup completed but integrity check failed. Check the file.";
            else
                ErrorMessage = "";
            await LoadAsync();
        });
    }

    private async Task BackupToUsbAsync()
    {
        var folder = _dialog.PickFolder("Select USB / external drive folder");
        if (folder is null) return;
        await RunAsync(async () =>
        {
            var r = await _backup.BackupAsync(folder, "USB backup");
            if (!r.Verified)
                ErrorMessage = "Backup completed but integrity check failed.";
            else
                ErrorMessage = "";
            await LoadAsync();
        });
    }

    private async Task RestoreAsync()
    {
        var file = _dialog.PickBackupFile();
        if (file is null) return;

        if (!_dialog.Confirm(
            "This will REPLACE the current database with the selected backup.\n" +
            "A safety backup will be created first.\n\nContinue?",
            "Restore Backup"))
            return;

        await RunAsync(async () =>
        {
            await _backup.RestoreAsync(file);
            _dialog.Alert("Restore complete. Please restart the application.", "Restore");
        });
    }

    private void OpenFolder()
    {
        var folder = _settings.Get(SettingsService.BackupFolder, _backup.DefaultBackupFolder);
        if (System.IO.Directory.Exists(folder))
            System.Diagnostics.Process.Start("explorer.exe", folder);
    }
}
