namespace Certiva.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the Professionals table.
/// Stores encrypted PII fields and a SHA-256 hash of NationalId for deduplication.
/// </summary>
public class ProfessionalEntity
{
    public Guid ProfessionalId { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>Maximum 100 characters, non-null, non-empty.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>AES-256 encrypted NationalId bytes.</summary>
    public byte[] NationalId_Encrypted { get; set; } = Array.Empty<byte>();

    /// <summary>SHA-256 hex digest of the plaintext NationalId, used for deduplication lookups.</summary>
    public string NationalId_Hash { get; set; } = string.Empty;

    /// <summary>AES-256 encrypted Phone bytes. Nullable — at least one of Phone/Email must be present.</summary>
    public byte[]? Phone_Encrypted { get; set; }

    /// <summary>AES-256 encrypted Email bytes. Nullable — at least one of Phone/Email must be present.</summary>
    public byte[]? Email_Encrypted { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>BCrypt hash of the Professional's password. Nullable — set when the Professional registers a login.</summary>
    public string? PasswordHash { get; set; }

    /// <summary>Base32-encoded TOTP secret for MFA. Nullable — set when the Professional enables MFA.</summary>
    public string? TotpSecret { get; set; }

    // Navigation properties
    public ICollection<CertificateEntity> Certificates { get; set; } = new List<CertificateEntity>();
}
