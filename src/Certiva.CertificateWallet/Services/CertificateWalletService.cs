using Certiva.CertificateWallet.Resources;
using Certiva.Infrastructure.Constants;
using Certiva.Infrastructure.Domain;
using Certiva.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace Certiva.CertificateWallet.Services;

/// <summary>
/// Implements <see cref="ICertificateWalletService"/>.
/// Handles certificate wallet operations for Professionals.
/// </summary>
public sealed class CertificateWalletService : ICertificateWalletService
{
    private readonly ICertificateRepository _certificates;
    private readonly string _domain;
    private readonly byte[] _hmacKey;

    public CertificateWalletService(ICertificateRepository certificates, IConfiguration config)
    {
        _certificates = certificates;
        _domain = config["App:Domain"]?.TrimEnd('/') ?? "https://certiva.app";
        var keyStr = config["App:SignedUrlHmacKey"] ?? "certiva-signed-url-default-key-change-in-prod";
        _hmacKey = Encoding.UTF8.GetBytes(keyStr);
    }

    /// <inheritdoc/>
    public async Task<Result<WalletSummary>> GetCertificatesAsync(
        Guid professionalId,
        Guid tenantId,
        CancellationToken ct)
    {
        var certs = await _certificates.GetWalletAsync(professionalId, tenantId, ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var warningThreshold = today.AddDays(30);

        WalletCertificateItem Map(Infrastructure.Persistence.Entities.CertificateEntity c) => new()
        {
            CertificateId = c.CertificateId,
            Name = c.Name,
            Status = c.Status,
            IssuerName = c.Issuer.OrganizationName,
            IssueDate = c.IssueDate,
            ExpiryDate = c.ExpiryDate,
            ExpiryWarning = c.ExpiryDate.HasValue && c.ExpiryDate.Value <= warningThreshold,
            QrCodeUrl = c.QRCodeUrl
        };

        var summary = new WalletSummary
        {
            Active = certs.Where(c => c.Status == CertificateStatus.Active).Select(Map).ToList(),
            Expired = certs.Where(c => c.Status == CertificateStatus.Expired).Select(Map).ToList(),
            Revoked = certs.Where(c => c.Status == CertificateStatus.Revoked).Select(Map).ToList()
        };

        return Result<WalletSummary>.Ok(summary);
    }

    /// <inheritdoc/>
    public async Task<Result<WalletCertificateDetail>> GetCertificateDetailAsync(
        Guid certificateId,
        Guid professionalId,
        Guid tenantId,
        CancellationToken ct)
    {
        var cert = await _certificates.FindByIdWithNavigationsAsync(certificateId, ct);

        if (cert is null || cert.TenantId != tenantId)
            return Result<WalletCertificateDetail>.NotFound(WalletMessages.CertificateNotFound(certificateId));

        if (cert.ProfessionalId != professionalId)
            return Result<WalletCertificateDetail>.Forbidden(WalletMessages.AccessDenied);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var pdfLink = cert.PdfStoragePath is not null
            ? $"{_domain}/api/v1/wallet/certificates/{certificateId}/pdf"
            : null;

        var detail = new WalletCertificateDetail
        {
            CertificateId = cert.CertificateId,
            Name = cert.Name,
            Status = cert.Status,
            IssuerName = cert.Issuer.OrganizationName,
            IssueDate = cert.IssueDate,
            ExpiryDate = cert.ExpiryDate,
            ExpiryWarning = cert.ExpiryDate.HasValue && cert.ExpiryDate.Value <= today.AddDays(30),
            QrCodeValue = cert.QRCodeUrl,
            QrCodeBase64 = cert.QRCodeBase64,
            PdfDownloadLink = pdfLink,
            ShareableLink = $"{_domain}/verify/{certificateId}"
        };

        return Result<WalletCertificateDetail>.Ok(detail);
    }

    /// <inheritdoc/>
    public async Task<Result<PdfDownloadUrlResult>> GetPdfDownloadUrlAsync(
        Guid certificateId,
        Guid professionalId,
        Guid tenantId,
        CancellationToken ct)
    {
        var cert = await _certificates.FindByIdWithNavigationsAsync(certificateId, ct);

        if (cert is null || cert.TenantId != tenantId)
            return Result<PdfDownloadUrlResult>.NotFound(WalletMessages.CertificateNotFound(certificateId));

        if (cert.ProfessionalId != professionalId)
            return Result<PdfDownloadUrlResult>.Forbidden(WalletMessages.PdfAccessDenied);

        if (string.IsNullOrEmpty(cert.PdfStoragePath))
        {
            return Result<PdfDownloadUrlResult>.Ok(new PdfDownloadUrlResult
            {
                PdfReady = false,
                JobState = WalletMessages.PdfNotReady
            });
        }

        // Generate HMAC-SHA256 signed URL valid for 15 minutes
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeSeconds();
        var payload = $"{certificateId}:{expiresAt}";
        var signature = ComputeHmac(payload);
        var signedUrl = $"{_domain}/api/v1/wallet/certificates/{certificateId}/pdf/download" +
                        $"?expires={expiresAt}&sig={signature}";

        return Result<PdfDownloadUrlResult>.Ok(new PdfDownloadUrlResult
        {
            PdfReady = true,
            SignedUrl = signedUrl
        });
    }

    /// <inheritdoc/>
    public async Task<Result<string>> GetShareableLinkAsync(
        Guid certificateId,
        Guid professionalId,
        Guid tenantId,
        CancellationToken ct)
    {
        var cert = await _certificates.FindByIdWithNavigationsAsync(certificateId, ct);

        if (cert is null || cert.TenantId != tenantId)
            return Result<string>.NotFound(WalletMessages.CertificateNotFound(certificateId));

        if (cert.ProfessionalId != professionalId)
            return Result<string>.Forbidden(WalletMessages.ShareAccessDenied);

        var link = $"{_domain}/verify/{certificateId}";
        return Result<string>.Ok(link);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Validates a signed PDF download URL.</summary>
    public bool ValidateSignedUrl(Guid certificateId, long expiresAt, string signature)
    {
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresAt)
            return false;

        var payload = $"{certificateId}:{expiresAt}";
        var expected = ComputeHmac(payload);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature));
    }

    private string ComputeHmac(string payload)
    {
        using var hmac = new HMACSHA256(_hmacKey);
        var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
