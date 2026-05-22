using Certiva.Infrastructure.Outbox;
using System.Text.Json;

namespace Certiva.Tests.Property.Helpers;

/// <summary>
/// In-memory outbox writer for tests — stages events without publishing or requiring a real DB.
/// </summary>
public sealed class FakeOutboxWriter : IOutboxWriter
{
    private readonly List<(Guid TenantId, string EventType, string Payload)> _messages = new();

    public IReadOnlyList<(Guid TenantId, string EventType, string Payload)> Messages => _messages;

    public Task WriteAsync(Guid tenantId, string eventType, object payload, CancellationToken ct = default)
    {
        _messages.Add((tenantId, eventType, JsonSerializer.Serialize(payload)));
        return Task.CompletedTask;
    }

    public bool HasEvent(string eventType) =>
        _messages.Any(m => m.EventType == eventType);

    public int CountEvents(string eventType) =>
        _messages.Count(m => m.EventType == eventType);
}
