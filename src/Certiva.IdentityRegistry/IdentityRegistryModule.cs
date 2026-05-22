using Certiva.IdentityRegistry.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Certiva.IdentityRegistry;

/// <summary>
/// Entry point for the IdentityRegistry module.
/// Handles Professional registration, Issuer onboarding, authentication, JWT issuance, RBAC, and MFA.
/// </summary>
public static class IdentityRegistryModule
{
    public static IServiceCollection AddIdentityRegistry(this IServiceCollection services)
    {
        services.AddScoped<INationalIdMaskingService, NationalIdMaskingService>();
        services.AddScoped<IEncryptionService, AesEncryptionService>();
        services.AddScoped<IIdentityRegistryService, IdentityRegistryService>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
