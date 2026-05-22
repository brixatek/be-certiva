using Asp.Versioning;
using Certiva.VerificationEngine.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Certiva.Api.Controllers.V1;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/verify")]
[AllowAnonymous]
public sealed class VerificationController : CertivaControllerBase
{
    private readonly IVerificationEngineService _verificationEngine;

    public VerificationController(IVerificationEngineService verificationEngine)
        => _verificationEngine = verificationEngine;

    /// <summary>GET /api/v1/verify/{id} — public certificate lookup</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Verify(Guid id, CancellationToken ct)
    {
        var result = await _verificationEngine.VerifyCertificateAsync(id, RemoteIp, ct);
        return ToResult(result);
    }
}
