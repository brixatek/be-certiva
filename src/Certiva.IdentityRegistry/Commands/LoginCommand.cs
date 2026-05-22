namespace Certiva.IdentityRegistry.Commands;

/// <summary>
/// Command to authenticate a Professional and issue a JWT + refresh token pair.
/// </summary>
/// <param name="Email">The Professional's email address.</param>
/// <param name="Password">The plaintext password to verify against the stored BCrypt hash.</param>
/// <param name="TotpCode">Optional TOTP code if MFA is enabled for this account.</param>
/// <param name="RequestingIp">The IP address of the client, used for rate limiting.</param>
/// <param name="TenantId">The tenant scope for this authentication attempt.</param>
public record LoginCommand(
    string Email,
    string Password,
    string? TotpCode,
    string RequestingIp,
    Guid TenantId);

/// <summary>
/// Result returned on successful authentication, containing the JWT access token and refresh token.
/// </summary>
/// <param name="AccessToken">A signed JWT valid for 15 minutes.</param>
/// <param name="RefreshToken">An opaque refresh token valid for 7 days.</param>
/// <param name="ExpiresAt">The UTC expiry time of the access token.</param>
public record AuthResult(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt);
