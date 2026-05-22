using Certiva.Infrastructure.Constants;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Certiva.Infrastructure.MultiTenancy;

/// <summary>
/// Resolves the <c>tenant_id</c> claim from the authenticated JWT and stores it in
/// <c>HttpContext.Items["TenantId"]</c> for downstream services.
///
/// Returns HTTP 400 if the claim is missing or not a valid GUID on authenticated requests.
/// Public endpoints (health, metrics, verify, auth) are skipped.
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private static readonly string[] PublicPathPrefixes =
    [
        "/health",
        "/metrics",
        "/api/v1/verify/",
        "/api/v1/auth"
    ];

    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip tenant resolution for public endpoints.
        var path = context.Request.Path.Value ?? string.Empty;
        if (IsPublicPath(path))
        {
            await _next(context);
            return;
        }

        // If the user is not authenticated, pass through — auth middleware handles 401.
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        // Extract the tenant_id claim.
        var tenantIdClaim = context.User.FindFirst(ClaimNames.TenantId)?.Value;

        if (string.IsNullOrWhiteSpace(tenantIdClaim) || !Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = "Missing or invalid tenant_id claim" }));
            return;
        }

        context.Items[HttpContextKeys.TenantId] = tenantId;

        await _next(context);
    }

    private static bool IsPublicPath(string path)
    {
        foreach (var prefix in PublicPathPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
