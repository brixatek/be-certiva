using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Certiva.Infrastructure.Authorization;

/// <summary>
/// Extension methods for registering JWT Bearer authentication for the Certiva platform.
/// </summary>
public static class JwtAuthenticationServiceExtensions
{
    /// <summary>
    /// Registers JWT Bearer authentication using settings from <c>Jwt:Issuer</c>,
    /// <c>Jwt:Audience</c>, and <c>Jwt:SigningKey</c> in application configuration.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="config">The application configuration containing JWT settings.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <c>Jwt:SigningKey</c> is not present in configuration.
    /// </exception>
    public static IServiceCollection AddCertivaJwtAuthentication(
        this IServiceCollection services,
        IConfiguration config)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    // Requirement 15.9 — validate issuer, audience, lifetime, and signing key
                    ValidateIssuer = true,
                    ValidIssuer = config["Jwt:Issuer"],

                    ValidateAudience = true,
                    ValidAudience = config["Jwt:Audience"],

                    ValidateLifetime = true,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            config["Jwt:SigningKey"]
                            ?? throw new InvalidOperationException("Jwt:SigningKey not configured"))),

                    // No clock skew tolerance — tokens must be valid at the exact moment of use
                    ClockSkew = TimeSpan.Zero
                };
            });

        return services;
    }
}
