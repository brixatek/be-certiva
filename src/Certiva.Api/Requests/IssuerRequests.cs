namespace Certiva.Api.Requests;

public record OnboardIssuerRequest(string OrganizationName, string? ContactEmail = null, string? IssuerType = null);
public record RejectIssuerRequest(string Reason);
