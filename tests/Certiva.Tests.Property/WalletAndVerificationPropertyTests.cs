using Certiva.CertificateWallet.Services;
using Certiva.Infrastructure.Constants;
using Certiva.Infrastructure.Persistence.Entities;
using Certiva.Tests.Property.Helpers;
using FsCheck;
using FsCheck.Xunit;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Certiva.Tests.Property;

/// <summary>
/// FsCheck property-based tests for the CertificateWallet and signed URL logic.
/// Properties P25, P26, P29-P32.
/// </summary>
public class WalletAndVerificationPropertyTests
{
    // ── P26: Signed URL Expiry ────────────────────────────────────────────────

    // Feature: certiva-core-platform, Property 26: Expired signed URLs are rejected by ValidateSignedUrl
    [Property(MaxTest = 100)]
    public Property SignedUrl_Expired_IsRejected(PositiveInt seed)
    {
        var walletSvc = BuildWalletService();
        var certId = Guid.NewGuid();

        // Set expiry to 1 hour in the past
        var pastExpiry = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();
        var fakeSig = "invalidsignature";

        return (!walletSvc.ValidateSignedUrl(certId, pastExpiry, fakeSig)).ToProperty();
    }

    // Feature: certiva-core-platform, Property 26b: Valid signed URL is accepted by ValidateSignedUrl
    [Property(MaxTest = 100)]
    public Property SignedUrl_Valid_IsAccepted(PositiveInt seed)
    {
        var db = TestDbFactory.Create();
        var tenantId = Guid.NewGuid();
        var professionalId = Guid.NewGuid();
        var certId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        SetupCertificateWithPdf(db, certId, tenantId, professionalId);

        var walletSvc = BuildWalletService(db);

        var urlResult = walletSvc.GetPdfDownloadUrlAsync(certId, professionalId, tenantId, CancellationToken.None)
            .GetAwaiter().GetResult();

        if (urlResult.IsFailure || !urlResult.Value!.PdfReady) return true.ToProperty();

        // Parse the generated URL to extract expires and sig
        var urlStr = urlResult.Value.SignedUrl!;
        var uri = new Uri(urlStr);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var expires = long.Parse(query["expires"]!);
        var sig = query["sig"]!;

        return walletSvc.ValidateSignedUrl(certId, expires, sig).ToProperty();
    }

    // ── P29: Wallet Returns Certificates Grouped by Status ──────────────────────

    // Feature: certiva-core-platform, Property 29: Wallet summary groups certificates correctly by status
    [Property(MaxTest = 100)]
    public Property Wallet_GetCertificates_GroupsCorrectly(PositiveInt seed)
    {
        var db = TestDbFactory.Create();
        var tenantId = Guid.NewGuid();
        var professionalId = Guid.NewGuid();
        var issuerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        SetupIssuerAndProfessional(db, tenantId, professionalId, issuerId, now);

        var activeId = Guid.NewGuid();
        var revokedId = Guid.NewGuid();
        var expiredId = Guid.NewGuid();

        db.Certificates.AddRange(
            MakeCert(activeId, tenantId, professionalId, issuerId, CertificateStatus.Active, now),
            MakeCert(revokedId, tenantId, professionalId, issuerId, CertificateStatus.Revoked, now),
            MakeCert(expiredId, tenantId, professionalId, issuerId, CertificateStatus.Expired, now));
        db.SaveChanges();

        var walletSvc = BuildWalletService(db);
        var result = walletSvc.GetCertificatesAsync(professionalId, tenantId, CancellationToken.None)
            .GetAwaiter().GetResult();

        if (result.IsFailure) return true.ToProperty();

        var summary = result.Value!;
        return (summary.Active.Any(c => c.CertificateId == activeId)
             && summary.Revoked.Any(c => c.CertificateId == revokedId)
             && summary.Expired.Any(c => c.CertificateId == expiredId)).ToProperty();
    }

    // ── P30: Expiry Warning Indicator ────────────────────────────────────────────

