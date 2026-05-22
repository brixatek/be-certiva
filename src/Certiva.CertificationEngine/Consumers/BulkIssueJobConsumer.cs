using Certiva.CertificationEngine.Commands;
using Certiva.CertificationEngine.Services;
using Certiva.Infrastructure.Constants;
using Certiva.Infrastructure.Events;
using Certiva.Infrastructure.Persistence;
using Certiva.Infrastructure.Persistence.Entities;
using Certiva.Infrastructure.Persistence.Repositories;
using MassTransit;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Certiva.CertificationEngine.Consumers;

/// <summary>
/// Processes a bulk certificate issuance job.
/// Subscribes to <see cref="BulkIssueJobEnqueued"/>, iterates each Professional entry,
/// calls <see cref="ICertificationEngineService.IssueCertificateAsync"/>, and
/// accumulates success/failure counts. Updates job status on completion.
/// Individual failures do not abort the batch.
/// </summary>
public sealed class BulkIssueJobConsumer : IConsumer<BulkIssueJobEnqueued>
{
    private readonly CertivaDbContext _db;
    private readonly ICertificationEngineService _certEngine;
    private readonly ILogger<BulkIssueJobConsumer> _logger;
    private readonly IBulkIssueJobRepository _bulkIssueJobs;
    private readonly IAuditLogRepository _auditLogs;

    public BulkIssueJobConsumer(
        CertivaDbContext db,
        ICertificationEngineService certEngine,
        ILogger<BulkIssueJobConsumer> logger,
        IBulkIssueJobRepository bulkIssueJobs,
        IAuditLogRepository auditLogs)
    {
        _db = db;
        _certEngine = certEngine;
        _logger = logger;
        _bulkIssueJobs = bulkIssueJobs;
        _auditLogs = auditLogs;
    }

    public async Task Consume(ConsumeContext<BulkIssueJobEnqueued> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        var job = await _bulkIssueJobs.FindByIdAsync(msg.JobId, msg.TenantId, ct);

        if (job is null)
        {
            _logger.LogWarning("BulkIssueJobConsumer: Job {JobId} not found.", msg.JobId);
            return;
        }

        job.Status = BulkJobStatus.Processing;
        await _db.SaveChangesAsync(ct);

        int success = 0;
        int failure = 0;
        var failures = new List<object>();

        foreach (var professionalId in msg.ProfessionalIds)
        {
            try
            {
                var cmd = new IssueCertificateCommand(
                    TenantId: msg.TenantId,
                    ProfessionalId: professionalId,
                    IssuerId: msg.IssuerId,
                    TemplateId: msg.TemplateId,
                    IdempotencyKey: $"bulk:{msg.JobId}:{professionalId}");

                var result = await _certEngine.IssueCertificateAsync(cmd, ct);

                if (result.IsSuccess)
                {
                    success++;
                }
                else
                {
                    failure++;
                    failures.Add(new { professionalId, error = result.Error });
                    _logger.LogWarning(
                        "BulkIssueJobConsumer: Failed to issue cert for Professional {ProfId} in Job {JobId}: {Error}.",
                        professionalId, msg.JobId, result.Error);
                }
            }
            catch (Exception ex)
            {
                failure++;
                failures.Add(new { professionalId, error = ex.Message });
                _logger.LogError(ex,
                    "BulkIssueJobConsumer: Exception issuing cert for Professional {ProfId} in Job {JobId}.",
                    professionalId, msg.JobId);
            }

            job.ProcessedCount++;
            job.SuccessCount = success;
            job.FailureCount = failure;
        }

        var finalStatus = failure == msg.ProfessionalIds.Count ? BulkJobStatus.Failed : BulkJobStatus.Completed;
        job.Status = finalStatus;
        job.ResultReport = JsonSerializer.Serialize(new
        {
            successCount = success,
            failureCount = failure,
            failures
        });
        job.CompletedAt = DateTimeOffset.UtcNow;

        if (finalStatus == BulkJobStatus.Failed)
        {
            var now = DateTimeOffset.UtcNow;
            var metadata = JsonSerializer.Serialize(new { jobId = msg.JobId, failureCount = failure });
            var raw = AuditActions.BulkJobFailed + msg.JobId.ToString() + now.ToString("O") + msg.IssuerId.ToString() + metadata;
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();

            _auditLogs.Add(new AuditLogEntity
            {
                AuditId = Guid.NewGuid(),
                TenantId = msg.TenantId,
                ActionType = AuditActions.BulkJobFailed,
                EntityId = msg.JobId.ToString(),
                Actor = msg.IssuerId.ToString(),
                Timestamp = now,
                Metadata = metadata,
                RecordHash = hash
            });
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "BulkIssueJobConsumer: Job {JobId} {Status}. Success={S}, Failure={F}.",
            msg.JobId, finalStatus, success, failure);
    }
}
