using Certiva.Infrastructure.Persistence;
using Certiva.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Certiva.Infrastructure.Idempotency;

/// <summary>
/// EF Core-backed implementation of <see cref="IIdempotencyKeyRepository"/>.
/// Uses the <c>IdempotencyKeys</c> table with a 24-hour TTL.
/// </summary>
public sealed class IdempotencyKeyRepository : IIdempotencyKeyRepository
{
    private readonly CertivaDbContext _db;

    public IdempotencyKeyRepository(CertivaDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<string?> GetResultAsync(string key, Guid tenantId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var entity = await _db.IdempotencyKeys
            .AsNoTracking()
            .Where(e => e.IdempotencyKey == key
                     && e.TenantId == tenantId
                     && e.ExpiresAt > now)
            .Select(e => e.ResultPayload)
            .FirstOrDefaultAsync(ct);

        return entity;
    }

    /// <inheritdoc />
    public async Task StoreAsync(
        string key,
        Guid tenantId,
        string operationType,
        string resultPayload,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var entity = new IdempotencyKeyEntity
        {
            IdempotencyKey = key,
            TenantId = tenantId,
            OperationType = operationType,
            ResultPayload = resultPayload,
            CreatedAt = now,
            ExpiresAt = now.AddHours(24)
        };

        _db.IdempotencyKeys.Add(entity);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Concurrent request already stored the same key — safe to ignore.
            // Detach the conflicting entity so the DbContext remains usable.
            _db.Entry(entity).State = EntityState.Detached;
        }
    }

    /// <inheritdoc />
    public async Task CleanupExpiredAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        await _db.IdempotencyKeys
            .Where(e => e.ExpiresAt <= now)
            .ExecuteDeleteAsync(ct);
    }
}
