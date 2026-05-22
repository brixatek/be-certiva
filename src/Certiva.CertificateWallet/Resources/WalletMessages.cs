using System.Resources;

namespace Certiva.CertificateWallet.Resources;

internal static class WalletMessages
{
    private static readonly ResourceManager Rm = new(
        "Certiva.CertificateWallet.Resources.WalletMessages",
        typeof(WalletMessages).Assembly);

    internal static string AccessDenied    => Rm.GetString(nameof(AccessDenied))!;
    internal static string PdfAccessDenied => Rm.GetString(nameof(PdfAccessDenied))!;
    internal static string ShareAccessDenied => Rm.GetString(nameof(ShareAccessDenied))!;
    internal static string PdfNotReady     => Rm.GetString(nameof(PdfNotReady))!;

    internal static string CertificateNotFound(Guid certificateId)
        => string.Format(Rm.GetString(nameof(CertificateNotFound))!, certificateId);
}
