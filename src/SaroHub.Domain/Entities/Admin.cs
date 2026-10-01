// src/SaroHub.Domain/Entities/Admin.cs
using SaroHub.Domain.Common;

namespace SaroHub.Domain.Entities;

public class Role : Entity
{
    public string Name { get; set; } = "";
    public string PermissionsCsv { get; set; } = "";
    public bool IsSystem { get; set; }

    public IReadOnlyList<string> Permissions()
        => PermissionsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public class User : Entity
{
    public string FullName { get; set; } = "";
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string? PinHash { get; set; }
    public int RoleId { get; set; }
    public Role? Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginUtc { get; set; }
}

public class Setting : Entity
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

public class NumberSequence : Entity
{
    public string Name { get; set; } = "";
    public string Prefix { get; set; } = "";
    public long Next { get; set; } = 1;
}

public class AuditLog : Entity
{
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
    public int? UserId { get; set; }
    public string UserName { get; set; } = "";
    public string Action { get; set; } = "";
    public string EntityName { get; set; } = "";
    public string RecordRef { get; set; } = "";
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}

public class BackupRecord : Entity
{
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
    public string FilePath { get; set; } = "";
    public long SizeBytes { get; set; }
    public bool Verified { get; set; }
    public string? Note { get; set; }
}