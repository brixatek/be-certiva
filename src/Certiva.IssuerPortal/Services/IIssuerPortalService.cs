using Certiva.Infrastructure.Domain;

namespace Certiva.IssuerPortal.Services;

/// <summary>
/// Template management, bulk issuance, analytics, and Professional search for Issuers.
/// </summary>
public interface IIssuerPortalService
{
    // ── Template management ──────────────────────────────────────────────────

    Task<Result<Guid>> CreateTemplateAsync(CreateTemplateCommand cmd, CancellationToken ct);
    Task<Result<Guid>> UpdateTemplateAsync(UpdateTemplateCommand cmd, CancellationToken ct);
    Task<Result<Guid>> DeactivateTemplateAsync(Guid templateId, Guid issuerId, Guid tenantId, CancellationToken ct);

    // ── Bulk issuance ────────────────────────────────────────────────────────

    Task<Result<Guid>> EnqueueBulkIssueAsync(EnqueueBulkIssueCommand cmd, CancellationToken ct);
    Task<Result<BulkJobStatusResult>> GetBulkJobStatusAsync(Guid jobId, Guid issuerId, Guid tenantId, CancellationToken ct);

    // ── Analytics ────────────────────────────────────────────────────────────

    Task<Result<IssuerAnalyticsResult>> GetAnalyticsAsync(Guid issuerId, Guid tenantId, CancellationToken ct);
    Task<Result<IReadOnlyList<ProfessionalSearchResult>>> SearchProfessionalsAsync(
        string query, Guid issuerId, Guid tenantId, CancellationToken ct);
}
