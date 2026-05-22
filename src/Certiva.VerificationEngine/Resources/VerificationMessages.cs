using System.Resources;

namespace Certiva.VerificationEngine.Resources;

internal static class VerificationMessages
{
    private static readonly ResourceManager Rm = new(
        "Certiva.VerificationEngine.Resources.VerificationMessages",
        typeof(VerificationMessages).Assembly);

    internal static string RateLimitExceeded => Rm.GetString(nameof(RateLimitExceeded))!;

    internal static string CertificateNotFound(Guid certificateId)
        => string.Format(Rm.GetString(nameof(CertificateNotFound))!, certificateId);
}
