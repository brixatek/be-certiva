using Certiva.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Certiva.Infrastructure.Persistence.Repositories;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly CertivaDbContext _db;

    public RefreshTokenRepository(CertivaDbContext db) => _db = db;

    public Task<RefreshTokenEntity?> FindByTokenAsync(string token, Guid tenantId, CancellationToken ct = default)
        => _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == token && t.TenantId == tenantId, ct);

    public void Add(RefreshTokenEntity entity) => _db.RefreshTokens.Add(entity);
}
