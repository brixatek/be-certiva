namespace Certiva.Infrastructure.Redis;

/// <summary>
/// Abstraction over the Redis Verification_Store.
/// Provides O(1) certificate lookups with 1-hour TTL and availability probing.
/// </summary>
public interface IVerificationStoreRepository
{
    /// <summary>
    /// Retrieves the verification view for the given certificate, or <c>null</c> if not cached.
    /// </summary>
    Task<CertificateVerificationView?> GetAsync(Guid certificateId, Guid tenantId, CancellationToken ct);

    /// <summary>
    /// Inserts or replaces the verification view, resetting the 1-hour TTL.
    /// </summary>
    Task UpsertAsync(CertificateVerificationView view, CancellationToken ct);

    /// <summary>
    /// Returns <c>true</c> when the Redis connection is healthy (PING succeeds).
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken ct);
}
