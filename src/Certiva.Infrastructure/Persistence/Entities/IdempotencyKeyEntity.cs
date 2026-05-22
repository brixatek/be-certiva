namespace Certiva.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the IdempotencyKeys table.
/// Stores client-supplied or system-generated idempotency tokens to prevent
/// duplicate processing of the same operation within the TTL window.
/// </summary>
public class IdempotencyKeyEntity
{
    /// <summary>Primary key. Maximum 256 characters. Client-supplied or system-generated token.</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    public Guid TenantId { get; set; }

    /// <summary>Maximum 100 characters (e.g., "IssueCertificate").</summary>
    public string OperationType { get; set; } = string.Empty;

    /// <summary>JSONB-serialized result of the original operation, returned on duplicate requests.</summary>
    public string ResultPayload { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>UTC expiry timestamp. Records past this time are eligible for cleanup.</summary>
    public DateTimeOffset ExpiresAt { get; set; }
}
