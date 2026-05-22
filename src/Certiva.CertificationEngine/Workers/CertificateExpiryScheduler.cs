using Certiva.Infrastructure.Constants;
using Certiva.Infrastructure.Events;
using Certiva.Infrastructure.Outbox;
using Certiva.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Certiva.CertificationEngine.Workers;

/// <summary>
/// Background service that runs at most every 60 minutes and transitions certificates
/// whose ExpiryDate has been reached from Active to Expired, publishing a
/// <see cref="CertificateExpired"/> outbox message per certificate.
/// Satisfies Requirement 6.8.
/// </summary>
public sealed class CertificateExpiryScheduler : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CertificateExpiryScheduler> _logger;

    private const int BatchSize = 100;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(60);

    public CertificateExpiryScheduler(
        IServiceScopeFactory scopeFactory,
        ILogger<CertificateExpiryScheduler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunExpiryPassAsync(stoppingToken);

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown — exit the loop.
                break;
            }
        }
    }

    private async Task RunExpiryPassAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CertificateExpiryScheduler: starting expiry pass at {UtcNow}.", DateTimeOffset.UtcNow);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        int totalExpired = 0;
        int skip = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<CertivaDbContext>();
                var outboxWriter = scope.ServiceProvider.GetRequiredService<IOutboxWriter>();

                var batch = await db.Certificates
                    .Where(c => c.ExpiryDate <= today && c.Status == CertificateStatus.Active)
                    .OrderBy(c => c.CertificateId)   // stable ordering for pagination
                    .Skip(skip)
                    .Take(BatchSize)
                    .ToListAsync(stoppingToken);

                if (batch.Count == 0)
                    break;

                var now = DateTimeOffset.UtcNow;

                foreach (var cert in batch)
                {
                    cert.Status = CertificateStatus.Expired;
                    cert.UpdatedAt = now;

                    await outboxWriter.WriteAsync(
                        cert.TenantId,
                        EventTypes.CertificateExpired,
                        new CertificateExpired
                        {
                            EventId = Guid.NewGuid(),
                            CertificateId = cert.CertificateId,
                            TenantId = cert.TenantId,
                            SequenceNumber = 1,
                            OccurredAt = now
                        },
                        stoppingToken);

                    _logger.LogInformation(
                        "Certificate operation: {OperationType} CertificateId={CertificateId} ProfessionalId={ProfessionalId} IssuerId={IssuerId} Timestamp={Timestamp}",
                        OperationTypes.ExpireCertificate, cert.CertificateId, cert.ProfessionalId, cert.IssuerId, now);
                }

                await db.SaveChangesAsync(stoppingToken);

                totalExpired += batch.Count;
                _logger.LogInformation(
                    "CertificateExpiryScheduler: expired batch of {Count} certificates (running total: {Total}).",
                    batch.Count, totalExpired);

                // If the batch was smaller than the page size we've processed all matching rows.
                if (batch.Count < BatchSize)
                    break;

                // Because we updated Status to "Expired" the rows no longer match the WHERE clause,
                // so we do NOT advance skip — the next query will naturally return the next batch
                // of still-Active expired certificates.
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "CertificateExpiryScheduler: error processing batch at skip={Skip}. Continuing to next batch.",
                    skip);

                // Advance skip so we don't retry the same batch indefinitely.
                skip += BatchSize;
            }
        }

        _logger.LogInformation(
            "CertificateExpiryScheduler: expiry pass complete. Total expired: {Total}.", totalExpired);
    }
}
