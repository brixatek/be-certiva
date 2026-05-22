namespace Certiva.CertificationEngine.Commands;

/// <summary>
/// Result returned after a certificate issuance attempt.
/// </summary>
/// <param name="CertificateId">The ID of the issued (or previously issued) certificate.</param>
/// <param name="IsNew">True if a new certificate was created; false if returned from idempotency cache.</param>
public record IssueCertificateResult(Guid CertificateId, bool IsNew);
