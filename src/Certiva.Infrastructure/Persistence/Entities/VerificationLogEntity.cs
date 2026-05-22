namespace Certiva.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the VerificationLogs table.
/// Immutable record of a single certificate verification lookup.
/// </summary>
public class VerificationLogEntity
{
    public Guid LogId { get; set; }
    public Guid TenantId { get; set; }
    public Guid CertificateId { get; set; }

    /// <summary>IPv4 or IPv6 address of the requesting client. Maximum 45 characters.</summary>
    public string RequestingIp { get; set; } = string.Empty;

    /// <summary>The certificate Status value returned in the verification response. Maximum 20 characters.</summary>
    public string StatusReturned { get; set; } = string.Empty;

    public DateTimeOffset Timestamp { get; set; }
}
