using Microsoft.Extensions.DependencyInjection;

namespace Certiva.Infrastructure.Idempotency;

/// <summary>
/// DI registration helpers for the idempotency infrastructure.
/// </summary>
public static class IdempotencyServiceExtensions
{
    /// <summary>
    /// Registers <see cref="IIdempotencyKeyRepository"/> → <see cref="IdempotencyKeyRepository"/>
    /// as a scoped service.
    /// </summary>
    public static IServiceCollection AddIdempotencyInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IIdempotencyKeyRepository, IdempotencyKeyRepository>();
        return services;
    }
}
