using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Certiva.Infrastructure.Redis;

/// <summary>
/// ASP.NET Core health check that reports Redis availability by delegating to
/// <see cref="IVerificationStoreRepository.IsAvailableAsync"/>.
/// </summary>
public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IVerificationStoreRepository _store;

    public RedisHealthCheck(IVerificationStoreRepository store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var isAvailable = await _store.IsAvailableAsync(cancellationToken);

        return isAvailable
            ? HealthCheckResult.Healthy("Redis is reachable.")
            : HealthCheckResult.Unhealthy("Redis is unreachable or PING failed.");
    }
}
