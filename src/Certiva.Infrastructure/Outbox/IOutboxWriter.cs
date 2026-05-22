namespace Certiva.Infrastructure.Outbox;

/// <summary>
/// Writes domain events to the transactional outbox within an existing EF Core transaction.
/// The caller is responsible for calling SaveChangesAsync — this writer only stages the entity.
/// </summary>
public interface IOutboxWriter
{
    /// <summary>
    /// Serializes <paramref name="payload"/> and adds an <c>OutboxMessageEntity</c> to the
    /// current DbContext change tracker. Does NOT call SaveChanges.
    /// </summary>
    Task WriteAsync(Guid tenantId, string eventType, object payload, CancellationToken ct = default);
}
