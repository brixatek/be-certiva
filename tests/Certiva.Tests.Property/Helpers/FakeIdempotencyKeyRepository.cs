using Certiva.Infrastructure.Idempotency;

namespace Certiva.Tests.Property.Helpers;

/// <summary>
/// In-memory idempotency key store for property tests.
/// </summary>
public sealed class FakeIdempotencyKeyRepository : IIdempotencyKeyRepository
{
    private readonly Dictionary<string, string> _store = new();

    public Task<string?> GetResultAsync(string idempotencyKey, Guid tenantId, CancellationToken ct = default)
    {
        _store.TryGetValue(Key(idempotencyKey, tenantId), out var result);
        return Task.FromResult(result);
    }

    public Task StoreAsync(
        string idempotencyKey,
        Guid tenantId,
        string operationType,
        string resultPayload,
        CancellationToken ct = default)
    {
        _store[Key(idempotencyKey, tenantId)] = resultPayload;
        return Task.CompletedTask;
    }

    public Task CleanupExpiredAsync(CancellationToken ct = default) => Task.CompletedTask;

    private static string Key(string idempotencyKey, Guid tenantId) =>
        $"{tenantId}:{idempotencyKey}";
}
