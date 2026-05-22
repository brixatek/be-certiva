using Certiva.Infrastructure.Constants;
using Microsoft.AspNetCore.Http;

namespace Certiva.Infrastructure.MultiTenancy;

/// <summary>
/// Reads the resolved <c>TenantId</c> from <see cref="IHttpContextAccessor"/>.
/// The value is set by <see cref="TenantResolutionMiddleware"/> early in the pipeline.
/// </summary>
public sealed class HttpContextTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextTenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public Guid TenantId =>
        _httpContextAccessor.HttpContext?.Items[HttpContextKeys.TenantId] is Guid id ? id : Guid.Empty;

    /// <inheritdoc />
    public bool IsResolved =>
        _httpContextAccessor.HttpContext?.Items[HttpContextKeys.TenantId] is Guid id && id != Guid.Empty;
}
