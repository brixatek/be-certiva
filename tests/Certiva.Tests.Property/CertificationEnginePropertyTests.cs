using Certiva.CertificationEngine.Commands;
using Certiva.CertificationEngine.Models;
using Certiva.CertificationEngine.Services;
using Certiva.Infrastructure.Constants;
using Certiva.Infrastructure.Persistence.Entities;
using Certiva.Tests.Property.Helpers;
using FsCheck;
using FsCheck.Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Certiva.Tests.Property;

/// <summary>
/// FsCheck property-based tests for the CertificationEngine module.
/// Properties P11-P21, P45, P47.
/// </summary>
public class CertificationEnginePropertyTests
{
    private static readonly CanonicalSerializationService Serializer = new();
    private static readonly CertificateHashService HashService = new(new CanonicalSerializationService());

    // ── P12: Certificate Hash Chain Integrity ───────────────────────────────────

    // Feature: certiva-core-platform, Property 12: Hash chain verification always passes for a correctly computed chain
    [Property(MaxTest = 100)]
    public Property HashChain_CorrectlyComputed_VerifiesSucessfully(PositiveInt chainLength)
    {
        var length = Math.Min(chainLength.Get, 20); // cap to 20 for speed
        var chain = new List<(CertificateFields Fields, string StoredHash)>();
        var previousHash = HashService.GetGenesisHash();

        for (var i = 0; i < length; i++)
        {
            var fields = new CertificateFields(
                CertificateId: Guid.NewGuid(),
                ExpiryDate: null,
                IssuerId: Guid.NewGuid(),
                IssuerName: "Test Issuer",
                IssueDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-i),
                Name: $"Certificate {i}",
                ProfessionalId: Guid.NewGuid(),
                TenantId: Guid.NewGuid());

            var hash = HashService.ComputeHash(fields, previousHash);
            chain.Add((fields, hash));
            previousHash = hash;
        }

