namespace Certiva.Infrastructure.Constants;

public static class AuditActions
{
    public const string IssuerApproved      = "IssuerApproved";
    public const string IssuerRejected      = "IssuerRejected";
    public const string CertificateRevoked  = "CertificateRevoked";
    public const string TamperDetected      = "TamperDetected";
    public const string AuthFailed          = "AuthFailed";
    public const string BulkJobFailed       = "BulkJobFailed";
}
