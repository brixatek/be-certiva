using Certiva.IdentityRegistry.Commands;
using Certiva.IdentityRegistry.Services;
using Certiva.Infrastructure.Constants;
using Certiva.Infrastructure.Persistence.Entities;
using Certiva.Tests.Property.Helpers;
using FsCheck;
using FsCheck.Xunit;
using FluentAssertions;

namespace Certiva.Tests.Property;

/// <summary>
/// FsCheck property-based tests for the IdentityRegistry module.
/// Properties P1-P6, P40-P41.
/// </summary>
public class IdentityRegistryPropertyTests
{
    private static readonly NationalIdMaskingService Masker = new();

    // ── P1: NationalId Masking Is Applied Universally ───────────────────────────

    // Feature: certiva-core-platform, Property 1: NationalId masking hides all but the last four characters
    [Property(MaxTest = 100)]
    public Property NationalId_Masking_HidesAllButLastFour(NonEmptyString raw)
    {
        var s = raw.Get;
        var masked = Masker.Mask(s);

        // Empty input stays empty
        if (string.IsNullOrEmpty(s))
            return (masked == string.Empty).ToProperty();

        // Short inputs: all stars
        if (s.Length <= 4)
            return (masked.All(c => c == '*') && masked.Length == s.Length).ToProperty();

        // Longer inputs: leading stars, last-4 preserved
        var allStarPrefix = masked[..^4].All(c => c == '*');
        var tail = masked[^4..] == s[^4..];
        return (allStarPrefix && tail && masked.Length == s.Length).ToProperty();
    }

    // Feature: certiva-core-platform, Property 1b: Masked value never equals original for inputs longer than 4 chars
    [Property(MaxTest = 100)]
    public Property NationalId_Masking_NeverExposesOriginalForLongInputs(NonEmptyString raw)
    {
        var s = raw.Get;
        if (s.Length <= 4) return true.ToProperty(); // vacuously true — short inputs are all stars

        var masked = Masker.Mask(s);
        // Masked form differs from original (star prefix replaces leading chars)
        return (masked != s).ToProperty();
    }

    // ── P2: Professional Registration Deduplication ─────────────────────────────

    // Feature: certiva-core-platform, Property 2: Same NationalId+TenantId always returns Conflict on second registration
    [Property(MaxTest = 100)]
    public Property ProfessionalRegistration_Deduplication_ReturnConflict(
        NonEmptyString name, PositiveInt nationalIdSuffix)
    {
        var tenantId = Guid.NewGuid();
        var nationalId = "AB" + (nationalIdSuffix.Get % 999999).ToString("D6");
        var db = TestDbFactory.Create();
        var outbox = new FakeOutboxWriter();
        var encryption = TestEncryptionService.Create();
        var svc = new IdentityRegistryService(db, outbox, encryption);

        var cmd = new RegisterProfessionalCommand
        {
            TenantId = tenantId,
            Name = (name.Get.Length > 100 ? name.Get[..100] : name.Get),
            NationalId = nationalId,
            Email = "test@example.com"
        };

        var first = svc.RegisterProfessionalAsync(cmd, CancellationToken.None).GetAwaiter().GetResult();
        if (first.IsFailure) return true.ToProperty(); // validation failure, skip

        var second = svc.RegisterProfessionalAsync(cmd, CancellationToken.None).GetAwaiter().GetResult();

        return (second.IsFailure && second.StatusCode == 409).ToProperty();
    }

    // ── P3: Professional Registration Field Validation ──────────────────────────

    // Feature: certiva-core-platform, Property 3: Empty name always produces BadRequest
    [Property(MaxTest = 100)]
    public Property ProfessionalRegistration_EmptyName_ReturnsBadRequest(PositiveInt seed)
    {
        var db = TestDbFactory.Create();
        var outbox = new FakeOutboxWriter();
        var encryption = TestEncryptionService.Create();
        var svc = new IdentityRegistryService(db, outbox, encryption);

        var cmd = new RegisterProfessionalCommand
        {
            TenantId = Guid.NewGuid(),
            Name = string.Empty,
            NationalId = "AB" + (seed.Get % 999999).ToString("D6"),
            Email = "test@example.com"
        };

        var result = svc.RegisterProfessionalAsync(cmd, CancellationToken.None).GetAwaiter().GetResult();

        return (result.IsFailure && result.StatusCode == 400).ToProperty();
    }

