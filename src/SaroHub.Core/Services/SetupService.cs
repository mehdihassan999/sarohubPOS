// src/SaroHub.Core/Services/SetupService.cs
using Microsoft.EntityFrameworkCore;
using SaroHub.Core.Abstractions;
using SaroHub.Core.Accounting;
using SaroHub.Domain.Accounting;
using SaroHub.Domain.Common;
using SaroHub.Domain.Entities;
using SaroHub.Domain.Security;

namespace SaroHub.Core.Services;

public sealed record FirstRunData(
    string ShopName, string OwnerName, string Phone, string Address, string ShopType,
    string Currency, string Language, long OpeningCashPaisa,
    string AdminUsername, string AdminPassword, string? AdminPin, string BackupFolder);

public sealed class SetupService
{
    private readonly IAppDbFactory _factory;
    private readonly IPasswordHasher _hasher;
    private readonly PostingEngine _posting;
    private readonly NumberService _numbers;
    private readonly SettingsService _settings;
    private readonly IClock _clock;
    private readonly IAppLogger _log;

    public SetupService(IAppDbFactory factory, IPasswordHasher hasher, PostingEngine posting,
                        NumberService numbers, SettingsService settings, IClock clock, IAppLogger log)
    { _factory = factory; _hasher = hasher; _posting = posting; _numbers = numbers; _settings = settings; _clock = clock; _log = log; }

    public async Task<bool> IsSetupCompleteAsync(CancellationToken ct = default)
    {
        using var db = _factory.Create();
        var s = await db.Settings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == SettingsService.SetupCompleted, ct);
        return s?.Value == "true";
    }

    public async Task<Result> CompleteAsync(FirstRunData data, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(data.ShopName)) return Result.Fail("Shop name is required.");
        if (string.IsNullOrWhiteSpace(data.AdminUsername)) return Result.Fail("Username is required.");
        if (data.AdminPassword.Length < 4) return Result.Fail("Password must be at least 4 characters.");

        using var db = _factory.Create();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var ownerRole = await db.Roles.FirstAsync(r => r.Name == "Owner", ct);

            if (!await db.Users.AnyAsync(ct))
            {
                db.Users.Add(new User
                {
                    FullName = string.IsNullOrWhiteSpace(data.OwnerName) ? data.AdminUsername : data.OwnerName,
                    Username = data.AdminUsername.Trim(),
                    PasswordHash = _hasher.Hash(data.AdminPassword),
                    PinHash = string.IsNullOrWhiteSpace(data.AdminPin) ? null : _hasher.Hash(data.AdminPin),
                    RoleId = ownerRole.Id,
                    IsActive = true
                });
                await db.SaveChangesAsync(ct);
            }

            var adminId = await db.Users.OrderBy(u => u.Id).Select(u => u.Id).FirstAsync(ct);

            if (data.OpeningCashPaisa > 0)
            {
                var draft = new JournalDraft
                {
                    EntryDateUtc = _clock.UtcNow,
                    Description = "Opening cash",
                    SourceType = "Setup",
                    DocumentNo = "OPENING"
                };
                draft.Debit(AccountCodes.CashInHand, data.OpeningCashPaisa)
                     .Credit(AccountCodes.OpeningBalanceEq, data.OpeningCashPaisa);
                await _posting.PostAsync(db, draft, adminId, ct);

                db.CashSessions.Add(new CashSession
                {
                    SessionNo = await _numbers.NextAsync(db, "Session", ct),
                    OpenedAtUtc = _clock.UtcNow,
                    OpeningCashPaisa = data.OpeningCashPaisa,
                    OpenedByUserId = adminId
                });
            }

            if (!await db.Customers.AnyAsync(c => c.IsWalkIn, ct))
                db.Customers.Add(new Customer { Name = "Walk-in Customer", IsWalkIn = true });

            db.AuditLogs.Add(new AuditLog
            {
                AtUtc = _clock.UtcNow,
                UserId = adminId,
                UserName = data.OwnerName,
                Action = "SetupCompleted",
                EntityName = "Shop",
                RecordRef = data.ShopName
            });

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await _settings.SetManyAsync(new Dictionary<string, string>
            {
                [SettingsService.ShopName] = data.ShopName,
                [SettingsService.OwnerName] = data.OwnerName,
                [SettingsService.ShopPhone] = data.Phone,
                [SettingsService.ShopAddress] = data.Address,
                [SettingsService.ShopType] = data.ShopType,
                [SettingsService.Currency] = string.IsNullOrWhiteSpace(data.Currency) ? "PKR" : data.Currency,
                [SettingsService.Language] = string.IsNullOrWhiteSpace(data.Language) ? "en" : data.Language,
                [SettingsService.BackupFolder] = data.BackupFolder,
                [SettingsService.BackupFrequencyHours] = "24",
                [SettingsService.AllowNegativeStock] = "false",
                [SettingsService.ReceiptFooter] = "Thank you for your business.",
                [SettingsService.SetupCompleted] = "true"
            }, ct);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _log.Error("Setup failed", ex);
            return Result.Fail("Setup could not be completed. Please try again.");
        }
    }
}