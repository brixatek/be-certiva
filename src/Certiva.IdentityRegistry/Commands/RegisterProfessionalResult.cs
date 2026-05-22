namespace Certiva.IdentityRegistry.Commands;

/// <summary>
/// Result returned from a Professional registration operation.
/// </summary>
public record RegisterProfessionalResult
{
    /// <summary>The system-assigned identifier for the Professional.</summary>
    public Guid ProfessionalId { get; init; }

    /// <summary>
    /// True when a new Professional record was created.
    /// False when the request was a duplicate (HTTP 409 path is handled via Result.Conflict,
    /// so this will always be true on the success path).
    /// </summary>
    public bool IsNew { get; init; }

    public RegisterProfessionalResult(Guid professionalId, bool isNew)
    {
        ProfessionalId = professionalId;
        IsNew = isNew;
    }
}
