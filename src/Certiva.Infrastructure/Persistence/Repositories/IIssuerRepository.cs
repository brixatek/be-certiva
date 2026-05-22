using Certiva.Infrastructure.Persistence.Entities;

namespace Certiva.Infrastructure.Persistence.Repositories;

public interface IIssuerRepository
{
    Task<IssuerEntity?> FindByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default);

    /// <summary>Case-insensitive name lookup within a tenant.</summary>
    Task<IssuerEntity?> FindByNameAsync(string name, Guid tenantId, CancellationToken ct = default);

    void Add(IssuerEntity entity);
}
