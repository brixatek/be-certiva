namespace Certiva.Infrastructure.Events.Messages;

/// <summary>
/// Message contract published when an Issuer's verification status is set to Approved.
/// </summary>
public record IssuerApproved
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public Guid IssuerId { get; init; }
    public Guid TenantId { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
