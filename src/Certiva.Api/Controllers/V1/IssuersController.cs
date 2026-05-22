using Asp.Versioning;
using Certiva.Api.Requests;
using Certiva.IdentityRegistry.Commands;
using Certiva.IdentityRegistry.Services;
using Certiva.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Certiva.Api.Controllers.V1;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/issuers")]
[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class IssuersController : CertivaControllerBase
{
    private readonly IIdentityRegistryService _identityRegistry;

    public IssuersController(IIdentityRegistryService identityRegistry)
        => _identityRegistry = identityRegistry;

    /// <summary>POST /api/v1/issuers — onboard a new issuer organisation</summary>
    [HttpPost]
    public async Task<IActionResult> Onboard([FromBody] OnboardIssuerRequest req, CancellationToken ct)
    {
        var cmd = new OnboardIssuerCommand(TenantId, req.OrganizationName, req.IssuerType ?? "TrainingProvider");
        var result = await _identityRegistry.OnboardIssuerAsync(cmd, ct);
        return ToResult(result, 201);
    }

    /// <summary>POST /api/v1/issuers/{id}/approve</summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        var cmd = new ApproveIssuerCommand(id, TenantId, Actor);
        var result = await _identityRegistry.ApproveIssuerAsync(cmd, ct);
        return ToResult(result);
    }

    /// <summary>POST /api/v1/issuers/{id}/reject</summary>
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectIssuerRequest req, CancellationToken ct)
    {
        var cmd = new RejectIssuerCommand(id, TenantId, Actor, req.Reason);
        var result = await _identityRegistry.RejectIssuerAsync(cmd, ct);
        return ToResult(result);
    }
}
