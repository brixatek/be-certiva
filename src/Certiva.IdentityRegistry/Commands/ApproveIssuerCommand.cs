namespace Certiva.IdentityRegistry.Commands;

/// <summary>
/// Command to approve a pending Issuer, transitioning its VerificationStatus to Verified.
/// </summary>
/// <param name="IssuerId">The Issuer to approve.</param>
/// <param name="TenantId">The tenant that owns the Issuer record.</param>
/// <param name="AdminActorId">The identity of the Platform Admin performing the approval.</param>
public sealed record ApproveIssuerCommand(
    Guid IssuerId,
    Guid TenantId,
    string AdminActorId);