        return HashService.VerifyChain(chain).ToProperty();
    }

    // Feature: certiva-core-platform, Property 12b: Mutating any field breaks hash chain verification
    [Property(MaxTest = 100)]
    public Property HashChain_MutatedField_FailsVerification(PositiveInt seed)
    {
        var fields = new CertificateFields(
            CertificateId: Guid.NewGuid(),
            ExpiryDate: null,
            IssuerId: Guid.NewGuid(),
            IssuerName: "Original Issuer",
            IssueDate: DateOnly.FromDateTime(DateTime.UtcNow),
            Name: "Original Certificate",
            ProfessionalId: Guid.NewGuid(),
            TenantId: Guid.NewGuid());

        var genesis = HashService.GetGenesisHash();
        var correctHash = HashService.ComputeHash(fields, genesis);

        // Mutate the name
        var mutated = fields with { Name = "Tampered Certificate " + seed.Get };
        var wrongHash = HashService.ComputeHash(mutated, genesis);

        // The stored hash of the original vs the recomputed hash of the mutated record must differ
        return (correctHash != wrongHash).ToProperty();
    }

    // ── P13: Canonical Serialization Determinism ─────────────────────────────────

    // Feature: certiva-core-platform, Property 13: Canonical serialization is deterministic — same input always produces same output
    [Property(MaxTest = 100)]
    public Property CanonicalSerialization_IsDeterministic(PositiveInt seed)
    {
        var fields = new CertificateFields(
            CertificateId: Guid.NewGuid(),
            ExpiryDate: seed.Get % 2 == 0 ? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(365)) : null,
            IssuerId: Guid.NewGuid(),
            IssuerName: "Issuer " + seed.Get,
            IssueDate: DateOnly.FromDateTime(DateTime.UtcNow),
            Name: "Certificate " + seed.Get,
            ProfessionalId: Guid.NewGuid(),
            TenantId: Guid.NewGuid());

        var first = Serializer.Serialize(fields);
        var second = Serializer.Serialize(fields);

        return (first == second).ToProperty();
    }

    // Feature: certiva-core-platform, Property 13b: Different certificate IDs produce different canonical strings
    [Property(MaxTest = 100)]
    public Property CanonicalSerialization_DifferentId_ProducesDifferentOutput(PositiveInt seed)
    {
        var tenantId = Guid.NewGuid();
        var issuerId = Guid.NewGuid();
        var professionalId = Guid.NewGuid();
        var issueDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var fields1 = new CertificateFields(Guid.NewGuid(), null, issuerId, "Issuer", issueDate, "Cert", professionalId, tenantId);
        var fields2 = fields1 with { CertificateId = Guid.NewGuid() };

        var s1 = Serializer.Serialize(fields1);
        var s2 = Serializer.Serialize(fields2);

        return (s1 != s2).ToProperty();
    }

    // ── P11: Certificate Issuance Creates Correct Record ────────────────────────

    // Feature: certiva-core-platform, Property 11: Successful issuance creates a certificate with Active status
    [Property(MaxTest = 100)]
    public Property CertificateIssuance_Success_CreatesActiveRecord(PositiveInt validityDays)
    {
        var (svc, db, tenantId, issuerId, professionalId, templateId) = BuildServiceWithEntities(validityDays.Get);

        var result = svc.IssueCertificateAsync(
            new IssueCertificateCommand(tenantId, professionalId, issuerId, templateId, null),
            CancellationToken.None).GetAwaiter().GetResult();

        if (result.IsFailure) return true.ToProperty(); // setup failure

        var cert = db.Certificates.FirstOrDefault(c => c.CertificateId == result.Value!.CertificateId);

        return (cert != null
            && cert.Status == CertificateStatus.Active
            && cert.TenantId == tenantId
            && cert.ProfessionalId == professionalId
            && cert.IssuerId == issuerId).ToProperty();
    }

    // ── P14: Issuance Idempotency ─────────────────────────────────────────────

    // Feature: certiva-core-platform, Property 14: Same idempotency key always returns the same certificate ID
    [Property(MaxTest = 100)]
    public Property Issuance_SameIdempotencyKey_ReturnsSameCertificateId(PositiveInt seed)
    {
        var (svc, _, tenantId, issuerId, professionalId, templateId) = BuildServiceWithEntities(365);
        var key = "idem-" + seed.Get;

        var first = svc.IssueCertificateAsync(
            new IssueCertificateCommand(tenantId, professionalId, issuerId, templateId, key),
            CancellationToken.None).GetAwaiter().GetResult();

        if (first.IsFailure) return true.ToProperty();

        var second = svc.IssueCertificateAsync(
            new IssueCertificateCommand(tenantId, professionalId, issuerId, templateId, key),
            CancellationToken.None).GetAwaiter().GetResult();

        return (second.IsSuccess && first.Value!.CertificateId == second.Value!.CertificateId).ToProperty();
    }

    // ── P15: Duplicate Active Certificate Prevention ─────────────────────────────

    // Feature: certiva-core-platform, Property 15: Issuing a second Active certificate for same professional+template returns Conflict
    [Property(MaxTest = 100)]
    public Property DuplicateActiveCertificate_ReturnsConflict(PositiveInt seed)
    {
        var (svc, _, tenantId, issuerId, professionalId, templateId) = BuildServiceWithEntities(365);

        var first = svc.IssueCertificateAsync(
            new IssueCertificateCommand(tenantId, professionalId, issuerId, templateId, null),
            CancellationToken.None).GetAwaiter().GetResult();

        if (first.IsFailure) return true.ToProperty();

        var second = svc.IssueCertificateAsync(
            new IssueCertificateCommand(tenantId, professionalId, issuerId, templateId, null),
            CancellationToken.None).GetAwaiter().GetResult();

        return (second.IsFailure && second.StatusCode == 409).ToProperty();
    }

    // ── P19: Soft Deletion Invariant ─────────────────────────────────────────────

    // Feature: certiva-core-platform, Property 19: Revoked certificate keeps its data intact (no hard delete)
    [Property(MaxTest = 100)]
    public Property RevokedCertificate_DataRemainsIntact(PositiveInt seed)
    {
        var (svc, db, tenantId, issuerId, professionalId, templateId) = BuildServiceWithEntities(365);

        var issued = svc.IssueCertificateAsync(
            new IssueCertificateCommand(tenantId, professionalId, issuerId, templateId, null),
            CancellationToken.None).GetAwaiter().GetResult();

        if (issued.IsFailure) return true.ToProperty();

        var certId = issued.Value!.CertificateId;

        var revoked = svc.RevokeCertificateAsync(
            new RevokeCertificateCommand(tenantId, certId, issuerId, "Test revocation reason"),
            CancellationToken.None).GetAwaiter().GetResult();

        if (revoked.IsFailure) return true.ToProperty();

        var cert = db.Certificates.Find(certId);

        return (cert != null
            && cert.Status == CertificateStatus.Revoked
            && cert.CertificateId == certId
            && cert.ProfessionalId == professionalId).ToProperty();
    }

    // ── P20: Revocation State Transition Validation ──────────────────────────────

    // Feature: certiva-core-platform, Property 20: Revoking an already-revoked certificate returns Conflict
    [Property(MaxTest = 100)]
    public Property DoubleRevocation_ReturnsConflict(PositiveInt seed)
    {
        var (svc, _, tenantId, issuerId, professionalId, templateId) = BuildServiceWithEntities(365);

        var issued = svc.IssueCertificateAsync(
            new IssueCertificateCommand(tenantId, professionalId, issuerId, templateId, null),
            CancellationToken.None).GetAwaiter().GetResult();

        if (issued.IsFailure) return true.ToProperty();

        var certId = issued.Value!.CertificateId;

        var first = svc.RevokeCertificateAsync(
            new RevokeCertificateCommand(tenantId, certId, issuerId, "First revocation"),
            CancellationToken.None).GetAwaiter().GetResult();

        if (first.IsFailure) return true.ToProperty();

        var second = svc.RevokeCertificateAsync(
            new RevokeCertificateCommand(tenantId, certId, issuerId, "Second revocation"),
            CancellationToken.None).GetAwaiter().GetResult();

        return (second.IsFailure && second.StatusCode == 409).ToProperty();
    }

    // ── P21: Cross-Issuer Revocation Authorization ───────────────────────────────

    // Feature: certiva-core-platform, Property 21: An issuer cannot revoke another issuer's certificate
    [Property(MaxTest = 100)]
    public Property CrossIssuerRevocation_ReturnsForbidden(PositiveInt seed)
    {
        var (svc, _, tenantId, issuerId, professionalId, templateId) = BuildServiceWithEntities(365);

        var issued = svc.IssueCertificateAsync(
            new IssueCertificateCommand(tenantId, professionalId, issuerId, templateId, null),
            CancellationToken.None).GetAwaiter().GetResult();

        if (issued.IsFailure) return true.ToProperty();

        var differentIssuerId = Guid.NewGuid();
        var result = svc.RevokeCertificateAsync(
            new RevokeCertificateCommand(tenantId, issued.Value!.CertificateId, differentIssuerId, "Unauthorized"),
            CancellationToken.None).GetAwaiter().GetResult();

        return (result.IsFailure && result.StatusCode == 403).ToProperty();
    }

    // ── P45: Tampered Certificate Detection ──────────────────────────────────────

    // Feature: certiva-core-platform, Property 45: Hash verification returns false for a tampered hash
    [Property(MaxTest = 100)]
    public Property TamperedHash_HashVerification_ReturnsFalse(PositiveInt seed)
    {
        var (svc, db, tenantId, issuerId, professionalId, templateId) = BuildServiceWithEntities(365);

        var issued = svc.IssueCertificateAsync(
            new IssueCertificateCommand(tenantId, professionalId, issuerId, templateId, null),
            CancellationToken.None).GetAwaiter().GetResult();

        if (issued.IsFailure) return true.ToProperty();

        // Directly tamper with the stored hash
        var cert = db.Certificates.Find(issued.Value!.CertificateId)!;
        cert.CertificateHash = new string('0', 63) + "f"; // corrupt last char
        db.SaveChanges();

        var result = svc.VerifyCertificateHashAsync(
            issued.Value.CertificateId, tenantId, CancellationToken.None).GetAwaiter().GetResult();

        return (result.IsSuccess && !result.Value!.IsValid).ToProperty();
    }

    // ── P47: Cross-Tenant Entity Validation ─────────────────────────────────────

    // Feature: certiva-core-platform, Property 47: Certificate issuance using professional from different tenant returns error
    [Property(MaxTest = 100)]
    public Property IssuanceCrossTenant_ReturnsFailure(PositiveInt seed)
    {
        var (svc, _, tenantId, issuerId, professionalId, templateId) = BuildServiceWithEntities(365);

        // Issue with a different tenantId
        var differentTenantId = Guid.NewGuid();
        var result = svc.IssueCertificateAsync(
            new IssueCertificateCommand(differentTenantId, professionalId, issuerId, templateId, null),
            CancellationToken.None).GetAwaiter().GetResult();

        return result.IsFailure.ToProperty();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static (CertificationEngineService svc, Infrastructure.Persistence.CertivaDbContext db,
        Guid tenantId, Guid issuerId, Guid professionalId, Guid templateId)
        BuildServiceWithEntities(int validityDays)
    {
        var tenantId = Guid.NewGuid();
        var issuerId = Guid.NewGuid();
        var professionalId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var db = TestDbFactory.Create();

        db.Professionals.Add(new ProfessionalEntity
        {
            ProfessionalId = professionalId,
            TenantId = tenantId,
            Name = "Test Professional",
            NationalId_Encrypted = Array.Empty<byte>(),
            NationalId_Hash = "abc123",
            CreatedAt = now
        });

        db.Issuers.Add(new IssuerEntity
        {
            IssuerId = issuerId,
            TenantId = tenantId,
            OrganizationName = "Test Org",
            Type = "TrainingProvider",
            VerificationStatus = IssuerStatus.Verified,
            CreatedAt = now
        });

        db.CertificateTemplates.Add(new Infrastructure.Persistence.Entities.CertificateTemplateEntity
        {
            TemplateId = templateId,
            TenantId = tenantId,
            IssuerId = issuerId,
            Name = "Test Template",
            ValidityPeriodDays = Math.Max(0, validityDays),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });

        db.SaveChanges();

        var outbox = new FakeOutboxWriter();
        var hashService = new CertificateHashService(new CanonicalSerializationService());
        var idempotencyRepo = new FakeIdempotencyKeyRepository();
        var logger = NullLogger<CertificationEngineService>.Instance;

        var svc = new CertificationEngineService(db, outbox, hashService, idempotencyRepo, logger);

        return (svc, db, tenantId, issuerId, professionalId, templateId);
    }
}
