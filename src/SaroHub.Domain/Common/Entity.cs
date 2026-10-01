// src/SaroHub.Domain/Common/Entity.cs
namespace SaroHub.Domain.Common;

public abstract class Entity
{
    public int Id { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int? CreatedByUserId { get; set; }
}