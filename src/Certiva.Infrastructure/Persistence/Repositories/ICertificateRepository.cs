using Certiva.Infrastructure.Persistence.Entities;

namespace Certiva.Infrastructure.Persistence.Repositories;

/// <summary>Projected row returned by the monthly-issuance analytics query.</summary>
public sealed record CertificateMonthlyCount(int Year, int Month, int Count);

public interface ICertificateRepository
{
    Task<CertificateEntity?> FindByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default);

    /// <summary>Loads a certificate with Professional and Issuer navigations included.</summary>
    Task<CertificateEntity?> FindByIdWithNavigationsAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns the currently-Active certificate for the given professional + template, or null.</summary>
    Task<CertificateEntity?> FindActiveAsync(
        Guid professionalId, Guid templateId, Guid tenantId, CancellationToken ct = default);

    /// <summary>Returns the most-recently created certificate for the professional (for hash-chain head).</summary>
    Task<CertificateEntity?> FindLastByProfessionalAsync(
        Guid professionalId, Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Returns the certificate created immediately before <paramref name="before"/> for the professional
    /// (for hash-chain predecessor lookup).
    /// </summary>
    Task<CertificateEntity?> FindPrecedingAsync(
        Guid professionalId, Guid tenantId, DateTimeOffset before, CancellationToken ct = default);

    /// <summary>Returns the TenantId for a certificate by its ID (public verification path).</summary>
    Task<Guid?> FindTenantIdAsync(Guid certificateId, CancellationToken ct = default);

    /// <summary>Returns all certificates for a professional's wallet, ordered newest-first, with Issuer included.</summary>
    Task<List<CertificateEntity>> GetWalletAsync(
        Guid professionalId, Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Returns a page of certificates with Professional and Issuer included, ordered by CertificateId,
    /// starting after <paramref name="afterId"/>. Used by the Redis resync batch loop.
    /// </summary>
    Task<List<CertificateEntity>> GetBatchAsync(Guid afterId, int batchSize, CancellationToken ct = default);

    /// <summary>
    /// Searches certificates by the associated professional's name (case-insensitive contains),
    /// scoped to the given issuer, with Professional included. Limited to 100 results.
    /// </summary>
    Task<List<CertificateEntity>> SearchByProfessionalNameAsync(
        string nameQuery, Guid issuerId, Guid tenantId, CancellationToken ct = default);

    // ── Analytics ────────────────────────────────────────────────────────────

    Task<int> CountByStatusAsync(Guid issuerId, Guid tenantId, string status, CancellationToken ct = default);

    Task<List<CertificateMonthlyCount>> GetMonthlyIssuanceAsync(
        Guid issuerId, Guid tenantId, DateOnly from, CancellationToken ct = default);

    void Add(CertificateEntity entity);
}
