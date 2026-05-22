using Certiva.Infrastructure.Domain;

namespace Certiva.AuditLog.Services;

/// <summary>
/// Append-only audit log query, pagination, and CSV export.
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Queries audit log entries with optional filters. Paginated at ≤100 records per page.
    /// </summary>
    Task<Result<PagedResult<AuditLogEntry>>> QueryAsync(AuditLogQuery query, CancellationToken ct);

    /// <summary>
    /// Streams a CSV export of matching audit log entries.
    /// Columns: ActionType, EntityId, Timestamp, Actor, Metadata.
    /// </summary>
    Task<Result<string>> ExportCsvAsync(AuditLogQuery query, CancellationToken ct);
}
