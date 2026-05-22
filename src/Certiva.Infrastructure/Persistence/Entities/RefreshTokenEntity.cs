namespace Certiva.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the RefreshTokens table.
/// Stores issued refresh tokens for session management.
/// </summary>
public class RefreshTokenEntity
{
    public Guid TokenId { get; set; }
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>The opaque refresh token value (64-byte random hex string).</summary>
    public string Token { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
