namespace Certiva.IdentityRegistry.Commands;

/// <summary>
/// Command to onboard a new Issuer organization.
/// </summary>
/// <param name="TenantId">The tenant this Issuer belongs to.</param>
/// <param name="OrganizationName">The organization's display name (max 200 chars). Uniqueness is enforced case-insensitively per tenant.</param>
/// <param name="Type">The issuer type (e.g. "TrainingProvider", "University"). Max 50 chars.</param>
public sealed record OnboardIssuerCommand(
    Guid TenantId,
    string OrganizationName,
    string Type);

/// <summary>
/// Result returned on successful Issuer onboarding.
/// </summary>
/// <param name="IssuerId">The system-generated identifier for the newly created Issuer.</param>
public sealed record OnboardIssuerResult(Guid IssuerId);
