using System.Text.Json.Serialization;

namespace Certiva.Infrastructure.Redis;

/// <summary>
/// Read-model stored in Redis for O(1) certificate verification lookups.
/// Key: cert:verify:{tenantId}:{certificateId}  TTL: 1 hour
/// </summary>
public sealed record CertificateVerificationView
{
    [JsonPropertyName("certificateId")]
    public Guid CertificateId { get; init; }

    [JsonPropertyName("tenantId")]
    public Guid TenantId { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("expiryDate")]
    public DateOnly? ExpiryDate { get; init; }

    [JsonPropertyName("professionalName")]
    public string ProfessionalName { get; init; } = string.Empty;

    [JsonPropertyName("issuerName")]
    public string IssuerName { get; init; } = string.Empty;

    [JsonPropertyName("qrCodeUrl")]
    public string? QrCodeUrl { get; init; }
}
