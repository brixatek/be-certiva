using Certiva.CertificationEngine.Models;

namespace Certiva.CertificationEngine.Services;

/// <summary>
/// Computes and verifies the tamper-evident SHA-256 hash chain for certificates.
/// </summary>
public interface ICertificateHashService
{
    /// <summary>
    /// Computes SHA-256(Canonical(fields) + previousHash) and returns the result as a lowercase hex string.
    /// </summary>
    string ComputeHash(CertificateFields fields, string previousHash);

    /// <summary>
    /// Returns the genesis hash value: 64 hexadecimal zeros used as the previous hash for the first certificate.
    /// </summary>
    string GetGenesisHash();

    /// <summary>
    /// Verifies the integrity of a certificate chain by recomputing each hash and comparing it to the stored value.
    /// Returns <c>true</c> if every hash in the chain is valid; <c>false</c> if any mismatch is detected.
    /// </summary>
    bool VerifyChain(IReadOnlyList<(CertificateFields Fields, string StoredHash)> chain);
}
