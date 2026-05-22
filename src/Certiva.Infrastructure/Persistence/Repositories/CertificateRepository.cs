using Certiva.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Certiva.Infrastructure.Persistence.Repositories;

public sealed class CertificateRepository : ICertificateRepository
{
    private readonly CertivaDbContext _db;

    public CertificateRepository(CertivaDbContext db) => _db = db;

    public Task<CertificateEntity?> FindByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default)
        => _db.Certificates
            .FirstOrDefaultAsync(c => c.CertificateId == id && c.TenantId == tenantId, ct);

    public Task<CertificateEntity?> FindByIdWithNavigationsAsync(Guid id, CancellationToken ct = default)
        => _db.Certificates
            .Include(c => c.Professional)
            .Include(c => c.Issuer)
            .FirstOrDefaultAsync(c => c.CertificateId == id, ct);

    public Task<CertificateEntity?> FindActiveAsync(
        Guid professionalId, Guid templateId, Guid tenantId, CancellationToken ct = default)
        => _db.Certificates
            .FirstOrDefaultAsync(c =>
                c.ProfessionalId == professionalId &&
                c.TemplateId == templateId &&
                c.TenantId == tenantId &&
                c.Status == Constants.CertificateStatus.Active,
                ct);

    public Task<CertificateEntity?> FindLastByProfessionalAsync(
        Guid professionalId, Guid tenantId, CancellationToken ct = default)
        => _db.Certificates
            .Where(c => c.ProfessionalId == professionalId && c.TenantId == tenantId)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public Task<CertificateEntity?> FindPrecedingAsync(
        Guid professionalId, Guid tenantId, DateTimeOffset before, CancellationToken ct = default)
        => _db.Certificates
            .Where(c =>
                c.ProfessionalId == professionalId &&
                c.TenantId == tenantId &&
                c.CreatedAt < before)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public Task<Guid?> FindTenantIdAsync(Guid certificateId, CancellationToken ct = default)
        => _db.Certificates
            .Where(c => c.CertificateId == certificateId)
            .Select(c => (Guid?)c.TenantId)
            .FirstOrDefaultAsync(ct);

    public Task<List<CertificateEntity>> GetWalletAsync(
        Guid professionalId, Guid tenantId, CancellationToken ct = default)
        => _db.Certificates
            .Include(c => c.Issuer)
            .Where(c => c.ProfessionalId == professionalId && c.TenantId == tenantId)
            .OrderByDescending(c => c.IssueDate)
            .ToListAsync(ct);

    public Task<List<CertificateEntity>> GetBatchAsync(Guid afterId, int batchSize, CancellationToken ct = default)
        => _db.Certificates
            .Include(c => c.Professional)
            .Include(c => c.Issuer)
            .Where(c => c.CertificateId > afterId)
            .OrderBy(c => c.CertificateId)
            .Take(batchSize)
            .ToListAsync(ct);

    public Task<List<CertificateEntity>> SearchByProfessionalNameAsync(
        string nameQuery, Guid issuerId, Guid tenantId, CancellationToken ct = default)
        => _db.Certificates
            .Include(c => c.Professional)
            .Where(c =>
                c.IssuerId == issuerId &&
                c.TenantId == tenantId &&
                c.Professional.Name.ToLower().Contains(nameQuery.ToLower()))
            .OrderBy(c => c.Professional.Name)
            .Take(100)
            .ToListAsync(ct);

    public Task<int> CountByStatusAsync(Guid issuerId, Guid tenantId, string status, CancellationToken ct = default)
        => _db.Certificates
            .CountAsync(c => c.IssuerId == issuerId && c.TenantId == tenantId && c.Status == status, ct);

    public async Task<List<CertificateMonthlyCount>> GetMonthlyIssuanceAsync(
        Guid issuerId, Guid tenantId, DateOnly from, CancellationToken ct = default)
    {
        var rows = await _db.Certificates
            .Where(c => c.IssuerId == issuerId && c.TenantId == tenantId && c.IssueDate >= from)
            .GroupBy(c => new { c.IssueDate.Year, c.IssueDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .OrderBy(m => m.Year).ThenBy(m => m.Month)
            .ToListAsync(ct);

        return rows.Select(r => new CertificateMonthlyCount(r.Year, r.Month, r.Count)).ToList();
    }

    public void Add(CertificateEntity entity) => _db.Certificates.Add(entity);
}
