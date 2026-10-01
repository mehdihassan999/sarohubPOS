// src/SaroHub.Infrastructure/Persistence/AppDbFactory.cs
using Microsoft.EntityFrameworkCore;
using SaroHub.Core.Abstractions;

namespace SaroHub.Infrastructure.Persistence;

public sealed class AppDbFactory : IAppDbFactory
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public AppDbFactory(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public IAppDbContext Create() => _factory.CreateDbContext();
}
