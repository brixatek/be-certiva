namespace Certiva.IdentityRegistry.Commands;

/// <summary>
/// Command to register a new Professional in the Identity Registry.
/// </summary>
public record RegisterProfessionalCommand
{
    /// <summary>The tenant this Professional belongs to.</summary>
    public Guid TenantId { get; init; }

    /// <summary>Full name of the Professional. Non-empty, maximum 100 characters.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>National identifier. Must be 6–20 alphanumeric characters.</summary>
    public string NationalId { get; init; } = string.Empty;

    /// <summary>Phone number in E.164 format (e.g. +1234567890). At least one of Phone/Email must be present.</summary>
    public string? Phone { get; init; }

    /// <summary>Email address conforming to RFC 5322. At least one of Phone/Email must be present.</summary>
    public string? Email { get; init; }
}
