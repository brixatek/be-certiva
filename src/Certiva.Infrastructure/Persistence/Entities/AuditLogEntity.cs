namespace Certiva.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the AuditLogs table.
/// Append-only record of every significant platform action.
/// EF Core is configured to prevent UPDATE and DELETE operations on this entity
/// (see CertivaDbContext.SaveChangesAsync). The database also enforces this via
/// PostgreSQL RULEs created in the InitialCreate migration.
/// </summary>
public class AuditLogEntity
{
    public Guid AuditId { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>Maximum 100 characters (e.g., "CertificateIssued", "IssuerApproved").</summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>String representation of the affected entity's identifier. Maximum 200 characters.</summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>Identity of the actor who performed the action. Maximum 200 characters.</summary>
    public string Actor { get; set; } = string.Empty;

    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Optional JSONB metadata (e.g., revocation reason, rejection notes).</summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// SHA-256 hex digest computed over (ActionType + EntityId + Timestamp + Actor + Metadata).
    /// Stored at write time; recomputing and comparing detects tampering. Maximum 64 characters.
    /// </summary>
    public string RecordHash { get; set; } = string.Empty;
}
