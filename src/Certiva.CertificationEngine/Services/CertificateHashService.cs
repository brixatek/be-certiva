using System.Security.Cryptography;
using System.Text;
using Certiva.CertificationEngine.Models;

namespace Certiva.CertificationEngine.Services;

/// <summary>
/// Computes and verifies the tamper-evident SHA-256 hash chain for certificates.
/// </summary>
public sealed class CertificateHashService : ICertificateHashService
{
    private readonly ICanonicalSerializationService _serialization;

    public CertificateHashService(ICanonicalSerializationService serialization)
    {
        _serialization = serialization;
    }

    /// <inheritdoc />
    public string GetGenesisHash() => new string('0', 64);

    /// <inheritdoc />
    public string ComputeHash(CertificateFields fields, string previousHash)
    {
        var canonical = _serialization.Serialize(fields);
        var input = canonical + previousHash;
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(inputBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <inheritdoc />
    public bool VerifyChain(IReadOnlyList<(CertificateFields Fields, string StoredHash)> chain)
    {
        if (chain.Count == 0)
            return true;

        var previousHash = GetGenesisHash();

        for (var i = 0; i < chain.Count; i++)
        {
            var (fields, storedHash) = chain[i];
            var recomputed = ComputeHash(fields, previousHash);

            if (!string.Equals(recomputed, storedHash, StringComparison.OrdinalIgnoreCase))
                return false;

            previousHash = storedHash;
        }

        return true;
    }
}
