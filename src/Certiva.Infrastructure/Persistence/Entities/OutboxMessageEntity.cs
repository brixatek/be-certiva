namespace Certiva.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the OutboxMessages table.
/// Implements the transactional outbox pattern — domain events are written here
/// atomically with the business operation and relayed to the Event Bus by a background worker.
/// </summary>
public class OutboxMessageEntity
{
    public Guid MessageId { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>Maximum 100 characters (e.g., "CertificateIssued", "IssuerApproved").</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>JSONB-serialized event payload.</summary>
    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>UTC timestamp when the message was successfully published to the Event Bus. Null if not yet published.</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>False until the background relay worker successfully publishes the message.</summary>
    public bool Published { get; set; } = false;
}
