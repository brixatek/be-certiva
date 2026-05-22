namespace Certiva.VerificationEngine.Services;

/// <summary>
/// The verification response returned for a public certificate lookup.
/// Contact details (Phone, Email, NationalId) are intentionally excluded (Req 16.4).
/// </summary>
public sealed record VerificationResult
{
    public bool Valid { get; init; }
    public Guid CertificateId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string ProfessionalName { get; init; } = string.Empty;
    public string CertificateName { get; init; } = string.Empty;
    public string IssuerName { get; init; } = string.Empty;
    public DateOnly IssueDate { get; init; }
    public DateOnly? ExpiryDate { get; init; }
    public string? QrCodeUrl { get; init; }

    /// <summary>Data source: "redis" or "postgresql".</summary>
    public string Source { get; init; } = string.Empty;
}
