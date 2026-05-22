using Certiva.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Certiva.Infrastructure.Persistence.Repositories;

public sealed class BulkIssueJobRepository : IBulkIssueJobRepository
{
    private readonly CertivaDbContext _db;

    public BulkIssueJobRepository(CertivaDbContext db) => _db = db;

    public Task<BulkIssueJobEntity?> FindByIdAsync(Guid jobId, Guid tenantId, CancellationToken ct = default)
        => _db.BulkIssueJobs
            .FirstOrDefaultAsync(j => j.JobId == jobId && j.TenantId == tenantId, ct);

    public void Add(BulkIssueJobEntity entity) => _db.BulkIssueJobs.Add(entity);
}
