// src/SaroHub.Core/Services/SettingsService.cs
using Microsoft.EntityFrameworkCore;
using SaroHub.Core.Abstractions;
using SaroHub.Domain.Entities;

namespace SaroHub.Core.Services;

public sealed class SettingsService
{
    public const string SetupCompleted = "setup.completed";
    public const string ShopName = "shop.name";
    public const string ShopPhone = "shop.phone";
    public const string ShopAddress = "shop.address";
    public const string ShopType = "shop.type";
    public const string OwnerName = "shop.owner";
    public const string Currency = "app.currency";
    public const string Language = "app.language";
    public const string AllowNegativeStock = "stock.allowNegative";
    public const string ReceiptFooter = "receipt.footer";
    public const string ReceiptPrinter = "printer.receipt";
    public const string BackupFolder = "backup.folder";
    public const string BackupFrequencyHours = "backup.frequencyHours";
    public const string LastBackupUtc = "backup.lastUtc";

    private readonly IAppDbFactory _factory;
    private readonly Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);
    private bool _loaded;

    public SettingsService(IAppDbFactory factory) => _factory = factory;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        using var db = _factory.Create();
        var all = await db.Settings.AsNoTracking().ToListAsync(ct);
        _cache.Clear();
        foreach (var s in all) _cache[s.Key] = s.Value;
        _loaded = true;
    }

    public string Get(string key, string fallback = "")
        => _cache.TryGetValue(key, out var v) ? v : fallback;

    public bool GetBool(string key, bool fallback = false)
        => bool.TryParse(Get(key, fallback.ToString()), out var b) ? b : fallback;

    public int GetInt(string key, int fallback = 0)
        => int.TryParse(Get(key, fallback.ToString()), out var i) ? i : fallback;

    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        var row = await db.Settings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (row is null) db.Settings.Add(new Setting { Key = key, Value = value });
        else row.Value = value;
        await db.SaveChangesAsync(ct);
        _cache[key] = value;
    }

    public async Task SetManyAsync(IDictionary<string, string> values, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        foreach (var kv in values)
        {
            var row = await db.Settings.FirstOrDefaultAsync(s => s.Key == kv.Key, ct);
            if (row is null) db.Settings.Add(new Setting { Key = kv.Key, Value = kv.Value });
            else row.Value = kv.Value;
            _cache[kv.Key] = kv.Value;
        }
        await db.SaveChangesAsync(ct);
    }

    public bool IsLoaded => _loaded;
}