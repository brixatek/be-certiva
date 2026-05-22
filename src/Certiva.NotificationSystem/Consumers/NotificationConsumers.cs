using Certiva.Infrastructure.Constants;
using Certiva.Infrastructure.Events;
using Certiva.Infrastructure.Persistence;
using Certiva.Infrastructure.Persistence.Entities;
using Certiva.Infrastructure.Persistence.Repositories;
using Certiva.NotificationSystem.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace Certiva.NotificationSystem.Consumers;

/// <summary>
/// Dispatches an issuance alert email when a <see cref="CertificateIssued"/> event is received.
/// Uses SHA-256(EventId + "IssuanceAlert") as idempotency key — at most one dispatch per event.
/// </summary>
public sealed class CertificateIssuedNotificationConsumer : IConsumer<CertificateIssued>
{
    private readonly CertivaDbContext _db;
    private readonly IEmailDispatchService _email;
    private readonly ILogger<CertificateIssuedNotificationConsumer> _logger;
    private readonly IProfessionalRepository _professionals;
    private readonly ICertificateRepository _certificates;
    private readonly INotificationLogRepository _notificationLogs;

    public CertificateIssuedNotificationConsumer(
        CertivaDbContext db,
        IEmailDispatchService email,
        ILogger<CertificateIssuedNotificationConsumer> logger,
        IProfessionalRepository professionals,
        ICertificateRepository certificates,
        INotificationLogRepository notificationLogs)
    {
        _db = db;
        _email = email;
        _logger = logger;
        _professionals = professionals;
        _certificates = certificates;
        _notificationLogs = notificationLogs;
    }

    public async Task Consume(ConsumeContext<CertificateIssued> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        const string notifType = NotificationTypes.IssuanceAlert;
        var idempotencyKey = ComputeIdempotencyKey(msg.EventId, notifType);

        if (await _notificationLogs.ExistsByIdempotencyKeyAsync(idempotencyKey, ct)) return;

        var professional = await _professionals.FindByIdAsync(msg.ProfessionalId, msg.TenantId, ct);

        if (professional is null) return;

        if (professional.Email_Encrypted is null || professional.Email_Encrypted.Length == 0)
        {
            await RecordSkippedAsync(msg.TenantId, msg.ProfessionalId, idempotencyKey, notifType, msg.EventId.ToString(), ct);
            return;
        }

        var cert = await _certificates.FindByIdAsync(msg.CertificateId, msg.TenantId, ct);
        var certName = cert?.Name ?? "Certificate";

        var notifId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var log = new NotificationLogEntity
        {
            NotificationId = notifId,
            TenantId = msg.TenantId,
            ProfessionalId = msg.ProfessionalId,
            IdempotencyKey = idempotencyKey,
            NotificationType = notifType,
            EventId = msg.EventId.ToString(),
            Status = NotificationStatus.Pending,
            CreatedAt = now
        };

        _notificationLogs.Add(log);
        await _db.SaveChangesAsync(ct);

        try
        {
            await _email.SendAsync(
                toEmail: $"professional-{msg.ProfessionalId}@certiva.internal",
                subject: $"Your certificate '{certName}' has been issued",
                body: $"Congratulations! Your certificate '{certName}' has been issued.",
                ct);

            log.Status = NotificationStatus.Dispatched;
            log.DispatchedAt = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "NotificationSystem: Failed to dispatch issuance alert for Professional {ProfId}.", msg.ProfessionalId);
            log.Status = NotificationStatus.Failed;
            log.FailedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task RecordSkippedAsync(
        Guid tenantId, Guid professionalId, string key, string type, string eventId, CancellationToken ct)
    {
        _logger.LogInformation(
            "NotificationSystem: Professional {ProfId} has no email — skipping {Type} notification.", professionalId, type);

        _notificationLogs.Add(new NotificationLogEntity
        {
            NotificationId = Guid.NewGuid(),
            TenantId = tenantId,
            ProfessionalId = professionalId,
            IdempotencyKey = key,
            NotificationType = type,
            EventId = eventId,
            Status = NotificationStatus.Skipped,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }

    private static string ComputeIdempotencyKey(Guid eventId, string notifType)
    {
        var raw = eventId.ToString() + notifType;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }
}

/// <summary>
/// Dispatches a revocation alert email when a <see cref="CertificateRevoked"/> event is received.
/// </summary>
public sealed class CertificateRevokedNotificationConsumer : IConsumer<CertificateRevoked>
{
    private readonly CertivaDbContext _db;
    private readonly IEmailDispatchService _email;
    private readonly ILogger<CertificateRevokedNotificationConsumer> _logger;
    private readonly ICertificateRepository _certificates;
    private readonly INotificationLogRepository _notificationLogs;

    public CertificateRevokedNotificationConsumer(
        CertivaDbContext db,
        IEmailDispatchService email,
        ILogger<CertificateRevokedNotificationConsumer> logger,
        ICertificateRepository certificates,
        INotificationLogRepository notificationLogs)
    {
        _db = db;
        _email = email;
        _logger = logger;
        _certificates = certificates;
        _notificationLogs = notificationLogs;
    }

    public async Task Consume(ConsumeContext<CertificateRevoked> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        const string notifType = NotificationTypes.RevocationAlert;
        var idempotencyKey = ComputeIdempotencyKey(msg.EventId, notifType);

        if (await _notificationLogs.ExistsByIdempotencyKeyAsync(idempotencyKey, ct)) return;

        var cert = await _certificates.FindByIdWithNavigationsAsync(msg.CertificateId, ct);

        if (cert is null || cert.TenantId != msg.TenantId) return;

        if (cert.Professional.Email_Encrypted is null || cert.Professional.Email_Encrypted.Length == 0)
        {
            _logger.LogInformation(
                "NotificationSystem: Professional {ProfId} has no email — skipping RevocationAlert.", cert.ProfessionalId);
            return;
        }

        var log = new NotificationLogEntity
        {
            NotificationId = Guid.NewGuid(),
            TenantId = msg.TenantId,
            ProfessionalId = cert.ProfessionalId,
            IdempotencyKey = idempotencyKey,
            NotificationType = notifType,
            EventId = msg.EventId.ToString(),
            Status = NotificationStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _notificationLogs.Add(log);
        await _db.SaveChangesAsync(ct);

        try
        {
            await _email.SendAsync(
                toEmail: $"professional-{cert.ProfessionalId}@certiva.internal",
                subject: $"Your certificate '{cert.Name}' has been revoked",
                body: $"Your certificate '{cert.Name}' was revoked. Reason: {cert.RevocationReason}",
                ct);

            log.Status = NotificationStatus.Dispatched;
            log.DispatchedAt = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "NotificationSystem: Failed to dispatch revocation alert for Certificate {CertId}.", msg.CertificateId);
            log.Status = NotificationStatus.Failed;
            log.FailedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }

    private static string ComputeIdempotencyKey(Guid eventId, string notifType)
    {
        var raw = eventId.ToString() + notifType;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }
}

/// <summary>
/// Consumer definition applying the notification-specific retry policy (3 retries: 5s, 25s, 125s).
/// </summary>
public sealed class NotificationConsumerDefinition<T> : ConsumerDefinition<T>
    where T : class, IConsumer
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<T> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r =>
            r.Intervals(
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(25),
                TimeSpan.FromSeconds(125)));
    }
}
