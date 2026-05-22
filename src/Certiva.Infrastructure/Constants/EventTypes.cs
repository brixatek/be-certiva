namespace Certiva.Infrastructure.Constants;

public static class EventTypes
{
    public const string ProfessionalRegistered = "ProfessionalRegistered";
    public const string IssuerApproved         = "IssuerApproved";
    public const string IssuerRejected         = "IssuerRejected";
    public const string CertificateIssued      = "CertificateIssued";
    public const string CertificateRevoked     = "CertificateRevoked";
    public const string CertificateExpired     = "CertificateExpired";
    public const string BulkIssueJobEnqueued   = "BulkIssueJobEnqueued";
    public const string QrCodeGenerated        = "QrCodeGenerated";
    public const string PdfGenerated           = "PdfGenerated";
}
