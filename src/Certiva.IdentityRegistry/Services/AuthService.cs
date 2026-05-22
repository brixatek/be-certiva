using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Certiva.IdentityRegistry.Commands;
using Certiva.IdentityRegistry.Resources;
using Certiva.Infrastructure.Constants;
using Certiva.Infrastructure.Domain;
using Certiva.Infrastructure.Persistence;
using Certiva.Infrastructure.Persistence.Entities;
using Certiva.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using OtpNet;
using StackExchange.Redis;

namespace Certiva.IdentityRegistry.Services;

/// <summary>
/// Default implementation of <see cref="IAuthService"/>.
/// Handles credential validation, TOTP MFA, JWT issuance, refresh token lifecycle,
/// Redis-backed rate limiting, and audit logging.
/// </summary>
public sealed class AuthService : IAuthService
{
    private const int MaxAttempts = 10;
    private const int RateLimitWindowSeconds = 900; // 15 minutes
    private const int AccessTokenLifetimeMinutes = 15;
    private const int RefreshTokenLifetimeDays = 7;

    private readonly CertivaDbContext _db;
    private readonly IConfiguration _config;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<AuthService> _logger;
    private readonly IEncryptionService _encryption;
    private readonly IProfessionalRepository _professionals;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IAuditLogRepository _auditLogs;

    public AuthService(
        CertivaDbContext db,
        IConfiguration config,
        IConnectionMultiplexer redis,
        ILogger<AuthService> logger,
        IEncryptionService encryption,
        IProfessionalRepository professionals,
        IRefreshTokenRepository refreshTokens,
        IAuditLogRepository auditLogs)
    {
        _db = db;
        _config = config;
        _redis = redis;
        _logger = logger;
        _encryption = encryption;
        _professionals = professionals;
        _refreshTokens = refreshTokens;
        _auditLogs = auditLogs;
    }

