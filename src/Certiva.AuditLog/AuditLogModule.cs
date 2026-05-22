using Certiva.AuditLog.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Certiva.AuditLog;

/// <summary>
/// Entry point for the AuditLog module.
/// Handles append-only audit trail, cryptographic hash per record, filtering, pagination, and CSV export.
/// </summary>
public static class AuditLogModule
{
    public static IServiceCollection AddAuditLog(this IServiceCollection services)
    {
        services.AddScoped<IAuditLogService, AuditLogService>();
        return services;
    }
}
