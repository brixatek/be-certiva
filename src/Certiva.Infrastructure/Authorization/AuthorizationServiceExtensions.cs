using Certiva.Infrastructure.Constants;
using Microsoft.Extensions.DependencyInjection;

namespace Certiva.Infrastructure.Authorization;

/// <summary>
/// Extension methods for registering Certiva RBAC authorization policies.
/// </summary>
public static class AuthorizationServiceExtensions
{
    /// <summary>
    /// Registers the three RBAC authorization policies (Admin, Issuer, Worker)
    /// based on JWT role claims.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddCertivaAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            // Requirement 15.6 — Admin role
            options.AddPolicy(AuthorizationPolicies.Admin, p =>
                p.RequireRole(Roles.Admin));

            // Requirement 15.7 — Issuer role
            options.AddPolicy(AuthorizationPolicies.Issuer, p =>
                p.RequireRole(Roles.Issuer));

            // Requirement 15.8 — Worker role
            options.AddPolicy(AuthorizationPolicies.Worker, p =>
                p.RequireRole(Roles.Worker));
        });

        return services;
    }
}
