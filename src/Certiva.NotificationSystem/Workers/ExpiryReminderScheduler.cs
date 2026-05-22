using Certiva.Infrastructure.Constants;
using Certiva.Infrastructure.Persistence;
using Certiva.Infrastructure.Persistence.Entities;
using Certiva.NotificationSystem.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace Certiva.NotificationSystem.Workers;

/// <summary>
/// Runs daily and dispatches expiry reminder emails for certificates expiring in 30 days or 7 days.
/// Uses SHA-256 idempotency keys to prevent duplicate reminders per day.
/// </summary>
public sealed class ExpiryReminderScheduler : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiryReminderScheduler> _logger;

    public ExpiryReminderScheduler(IServiceScopeFactory scopeFactory, ILogger<ExpiryReminderScheduler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExpiryReminderScheduler started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ExpiryReminderScheduler: Unhandled error during run.");
            }

            await Task.Delay(RunInterval, stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("ExpiryReminderScheduler stopped.");
    }

    private async Task RunAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CertivaDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailDispatchService>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var in30 = today.AddDays(30);
        var in7 = today.AddDays(7);

        // Certificates expiring in exactly 30 or 7 days
        var certs = await db.Certificates
            .Include(c => c.Professional)
            .Where(c =>
                c.Status == CertificateStatus.Active &&
                c.ExpiryDate.HasValue &&
                (c.ExpiryDate.Value == in30 || c.ExpiryDate.Value == in7))
            .ToListAsync(ct);

        _logger.LogInformation("ExpiryReminderScheduler: Found {Count} certificates needing reminders.", certs.Count);

        foreach (var cert in certs)
        {
            if (cert.Professional.Email_Encrypted is null || cert.Professional.Email_Encrypted.Length == 0)
                continue;

            var daysLeft = cert.ExpiryDate!.Value.DayNumber - today.DayNumber;
            var notifType = daysLeft == 30 ? NotificationTypes.ExpiryReminder30 : NotificationTypes.ExpiryReminder7;
            var idempotencyKey = ComputeKey(cert.CertificateId, notifType, today);

            if (await db.NotificationLogs.AnyAsync(n => n.IdempotencyKey == idempotencyKey, ct))
                continue;

            var log = new NotificationLogEntity
            {
                NotificationId = Guid.NewGuid(),
                TenantId = cert.TenantId,
                ProfessionalId = cert.ProfessionalId,
                IdempotencyKey = idempotencyKey,
                NotificationType = notifType,
                EventId = $"{cert.CertificateId}:{today}",
                Status = NotificationStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow
            };

            db.NotificationLogs.Add(log);
            await db.SaveChangesAsync(ct);

            try
            {
                await emailService.SendAsync(
                    toEmail: $"professional-{cert.ProfessionalId}@certiva.internal",
                    subject: $"Your certificate '{cert.Name}' expires in {daysLeft} days",
                    body: $"This is a reminder that your certificate '{cert.Name}' expires on {cert.ExpiryDate.Value:dd MMM yyyy}.",
                    ct);

                log.Status = NotificationStatus.Dispatched;
                log.DispatchedAt = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "ExpiryReminderScheduler: Failed to send {Type} for Certificate {CertId}.", notifType, cert.CertificateId);
                log.Status = NotificationStatus.Failed;
                log.FailedAt = DateTimeOffset.UtcNow;
            }

            await db.SaveChangesAsync(ct);
        }
    }

    private static string ComputeKey(Guid certId, string notifType, DateOnly date)
    {
        var raw = certId.ToString() + notifType + date.ToString("yyyy-MM-dd");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }
}
