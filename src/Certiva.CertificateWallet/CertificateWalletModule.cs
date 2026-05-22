using Certiva.CertificateWallet.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Certiva.CertificateWallet;

/// <summary>
/// Entry point for the CertificateWallet module.
/// Handles worker-facing certificate list, detail view, PDF download via signed URL,
/// shareable link generation, and expiry warning indicators.
/// </summary>
public static class CertificateWalletModule
{
    public static IServiceCollection AddCertificateWallet(this IServiceCollection services)
    {
        services.AddScoped<ICertificateWalletService, CertificateWalletService>();
        return services;
    }
}
