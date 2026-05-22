using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Certiva.Infrastructure.Redis;

/// <summary>
/// Extension methods for registering Redis infrastructure services.
/// </summary>
public static class RedisServiceExtensions
{
    /// <summary>
    /// Registers the Redis <see cref="IConnectionMultiplexer"/> singleton,
    /// the <see cref="IVerificationStoreRepository"/> scoped service, and the
    /// <see cref="RedisHealthCheck"/> with the health-check builder.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="config">Application configuration; reads <c>Redis:ConnectionString</c>.</param>
    public static IServiceCollection AddRedisInfrastructure(
        this IServiceCollection services,
        IConfiguration config)
    {
        var connectionString = config["Redis:ConnectionString"]
            ?? throw new InvalidOperationException(
                "Redis connection string is not configured. " +
                "Set 'Redis:ConnectionString' in application settings.");

        // Register the multiplexer as a singleton — StackExchange.Redis recommends
        // sharing a single IConnectionMultiplexer instance for the lifetime of the app.
        services.AddSingleton<IConnectionMultiplexer>(
            _ => ConnectionMultiplexer.Connect(connectionString));

        // Scoped repository: each request gets its own logical scope while sharing
        // the underlying singleton connection.
        services.AddScoped<IVerificationStoreRepository, RedisVerificationStoreRepository>();

        // Health check — registered under the name "redis".
        services.AddHealthChecks()
            .AddCheck<RedisHealthCheck>("redis");

        return services;
    }
}
