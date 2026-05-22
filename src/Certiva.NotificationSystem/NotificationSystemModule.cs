using Certiva.NotificationSystem.Services;
using Certiva.NotificationSystem.Workers;
using Microsoft.Extensions.DependencyInjection;

namespace Certiva.NotificationSystem;

/// <summary>
/// Entry point for the NotificationSystem module.
/// Handles email dispatch for issuance alerts, revocation alerts, and expiry reminders;
/// idempotency via derived keys; retry with exponential backoff.
/// </summary>
public static class NotificationSystemModule
{
    public static IServiceCollection AddNotificationSystem(this IServiceCollection services)
    {
        // Default to no-op email service — replace with a real implementation in production.
        services.AddScoped<IEmailDispatchService, NoOpEmailDispatchService>();

        services.AddHostedService<ExpiryReminderScheduler>();

        return services;
    }
}
