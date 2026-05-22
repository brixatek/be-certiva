using System.Resources;

namespace Certiva.IdentityRegistry.Resources;

internal static class AuthMessages
{
    private static readonly ResourceManager Rm = new(
        "Certiva.IdentityRegistry.Resources.AuthMessages",
        typeof(AuthMessages).Assembly);

    internal static string RateLimitExceeded  => Rm.GetString(nameof(RateLimitExceeded))!;
    internal static string InvalidCredentials => Rm.GetString(nameof(InvalidCredentials))!;
    internal static string MfaRequired        => Rm.GetString(nameof(MfaRequired))!;
    internal static string TokenInvalid       => Rm.GetString(nameof(TokenInvalid))!;
    internal static string TokenRevoked       => Rm.GetString(nameof(TokenRevoked))!;
    internal static string TokenExpired       => Rm.GetString(nameof(TokenExpired))!;
    internal static string TokenNotFound      => Rm.GetString(nameof(TokenNotFound))!;
}
