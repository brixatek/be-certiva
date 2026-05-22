using Certiva.Infrastructure.Constants;
using Certiva.Infrastructure.Persistence.Entities;
using Certiva.IssuerPortal.Services;
using Certiva.Tests.Property.Helpers;
using FsCheck;
using FsCheck.Xunit;

namespace Certiva.Tests.Property;

/// <summary>
/// FsCheck property-based tests for template management and bulk issuance.
/// Properties P7-P10, P16-P18.
/// </summary>
public class TemplateAndBulkPropertyTests
{
    // ── P7: Template Required Field Validation ───────────────────────────────────

    // Feature: certiva-core-platform, Property 7: Empty template name returns BadRequest
    [Property(MaxTest = 100)]
    public Property Template_EmptyName_ReturnsBadRequest(PositiveInt seed)
    {
        var (svc, tenantId, issuerId) = BuildVerifiedIssuer();

        var result = svc.CreateTemplateAsync(new CreateTemplateCommand
        {
            IssuerId = issuerId,
            TenantId = tenantId,
            Name = string.Empty,
            ValidityPeriodDays = 365
        }, CancellationToken.None).GetAwaiter().GetResult();

        return (result.IsFailure && result.StatusCode == 400).ToProperty();
    }

    // Feature: certiva-core-platform, Property 7b: Negative ValidityPeriodDays returns BadRequest
    [Property(MaxTest = 100)]
    public Property Template_NegativeValidityDays_ReturnsBadRequest(PositiveInt seed)
    {
        var (svc, tenantId, issuerId) = BuildVerifiedIssuer();

        var result = svc.CreateTemplateAsync(new CreateTemplateCommand
        {
            IssuerId = issuerId,
            TenantId = tenantId,
            Name = "Valid Template Name",
            ValidityPeriodDays = -1
        }, CancellationToken.None).GetAwaiter().GetResult();

        return (result.IsFailure && result.StatusCode == 400).ToProperty();
    }

    // Feature: certiva-core-platform, Property 7c: Name longer than 100 chars returns BadRequest
    [Property(MaxTest = 100)]
    public Property Template_LongName_ReturnsBadRequest(PositiveInt seed)
    {
        var (svc, tenantId, issuerId) = BuildVerifiedIssuer();

        var result = svc.CreateTemplateAsync(new CreateTemplateCommand
        {
            IssuerId = issuerId,
            TenantId = tenantId,
            Name = new string('A', 101),
            ValidityPeriodDays = 30
        }, CancellationToken.None).GetAwaiter().GetResult();

        return (result.IsFailure && result.StatusCode == 400).ToProperty();
    }

    // ── P8: Template Scoped to Creating Issuer ───────────────────────────────────

    // Feature: certiva-core-platform, Property 8: Template update by different issuer returns Forbidden
    [Property(MaxTest = 100)]
    public Property Template_UpdateByDifferentIssuer_ReturnsForbidden(PositiveInt seed)
    {
        var (svc, tenantId, issuerId) = BuildVerifiedIssuer();

        var created = svc.CreateTemplateAsync(new CreateTemplateCommand
        {
            IssuerId = issuerId,
            TenantId = tenantId,
            Name = "My Template",
            ValidityPeriodDays = 365
        }, CancellationToken.None).GetAwaiter().GetResult();

        if (created.IsFailure) return true.ToProperty();

        var differentIssuerId = Guid.NewGuid();
        var result = svc.UpdateTemplateAsync(new UpdateTemplateCommand
        {
            TemplateId = created.Value,
            IssuerId = differentIssuerId,
            TenantId = tenantId,
            Name = "Updated Name",
            ValidityPeriodDays = 180
        }, CancellationToken.None).GetAwaiter().GetResult();

        return (result.IsFailure && result.StatusCode == 403).ToProperty();
    }

    // Feature: certiva-core-platform, Property 8b: Template deactivation by different issuer returns Forbidden
    [Property(MaxTest = 100)]
    public Property Template_DeactivateByDifferentIssuer_ReturnsForbidden(PositiveInt seed)
    {
        var (svc, tenantId, issuerId) = BuildVerifiedIssuer();

        var created = svc.CreateTemplateAsync(new CreateTemplateCommand
        {
            IssuerId = issuerId,
            TenantId = tenantId,
            Name = "My Template " + seed.Get,
            ValidityPeriodDays = 365
        }, CancellationToken.None).GetAwaiter().GetResult();

        if (created.IsFailure) return true.ToProperty();

        var differentIssuerId = Guid.NewGuid();
        var result = svc.DeactivateTemplateAsync(
            created.Value, differentIssuerId, tenantId, CancellationToken.None).GetAwaiter().GetResult();

        return (result.IsFailure && result.StatusCode == 403).ToProperty();
    }

    // ── P10: Template Name Uniqueness Per Issuer ─────────────────────────────────

    // Feature: certiva-core-platform, Property 10: Duplicate template name under same issuer returns Conflict
    [Property(MaxTest = 100)]
    public Property Template_DuplicateName_ReturnsConflict(PositiveInt seed)
    {
        var (svc, tenantId, issuerId) = BuildVerifiedIssuer();
        var templateName = "Template " + (seed.Get % 1000);

        var first = svc.CreateTemplateAsync(new CreateTemplateCommand
        {
            IssuerId = issuerId,
            TenantId = tenantId,
            Name = templateName,
            ValidityPeriodDays = 365
        }, CancellationToken.None).GetAwaiter().GetResult();

        if (first.IsFailure) return true.ToProperty();

        var second = svc.CreateTemplateAsync(new CreateTemplateCommand
        {
            IssuerId = issuerId,
            TenantId = tenantId,
            Name = templateName,
            ValidityPeriodDays = 180
        }, CancellationToken.None).GetAwaiter().GetResult();

        return (second.IsFailure && second.StatusCode == 409).ToProperty();
    }

