using Asp.Versioning;
using Certiva.Api.Requests;
using Certiva.IdentityRegistry.Commands;
using Certiva.IdentityRegistry.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Certiva.Api.Controllers.V1;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
[AllowAnonymous]
public sealed class AuthController : CertivaControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    /// <summary>POST /api/v1/auth/login</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var cmd = new LoginCommand(req.Email, req.Password, req.TotpCode, RemoteIp, req.TenantId);
        var result = await _authService.AuthenticateAsync(cmd, ct);
        return ToResult(result);
    }

    /// <summary>POST /api/v1/auth/refresh</summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req, CancellationToken ct)
    {
        var result = await _authService.RefreshTokenAsync(req.RefreshToken, req.TenantId, ct);
        return ToResult(result);
    }

    /// <summary>DELETE /api/v1/auth/session — requires bearer token</summary>
    [HttpDelete("session")]
    [Authorize]
    public async Task<IActionResult> RevokeSession([FromBody] RevokeSessionRequest req, CancellationToken ct)
    {
        var result = await _authService.RevokeSessionAsync(req.RefreshToken, req.TenantId, ct);
        return ToResult(result);
    }
}
