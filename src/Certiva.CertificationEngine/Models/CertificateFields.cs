namespace Certiva.CertificationEngine.Models;

/// <summary>
/// Represents the certificate fields used for canonical serialization and hash chain computation.
/// </summary>
public record CertificateFields(
    Guid CertificateId,
    DateOnly? ExpiryDate,
    Guid IssuerId,
    string IssuerName,
    DateOnly IssueDate,
    string Name,
    Guid ProfessionalId,
    Guid TenantId);
