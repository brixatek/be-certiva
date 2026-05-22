using Asp.Versioning;
using Certiva.Api.Requests;
using Certiva.IdentityRegistry.Commands;
using Certiva.IdentityRegistry.Services;
using Certiva.Infrastructure.Authorization;
using Certiva.IssuerPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Certiva.Api.Controllers.V1;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/professionals")]
public sealed class ProfessionalsController : CertivaControllerBase
{
    private readonly IIdentityRegistryService _identityRegistry;
    private readonly IIssuerPortalService _portal;

    public ProfessionalsController(IIdentityRegistryService identityRegistry, IIssuerPortalService portal)
    {
        _identityRegistry = identityRegistry;
        _portal = portal;
    }

    /// <summary>POST /api/v1/professionals — Admin: register a new professional</summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    public async Task<IActionResult> Register([FromBody] RegisterProfessionalRequest req, CancellationToken ct)
    {
        var cmd = new RegisterProfessionalCommand
        {
            TenantId = TenantId,
            Name = req.Name,
            NationalId = req.NationalId,
            Phone = req.Phone,
            Email = req.Email
        };
        var result = await _identityRegistry.RegisterProfessionalAsync(cmd, ct);
        return ToResult(result, 201);
    }

    /// <summary>GET /api/v1/professionals/search?q=... — Issuer: search by name</summary>
    [HttpGet("search")]
    [Authorize(Policy = AuthorizationPolicies.Issuer)]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct)
    {
        var result = await _portal.SearchProfessionalsAsync(q, CurrentUserId, TenantId, ct);
        return ToResult(result);
    }
}
