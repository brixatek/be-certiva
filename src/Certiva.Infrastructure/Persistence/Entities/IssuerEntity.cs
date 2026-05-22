namespace Certiva.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the Issuers table.
/// Represents a Training Provider organization authorized to issue certificates.
/// </summary>
public class IssuerEntity
{
    public Guid IssuerId { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>Maximum 200 characters. Case-insensitive uniqueness enforced per TenantId.</summary>
    public string OrganizationName { get; set; } = string.Empty;

    /// <summary>Maximum 50 characters (e.g., "TrainingProvider", "University").</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>One of: Pending, Verified, Rejected. Defaults to Pending.</summary>
    public string VerificationStatus { get; set; } = "Pending";

    public DateTimeOffset CreatedAt { get; set; }

    // Navigation properties
    public ICollection<CertificateTemplateEntity> Templates { get; set; } = new List<CertificateTemplateEntity>();
    public ICollection<CertificateEntity> Certificates { get; set; } = new List<CertificateEntity>();
}
