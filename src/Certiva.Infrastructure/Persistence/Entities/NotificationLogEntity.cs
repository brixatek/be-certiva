namespace Certiva.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the NotificationLogs table.
/// Tracks notification dispatch state and enforces idempotency via a unique IdempotencyKey.
/// </summary>
public class NotificationLogEntity
{
    public Guid NotificationId { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProfessionalId { get; set; }

    /// <summary>
    /// SHA-256(EventId + NotificationType). Maximum 64 characters. Unique across all records.
    /// Used to prevent duplicate notification dispatch.
    /// </summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>Maximum 50 characters (e.g., "IssuanceAlert", "RevocationAlert", "ExpiryReminder").</summary>
    public string NotificationType { get; set; } = string.Empty;

    /// <summary>The source event identifier. Maximum 200 characters.</summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>One of: Pending, Dispatched, Failed. Maximum 20 characters.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the notification was successfully dispatched. Null if not yet dispatched.</summary>
    public DateTimeOffset? DispatchedAt { get; set; }

    /// <summary>UTC timestamp of the final dispatch failure. Null unless Status is Failed.</summary>
    public DateTimeOffset? FailedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
