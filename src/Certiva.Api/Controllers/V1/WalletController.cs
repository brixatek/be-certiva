using Asp.Versioning;
using Certiva.CertificateWallet.Services;
using Certiva.Infrastructure.Authorization;
using Certiva.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Certiva.Api.Controllers.V1;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/wallet")]
[Authorize(Policy = AuthorizationPolicies.Worker)]
public sealed class WalletController : CertivaControllerBase
{
    private readonly ICertificateWalletService _wallet;
    private readonly ICertificateRepository _certificates;
    private readonly IConfiguration _config;

    public WalletController(
        ICertificateWalletService wallet,
        ICertificateRepository certificates,
        IConfiguration config)
    {
        _wallet = wallet;
        _certificates = certificates;
        _config = config;
    }

    /// <summary>GET /api/v1/wallet/certificates — all certs grouped by status</summary>
    [HttpGet("certificates")]
    public async Task<IActionResult> GetCertificates(CancellationToken ct)
    {
        var result = await _wallet.GetCertificatesAsync(CurrentUserId, TenantId, ct);
        return ToResult(result);
    }

    /// <summary>GET /api/v1/wallet/certificates/{id}</summary>
    [HttpGet("certificates/{id:guid}")]
    public async Task<IActionResult> GetCertificate(Guid id, CancellationToken ct)
    {
        var result = await _wallet.GetCertificateDetailAsync(id, CurrentUserId, TenantId, ct);
        return ToResult(result);
    }

    /// <summary>GET /api/v1/wallet/certificates/{id}/share — shareable verify link</summary>
    [HttpGet("certificates/{id:guid}/share")]
    public async Task<IActionResult> GetShareableLink(Guid id, CancellationToken ct)
    {
        var result = await _wallet.GetShareableLinkAsync(id, CurrentUserId, TenantId, ct);
        return ToResult(result);
    }

    /// <summary>GET /api/v1/wallet/certificates/{id}/pdf-url — HMAC-signed PDF download URL</summary>
    [HttpGet("certificates/{id:guid}/pdf-url")]
    public async Task<IActionResult> GetPdfUrl(Guid id, CancellationToken ct)
    {
        var result = await _wallet.GetPdfDownloadUrlAsync(id, CurrentUserId, TenantId, ct);
        if (!result.IsSuccess) return ToResult(result);
        if (!result.Value!.PdfReady)
            return Accepted(new { message = result.Value.JobState });
        return Ok(new { signedUrl = result.Value.SignedUrl });
    }

    /// <summary>GET /api/v1/wallet/certificates/{id}/pdf/download — token-authenticated PDF stream</summary>
    [HttpGet("certificates/{id:guid}/pdf/download")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadPdf(
        Guid id,
        [FromQuery] long expires,
        [FromQuery] string sig,
        CancellationToken ct)
    {
        if (!_wallet.ValidateSignedUrl(id, expires, sig))
            return Problem("Invalid or expired download URL.", statusCode: 403);

        var cert = await _certificates.FindByIdWithNavigationsAsync(id, ct);
        if (cert is null || string.IsNullOrEmpty(cert.PdfStoragePath))
            return NotFound();

        var basePath = _config["Storage:BasePath"] ?? Path.Combine(Path.GetTempPath(), "certiva-pdfs");
        var filePath = Path.Combine(basePath, cert.PdfStoragePath);

        if (!System.IO.File.Exists(filePath))
            return NotFound();

        var bytes = await System.IO.File.ReadAllBytesAsync(filePath, ct);
        return File(bytes, "application/pdf", $"certificate-{id}.pdf");
    }
}
