using Certiva.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Certiva.Infrastructure.Persistence.Repositories;

public sealed class NotificationLogRepository : INotificationLogRepository
{
    private readonly CertivaDbContext _db;

    public NotificationLogRepository(CertivaDbContext db) => _db = db;

    public Task<bool> ExistsByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
        => _db.NotificationLogs.AnyAsync(n => n.IdempotencyKey == idempotencyKey, ct);

    public void Add(NotificationLogEntity entity) => _db.NotificationLogs.Add(entity);
}
