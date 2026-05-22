using System.Resources;

namespace Certiva.IdentityRegistry.Resources;

internal static class IssuerMessages
{
    private static readonly ResourceManager Rm = new(
        "Certiva.IdentityRegistry.Resources.IssuerMessages",
        typeof(IssuerMessages).Assembly);

    internal static string AlreadyVerified => Rm.GetString(nameof(AlreadyVerified))!;
    internal static string AlreadyRejected => Rm.GetString(nameof(AlreadyRejected))!;
    internal static string NotVerified     => Rm.GetString(nameof(NotVerified))!;

    internal static string NotFound(Guid issuerId)
        => string.Format(Rm.GetString(nameof(NotFound))!, issuerId);

    internal static string AlreadyExists(Guid issuerId)
        => string.Format(Rm.GetString(nameof(AlreadyExists))!, issuerId);
}
