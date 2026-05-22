using Certiva.Infrastructure.Persistence.Entities;

namespace Certiva.Infrastructure.Persistence.Repositories;

public interface IBulkIssueJobRepository
{
    Task<BulkIssueJobEntity?> FindByIdAsync(Guid jobId, Guid tenantId, CancellationToken ct = default);

    void Add(BulkIssueJobEntity entity);
}
