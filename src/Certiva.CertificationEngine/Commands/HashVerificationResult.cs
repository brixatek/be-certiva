namespace Certiva.CertificationEngine.Commands;

/// <summary>
/// Result returned by <c>VerifyCertificateHashAsync</c>.
/// </summary>
/// <param name="CertificateId">The certificate that was verified.</param>
/// <param name="IsValid">True when the recomputed hash matches the stored hash; false indicates tampering.</param>
/// <param name="StoredHash">The SHA-256 hash currently stored on the certificate record.</param>
/// <param name="RecomputedHash">The SHA-256 hash recomputed from the certificate's fields and its preceding hash.</param>
public record HashVerificationResult(
    Guid CertificateId,
    bool IsValid,
    string StoredHash,
    string RecomputedHash);
