namespace Certiva.Infrastructure.Events;

/// <summary>
/// Message contract published when a Certificate is successfully issued.
/// Consumed by: VerificationEngine, QrWorker, PdfWorker, NotificationSystem.
/// </summary>
public record CertificateIssued
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public Guid CertificateId { get; init; }
    public Guid ProfessionalId { get; init; }
    public Guid IssuerId { get; init; }
    public Guid TenantId { get; init; }

    /// <summary>
    /// Monotonically increasing sequence number per CertificateId.
    /// Used by the Verification Engine to enforce ordered, exactly-once event application.
    /// </summary>
    public long SequenceNumber { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