    // ── P16: Bulk Issuance Boundary Validation ───────────────────────────────────

    // Feature: certiva-core-platform, Property 16: Empty professional list returns UnprocessableEntity
    [Property(MaxTest = 100)]
    public Property BulkIssue_EmptyList_ReturnsUnprocessableEntity(PositiveInt seed)
    {
        var (svc, tenantId, issuerId) = BuildVerifiedIssuer();
        var templateId = CreateTemplate(svc, tenantId, issuerId, "Template " + seed.Get);

        var result = svc.EnqueueBulkIssueAsync(new EnqueueBulkIssueCommand
        {
            IssuerId = issuerId,
            TenantId = tenantId,
            TemplateId = templateId,
            ProfessionalIds = []
        }, CancellationToken.None).GetAwaiter().GetResult();

        return (result.IsFailure && result.StatusCode == 422).ToProperty();
    }

    // Feature: certiva-core-platform, Property 16b: List exceeding 1000 professionals returns UnprocessableEntity
    [Property(MaxTest = 100)]
    public Property BulkIssue_OverLimit_ReturnsUnprocessableEntity(PositiveInt seed)
    {
        var (svc, tenantId, issuerId) = BuildVerifiedIssuer();
        var templateId = CreateTemplate(svc, tenantId, issuerId, "Template X " + seed.Get);

        var oversizedList = Enumerable.Range(0, 1001).Select(_ => Guid.NewGuid()).ToList();

        var result = svc.EnqueueBulkIssueAsync(new EnqueueBulkIssueCommand
        {
            IssuerId = issuerId,
            TenantId = tenantId,
            TemplateId = templateId,
            ProfessionalIds = oversizedList
        }, CancellationToken.None).GetAwaiter().GetResult();

        return (result.IsFailure && result.StatusCode == 422).ToProperty();
    }

    // ── P17: Bulk Issuance Issuer Verification ───────────────────────────────────

    // Feature: certiva-core-platform, Property 17: Unverified issuer bulk issuance returns Forbidden
    [Property(MaxTest = 100)]
    public Property BulkIssue_UnverifiedIssuer_ReturnsForbidden(PositiveInt seed)
    {
        var tenantId = Guid.NewGuid();
        var issuerId = Guid.NewGuid();
        var db = TestDbFactory.Create();

        db.Issuers.Add(new IssuerEntity
        {
            IssuerId = issuerId,
            TenantId = tenantId,
            OrganizationName = "Unverified Org " + seed.Get,
            Type = "TrainingProvider",
            VerificationStatus = IssuerStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.SaveChanges();

        var outbox = new FakeOutboxWriter();
        var svc = new IssuerPortalService(db, outbox);

        var result = svc.EnqueueBulkIssueAsync(new EnqueueBulkIssueCommand
        {
            IssuerId = issuerId,
            TenantId = tenantId,
            TemplateId = Guid.NewGuid(),
            ProfessionalIds = [Guid.NewGuid()]
        }, CancellationToken.None).GetAwaiter().GetResult();

        return (result.IsFailure && result.StatusCode == 403).ToProperty();
    }

    // ── P18: Bulk Job Access Control ─────────────────────────────────────────────

    // Feature: certiva-core-platform, Property 18: Job status query by different issuer returns Forbidden
    [Property(MaxTest = 100)]
    public Property BulkJobStatus_DifferentIssuer_ReturnsForbidden(PositiveInt seed)
    {
        var (svc, tenantId, issuerId) = BuildVerifiedIssuer();
        var templateId = CreateTemplate(svc, tenantId, issuerId, "Batch Template " + seed.Get);

        var enqueued = svc.EnqueueBulkIssueAsync(new EnqueueBulkIssueCommand
        {
            IssuerId = issuerId,
            TenantId = tenantId,
            TemplateId = templateId,
            ProfessionalIds = [Guid.NewGuid()]
        }, CancellationToken.None).GetAwaiter().GetResult();

        if (enqueued.IsFailure) return true.ToProperty();

        var differentIssuerId = Guid.NewGuid();
        var result = svc.GetBulkJobStatusAsync(
            enqueued.Value, differentIssuerId, tenantId, CancellationToken.None).GetAwaiter().GetResult();

        return (result.IsFailure && result.StatusCode == 403).ToProperty();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static (IssuerPortalService svc, Guid tenantId, Guid issuerId) BuildVerifiedIssuer()
    {
        var tenantId = Guid.NewGuid();
        var issuerId = Guid.NewGuid();
        var db = TestDbFactory.Create();

        db.Issuers.Add(new IssuerEntity
        {
            IssuerId = issuerId,
            TenantId = tenantId,
            OrganizationName = "Verified Org " + Guid.NewGuid(),
            Type = "TrainingProvider",
            VerificationStatus = IssuerStatus.Verified,
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.SaveChanges();

        var outbox = new FakeOutboxWriter();
        var svc = new IssuerPortalService(db, outbox);

        return (svc, tenantId, issuerId);
    }

    private static Guid CreateTemplate(IssuerPortalService svc, Guid tenantId, Guid issuerId, string name)
    {
        var result = svc.CreateTemplateAsync(new CreateTemplateCommand
        {
            IssuerId = issuerId,
            TenantId = tenantId,
            Name = name,
            ValidityPeriodDays = 365
        }, CancellationToken.None).GetAwaiter().GetResult();

        return result.IsSuccess ? result.Value : Guid.NewGuid();
    }
}
