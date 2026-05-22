using System.Text;
using System.Text.Json;
using Certiva.CertificationEngine.Models;

namespace Certiva.CertificationEngine.Services;

/// <summary>
/// Produces a deterministic, canonical JSON representation of <see cref="CertificateFields"/>.
/// Keys are emitted in strict alphabetical order with no whitespace, ensuring the same input
/// always produces the same UTF-8 byte sequence regardless of runtime locale or call order.
/// </summary>
public sealed class CanonicalSerializationService : ICanonicalSerializationService
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = false,
        SkipValidation = false
    };

    /// <inheritdoc />
    public string Serialize(CertificateFields fields)
    {
        using var stream = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();

            // Keys emitted in strict alphabetical order:
            // certificateId, expiryDate, issuerId, issuerName, issueDate, name, professionalId, tenantId

            writer.WriteString("certificateId", fields.CertificateId.ToString("D").ToLowerInvariant());

            if (fields.ExpiryDate.HasValue)
                writer.WriteString("expiryDate", fields.ExpiryDate.Value.ToString("yyyy-MM-dd"));
            else
                writer.WriteNull("expiryDate");

            writer.WriteString("issuerId", fields.IssuerId.ToString("D").ToLowerInvariant());
            writer.WriteString("issuerName", fields.IssuerName);
            writer.WriteString("issueDate", fields.IssueDate.ToString("yyyy-MM-dd"));
            writer.WriteString("name", fields.Name);
            writer.WriteString("professionalId", fields.ProfessionalId.ToString("D").ToLowerInvariant());
            writer.WriteString("tenantId", fields.TenantId.ToString("D").ToLowerInvariant());

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
