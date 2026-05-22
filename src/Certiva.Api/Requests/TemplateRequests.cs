namespace Certiva.Api.Requests;

public record CreateTemplateRequest(string Name, int ValidityPeriodDays);
public record UpdateTemplateRequest(string Name, int ValidityPeriodDays);
