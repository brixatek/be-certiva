namespace Certiva.IdentityRegistry.Commands;

/// <summary>
/// Command to reject a pending Issuer, transitioning its VerificationStatus to Rejected.
/// </summary>
/// <param name="IssuerId">The Issuer to reject.</param>
/// <param name="TenantId">The tenant that owns the Issuer record.</param>
/// <param name="AdminActorId">The identity of the Platform Admin performing the rejection.</param>
/// <param name="Reason">A human-readable explanation for the rejection.</param>
public sealed record RejectIssuerCommand(
    Guid IssuerId,
    Guid TenantId,
    string AdminActorId,
    string Reason);
