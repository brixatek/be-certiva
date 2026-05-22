using Certiva.Infrastructure.Domain;

namespace Certiva.CertificateWallet.Services;

/// <summary>
/// Worker-facing certificate wallet: list, detail, shareable link, and signed PDF download URL.
/// </summary>
public interface ICertificateWalletService
{
    /// <summary>
    /// Returns all certificates for <paramref name="professionalId"/> grouped by status
    /// (Active, Expired, Revoked), sorted by IssueDate DESC within each group.
    /// Sets ExpiryWarning = true when ExpiryDate != null and ExpiryDate &lt;= today + 30 days.
    /// Returns empty list (not 404) when no certificates exist.
    /// </summary>
    Task<Result<WalletSummary>> GetCertificatesAsync(
        Guid professionalId,
        Guid tenantId,
        CancellationToken ct);

    /// <summary>
    /// Returns full certificate detail including QRCodeValue and PDF download link.
    /// Returns HTTP 403 if the requesting ProfessionalId does not own the certificate.
    /// </summary>
    Task<Result<WalletCertificateDetail>> GetCertificateDetailAsync(
        Guid certificateId,
        Guid professionalId,
        Guid tenantId,
        CancellationToken ct);

    /// <summary>
    /// Generates an HMAC-SHA256 signed URL for PDF download valid for 15 minutes.
    /// Returns HTTP 202 with job state if the PDF has not yet been generated.
    /// </summary>
    Task<Result<PdfDownloadUrlResult>> GetPdfDownloadUrlAsync(
        Guid certificateId,
        Guid professionalId,
        Guid tenantId,
        CancellationToken ct);

    /// <summary>
    /// Returns the public shareable link: https://{domain}/verify/{CertificateId}.
    /// </summary>
    Task<Result<string>> GetShareableLinkAsync(
        Guid certificateId,
        Guid professionalId,
        Guid tenantId,
        CancellationToken ct);

    /// <summary>
    /// Validates a signed PDF download URL. Returns false if the signature is invalid or the URL is expired.
    /// </summary>
    bool ValidateSignedUrl(Guid certificateId, long expiresAt, string signature);
}
