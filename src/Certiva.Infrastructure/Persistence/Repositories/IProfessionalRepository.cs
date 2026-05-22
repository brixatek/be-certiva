using Certiva.Infrastructure.Persistence.Entities;

namespace Certiva.Infrastructure.Persistence.Repositories;

public interface IProfessionalRepository
{
    Task<ProfessionalEntity?> FindByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default);
    Task<ProfessionalEntity?> FindByNationalIdHashAsync(string hash, Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Returns all professionals in a tenant that have an encrypted email set.
    /// Used for email-based login lookup (O(n) decryption scan).
    /// </summary>
    Task<List<ProfessionalEntity>> GetAllWithEmailByTenantAsync(Guid tenantId, CancellationToken ct = default);

    void Add(ProfessionalEntity entity);
}
