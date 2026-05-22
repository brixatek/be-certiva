using System.Text.Json;
using Certiva.Infrastructure.Persistence;
using Certiva.Infrastructure.Persistence.Entities;

namespace Certiva.Infrastructure.Outbox;

/// <summary>
/// Scoped implementation of <see cref="IOutboxWriter"/>.
/// Stages an <see cref="OutboxMessageEntity"/> in the EF change tracker without calling SaveChanges,
/// so the caller's ambient transaction commits the outbox row atomically with the business operation.
/// </summary>
public sealed class OutboxWriter : IOutboxWriter
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly CertivaDbContext _db;

    public OutboxWriter(CertivaDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public Task WriteAsync(Guid tenantId, string eventType, object payload, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(payload, payload.GetType(), _jsonOptions);

        var entity = new OutboxMessageEntity
        {
            MessageId = Guid.NewGuid(),
            TenantId = tenantId,
            EventType = eventType,
            Payload = json,
            CreatedAt = DateTimeOffset.UtcNow,
            Published = false
        };

        _db.OutboxMessages.Add(entity);

        return Task.CompletedTask;
    }
}
