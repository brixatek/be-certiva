using Certiva.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Certiva.Infrastructure.Persistence.Repositories;

public sealed class ProfessionalRepository : IProfessionalRepository
{
    private readonly CertivaDbContext _db;

    public ProfessionalRepository(CertivaDbContext db) => _db = db;

    public Task<ProfessionalEntity?> FindByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default)
        => _db.Professionals
            .FirstOrDefaultAsync(p => p.ProfessionalId == id && p.TenantId == tenantId, ct);

    public Task<ProfessionalEntity?> FindByNationalIdHashAsync(string hash, Guid tenantId, CancellationToken ct = default)
        => _db.Professionals
            .FirstOrDefaultAsync(p => p.NationalId_Hash == hash && p.TenantId == tenantId, ct);

    public Task<List<ProfessionalEntity>> GetAllWithEmailByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => _db.Professionals
            .Where(p => p.TenantId == tenantId && p.Email_Encrypted != null)
            .ToListAsync(ct);

    public void Add(ProfessionalEntity entity) => _db.Professionals.Add(entity);
}
