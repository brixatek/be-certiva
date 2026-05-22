using Certiva.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Certiva.Infrastructure.Persistence.Repositories;

public sealed class CertificateTemplateRepository : ICertificateTemplateRepository
{
    private readonly CertivaDbContext _db;

    public CertificateTemplateRepository(CertivaDbContext db) => _db = db;

    public Task<CertificateTemplateEntity?> FindByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default)
        => _db.CertificateTemplates
            .FirstOrDefaultAsync(t => t.TemplateId == id && t.TenantId == tenantId, ct);

    public Task<bool> ExistsWithNameAsync(
        Guid issuerId, Guid tenantId, string name,
        Guid? excludeTemplateId = null, CancellationToken ct = default)
        => _db.CertificateTemplates
            .AnyAsync(t =>
                t.IssuerId == issuerId &&
                t.TenantId == tenantId &&
                t.Name.ToLower() == name.ToLower() &&
                (excludeTemplateId == null || t.TemplateId != excludeTemplateId.Value),
                ct);

    public void Add(CertificateTemplateEntity entity) => _db.CertificateTemplates.Add(entity);
}
