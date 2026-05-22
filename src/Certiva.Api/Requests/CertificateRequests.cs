namespace Certiva.Api.Requests;

public record IssueCertificateRequest(Guid ProfessionalId, Guid TemplateId);
public record RevokeCertificateRequest(string RevocationReason);
public record BulkIssueRequest(Guid TemplateId, List<Guid> ProfessionalIds);
