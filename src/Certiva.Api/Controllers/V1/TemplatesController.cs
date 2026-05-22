using Asp.Versioning;
using Certiva.Api.Requests;
using Certiva.Infrastructure.Authorization;
using Certiva.IssuerPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Certiva.Api.Controllers.V1;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/templates")]
[Authorize(Policy = AuthorizationPolicies.Issuer)]
public sealed class TemplatesController : CertivaControllerBase
{
    private readonly IIssuerPortalService _portal;

    public TemplatesController(IIssuerPortalService portal) => _portal = portal;

    /// <summary>POST /api/v1/templates</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTemplateRequest req, CancellationToken ct)
    {
        var result = await _portal.CreateTemplateAsync(new CreateTemplateCommand
        {
            IssuerId = CurrentUserId,
            TenantId = TenantId,
            Name = req.Name,
            ValidityPeriodDays = req.ValidityPeriodDays
        }, ct);
        return ToResult(result, 201);
    }

    /// <summary>PUT /api/v1/templates/{id}</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTemplateRequest req, CancellationToken ct)
    {
        var result = await _portal.UpdateTemplateAsync(new UpdateTemplateCommand
        {
            TemplateId = id,
            IssuerId = CurrentUserId,
            TenantId = TenantId,
            Name = req.Name,
            ValidityPeriodDays = req.ValidityPeriodDays
        }, ct);
        return ToResult(result);
    }

    /// <summary>DELETE /api/v1/templates/{id}</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var result = await _portal.DeactivateTemplateAsync(id, CurrentUserId, TenantId, ct);
        return ToResult(result);
    }
}
