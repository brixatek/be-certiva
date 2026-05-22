namespace Certiva.Infrastructure.Events.Messages;

/// <summary>
/// Message contract published when a bulk certificate issuance job is enqueued by the Issuer Portal.
/// Consumed by: CertificationEngine (bulk processor).
/// </summary>
public record BulkIssueJobEnqueued
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public Guid JobId { get; init; }
    public Guid IssuerId { get; init; }
    public Guid TemplateId { get; init; }
    public Guid TenantId { get; init; }

    /// <summary>
    /// The list of Professional IDs for whom certificates should be issued.
    /// Must contain between 1 and 1,000 entries (validated before enqueueing).
    /// </summary>
    public List<Guid> ProfessionalIds { get; init; } = [];

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
