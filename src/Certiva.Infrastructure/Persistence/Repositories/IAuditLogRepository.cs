using Certiva.Infrastructure.Persistence.Entities;

namespace Certiva.Infrastructure.Persistence.Repositories;

public interface IAuditLogRepository
{
    void Add(AuditLogEntity entity);

    /// <summary>
    /// Returns a filtered, paginated page of audit log entries (ordered newest-first)
    /// together with the total matching count (for pagination metadata).
    /// </summary>
    Task<(List<AuditLogEntity> Items, int Total)> QueryPagedAsync(
        Guid tenantId,
        string? actionType,
        string? entityId,
        string? actor,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Returns all matching audit log entries without pagination (for CSV export).
    /// </summary>
    Task<List<AuditLogEntity>> QueryAllAsync(
        Guid tenantId,
        string? actionType,
        string? entityId,
        string? actor,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default);
}
