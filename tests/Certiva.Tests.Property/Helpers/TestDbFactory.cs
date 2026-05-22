using Certiva.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Certiva.Tests.Property.Helpers;

/// <summary>
/// Creates fresh EF Core InMemory DbContext instances for property tests.
/// Each call returns an isolated context with a unique database name.
/// </summary>
public static class TestDbFactory
{
    public static CertivaDbContext Create(string? name = null)
    {
        var dbName = name ?? Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<CertivaDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new CertivaDbContext(options);
    }
}
