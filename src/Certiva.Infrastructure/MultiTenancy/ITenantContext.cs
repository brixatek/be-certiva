namespace Certiva.Infrastructure.MultiTenancy;

/// <summary>
/// Provides the resolved TenantId for the current HTTP request.
/// Populated by <see cref="TenantResolutionMiddleware"/>.
/// </summary>
public interface ITenantContext
{
    /// <summary>The resolved tenant identifier. Returns <see cref="Guid.Empty"/> if not resolved.</summary>
    Guid TenantId { get; }

    /// <summary>True when a valid TenantId has been resolved for the current request.</summary>
    bool IsResolved { get; }
}
