namespace Certiva.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the CertificateTemplates table.
/// Represents a reusable certificate definition owned by an Issuer.
/// </summary>
public class CertificateTemplateEntity
{
    public Guid TemplateId { get; set; }
    public Guid TenantId { get; set; }
    public Guid IssuerId { get; set; }

    /// <summary>Maximum 100 characters. Case-insensitive uniqueness enforced per IssuerId+TenantId.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Maximum 500 characters. Optional.</summary>
    public string? Description { get; set; }

    /// <summary>Non-negative integer. Zero means the certificate does not expire.</summary>
    public int ValidityPeriodDays { get; set; }

    /// <summary>When false, the template cannot be selected for new issuance requests.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation properties
    public IssuerEntity Issuer { get; set; } = null!;
    public ICollection<CertificateEntity> Certificates { get; set; } = new List<CertificateEntity>();
}
