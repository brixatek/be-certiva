namespace Certiva.CertificationEngine.Commands;

/// <summary>
/// Command to revoke a certificate that was previously issued by the specified Issuer.
/// </summary>
public record RevokeCertificateCommand(
    Guid TenantId,
    Guid CertificateId,
    Guid IssuerId,
    string RevocationReason);
