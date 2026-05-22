using Certiva.Infrastructure.Persistence.Entities;

namespace Certiva.Infrastructure.Persistence.Repositories;

public interface ICertificateTemplateRepository
{
    Task<CertificateTemplateEntity?> FindByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Returns true if a template with the given name already exists for the issuer (case-insensitive).
    /// Pass <paramref name="excludeTemplateId"/> to exclude a specific template when checking uniqueness on update.
    /// </summary>
    Task<bool> ExistsWithNameAsync(
        Guid issuerId, Guid tenantId, string name,
        Guid? excludeTemplateId = null, CancellationToken ct = default);

    void Add(CertificateTemplateEntity entity);
}
