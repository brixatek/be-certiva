using Certiva.IssuerPortal.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Certiva.IssuerPortal;

/// <summary>
/// Entry point for the IssuerPortal module.
/// Handles certification template management, bulk issuance job management,
/// analytics dashboard, and Professional search.
/// </summary>
public static class IssuerPortalModule
{
    public static IServiceCollection AddIssuerPortal(this IServiceCollection services)
    {
        services.AddScoped<IIssuerPortalService, IssuerPortalService>();
        return services;
    }
}
