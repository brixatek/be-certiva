namespace Certiva.CertificateWallet.Services;

public sealed record WalletCertificateItem
{
    public Guid CertificateId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string IssuerName { get; init; } = string.Empty;
    public DateOnly IssueDate { get; init; }
    public DateOnly? ExpiryDate { get; init; }
    public bool ExpiryWarning { get; init; }
    public string? QrCodeUrl { get; init; }
}

public sealed record WalletSummary
{
    public IReadOnlyList<WalletCertificateItem> Active { get; init; } = [];
    public IReadOnlyList<WalletCertificateItem> Expired { get; init; } = [];
    public IReadOnlyList<WalletCertificateItem> Revoked { get; init; } = [];
}

public sealed record WalletCertificateDetail
{
    public Guid CertificateId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string IssuerName { get; init; } = string.Empty;
    public DateOnly IssueDate { get; init; }
    public DateOnly? ExpiryDate { get; init; }
    public bool ExpiryWarning { get; init; }
    public string? QrCodeValue { get; init; }
    public string? QrCodeBase64 { get; init; }
    public string? PdfDownloadLink { get; init; }
    public string ShareableLink { get; init; } = string.Empty;
}

public sealed record PdfDownloadUrlResult
{
    /// <summary>True if PDF is ready; false if still being generated (HTTP 202).</summary>
    public bool PdfReady { get; init; }

    /// <summary>Signed URL valid for 15 minutes. Null when PdfReady = false.</summary>
    public string? SignedUrl { get; init; }

    /// <summary>Job state message when PdfReady = false.</summary>
    public string? JobState { get; init; }
}
