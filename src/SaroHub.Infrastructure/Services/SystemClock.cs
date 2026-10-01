// src/SaroHub.Infrastructure/Services/SystemClock.cs
using SaroHub.Core.Abstractions;

namespace SaroHub.Infrastructure.Services;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime Now    => DateTime.Now;
}
