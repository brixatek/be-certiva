using Certiva.Infrastructure.Domain;

namespace Certiva.VerificationEngine.Services;

/// <summary>
/// Public certificate verification with Redis/PostgreSQL fallback,
/// VerificationLog recording, and rate limiting.
/// </summary>
public interface IVerificationEngineService
{
    /// <summary>
    /// Verifies a certificate by CertificateId.
    /// Attempts Redis first; falls back to PostgreSQL on miss or Redis unavailability.
    /// Records a VerificationLog entry on every call.
    /// Returns HTTP 404 for non-existent certificates; valid=false for Revoked/Expired.
    /// </summary>
    Task<Result<VerificationResult>> VerifyCertificateAsync(
        Guid certificateId,
        string requestingIp,
        CancellationToken ct);

    /// <summary>
    /// Resynchronises the Redis Verification_Store from PostgreSQL.
    /// Called after Redis restoration. Runs within a 5-minute deadline.
    /// </summary>
    Task ResynchronizeVerificationStoreAsync(CancellationToken ct);
}
