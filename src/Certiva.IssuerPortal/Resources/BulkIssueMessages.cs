using System.Resources;

namespace Certiva.IssuerPortal.Resources;

internal static class BulkIssueMessages
{
    private static readonly ResourceManager Rm = new(
        "Certiva.IssuerPortal.Resources.BulkIssueMessages",
        typeof(BulkIssueMessages).Assembly);

    internal static string InvalidListSize          => Rm.GetString(nameof(InvalidListSize))!;
    internal static string IssuerNotVerified        => Rm.GetString(nameof(IssuerNotVerified))!;
    internal static string TemplateNotOwnedByIssuer => Rm.GetString(nameof(TemplateNotOwnedByIssuer))!;
    internal static string JobAccessDenied          => Rm.GetString(nameof(JobAccessDenied))!;

    internal static string IssuerNotFound(Guid issuerId)
        => string.Format(Rm.GetString(nameof(IssuerNotFound))!, issuerId);

    internal static string TemplateNotFound(Guid templateId)
        => string.Format(Rm.GetString(nameof(TemplateNotFound))!, templateId);

    internal static string JobNotFound(Guid jobId)
        => string.Format(Rm.GetString(nameof(JobNotFound))!, jobId);
}
