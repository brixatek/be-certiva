using System.Resources;

namespace Certiva.IssuerPortal.Resources;

internal static class TemplateMessages
{
    private static readonly ResourceManager Rm = new(
        "Certiva.IssuerPortal.Resources.TemplateMessages",
        typeof(TemplateMessages).Assembly);

    internal static string NameInvalid            => Rm.GetString(nameof(NameInvalid))!;
    internal static string ValidityInvalid        => Rm.GetString(nameof(ValidityInvalid))!;
    internal static string IssuerNotVerified      => Rm.GetString(nameof(IssuerNotVerified))!;
    internal static string UpdateNotAuthorized    => Rm.GetString(nameof(UpdateNotAuthorized))!;
    internal static string DeactivateNotAuthorized => Rm.GetString(nameof(DeactivateNotAuthorized))!;

    internal static string IssuerNotFound(Guid issuerId)
        => string.Format(Rm.GetString(nameof(IssuerNotFound))!, issuerId);

    internal static string NotFound(Guid templateId)
        => string.Format(Rm.GetString(nameof(NotFound))!, templateId);

    internal static string DuplicateName(string name)
        => string.Format(Rm.GetString(nameof(DuplicateName))!, name);
}
