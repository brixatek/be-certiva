namespace Certiva.Infrastructure.Events;

/// <summary>
/// Message contract published when a QR code has been generated for a Certificate.
/// Consumed by: CertificationEngine (to update the QRCodeUrl / QRCodeBase64 fields on the record).
/// </summary>
public record QrCodeGenerated
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public Guid CertificateId { get; init; }
    public Guid TenantId { get; init; }

    /// <summary>
    /// Public URL at which the QR code image can be retrieved.
    /// </summary>
    public string QrCodeUrl { get; init; } = string.Empty;

    /// <summary>
    /// Base64-encoded PNG of the QR code for inline embedding.
    /// </summary>
    public string QrCodeBase64 { get; init; } = string.Empty;

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
