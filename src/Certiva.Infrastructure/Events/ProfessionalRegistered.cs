namespace Certiva.Infrastructure.Events;

/// <summary>
/// Message contract published when a new Professional is successfully registered.
/// </summary>
public record ProfessionalRegistered
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public Guid ProfessionalId { get; init; }
    public Guid TenantId { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