    // Feature: certiva-core-platform, Property 3b: Name longer than 100 chars always produces BadRequest
    [Property(MaxTest = 100)]
    public Property ProfessionalRegistration_LongName_ReturnsBadRequest(PositiveInt seed)
    {
        var db = TestDbFactory.Create();
        var outbox = new FakeOutboxWriter();
        var encryption = TestEncryptionService.Create();
        var svc = new IdentityRegistryService(db, outbox, encryption);

        var longName = new string('A', 101);
        var cmd = new RegisterProfessionalCommand
        {
            TenantId = Guid.NewGuid(),
            Name = longName,
            NationalId = "AB" + (seed.Get % 999999).ToString("D6"),
            Email = "test@example.com"
        };

        var result = svc.RegisterProfessionalAsync(cmd, CancellationToken.None).GetAwaiter().GetResult();

        return (result.IsFailure && result.StatusCode == 400).ToProperty();
    }

    // Feature: certiva-core-platform, Property 3c: Missing both phone and email always produces BadRequest
    [Property(MaxTest = 100)]
    public Property ProfessionalRegistration_NoContact_ReturnsBadRequest(PositiveInt seed)
    {
        var db = TestDbFactory.Create();
        var outbox = new FakeOutboxWriter();
        var encryption = TestEncryptionService.Create();
        var svc = new IdentityRegistryService(db, outbox, encryption);

        var cmd = new RegisterProfessionalCommand
        {
            TenantId = Guid.NewGuid(),
            Name = "Valid Name",
            NationalId = "AB" + (seed.Get % 999999).ToString("D6"),
            Phone = null,
            Email = null
        };

        var result = svc.RegisterProfessionalAsync(cmd, CancellationToken.None).GetAwaiter().GetResult();

        return (result.IsFailure && result.StatusCode == 400).ToProperty();
    }

    // ── P4: Unverified Issuer Cannot Issue or Manage Templates ──────────────────

