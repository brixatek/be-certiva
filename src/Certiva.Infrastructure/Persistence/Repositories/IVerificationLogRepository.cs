using Certiva.Infrastructure.Persistence.Entities;

namespace Certiva.Infrastructure.Persistence.Repositories;

public interface IVerificationLogRepository
{
    void Add(VerificationLogEntity entity);
}
