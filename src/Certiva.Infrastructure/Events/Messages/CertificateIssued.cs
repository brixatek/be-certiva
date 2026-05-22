namespace Certiva.Infrastructure.Events.Messages;

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
    public Guid TemplateId { get; init; }
    public Guid TenantId { get; init; }

    /// <summary>
    /// Monotonically increasing sequence number per CertificateId.
    /// Used by the Verification Engine to enforce ordered, exactly-once event application.
    /// </summary>
    public long SequenceNumber { get; init; }

    /// <summary>
    /// The certificate name (e.g. "Certificate of Completion — Safety Training").
    /// </summary>
    public string Name { get; init; } = string.Empty;

    public DateOnly IssueDate { get; init; }

    /// <summary>
    /// Null when the template has ValidityPeriodDays = 0 (does not expire).
    /// </summary>
    public DateOnly? ExpiryDate { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
