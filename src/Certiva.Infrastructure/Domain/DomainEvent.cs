namespace Certiva.Infrastructure.Domain;

/// <summary>
/// Base record for all domain events in the Certiva platform.
/// All domain events carry a unique EventId, the TenantId they belong to,
/// a monotonically increasing SequenceNumber per CertificateId, and the
/// UTC timestamp at which the event occurred.
/// </summary>
public abstract record DomainEvent
{
    /// <summary>
    /// Unique identifier for this event instance.
    /// </summary>
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// The tenant (organization) this event belongs to.
    /// </summary>
    public TenantId TenantId { get; init; }

    /// <summary>
    /// Monotonically increasing sequence number per CertificateId.
    /// Used by the Verification Engine to enforce ordered, exactly-once event application.
    /// </summary>
    public long SequenceNumber { get; init; }

    /// <summary>
    /// UTC timestamp at which the event occurred.
    /// </summary>
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    protected DomainEvent() { }

    protected DomainEvent(TenantId tenantId, long sequenceNumber)
    {
        TenantId = tenantId;
        SequenceNumber = sequenceNumber;
    }
}
