// src/SaroHub.Infrastructure/Services/SqliteBackupService.cs
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SaroHub.Core.Abstractions;
using SaroHub.Domain.Entities;
using SaroHub.Infrastructure.Persistence;

namespace SaroHub.Infrastructure.Services;

/// <summary>
/// Uses the SQLite online-backup API so a live backup can be taken without closing the app.
/// Before a restore, a safety backup of the current database is created automatically.
/// </summary>
public sealed class SqliteBackupService : IBackupService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IAppLogger _log;

    public SqliteBackupService(IDbContextFactory<AppDbContext> factory, IAppLogger log)
    {
        _factory = factory;
        _log = log;
    }

    public string DefaultBackupFolder => AppPaths.BackupFolder;

    public async Task<BackupRecord> BackupAsync(
        string? targetFolder = null, string? note = null, CancellationToken ct = default)
    {
        var folder = string.IsNullOrWhiteSpace(targetFolder) ? DefaultBackupFolder : targetFolder;
        Directory.CreateDirectory(folder);

        var stamp    = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var fileName = $"sarohub-backup-{stamp}.db";
        var destPath = Path.Combine(folder, fileName);

        // SQLite online backup via the backup API — no file lock required on the source
        await using var src = new SqliteConnection(AppPaths.ConnectionString);
        await src.OpenAsync(ct);

        await using var dst = new SqliteConnection($"Data Source={destPath}");
        await dst.OpenAsync(ct);

        src.BackupDatabase(dst);
        await dst.CloseAsync();
        await src.CloseAsync();

        var size = new FileInfo(destPath).Length;
        var verified = await VerifyAsync(destPath, ct);

        await using var db = await _factory.CreateDbContextAsync(ct);
        var record = new BackupRecord
        {
            AtUtc     = DateTime.UtcNow,
            FilePath  = destPath,
            SizeBytes = size,
            Verified  = verified,
            Note      = note
        };
        db.Backups.Add(record);
        await db.SaveChangesAsync(ct);

        _log.Info($"Backup created: {destPath} ({size:N0} bytes, verified={verified})");
        return record;
    }

    public async Task<bool> VerifyAsync(string backupFilePath, CancellationToken ct = default)
    {
        try
        {
            await using var conn = new SqliteConnection($"Data Source={backupFilePath}");
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA integrity_check;";
            var result = (string?)await cmd.ExecuteScalarAsync(ct);
            return string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _log.Error("Backup verify failed", ex);
            return false;
        }
    }

    public async Task RestoreAsync(string backupFilePath, CancellationToken ct = default)
    {
        if (!File.Exists(backupFilePath))
            throw new FileNotFoundException("Backup file not found.", backupFilePath);

        var ok = await VerifyAsync(backupFilePath, ct);
        if (!ok) throw new InvalidOperationException("Backup file failed integrity check.");

        // Safety backup of the current database before overwriting
        _log.Info("Creating safety backup before restore...");
        try { await BackupAsync(note: "Pre-restore safety backup", ct: ct); }
        catch (Exception ex) { _log.Error("Could not create safety backup", ex); }

        // Restore via online backup in reverse: source = backup, dest = live db
        await using var src = new SqliteConnection($"Data Source={backupFilePath}");
        await src.OpenAsync(ct);

        await using var dst = new SqliteConnection(AppPaths.ConnectionString);
        await dst.OpenAsync(ct);

        src.BackupDatabase(dst);
        await dst.CloseAsync();
        await src.CloseAsync();

        _log.Info($"Database restored from: {backupFilePath}");
    }
}
