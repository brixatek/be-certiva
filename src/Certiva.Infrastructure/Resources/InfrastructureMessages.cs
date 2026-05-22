using System.Resources;

namespace Certiva.Infrastructure.Resources;

internal static class InfrastructureMessages
{
    private static readonly ResourceManager Rm = new(
        "Certiva.Infrastructure.Resources.InfrastructureMessages",
        typeof(InfrastructureMessages).Assembly);

    internal static string AuditLogImmutable(int illegalChangeCount)
        => string.Format(Rm.GetString(nameof(AuditLogImmutable))!, illegalChangeCount);
}
