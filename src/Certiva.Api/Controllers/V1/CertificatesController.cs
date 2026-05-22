using Asp.Versioning;
using Certiva.Api.Requests;
using Certiva.CertificationEngine.Commands;
using Certiva.CertificationEngine.Services;
using Certiva.Infrastructure.Authorization;
using Certiva.Infrastructure.Constants;
using Certiva.IssuerPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Certiva.Api.Controllers.V1;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/certificates")]
[Authorize(Policy = AuthorizationPolicies.Issuer)]
public sealed class CertificatesController : CertivaControllerBase
{
    private readonly ICertificationEngineService _certEngine;
    private readonly IIssuerPortalService _portal;

    public CertificatesController(ICertificationEngineService certEngine, IIssuerPortalService portal)
    {
        _certEngine = certEngine;
        _portal = portal;
    }

    /// <summary>POST /api/v1/certificates — issue a single certificate</summary>
    [HttpPost]
    public async Task<IActionResult> Issue([FromBody] IssueCertificateRequest req, CancellationToken ct)
    {
        var idempotencyKey = Request.Headers[CertivaHeaderNames.IdempotencyKey].FirstOrDefault();
        var result = await _certEngine.IssueCertificateAsync(new IssueCertificateCommand(
            TenantId: TenantId,
            ProfessionalId: req.ProfessionalId,
            IssuerId: CurrentUserId,
            TemplateId: req.TemplateId,
            IdempotencyKey: idempotencyKey), ct);
        return ToResult(result, 201);
    }

    /// <summary>POST /api/v1/certificates/{id}/revoke</summary>
    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, [FromBody] RevokeCertificateRequest req, CancellationToken ct)
    {
        var result = await _certEngine.RevokeCertificateAsync(new RevokeCertificateCommand(
            TenantId: TenantId,
            CertificateId: id,
            IssuerId: CurrentUserId,
            RevocationReason: req.RevocationReason), ct);
        return ToResult(result);
    }

    /// <summary>POST /api/v1/certificates/{id}/verify-hash — integrity check</summary>
    [HttpPost("{id:guid}/verify-hash")]
    public async Task<IActionResult> VerifyHash(Guid id, CancellationToken ct)
    {
        var result = await _certEngine.VerifyCertificateHashAsync(id, TenantId, ct);
        return ToResult(result);
    }

    /// <summary>POST /api/v1/certificates/bulk — enqueue a bulk issuance job</summary>
    [HttpPost("bulk")]
    public async Task<IActionResult> BulkIssue([FromBody] BulkIssueRequest req, CancellationToken ct)
    {
        var result = await _portal.EnqueueBulkIssueAsync(new EnqueueBulkIssueCommand
        {
            IssuerId = CurrentUserId,
            TemplateId = req.TemplateId,
            TenantId = TenantId,
            ProfessionalIds = req.ProfessionalIds
        }, ct);
        return ToResult(result, 202);
    }

    /// <summary>GET /api/v1/certificates/bulk/{jobId} — poll bulk job status</summary>
    [HttpGet("bulk/{jobId:guid}")]
    public async Task<IActionResult> GetBulkJobStatus(Guid jobId, CancellationToken ct)
    {
        var result = await _portal.GetBulkJobStatusAsync(jobId, CurrentUserId, TenantId, ct);
        return ToResult(result);
    }
}
