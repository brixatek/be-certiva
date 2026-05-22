namespace Certiva.CertificationEngine.Commands;

/// <summary>
/// Command to issue a certificate to a specific Professional using a given template.
/// </summary>
public record IssueCertificateCommand(
    Guid TenantId,
    Guid ProfessionalId,
    Guid IssuerId,
    Guid TemplateId,
    string? IdempotencyKey);
