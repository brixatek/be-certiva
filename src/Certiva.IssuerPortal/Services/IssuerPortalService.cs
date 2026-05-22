using Certiva.Infrastructure.Constants;
using Certiva.Infrastructure.Domain;
using Certiva.Infrastructure.Events;
using Certiva.Infrastructure.Outbox;
using Certiva.Infrastructure.Persistence;
using Certiva.Infrastructure.Persistence.Entities;
using Certiva.Infrastructure.Persistence.Repositories;
using Certiva.IssuerPortal.Resources;
using System.Text.Json;

namespace Certiva.IssuerPortal.Services;

/// <summary>
/// Implements <see cref="IIssuerPortalService"/> — template management, bulk issuance,
/// analytics, and Professional search for Issuers.
/// </summary>
public sealed class IssuerPortalService : IIssuerPortalService
{
    private readonly CertivaDbContext _db;
    private readonly IOutboxWriter _outboxWriter;
    private readonly IIssuerRepository _issuers;
    private readonly ICertificateTemplateRepository _templates;
    private readonly ICertificateRepository _certificates;
    private readonly IBulkIssueJobRepository _bulkIssueJobs;

    public IssuerPortalService(
        CertivaDbContext db,
        IOutboxWriter outboxWriter,
        IIssuerRepository issuers,
        ICertificateTemplateRepository templates,
        ICertificateRepository certificates,
        IBulkIssueJobRepository bulkIssueJobs)
    {
        _db = db;
        _outboxWriter = outboxWriter;
        _issuers = issuers;
        _templates = templates;
        _certificates = certificates;
        _bulkIssueJobs = bulkIssueJobs;
    }

    // ── Template management ──────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<Result<Guid>> CreateTemplateAsync(CreateTemplateCommand cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.Name) || cmd.Name.Length > 100)
            return Result<Guid>.BadRequest(TemplateMessages.NameInvalid);

        if (cmd.ValidityPeriodDays < 0)
            return Result<Guid>.BadRequest(TemplateMessages.ValidityInvalid);

        var issuer = await _issuers.FindByIdAsync(cmd.IssuerId, cmd.TenantId, ct);

        if (issuer is null)
            return Result<Guid>.NotFound(TemplateMessages.IssuerNotFound(cmd.IssuerId));

        if (issuer.VerificationStatus != IssuerStatus.Verified)
            return Result<Guid>.Forbidden(TemplateMessages.IssuerNotVerified);

        if (await _templates.ExistsWithNameAsync(cmd.IssuerId, cmd.TenantId, cmd.Name, null, ct))
            return Result<Guid>.Conflict(TemplateMessages.DuplicateName(cmd.Name));

        var now = DateTimeOffset.UtcNow;
        var template = new CertificateTemplateEntity
        {
            TemplateId = Guid.NewGuid(),
            TenantId = cmd.TenantId,
            IssuerId = cmd.IssuerId,
            Name = cmd.Name,
            ValidityPeriodDays = cmd.ValidityPeriodDays,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _templates.Add(template);
        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Ok(template.TemplateId);
    }

