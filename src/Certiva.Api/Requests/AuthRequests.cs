namespace Certiva.Api.Requests;

public record LoginRequest(string Email, string Password, Guid TenantId, string? TotpCode = null);
public record RefreshRequest(string RefreshToken, Guid TenantId);
public record RevokeSessionRequest(string RefreshToken, Guid TenantId);
