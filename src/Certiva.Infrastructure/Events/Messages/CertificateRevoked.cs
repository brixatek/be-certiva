namespace Certiva.Infrastructure.Events.Messages;

/// <summary>
/// Message contract published when a Certificate is revoked.
/// Consumed by: VerificationEngine, NotificationSystem.
/// </summary>
public record CertificateRevoked
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public Guid CertificateId { get; init; }
    public Guid TenantId { get; init; }

    /// <summary>
    /// Monotonically increasing sequence number per CertificateId.
    /// Used by the Verification Engine to enforce ordered, exactly-once event application.
    /// </summary>
    public long SequenceNumber { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
