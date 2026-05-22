using Microsoft.Extensions.DependencyInjection;

namespace Certiva.Infrastructure.MultiTenancy;

/// <summary>
/// DI registration helpers for multi-tenancy infrastructure.
/// </summary>
public static class MultiTenancyServiceExtensions
{
    /// <summary>
    /// Registers <see cref="IHttpContextAccessor"/> and
    /// <see cref="ITenantContext"/> → <see cref="HttpContextTenantContext"/> (scoped).
    /// </summary>
    public static IServiceCollection AddMultiTenancy(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, HttpContextTenantContext>();
        return services;
    }
}
