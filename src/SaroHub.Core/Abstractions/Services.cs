// src/SaroHub.Core/Abstractions/Services.cs
using SaroHub.Domain.Entities;

namespace SaroHub.Core.Abstractions;

public interface IAppDbFactory { IAppDbContext Create(); }

public interface IClock { DateTime UtcNow { get; } DateTime Now { get; } }

public interface IAppLogger
{
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? ex = null);
}

public interface IPasswordHasher
{
    string Hash(string plainText);
    bool Verify(string plainText, string hash);
}

public interface ICurrentUser
{
    int UserId { get; }
    string UserName { get; }
    string RoleName { get; }
    bool IsAuthenticated { get; }
    bool Has(string permission);
}

public interface IReceiptPrinter
{
    /// <summary>Prints after the sale is already committed. Failure must not roll back the sale.</summary>
    Task<bool> PrintSaleAsync(int saleId, bool showDialog);
    Task<string> SaveSaleTextAsync(int saleId);
}

public interface IBackupService
{
    Task<BackupRecord> BackupAsync(string? targetFolder = null, string? note = null, CancellationToken ct = default);
    Task<bool> VerifyAsync(string backupFilePath, CancellationToken ct = default);
    Task RestoreAsync(string backupFilePath, CancellationToken ct = default);
    string DefaultBackupFolder { get; }
}