    /// <inheritdoc />
    public async Task<Result<AuthResult>> AuthenticateAsync(LoginCommand cmd, CancellationToken ct)
    {
        // --- Rate limiting (Requirement 15.10): Redis counter per IP ---
        var rateLimitKey = RedisKeys.AuthRateLimit(cmd.RequestingIp);
        var db = _redis.GetDatabase();

        var count = await db.StringIncrementAsync(rateLimitKey);
        if (count == 1)
        {
            // First attempt in this window — set the expiry
            await db.KeyExpireAsync(rateLimitKey, TimeSpan.FromSeconds(RateLimitWindowSeconds));
        }

        if (count > MaxAttempts)
        {
            _logger.LogWarning("Rate limit exceeded for IP {Ip}", cmd.RequestingIp);
            return Result<AuthResult>.Fail(AuthMessages.RateLimitExceeded, 429);
        }

        // --- Look up user by email within tenant ---
        // Emails are AES-256 encrypted; scan all professionals for the tenant and decrypt to find match.
        // Acceptable for Phase 1 (small tenant sizes); replace with hash-based lookup in Phase 2.
        var professionals = await _professionals.GetAllWithEmailByTenantAsync(cmd.TenantId, ct);

        ProfessionalEntity? user = null;
        foreach (var p in professionals)
        {
            try
            {
                var decryptedEmail = _encryption.Decrypt(p.Email_Encrypted!);
                if (string.Equals(decryptedEmail, cmd.Email, StringComparison.OrdinalIgnoreCase))
                {
                    user = p;
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to decrypt email for professional {Id}", p.ProfessionalId);
            }
        }

        if (user is null || string.IsNullOrEmpty(user.PasswordHash))
        {
            await WriteAuthFailedAuditAsync(cmd.TenantId, cmd.Email, "User not found", ct);
            return Result<AuthResult>.Fail(AuthMessages.InvalidCredentials, 401);
        }

        // --- Password verification (Requirement 15.1) ---
        bool passwordValid;
        try
        {
            passwordValid = BCrypt.Net.BCrypt.Verify(cmd.Password, user.PasswordHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BCrypt verification error for user {Id}", user.ProfessionalId);
            passwordValid = false;
        }

        if (!passwordValid)
        {
            await WriteAuthFailedAuditAsync(cmd.TenantId, user.ProfessionalId.ToString(), "Invalid password", ct);
            return Result<AuthResult>.Fail(AuthMessages.InvalidCredentials, 401);
        }

        // --- TOTP MFA validation (Requirement 15.3) ---
        if (!string.IsNullOrEmpty(user.TotpSecret))
        {
            if (string.IsNullOrWhiteSpace(cmd.TotpCode))
            {
                await WriteAuthFailedAuditAsync(cmd.TenantId, user.ProfessionalId.ToString(), "MFA code required", ct);
                return Result<AuthResult>.Fail(AuthMessages.MfaRequired, 401);
            }

            var secretBytes = Base32Encoding.ToBytes(user.TotpSecret);
            var totp = new Totp(secretBytes);

            // Verify with ±30s window (1 step tolerance)
            var totpValid = totp.VerifyTotp(
                cmd.TotpCode,
                out _,
                new VerificationWindow(previous: 1, future: 1));

            if (!totpValid)
            {
                await WriteAuthFailedAuditAsync(cmd.TenantId, user.ProfessionalId.ToString(), "Invalid TOTP code", ct);
                return Result<AuthResult>.Fail(AuthMessages.InvalidCredentials, 401);
            }
        }

        // --- Issue JWT access token (Requirement 15.2) ---
        var now = DateTimeOffset.UtcNow;
        var accessTokenExpiry = now.AddMinutes(AccessTokenLifetimeMinutes);
        var accessToken = GenerateJwt(user.ProfessionalId, user.TenantId, accessTokenExpiry);

        // --- Issue refresh token (Requirement 15.4) ---
        var refreshTokenValue = GenerateRefreshTokenValue();
        _refreshTokens.Add(new RefreshTokenEntity
        {
            TokenId = Guid.NewGuid(),
            UserId = user.ProfessionalId,
            TenantId = user.TenantId,
            Token = refreshTokenValue,
            ExpiresAt = now.AddDays(RefreshTokenLifetimeDays),
            IsRevoked = false,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} authenticated successfully from IP {Ip}", user.ProfessionalId, cmd.RequestingIp);

        return Result<AuthResult>.Ok(new AuthResult(accessToken, refreshTokenValue, accessTokenExpiry));
    }

    /// <inheritdoc />
    public async Task<Result<AuthResult>> RefreshTokenAsync(string refreshToken, Guid tenantId, CancellationToken ct)
    {
        // --- Find the refresh token ---
        var tokenEntity = await _refreshTokens.FindByTokenAsync(refreshToken, tenantId, ct);

        if (tokenEntity is null)
            return Result<AuthResult>.Fail(AuthMessages.TokenInvalid, 401);

        if (tokenEntity.IsRevoked)
            return Result<AuthResult>.Fail(AuthMessages.TokenRevoked, 401);

        if (tokenEntity.ExpiresAt <= DateTimeOffset.UtcNow)
            return Result<AuthResult>.Fail(AuthMessages.TokenExpired, 401);

        // --- Revoke the old token ---
        tokenEntity.IsRevoked = true;

        // --- Issue new JWT + refresh token pair ---
        var now = DateTimeOffset.UtcNow;
        var accessTokenExpiry = now.AddMinutes(AccessTokenLifetimeMinutes);
        var newAccessToken = GenerateJwt(tokenEntity.UserId, tokenEntity.TenantId, accessTokenExpiry);

        var newRefreshTokenValue = GenerateRefreshTokenValue();
        _refreshTokens.Add(new RefreshTokenEntity
        {
            TokenId = Guid.NewGuid(),
            UserId = tokenEntity.UserId,
            TenantId = tokenEntity.TenantId,
            Token = newRefreshTokenValue,
            ExpiresAt = now.AddDays(RefreshTokenLifetimeDays),
            IsRevoked = false,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);

        return Result<AuthResult>.Ok(new AuthResult(newAccessToken, newRefreshTokenValue, accessTokenExpiry));
    }

    /// <inheritdoc />
    public async Task<Result> RevokeSessionAsync(string refreshToken, Guid tenantId, CancellationToken ct)
    {
        var tokenEntity = await _refreshTokens.FindByTokenAsync(refreshToken, tenantId, ct);

        if (tokenEntity is null)
            return Result.NotFound(AuthMessages.TokenNotFound);

        if (tokenEntity.IsRevoked)
            return Result.Ok(); // Already revoked — idempotent

        tokenEntity.IsRevoked = true;
        await _db.SaveChangesAsync(ct);

        return Result.Ok();
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private string GenerateJwt(Guid userId, Guid tenantId, DateTimeOffset expiresAt)
    {
        var signingKey = _config["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");
        var issuer = _config["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
        var audience = _config["Jwt:Audience"]
            ?? throw new InvalidOperationException("Jwt:Audience is not configured.");

        var keyBytes = Encoding.UTF8.GetBytes(signingKey);
        var securityKey = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(ClaimNames.TenantId, tenantId.ToString()),
            new Claim(ClaimTypes.Role, Roles.Worker),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt.UtcDateTime,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);
        return handler.WriteToken(token);
    }

    private static string GenerateRefreshTokenValue()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private async Task WriteAuthFailedAuditAsync(
        Guid tenantId,
        string entityId,
        string reason,
        CancellationToken ct)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var metadata = $"{{\"reason\":\"{reason}\"}}";
        var recordHash = ComputeAuditHash(AuditActions.AuthFailed, entityId, timestamp, Actors.System, metadata);

        _auditLogs.Add(new AuditLogEntity
        {
            AuditId = Guid.NewGuid(),
            TenantId = tenantId,
            ActionType = AuditActions.AuthFailed,
            EntityId = entityId,
            Actor = Actors.System,
            Timestamp = timestamp,
            Metadata = metadata,
            RecordHash = recordHash
        });

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Audit log failure must not block the auth response
            _logger.LogError(ex, "Failed to write AuthFailed audit log for entity {EntityId}", entityId);
        }
    }

    private static string ComputeAuditHash(
        string actionType,
        string entityId,
        DateTimeOffset timestamp,
        string actor,
        string? metadata)
    {
        var raw = actionType
                  + entityId
                  + timestamp.ToString("O")
                  + actor
                  + (metadata ?? string.Empty);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
