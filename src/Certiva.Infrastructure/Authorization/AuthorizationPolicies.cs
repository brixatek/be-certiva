namespace Certiva.Infrastructure.Authorization;

/// <summary>
/// Constants for RBAC authorization policy names used across the platform.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Policy name for Platform Admins. Requires the "Admin" role claim.</summary>
    public const string Admin = "AdminPolicy";

    /// <summary>Policy name for verified Issuers (Training Providers). Requires the "Issuer" role claim.</summary>
    public const string Issuer = "IssuerPolicy";

    /// <summary>Policy name for Workers (Professionals). Requires the "Worker" role claim.</summary>
    public const string Worker = "WorkerPolicy";
}
