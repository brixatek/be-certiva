using Certiva.CertificationEngine.Commands;
using Certiva.Infrastructure.Domain;

namespace Certiva.CertificationEngine.Services;

/// <summary>
/// Core service for certificate issuance and lifecycle management.
/// </summary>
public interface ICertificationEngineService
{
    /// <summary>
    /// Issues a certificate to a Professional using the specified template.
    /// Validates all entities, enforces business rules, computes the hash chain,
    /// and writes the certificate + outbox message in a single transaction.
    /// </summary>
    Task<Result<IssueCertificateResult>> IssueCertificateAsync(IssueCertificateCommand cmd, CancellationToken ct);

    /// <summary>
    /// Revokes a certificate that was previously issued by the specified Issuer.
    /// Validates ownership, current status, and revocation reason, then updates
    /// the certificate, writes an AuditLog entry, and publishes a CertificateRevoked
    /// event via the transactional outbox — all in a single transaction.
    /// </summary>
    Task<Result<Guid>> RevokeCertificateAsync(RevokeCertificateCommand cmd, CancellationToken ct);

    /// <summary>
    /// Recomputes the certificate's hash from its stored fields and the preceding certificate's hash,
    /// then compares it against the stored <c>CertificateHash</c>.
    /// If a mismatch is detected the certificate Status is set to Tampered and an AuditLog entry is written.
    /// Satisfies Requirements 19.3, 19.4, 19.5.
    /// </summary>
    Task<Result<HashVerificationResult>> VerifyCertificateHashAsync(
        Guid certificateId,
        Guid tenantId,
        CancellationToken ct);
}
