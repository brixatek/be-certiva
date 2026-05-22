namespace Certiva.AuditLog.Services;

public sealed record AuditLogQuery
{
    public Guid TenantId { get; init; }
    public string? ActionType { get; init; }
    public string? EntityId { get; init; }
    public string? Actor { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public sealed record AuditLogEntry
{
    public Guid AuditId { get; init; }
    public string ActionType { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; }
    public string Actor { get; init; } = string.Empty;
    public string? Metadata { get; init; }
    public string RecordHash { get; init; } = string.Empty;
}
