// src/SaroHub.Core/Services/AuthService.cs
using Microsoft.EntityFrameworkCore;
using SaroHub.Core.Abstractions;
using SaroHub.Domain.Common;
using SaroHub.Domain.Entities;

namespace SaroHub.Core.Services;

public sealed class AuthService : ICurrentUser
{
    private readonly IAppDbFactory _factory;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;
    private readonly IAppLogger _log;

    private User? _user;
    private HashSet<string> _permissions = new(StringComparer.OrdinalIgnoreCase);

    public AuthService(IAppDbFactory factory, IPasswordHasher hasher, IClock clock, IAppLogger log)
    {
        _factory = factory; _hasher = hasher; _clock = clock; _log = log;
    }

    public int UserId => _user?.Id ?? 0;
    public string UserName => _user?.FullName ?? "";
    public string RoleName => _user?.Role?.Name ?? "";
    public bool IsAuthenticated => _user is not null;

    public bool Has(string permission) => _permissions.Contains(permission);

    public async Task<Result> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        var user = await db.Users.Include(u => u.Role)
                                 .FirstOrDefaultAsync(u => u.Username == username && u.IsActive, ct);

        if (user is null || !_hasher.Verify(password, user.PasswordHash))
            return Result.Fail("Wrong username or password.");

        user.LastLoginUtc = _clock.UtcNow;
        db.AuditLogs.Add(new AuditLog
        {
            AtUtc = _clock.UtcNow,
            UserId = user.Id,
            UserName = user.FullName,
            Action = "Login",
            EntityName = nameof(User),
            RecordRef = user.Username
        });
        await db.SaveChangesAsync(ct);

        _user = user;
        _permissions = new HashSet<string>(user.Role?.Permissions() ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        _log.Info($"User '{user.Username}' logged in.");
        return Result.Ok();
    }

    public async Task<Result> LoginWithPinAsync(string pin, CancellationToken ct = default)
    {
        using var db = _factory.Create();
        var users = await db.Users.Include(u => u.Role).Where(u => u.IsActive && u.PinHash != null).ToListAsync(ct);
        var match = users.FirstOrDefault(u => _hasher.Verify(pin, u.PinHash!));
        if (match is null) return Result.Fail("Wrong PIN.");

        _user = match;
        _permissions = new HashSet<string>(match.Role?.Permissions() ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        return Result.Ok();
    }

    public void Logout()
    {
        _user = null;
        _permissions.Clear();
    }
}