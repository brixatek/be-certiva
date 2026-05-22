using Certiva.CertificationEngine.Models;

namespace Certiva.CertificationEngine.Services;

/// <summary>
/// Produces a deterministic, canonical JSON representation of certificate fields
/// for use in hash chain computation.
/// </summary>
public interface ICanonicalSerializationService
{
    /// <summary>
    /// Serializes <paramref name="fields"/> as a compact, alphabetically-keyed JSON string (UTF-8).
    /// The same input always produces the same byte sequence regardless of call order or locale.
    /// </summary>
    string Serialize(CertificateFields fields);
}