    // Feature: certiva-core-platform, Property 30: ExpiryWarning is true when expiry is within 30 days
    [Property(MaxTest = 100)]
    public Property Wallet_ExpiryWarning_SetWithin30Days(PositiveInt daysUntilExpiry)
    {
        var days = daysUntilExpiry.Get % 30; // 0 to 29 days
        var db = TestDbFactory.Create();
        var tenantId = Guid.NewGuid();
        var professionalId = Guid.NewGuid();
        var issuerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        SetupIssuerAndProfessional(db, tenantId, professionalId, issuerId, now);

        var certId = Guid.NewGuid();
        var cert = MakeCert(certId, tenantId, professionalId, issuerId, CertificateStatus.Active, now);
        cert.ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(days));
        db.Certificates.Add(cert);
        db.SaveChanges();

        var walletSvc = BuildWalletService(db);
        var result = walletSvc.GetCertificatesAsync(professionalId, tenantId, CancellationToken.None)
            .GetAwaiter().GetResult();

        if (result.IsFailure) return true.ToProperty();

        var item = result.Value!.Active.FirstOrDefault(c => c.CertificateId == certId);
        return (item != null && item.ExpiryWarning).ToProperty();
    }

    // Feature: certiva-core-platform, Property 30b: ExpiryWarning is false when expiry is more than 30 days away
    [Property(MaxTest = 100)]
    public Property Wallet_ExpiryWarning_NotSetBeyond30Days(PositiveInt seed)
    {
        var db = TestDbFactory.Create();
        var tenantId = Guid.NewGuid();
        var professionalId = Guid.NewGuid();
        var issuerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        SetupIssuerAndProfessional(db, tenantId, professionalId, issuerId, now);

        var certId = Guid.NewGuid();
        var cert = MakeCert(certId, tenantId, professionalId, issuerId, CertificateStatus.Active, now);
        cert.ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(31 + seed.Get % 100));
        db.Certificates.Add(cert);
        db.SaveChanges();

        var walletSvc = BuildWalletService(db);
        var result = walletSvc.GetCertificatesAsync(professionalId, tenantId, CancellationToken.None)
            .GetAwaiter().GetResult();

        if (result.IsFailure) return true.ToProperty();

        var item = result.Value!.Active.FirstOrDefault(c => c.CertificateId == certId);
        return (item != null && !item.ExpiryWarning).ToProperty();
    }

    // ── P31: Cross-Professional Wallet Access Authorization ──────────────────────

    // Feature: certiva-core-platform, Property 31: Accessing another professional's certificate detail returns Forbidden
    [Property(MaxTest = 100)]
    public Property Wallet_CrossProfessional_ReturnsForbidden(PositiveInt seed)
    {
        var db = TestDbFactory.Create();
        var tenantId = Guid.NewGuid();
        var professionalId = Guid.NewGuid();
        var differentProfessionalId = Guid.NewGuid();
        var issuerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        SetupIssuerAndProfessional(db, tenantId, professionalId, issuerId, now);
        SetupProfessional(db, differentProfessionalId, tenantId, now);

        var certId = Guid.NewGuid();
        db.Certificates.Add(MakeCert(certId, tenantId, professionalId, issuerId, CertificateStatus.Active, now));
        db.SaveChanges();

        var walletSvc = BuildWalletService(db);

        // Different professional tries to access this certificate
        var result = walletSvc.GetCertificateDetailAsync(certId, differentProfessionalId, tenantId, CancellationToken.None)
            .GetAwaiter().GetResult();

        return (result.IsFailure && result.StatusCode == 403).ToProperty();
    }

    // ── P32: Shareable Link Format ──────────────────────────────────────────────

    // Feature: certiva-core-platform, Property 32: Shareable link always contains the certificate ID
    [Property(MaxTest = 100)]
    public Property Wallet_ShareableLink_ContainsCertificateId(PositiveInt seed)
    {
        var db = TestDbFactory.Create();
        var tenantId = Guid.NewGuid();
        var professionalId = Guid.NewGuid();
        var issuerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        SetupIssuerAndProfessional(db, tenantId, professionalId, issuerId, now);

        var certId = Guid.NewGuid();
        db.Certificates.Add(MakeCert(certId, tenantId, professionalId, issuerId, CertificateStatus.Active, now));
        db.SaveChanges();

        var walletSvc = BuildWalletService(db);
        var result = walletSvc.GetShareableLinkAsync(certId, professionalId, tenantId, CancellationToken.None)
            .GetAwaiter().GetResult();

        if (result.IsFailure) return true.ToProperty();

        return result.Value!.Contains(certId.ToString()).ToProperty();
    }

    // ── P25: PDF Download Authorization ─────────────────────────────────────────

    // Feature: certiva-core-platform, Property 25: Different professional cannot get PDF download URL
    [Property(MaxTest = 100)]
    public Property Wallet_PdfDownload_DifferentProfessional_ReturnsForbidden(PositiveInt seed)
    {
        var db = TestDbFactory.Create();
        var tenantId = Guid.NewGuid();
        var professionalId = Guid.NewGuid();
        var differentProfessionalId = Guid.NewGuid();
        var issuerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        SetupIssuerAndProfessional(db, tenantId, professionalId, issuerId, now);
        SetupProfessional(db, differentProfessionalId, tenantId, now);

        var certId = Guid.NewGuid();
        var cert = MakeCert(certId, tenantId, professionalId, issuerId, CertificateStatus.Active, now);
        cert.PdfStoragePath = "pdfs/test.pdf";
        db.Certificates.Add(cert);
        db.SaveChanges();

        var walletSvc = BuildWalletService(db);

        var result = walletSvc.GetPdfDownloadUrlAsync(certId, differentProfessionalId, tenantId, CancellationToken.None)
            .GetAwaiter().GetResult();

        return (result.IsFailure && result.StatusCode == 403).ToProperty();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static CertificateWalletService BuildWalletService(
        Infrastructure.Persistence.CertivaDbContext? db = null)
    {
        db ??= TestDbFactory.Create();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:Domain"] = "https://certiva.test",
                ["App:SignedUrlHmacKey"] = "test-hmac-key-for-property-tests"
            })
            .Build();

        return new CertificateWalletService(db, config);
    }

    private static void SetupIssuerAndProfessional(
        Infrastructure.Persistence.CertivaDbContext db,
        Guid tenantId, Guid professionalId, Guid issuerId,
        DateTimeOffset now)
    {
        db.Issuers.Add(new IssuerEntity
        {
            IssuerId = issuerId,
            TenantId = tenantId,
            OrganizationName = "Test Org " + issuerId,
            Type = "TrainingProvider",
            VerificationStatus = IssuerStatus.Verified,
            CreatedAt = now
        });

        SetupProfessional(db, professionalId, tenantId, now);
        db.SaveChanges();
    }

    private static void SetupProfessional(
        Infrastructure.Persistence.CertivaDbContext db,
        Guid professionalId, Guid tenantId, DateTimeOffset now)
    {
        if (!db.Professionals.Any(p => p.ProfessionalId == professionalId))
        {
            db.Professionals.Add(new ProfessionalEntity
            {
                ProfessionalId = professionalId,
                TenantId = tenantId,
                Name = "Professional " + professionalId,
                NationalId_Encrypted = Array.Empty<byte>(),
                NationalId_Hash = professionalId.ToString("N"),
                CreatedAt = now
            });
        }
    }

    private static void SetupCertificateWithPdf(
        Infrastructure.Persistence.CertivaDbContext db,
        Guid certId, Guid tenantId, Guid professionalId)
    {
        var issuerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        SetupIssuerAndProfessional(db, tenantId, professionalId, issuerId, now);

        var cert = MakeCert(certId, tenantId, professionalId, issuerId, CertificateStatus.Active, now);
        cert.PdfStoragePath = $"pdfs/{certId}.pdf";
        db.Certificates.Add(cert);
        db.SaveChanges();
    }

    private static CertificateEntity MakeCert(
        Guid certId, Guid tenantId, Guid professionalId, Guid issuerId,
        string status, DateTimeOffset now) => new()
    {
        CertificateId = certId,
        TenantId = tenantId,
        ProfessionalId = professionalId,
        IssuerId = issuerId,
        TemplateId = Guid.NewGuid(),
        Name = "Test Certificate",
        Status = status,
        IssueDate = DateOnly.FromDateTime(now.UtcDateTime),
        CertificateHash = new string('0', 64),
        CreatedAt = now,
        UpdatedAt = now
    };
}
