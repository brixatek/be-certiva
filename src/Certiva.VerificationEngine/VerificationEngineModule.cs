using Certiva.VerificationEngine.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Certiva.VerificationEngine;

/// <summary>
/// Entry point for the VerificationEngine module.
/// Handles public certificate verification, Verification_Store management,
/// consistency with PostgreSQL, VerificationLog recording, and rate limiting.
/// </summary>
public static class VerificationEngineModule
{
    public static IServiceCollection AddVerificationEngine(this IServiceCollection services)
    {
        services.AddScoped<IVerificationEngineService, VerificationEngineService>();
        return services;
    }
}
