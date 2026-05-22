using Certiva.Infrastructure.Persistence.Entities;

namespace Certiva.Infrastructure.Persistence.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshTokenEntity?> FindByTokenAsync(string token, Guid tenantId, CancellationToken ct = default);

    void Add(RefreshTokenEntity entity);
}
