using Certiva.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Certiva.Infrastructure.Persistence.Repositories;

public sealed class IssuerRepository : IIssuerRepository
{
    private readonly CertivaDbContext _db;

    public IssuerRepository(CertivaDbContext db) => _db = db;

    public Task<IssuerEntity?> FindByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default)
        => _db.Issuers
            .FirstOrDefaultAsync(i => i.IssuerId == id && i.TenantId == tenantId, ct);

    public Task<IssuerEntity?> FindByNameAsync(string name, Guid tenantId, CancellationToken ct = default)
        => _db.Issuers
            .FirstOrDefaultAsync(
                i => i.OrganizationName.ToLower() == name.ToLower() && i.TenantId == tenantId, ct);

    public void Add(IssuerEntity entity) => _db.Issuers.Add(entity);
}
