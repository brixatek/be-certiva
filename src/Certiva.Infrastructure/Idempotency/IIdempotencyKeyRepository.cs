namespace Certiva.Infrastructure.Idempotency;

/// <summary>
/// Repository for idempotency key check-and-store operations.
/// Supports 24-hour TTL-based deduplication of API operations.
/// </summary>
public interface IIdempotencyKeyRepository
{
    /// <summary>
    /// Returns the cached result payload for the given key and tenant if it exists and has not expired.
    /// Returns <c>null</c> if no matching, non-expired record is found.
    /// </summary>
    Task<string?> GetResultAsync(string key, Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Stores the result of a completed operation under the given idempotency key.
    /// Sets <c>ExpiresAt = UtcNow + 24 hours</c>.
    /// Silently ignores duplicate-key exceptions caused by concurrent requests.
    /// </summary>
    Task StoreAsync(string key, Guid tenantId, string operationType, string resultPayload, CancellationToken ct = default);

    /// <summary>
    /// Deletes all records whose <c>ExpiresAt</c> is less than or equal to the current UTC time.
    /// Intended to be called by a background cleanup job.
    /// </summary>
    Task CleanupExpiredAsync(CancellationToken ct = default);
}
