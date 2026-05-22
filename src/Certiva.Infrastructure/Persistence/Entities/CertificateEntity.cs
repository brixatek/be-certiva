namespace Certiva.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the Certificates table.
/// Represents a tamper-evident digital credential issued to a Professional.
/// </summary>
public class CertificateEntity
{
    public Guid CertificateId { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProfessionalId { get; set; }
    public Guid IssuerId { get; set; }
    public Guid TemplateId { get; set; }

    /// <summary>Maximum 200 characters.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>One of: Active, Expired, Revoked, Tampered. Defaults to Active.</summary>
    public string Status { get; set; } = "Active";

    public DateOnly IssueDate { get; set; }

    /// <summary>Null when ValidityPeriodDays is zero (certificate does not expire).</summary>
    public DateOnly? ExpiryDate { get; set; }

    /// <summary>SHA-256 hex digest forming the tamper-evident hash chain. Maximum 64 characters.</summary>
    public string CertificateHash { get; set; } = string.Empty;

    /// <summary>Verification URL. Maximum 500 characters. Populated asynchronously after QR generation.</summary>
    public string? QRCodeUrl { get; set; }

    /// <summary>Base64-encoded PNG of the QR code. Populated asynchronously after QR generation.</summary>
    public string? QRCodeBase64 { get; set; }

    /// <summary>Blob storage path for the generated PDF. Maximum 500 characters. Nullable until PDF is generated.</summary>
    public string? PdfStoragePath { get; set; }

    /// <summary>Maximum 500 characters. Required when Status is Revoked.</summary>
    public string? RevocationReason { get; set; }

    /// <summary>UTC timestamp of revocation. Null unless Status is Revoked.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation properties
    public ProfessionalEntity Professional { get; set; } = null!;
    public IssuerEntity Issuer { get; set; } = null!;
    public CertificateTemplateEntity Template { get; set; } = null!;
}
