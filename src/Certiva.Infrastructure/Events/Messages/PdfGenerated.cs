namespace Certiva.Infrastructure.Events.Messages;

/// <summary>
/// Message contract published when a PDF has been generated and stored for a Certificate.
/// Consumed by: CertificateWallet (availability signal so the wallet can surface the download link).
/// </summary>
public record PdfGenerated
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public Guid CertificateId { get; init; }
    public Guid TenantId { get; init; }

    /// <summary>
    /// Blob storage path (e.g. MinIO object key or Azure Blob path) where the PDF is stored.
    /// </summary>
    public string PdfStoragePath { get; init; } = string.Empty;

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
