using Asp.Versioning;
using Certiva.AuditLog.Services;
using Certiva.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Certiva.Api.Controllers.V1;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/audit")]
[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class AuditController : CertivaControllerBase
{
    private readonly IAuditLogService _auditLog;

    public AuditController(IAuditLogService auditLog) => _auditLog = auditLog;

    /// <summary>GET /api/v1/audit — paginated audit log query</summary>
    [HttpGet]
    public async Task<IActionResult> Query(
        CancellationToken ct,
        [FromQuery] string? actionType = null,
        [FromQuery] string? entityId = null,
        [FromQuery] string? actor = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = new AuditLogQuery
        {
            TenantId = TenantId,
            ActionType = actionType,
            EntityId = entityId,
            Actor = actor,
            From = from,
            To = to,
            Page = page,
            PageSize = pageSize
        };
        var result = await _auditLog.QueryAsync(query, ct);
        return ToResult(result);
    }

    /// <summary>GET /api/v1/audit/export — CSV download</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        CancellationToken ct,
        [FromQuery] string? actionType = null,
        [FromQuery] string? entityId = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null)
    {
        var query = new AuditLogQuery
        {
            TenantId = TenantId,
            ActionType = actionType,
            EntityId = entityId,
            From = from,
            To = to
        };
        var result = await _auditLog.ExportCsvAsync(query, ct);
        if (!result.IsSuccess) return Problem(result.Error, statusCode: result.StatusCode ?? 500);
        return File(System.Text.Encoding.UTF8.GetBytes(result.Value!), "text/csv", "audit-log.csv");
    }
}
