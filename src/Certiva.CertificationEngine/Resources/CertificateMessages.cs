using System.Resources;

namespace Certiva.CertificationEngine.Resources;

internal static class CertificateMessages
{
    private static readonly ResourceManager Rm = new(
        "Certiva.CertificationEngine.Resources.CertificateMessages",
        typeof(CertificateMessages).Assembly);

    internal static string TemplateNotOwnedByIssuer  => Rm.GetString(nameof(TemplateNotOwnedByIssuer))!;
    internal static string IssuerNotVerified          => Rm.GetString(nameof(IssuerNotVerified))!;
    internal static string CrossTenantMismatch        => Rm.GetString(nameof(CrossTenantMismatch))!;
    internal static string RevocationNotAuthorized    => Rm.GetString(nameof(RevocationNotAuthorized))!;
    internal static string RevocationReasonRequired   => Rm.GetString(nameof(RevocationReasonRequired))!;

    internal static string ProfessionalNotFound(Guid professionalId, Guid tenantId)
        => string.Format(Rm.GetString(nameof(ProfessionalNotFound))!, professionalId, tenantId);

    internal static string TemplateNotFound(Guid templateId, Guid tenantId)
        => string.Format(Rm.GetString(nameof(TemplateNotFound))!, templateId, tenantId);

    internal static string IssuerNotFound(Guid issuerId, Guid tenantId)
        => string.Format(Rm.GetString(nameof(IssuerNotFound))!, issuerId, tenantId);

    internal static string NotFound(Guid certificateId, Guid tenantId)
        => string.Format(Rm.GetString(nameof(NotFound))!, certificateId, tenantId);

    internal static string DuplicateActive(Guid certificateId)
        => string.Format(Rm.GetString(nameof(DuplicateActive))!, certificateId);

    internal static string AlreadyTerminated(Guid certificateId, string status)
        => string.Format(Rm.GetString(nameof(AlreadyTerminated))!, certificateId, status);
}
