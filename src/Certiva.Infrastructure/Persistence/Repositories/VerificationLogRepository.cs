using Certiva.Infrastructure.Persistence.Entities;

namespace Certiva.Infrastructure.Persistence.Repositories;

public sealed class VerificationLogRepository : IVerificationLogRepository
{
    private readonly CertivaDbContext _db;

    public VerificationLogRepository(CertivaDbContext db) => _db = db;

    public void Add(VerificationLogEntity entity) => _db.VerificationLogs.Add(entity);
}
