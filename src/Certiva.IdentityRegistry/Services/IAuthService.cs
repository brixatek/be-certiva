using Certiva.IdentityRegistry.Commands;
using Certiva.Infrastructure.Domain;

namespace Certiva.IdentityRegistry.Services;

/// <summary>
/// Handles authentication, token issuance, refresh, and session revocation.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Validates credentials (and TOTP if MFA is enabled), applies rate limiting,
    /// and issues a JWT access token + refresh token pair on success.
    /// </summary>
    /// <param name="cmd">The login command containing email, password, optional TOTP code, IP, and tenant.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see cref="Result{AuthResult}"/> containing the token pair on success,
    /// or a failure result with an appropriate HTTP status code on error.
    /// </returns>
    Task<Result<AuthResult>> AuthenticateAsync(LoginCommand cmd, CancellationToken ct);

    /// <summary>
    /// Validates a refresh token, issues a new JWT + refresh token pair, and revokes the old refresh token.
    /// </summary>
    /// <param name="refreshToken">The opaque refresh token value.</param>
    /// <param name="tenantId">The tenant scope for this refresh request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see cref="Result{AuthResult}"/> containing the new token pair on success,
    /// or a failure result if the token is invalid, expired, or revoked.
    /// </returns>
    Task<Result<AuthResult>> RefreshTokenAsync(string refreshToken, Guid tenantId, CancellationToken ct);

    /// <summary>
    /// Revokes a refresh token, invalidating the associated session.
    /// </summary>
    /// <param name="refreshToken">The opaque refresh token value to revoke.</param>
    /// <param name="tenantId">The tenant scope for this revocation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see cref="Result"/> indicating success or failure.
    /// </returns>
    Task<Result> RevokeSessionAsync(string refreshToken, Guid tenantId, CancellationToken ct);
}