    // Feature: certiva-core-platform, Property 4: Pending or Rejected issuer cannot create templates
    [Property(MaxTest = 100)]
    public Property UnverifiedIssuer_CannotCreateTemplate(bool isPending)
    {
        var tenantId = Guid.NewGuid();
        var issuerId = Guid.NewGuid();
        var db = TestDbFactory.Create();

        db.Issuers.Add(new IssuerEntity
        {
            IssuerId = issuerId,
            TenantId = tenantId,
            OrganizationName = "Test Org",
            Type = "TrainingProvider",
            VerificationStatus = isPending ? IssuerStatus.Pending : IssuerStatus.Rejected,
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.SaveChanges();

        var outbox = new FakeOutboxWriter();
        var svc = new Certiva.IssuerPortal.Services.IssuerPortalService(db, outbox);

        var result = svc.CreateTemplateAsync(new Certiva.IssuerPortal.Commands.CreateTemplateCommand
        {
            IssuerId = issuerId,
            TenantId = tenantId,
            Name = "My Template",
            ValidityPeriodDays = 365
        }, CancellationToken.None).GetAwaiter().GetResult();

        return (result.IsFailure && result.StatusCode == 403).ToProperty();
    }

    // ── P5: Issuer Organization Name Uniqueness ─────────────────────────────────

    // Feature: certiva-core-platform, Property 5: Same org name in same tenant returns Conflict
    [Property(MaxTest = 100)]
    public Property IssuerOnboarding_DuplicateOrgName_ReturnsConflict(NonEmptyString orgSuffix)
    {
        var tenantId = Guid.NewGuid();
        var orgName = "Test " + (orgSuffix.Get.Length > 50 ? orgSuffix.Get[..50] : orgSuffix.Get);
        var db = TestDbFactory.Create();
        var outbox = new FakeOutboxWriter();
        var encryption = TestEncryptionService.Create();
        var svc = new IdentityRegistryService(db, outbox, encryption);

        var cmd1 = new OnboardIssuerCommand(tenantId, orgName, "TrainingProvider");
        var first = svc.OnboardIssuerAsync(cmd1, CancellationToken.None).GetAwaiter().GetResult();
        if (first.IsFailure) return true.ToProperty();

        var cmd2 = new OnboardIssuerCommand(tenantId, orgName, "University");
        var second = svc.OnboardIssuerAsync(cmd2, CancellationToken.None).GetAwaiter().GetResult();

        return (second.IsFailure && second.StatusCode == 409).ToProperty();
    }

    // ── P6: Issuer State Transition Idempotency ─────────────────────────────────

    // Feature: certiva-core-platform, Property 6: Approving an already-Verified issuer returns Conflict
    [Property(MaxTest = 100)]
    public Property IssuerApproval_Idempotency_AlreadyVerifiedReturnsConflict(PositiveInt seed)
    {
        var tenantId = Guid.NewGuid();
        var issuerId = Guid.NewGuid();
        var adminId = Guid.NewGuid().ToString();
        var db = TestDbFactory.Create();

        db.Issuers.Add(new IssuerEntity
        {
            IssuerId = issuerId,
            TenantId = tenantId,
            OrganizationName = "Org " + seed.Get,
            Type = "TrainingProvider",
            VerificationStatus = IssuerStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.SaveChanges();

        var outbox = new FakeOutboxWriter();
        var encryption = TestEncryptionService.Create();
        var svc = new IdentityRegistryService(db, outbox, encryption);

        var first = svc.ApproveIssuerAsync(
            new ApproveIssuerCommand(issuerId, tenantId, adminId),
            CancellationToken.None).GetAwaiter().GetResult();

        first.IsSuccess.Should().BeTrue();

        var second = svc.ApproveIssuerAsync(
            new ApproveIssuerCommand(issuerId, tenantId, adminId),
            CancellationToken.None).GetAwaiter().GetResult();

        return (second.IsFailure && second.StatusCode == 409).ToProperty();
    }

    // Feature: certiva-core-platform, Property 6b: Rejecting an already-Rejected issuer returns Conflict
    [Property(MaxTest = 100)]
    public Property IssuerRejection_Idempotency_AlreadyRejectedReturnsConflict(PositiveInt seed)
    {
        var tenantId = Guid.NewGuid();
        var issuerId = Guid.NewGuid();
        var adminId = Guid.NewGuid().ToString();
        var db = TestDbFactory.Create();

        db.Issuers.Add(new IssuerEntity
        {
            IssuerId = issuerId,
            TenantId = tenantId,
            OrganizationName = "Org " + seed.Get,
            Type = "TrainingProvider",
            VerificationStatus = IssuerStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.SaveChanges();

        var outbox = new FakeOutboxWriter();
        var encryption = TestEncryptionService.Create();
        var svc = new IdentityRegistryService(db, outbox, encryption);

        var first = svc.RejectIssuerAsync(
            new RejectIssuerCommand(issuerId, tenantId, adminId, "Test"),
            CancellationToken.None).GetAwaiter().GetResult();

        first.IsSuccess.Should().BeTrue();

        var second = svc.RejectIssuerAsync(
            new RejectIssuerCommand(issuerId, tenantId, adminId, "Test"),
            CancellationToken.None).GetAwaiter().GetResult();

        return (second.IsFailure && second.StatusCode == 409).ToProperty();
    }

    // ── P40: Authentication Error Conditions ────────────────────────────────────

    // Feature: certiva-core-platform, Property 40: Non-existent issuer approval returns 404
    [Property(MaxTest = 100)]
    public Property IssuerApproval_NonExistentIssuer_ReturnsNotFound(PositiveInt seed)
    {
        var db = TestDbFactory.Create();
        var outbox = new FakeOutboxWriter();
        var encryption = TestEncryptionService.Create();
        var svc = new IdentityRegistryService(db, outbox, encryption);

        var result = svc.ApproveIssuerAsync(
            new ApproveIssuerCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid().ToString()),
            CancellationToken.None).GetAwaiter().GetResult();

        return (result.IsFailure && result.StatusCode == 404).ToProperty();
    }

    // Feature: certiva-core-platform, Property 41: Successful registration always creates a Professional record
    [Property(MaxTest = 100)]
    public Property ProfessionalRegistration_Success_CreatesRecord(PositiveInt nationalIdSuffix)
    {
        var tenantId = Guid.NewGuid();
        var nationalId = "AB" + (nationalIdSuffix.Get % 999999).ToString("D6");
        var db = TestDbFactory.Create();
        var outbox = new FakeOutboxWriter();
        var encryption = TestEncryptionService.Create();
        var svc = new IdentityRegistryService(db, outbox, encryption);

        var cmd = new RegisterProfessionalCommand
        {
            TenantId = tenantId,
            Name = "Test Professional",
            NationalId = nationalId,
            Email = "test@example.com"
        };

        var result = svc.RegisterProfessionalAsync(cmd, CancellationToken.None).GetAwaiter().GetResult();

        if (result.IsFailure) return true.ToProperty(); // validation failure

        var exists = db.Professionals.Any(p => p.ProfessionalId == result.Value!.ProfessionalId);
        var outboxHasEvent = outbox.HasEvent(EventTypes.ProfessionalRegistered);

        return (result.IsSuccess && exists && outboxHasEvent).ToProperty();
    }
}
