using Certiva.CertificationEngine.Services;
using Certiva.CertificationEngine.Workers;
using Microsoft.Extensions.DependencyInjection;

namespace Certiva.CertificationEngine;

/// <summary>
/// Entry point for the CertificationEngine module.
/// Handles certificate issuance, lifecycle management, QR code generation, PDF generation,
/// certificate hash chain, and idempotency.
/// </summary>
public static class CertificationEngineModule
{
    public static IServiceCollection AddCertificationEngine(this IServiceCollection services)
    {
        services.AddScoped<ICanonicalSerializationService, CanonicalSerializationService>();
        services.AddScoped<ICertificateHashService, CertificateHashService>();
        services.AddScoped<ICertificationEngineService, CertificationEngineService>();

        // Background service: transitions Active certificates to Expired on schedule (Req 6.8).
        services.AddHostedService<CertificateExpiryScheduler>();

        return services;
    }
}
