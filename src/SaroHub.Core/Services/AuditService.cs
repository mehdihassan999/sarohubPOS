// src/SaroHub.Core/Services/AuditService.cs
using SaroHub.Core.Abstractions;
using SaroHub.Domain.Entities;

namespace SaroHub.Core.Services;

public sealed class AuditService
{
    private readonly IClock _clock;
    public AuditService(IClock clock) => _clock = clock;

    /// <summary>Adds the audit row to the caller's context so it commits with the same transaction.</summary>
    public void Add(IAppDbContext db, ICurrentUser user, string action, string entityName,
                    string recordRef, string? oldValue = null, string? newValue = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            AtUtc = _clock.UtcNow,
            UserId = user.IsAuthenticated ? user.UserId : null,
            UserName = user.UserName,
            Action = action,
            EntityName = entityName,
            RecordRef = recordRef,
            OldValue = oldValue,
            NewValue = newValue
        });
    }
}