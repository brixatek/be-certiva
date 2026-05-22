using Microsoft.Extensions.DependencyInjection;

namespace Certiva.Infrastructure.Outbox;

/// <summary>
/// Extension methods for registering outbox infrastructure services with the DI container.
/// </summary>
public static class OutboxServiceExtensions
{
    /// <summary>
    /// Registers the transactional outbox infrastructure:
    /// <list type="bullet">
    ///   <item><see cref="IOutboxWriter"/> → <see cref="OutboxWriter"/> (scoped)</item>
    ///   <item><see cref="OutboxRelayWorker"/> as a hosted background service</item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddOutboxInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddHostedService<OutboxRelayWorker>();

        return services;
    }
}
