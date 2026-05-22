using Asp.Versioning;
using Certiva.Infrastructure.Authorization;
using Certiva.IssuerPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Certiva.Api.Controllers.V1;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/analytics")]
[Authorize(Policy = AuthorizationPolicies.Issuer)]
public sealed class AnalyticsController : CertivaControllerBase
{
    private readonly IIssuerPortalService _portal;

    public AnalyticsController(IIssuerPortalService portal) => _portal = portal;

    /// <summary>GET /api/v1/analytics — issuance stats for the authenticated issuer</summary>
    [HttpGet]
    public async Task<IActionResult> GetAnalytics(CancellationToken ct)
    {
        var result = await _portal.GetAnalyticsAsync(CurrentUserId, TenantId, ct);
        return ToResult(result);
    }
}
