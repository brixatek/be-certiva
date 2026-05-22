using Certiva.Infrastructure.Domain;
using Certiva.Infrastructure.Persistence.Repositories;
using System.Text;

namespace Certiva.AuditLog.Services;

/// <summary>
/// Implements <see cref="IAuditLogService"/> — query, paginate, and export audit log entries.
/// All operations are read-only (append-only enforcement is in the DbContext).
/// </summary>
public sealed class AuditLogService : IAuditLogService
{
    private readonly IAuditLogRepository _auditLogs;

    public AuditLogService(IAuditLogRepository auditLogs)
    {
        _auditLogs = auditLogs;
    }

    /// <inheritdoc/>
    public async Task<Result<PagedResult<AuditLogEntry>>> QueryAsync(AuditLogQuery query, CancellationToken ct)
    {
        var pageSize = Math.Min(query.PageSize, 100);
        var page = Math.Max(query.Page, 1);

        var (items, total) = await _auditLogs.QueryPagedAsync(
            query.TenantId,
            query.ActionType,
            query.EntityId,
            query.Actor,
            query.From,
            query.To,
            page,
            pageSize,
            ct);

        var entries = items.Select(a => new AuditLogEntry
        {
            AuditId = a.AuditId,
            ActionType = a.ActionType,
            EntityId = a.EntityId,
            Timestamp = a.Timestamp,
            Actor = a.Actor,
            Metadata = a.Metadata,
            RecordHash = a.RecordHash
        }).ToList();

        return Result<PagedResult<AuditLogEntry>>.Ok(
            new PagedResult<AuditLogEntry>(entries, total, page, pageSize));
    }

    /// <inheritdoc/>
    public async Task<Result<string>> ExportCsvAsync(AuditLogQuery query, CancellationToken ct)
    {
        var rows = await _auditLogs.QueryAllAsync(
            query.TenantId,
            query.ActionType,
            query.EntityId,
            query.Actor,
            query.From,
            query.To,
            ct);

        var sb = new StringBuilder();
        sb.AppendLine("ActionType,EntityId,Timestamp,Actor,Metadata");

        foreach (var row in rows)
        {
            sb.Append(CsvEscape(row.ActionType)).Append(',');
            sb.Append(CsvEscape(row.EntityId)).Append(',');
            sb.Append(CsvEscape(row.Timestamp.ToString("O"))).Append(',');
            sb.Append(CsvEscape(row.Actor)).Append(',');
            sb.AppendLine(CsvEscape(row.Metadata ?? string.Empty));
        }

        return Result<string>.Ok(sb.ToString());
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