    /// <inheritdoc/>
    public async Task<Result<Guid>> UpdateTemplateAsync(UpdateTemplateCommand cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.Name) || cmd.Name.Length > 100)
            return Result<Guid>.BadRequest(TemplateMessages.NameInvalid);

        if (cmd.ValidityPeriodDays < 0)
            return Result<Guid>.BadRequest(TemplateMessages.ValidityInvalid);

        var template = await _templates.FindByIdAsync(cmd.TemplateId, cmd.TenantId, ct);

        if (template is null)
            return Result<Guid>.NotFound(TemplateMessages.NotFound(cmd.TemplateId));

        if (template.IssuerId != cmd.IssuerId)
            return Result<Guid>.Forbidden(TemplateMessages.UpdateNotAuthorized);

        if (await _templates.ExistsWithNameAsync(cmd.IssuerId, cmd.TenantId, cmd.Name, cmd.TemplateId, ct))
            return Result<Guid>.Conflict(TemplateMessages.DuplicateName(cmd.Name));

        template.Name = cmd.Name;
        template.ValidityPeriodDays = cmd.ValidityPeriodDays;
        template.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Ok(template.TemplateId);
    }

    /// <inheritdoc/>
    public async Task<Result<Guid>> DeactivateTemplateAsync(
        Guid templateId, Guid issuerId, Guid tenantId, CancellationToken ct)
    {
        var template = await _templates.FindByIdAsync(templateId, tenantId, ct);

        if (template is null)
            return Result<Guid>.NotFound(TemplateMessages.NotFound(templateId));

        if (template.IssuerId != issuerId)
            return Result<Guid>.Forbidden(TemplateMessages.DeactivateNotAuthorized);

        template.IsActive = false;
        template.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Ok(templateId);
    }

    // ── Bulk issuance ────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<Result<Guid>> EnqueueBulkIssueAsync(EnqueueBulkIssueCommand cmd, CancellationToken ct)
    {
        if (cmd.ProfessionalIds.Count < 1 || cmd.ProfessionalIds.Count > 1000)
            return Result<Guid>.UnprocessableEntity(BulkIssueMessages.InvalidListSize);

        var issuer = await _issuers.FindByIdAsync(cmd.IssuerId, cmd.TenantId, ct);

        if (issuer is null)
            return Result<Guid>.NotFound(BulkIssueMessages.IssuerNotFound(cmd.IssuerId));

        if (issuer.VerificationStatus != IssuerStatus.Verified)
            return Result<Guid>.Forbidden(BulkIssueMessages.IssuerNotVerified);

        var template = await _templates.FindByIdAsync(cmd.TemplateId, cmd.TenantId, ct);

        if (template is null)
            return Result<Guid>.NotFound(BulkIssueMessages.TemplateNotFound(cmd.TemplateId));

        if (template.IssuerId != cmd.IssuerId)
            return Result<Guid>.Forbidden(BulkIssueMessages.TemplateNotOwnedByIssuer);

        var now = DateTimeOffset.UtcNow;
        var job = new BulkIssueJobEntity
        {
            JobId = Guid.NewGuid(),
            TenantId = cmd.TenantId,
            IssuerId = cmd.IssuerId,
            TemplateId = cmd.TemplateId,
            Status = BulkJobStatus.Queued,
            TotalCount = cmd.ProfessionalIds.Count,
            ProcessedCount = 0,
            SuccessCount = 0,
            FailureCount = 0,
            SubmittedAt = now
        };

        _bulkIssueJobs.Add(job);

        await _outboxWriter.WriteAsync(
            cmd.TenantId,
            EventTypes.BulkIssueJobEnqueued,
            new BulkIssueJobEnqueued
            {
                EventId = Guid.NewGuid(),
                JobId = job.JobId,
                IssuerId = cmd.IssuerId,
                TemplateId = cmd.TemplateId,
                TenantId = cmd.TenantId,
                ProfessionalIds = cmd.ProfessionalIds,
                OccurredAt = now
            },
            ct);

        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Ok(job.JobId);
    }

    /// <inheritdoc/>
    public async Task<Result<BulkJobStatusResult>> GetBulkJobStatusAsync(
        Guid jobId, Guid issuerId, Guid tenantId, CancellationToken ct)
    {
        var job = await _bulkIssueJobs.FindByIdAsync(jobId, tenantId, ct);

        if (job is null)
            return Result<BulkJobStatusResult>.NotFound(BulkIssueMessages.JobNotFound(jobId));

        if (job.IssuerId != issuerId)
            return Result<BulkJobStatusResult>.Forbidden(BulkIssueMessages.JobAccessDenied);

        return Result<BulkJobStatusResult>.Ok(new BulkJobStatusResult
        {
            JobId = job.JobId,
            Status = job.Status,
            TotalCount = job.TotalCount,
            ProcessedCount = job.ProcessedCount,
            SuccessCount = job.SuccessCount,
            FailureCount = job.FailureCount,
            SubmittedAt = job.SubmittedAt
        });
    }

    // ── Analytics ────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<Result<IssuerAnalyticsResult>> GetAnalyticsAsync(
        Guid issuerId, Guid tenantId, CancellationToken ct)
    {
        var active = await _certificates.CountByStatusAsync(issuerId, tenantId, CertificateStatus.Active, ct);
        var expired = await _certificates.CountByStatusAsync(issuerId, tenantId, CertificateStatus.Expired, ct);
        var revoked = await _certificates.CountByStatusAsync(issuerId, tenantId, CertificateStatus.Revoked, ct);

        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-12));
        var monthlyCounts = await _certificates.GetMonthlyIssuanceAsync(issuerId, tenantId, cutoff, ct);

        var monthly = monthlyCounts
            .Select(m => new MonthlyIssuancePoint { Year = m.Year, Month = m.Month, Count = m.Count })
            .ToList();

        return Result<IssuerAnalyticsResult>.Ok(new IssuerAnalyticsResult
        {
            TotalActive = active,
            TotalExpired = expired,
            TotalRevoked = revoked,
            MonthlyIssuance = monthly
        });
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<ProfessionalSearchResult>>> SearchProfessionalsAsync(
        string query, Guid issuerId, Guid tenantId, CancellationToken ct)
    {
        var certs = await _certificates.SearchByProfessionalNameAsync(query, issuerId, tenantId, ct);

        var results = certs.Select(c => new ProfessionalSearchResult
        {
            ProfessionalId = c.ProfessionalId,
            Name = c.Professional.Name,
            CertificateId = c.CertificateId,
            CertificateName = c.Name,
            Status = c.Status,
            IssueDate = c.IssueDate
        }).ToList();

        return Result<IReadOnlyList<ProfessionalSearchResult>>.Ok(results);
    }
}
