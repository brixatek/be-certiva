using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Certiva.Infrastructure.Events;

/// <summary>
/// Extension methods for registering MassTransit and the RabbitMQ event bus
/// with all domain event message type contracts.
/// </summary>
public static class MassTransitServiceExtensions
{
    /// <summary>
    /// Registers MassTransit with RabbitMQ transport, all domain event contracts,
    /// global exponential retry policy (3 retries, 1s–60s), and dead-letter queue routing.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="config">Application configuration (reads RabbitMQ:Host, RabbitMQ:Username, RabbitMQ:Password).</param>
    public static IServiceCollection AddMassTransitEventBus(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.AddMassTransit(x =>
        {
            // ---------------------------------------------------------------
            // Module consumers are registered via x.AddConsumersFromNamespaceContaining<T>()
            // e.g. x.AddConsumersFromNamespaceContaining<NotificationSystem.Consumers.CertificateIssuedConsumer>()
            // ---------------------------------------------------------------

            x.UsingRabbitMq((ctx, cfg) =>
            {
                // RabbitMQ connection
                cfg.Host(
                    config["RabbitMQ:Host"] ?? "localhost",
                    h =>
                    {
                        h.Username(config["RabbitMQ:Username"] ?? "guest");
                        h.Password(config["RabbitMQ:Password"] ?? "guest");
                    });

                // Global retry policy: exponential back-off, 3 retries, 1s base, 60s max
                // Dead-letter queue: messages that exhaust all retries are automatically
                // routed to the "<queue>_error" exchange/queue by MassTransit convention.
                cfg.UseMessageRetry(r =>
                    r.Exponential(
                        retryLimit: 3,
                        minInterval: TimeSpan.FromSeconds(1),
                        maxInterval: TimeSpan.FromSeconds(60),
                        intervalDelta: TimeSpan.FromSeconds(1)));

                // Publish topology — declare exchanges for all domain event contracts
                // so that producers can publish without a consumer being registered first.
                cfg.Message<ProfessionalRegistered>(m => m.SetEntityName("certiva.professional-registered"));
                cfg.Message<IssuerApproved>(m => m.SetEntityName("certiva.issuer-approved"));
                cfg.Message<IssuerRejected>(m => m.SetEntityName("certiva.issuer-rejected"));
                cfg.Message<CertificateIssued>(m => m.SetEntityName("certiva.certificate-issued"));
                cfg.Message<CertificateRevoked>(m => m.SetEntityName("certiva.certificate-revoked"));
                cfg.Message<CertificateExpired>(m => m.SetEntityName("certiva.certificate-expired"));
                cfg.Message<QrCodeGenerated>(m => m.SetEntityName("certiva.qr-code-generated"));
                cfg.Message<PdfGenerated>(m => m.SetEntityName("certiva.pdf-generated"));
                cfg.Message<BulkIssueJobEnqueued>(m => m.SetEntityName("certiva.bulk-issue-job-enqueued"));

                cfg.ConfigureEndpoints(ctx);
            });
        });

        return services;
    }
}
