using Certiva.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Certiva.Infrastructure.Persistence.Repositories;

public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly CertivaDbContext _db;

    public AuditLogRepository(CertivaDbContext db) => _db = db;

    public void Add(AuditLogEntity entity) => _db.AuditLogs.Add(entity);

    public async Task<(List<AuditLogEntity> Items, int Total)> QueryPagedAsync(
        Guid tenantId,
        string? actionType,
        string? entityId,
        string? actor,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var q = BuildQuery(tenantId, actionType, entityId, actor, from, to);
        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<List<AuditLogEntity>> QueryAllAsync(
        Guid tenantId,
        string? actionType,
        string? entityId,
        string? actor,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default)
        => BuildQuery(tenantId, actionType, entityId, actor, from, to)
            .OrderByDescending(a => a.Timestamp)
            .AsNoTracking()
            .ToListAsync(ct);

    private IQueryable<AuditLogEntity> BuildQuery(
        Guid tenantId,
        string? actionType,
        string? entityId,
        string? actor,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        var q = _db.AuditLogs.Where(a => a.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(actionType))
            q = q.Where(a => a.ActionType == actionType);

        if (!string.IsNullOrWhiteSpace(entityId))
            q = q.Where(a => a.EntityId == entityId);

        if (!string.IsNullOrWhiteSpace(actor))
            q = q.Where(a => a.Actor == actor);

        if (from.HasValue)
            q = q.Where(a => a.Timestamp >= from.Value);

        if (to.HasValue)
            q = q.Where(a => a.Timestamp <= to.Value);

        return q;
    }
}
