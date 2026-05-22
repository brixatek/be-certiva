namespace Certiva.Api.Requests;

public record RegisterProfessionalRequest(string Name, string NationalId, string? Phone, string? Email);
