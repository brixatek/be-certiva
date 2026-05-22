using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Certiva.IdentityRegistry.Commands;
using Certiva.IdentityRegistry.Resources;
using Certiva.Infrastructure.Constants;
using Certiva.Infrastructure.Domain;
using Certiva.Infrastructure.Events;
using Certiva.Infrastructure.Outbox;
using Certiva.Infrastructure.Persistence;
using Certiva.Infrastructure.Persistence.Entities;
using Certiva.Infrastructure.Persistence.Repositories;

namespace Certiva.IdentityRegistry.Services;

/// <summary>
/// Default implementation of <see cref="IIdentityRegistryService"/>.
/// Handles Professional registration with field validation, AES-256 encryption,
/// SHA-256 deduplication, and transactional outbox event publishing.
/// </summary>
public sealed class IdentityRegistryService : IIdentityRegistryService
{
    // Validation patterns (Requirements 1.3, 1.4, 1.5)
    private static readonly Regex NationalIdPattern =
        new(@"^[a-zA-Z0-9]{6,20}$", RegexOptions.Compiled);

    private static readonly Regex PhonePattern =
        new(@"^\+[1-9]\d{1,14}$", RegexOptions.Compiled);

    private static readonly Regex EmailPattern =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private readonly CertivaDbContext _db;
    private readonly IOutboxWriter _outboxWriter;
    private readonly IEncryptionService _encryption;
    private readonly IProfessionalRepository _professionals;
    private readonly IIssuerRepository _issuers;
    private readonly IAuditLogRepository _auditLogs;

    public IdentityRegistryService(
        CertivaDbContext db,
        IOutboxWriter outboxWriter,
        IEncryptionService encryption,
        IProfessionalRepository professionals,
        IIssuerRepository issuers,
        IAuditLogRepository auditLogs)
    {
        _db = db;
        _outboxWriter = outboxWriter;
        _encryption = encryption;
        _professionals = professionals;
        _issuers = issuers;
        _auditLogs = auditLogs;
    }

    /// <inheritdoc />
    public async Task<Result<RegisterProfessionalResult>> RegisterProfessionalAsync(
        RegisterProfessionalCommand cmd,
        CancellationToken ct)
    {
        // --- Validation (collect all errors before returning) ---
        var errors = new List<string>();

        // Name: non-empty, max 100 chars (Requirement 1.3)
        if (string.IsNullOrWhiteSpace(cmd.Name))
            errors.Add(ProfessionalMessages.NameRequired);
        else if (cmd.Name.Length > 100)
            errors.Add(ProfessionalMessages.NameTooLong);

        // NationalId: 6–20 alphanumeric (Requirements 1.3, 1.4)
        if (string.IsNullOrWhiteSpace(cmd.NationalId))
            errors.Add(ProfessionalMessages.NationalIdRequired);
        else if (!NationalIdPattern.IsMatch(cmd.NationalId))
            errors.Add(ProfessionalMessages.NationalIdInvalid);

        // At least one of Phone or Email must be present (Requirement 1.3)
        var hasPhone = !string.IsNullOrWhiteSpace(cmd.Phone);
        var hasEmail = !string.IsNullOrWhiteSpace(cmd.Email);

        if (!hasPhone && !hasEmail)
            errors.Add(ProfessionalMessages.ContactRequired);

        // Phone: E.164 format if provided (Requirement 1.5)
        if (hasPhone && !PhonePattern.IsMatch(cmd.Phone!))
            errors.Add(ProfessionalMessages.PhoneInvalid);

        // Email: basic RFC 5322 format if provided (Requirement 1.5)
        if (hasEmail && !EmailPattern.IsMatch(cmd.Email!))
            errors.Add(ProfessionalMessages.EmailInvalid);

        if (errors.Count > 0)
            return Result<RegisterProfessionalResult>.BadRequest(string.Join(" ", errors));

        // --- Deduplication check (Requirement 1.2) ---
        var nationalIdHash = _encryption.ComputeHash(cmd.NationalId);

        var existing = await _professionals.FindByNationalIdHashAsync(nationalIdHash, cmd.TenantId, ct);

        if (existing is not null)
            return Result<RegisterProfessionalResult>.Conflict(
                ProfessionalMessages.AlreadyExists(existing.ProfessionalId));

        // --- Encrypt PII fields (Requirement 1.2 / 16.2) ---
        var entity = new ProfessionalEntity
        {
            ProfessionalId = Guid.NewGuid(),
            TenantId = cmd.TenantId,
            Name = cmd.Name,
            NationalId_Encrypted = _encryption.Encrypt(cmd.NationalId),
            NationalId_Hash = nationalIdHash,
            Phone_Encrypted = hasPhone ? _encryption.Encrypt(cmd.Phone!) : null,
            Email_Encrypted = hasEmail ? _encryption.Encrypt(cmd.Email!) : null,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _professionals.Add(entity);

        // --- Publish outbox event in the same transaction (Requirement 1.6) ---
        await _outboxWriter.WriteAsync(
            cmd.TenantId,
            EventTypes.ProfessionalRegistered,
            new ProfessionalRegistered
            {
                ProfessionalId = entity.ProfessionalId,
                TenantId = cmd.TenantId,
                OccurredAt = DateTimeOffset.UtcNow
            },
            ct);

        // --- Commit both the Professional record and the outbox message atomically ---
        await _db.SaveChangesAsync(ct);

        return Result<RegisterProfessionalResult>.Ok(
            new RegisterProfessionalResult(entity.ProfessionalId, true));
    }

    // -----------------------------------------------------------------------
    // Issuer Onboarding (Requirements 2.1, 2.5, 2.6)
    // -----------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<Result<OnboardIssuerResult>> OnboardIssuerAsync(
        OnboardIssuerCommand cmd,
        CancellationToken ct)
    {
        // Case-insensitive uniqueness check per tenant (Requirement 2.5, 2.6)
        var existing = await _issuers.FindByNameAsync(cmd.OrganizationName, cmd.TenantId, ct);

        if (existing is not null)
            return Result<OnboardIssuerResult>.Conflict(
                IssuerMessages.AlreadyExists(existing.IssuerId));

        // Create Issuer with VerificationStatus = Pending (Requirement 2.1)
        var entity = new IssuerEntity
        {
            IssuerId = Guid.NewGuid(),
            TenantId = cmd.TenantId,
            OrganizationName = cmd.OrganizationName,
            Type = cmd.Type,
            VerificationStatus = IssuerStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _issuers.Add(entity);
        await _db.SaveChangesAsync(ct);

        return Result<OnboardIssuerResult>.Ok(new OnboardIssuerResult(entity.IssuerId));
    }

    // -----------------------------------------------------------------------
    // Issuer Approval (Requirements 2.2, 2.7, 2.8)
    // -----------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<Result<Guid>> ApproveIssuerAsync(
        ApproveIssuerCommand cmd,
        CancellationToken ct)
    {
        var issuer = await _issuers.FindByIdAsync(cmd.IssuerId, cmd.TenantId, ct);

        if (issuer is null)
            return Result<Guid>.NotFound(IssuerMessages.NotFound(cmd.IssuerId));

        // Guard against invalid state transition (Requirement 2.7)
        if (issuer.VerificationStatus == IssuerStatus.Verified)
            return Result<Guid>.Conflict(IssuerMessages.AlreadyVerified);

        var timestamp = DateTimeOffset.UtcNow;

        // Transition to Verified (Requirement 2.2)
        issuer.VerificationStatus = IssuerStatus.Verified;

        // Append-only AuditLog entry (Requirement 2.2)
        _auditLogs.Add(new AuditLogEntity
        {
            AuditId = Guid.NewGuid(),
            TenantId = cmd.TenantId,
            ActionType = AuditActions.IssuerApproved,
            EntityId = cmd.IssuerId.ToString(),
            Actor = cmd.AdminActorId,
            Timestamp = timestamp,
            RecordHash = ComputeAuditHash(AuditActions.IssuerApproved, cmd.IssuerId.ToString(), timestamp, cmd.AdminActorId, null)
        });

        // Publish IssuerApproved via outbox (Requirement 2.8)
        await _outboxWriter.WriteAsync(
            cmd.TenantId,
            EventTypes.IssuerApproved,
            new IssuerApproved
            {
                IssuerId = cmd.IssuerId,
                TenantId = cmd.TenantId,
                OccurredAt = timestamp
            },
            ct);

        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Ok(cmd.IssuerId);
    }

    // -----------------------------------------------------------------------
    // Issuer Rejection (Requirements 2.3, 2.7, 2.8)
    // -----------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<Result<Guid>> RejectIssuerAsync(
        RejectIssuerCommand cmd,
        CancellationToken ct)
    {
        var issuer = await _issuers.FindByIdAsync(cmd.IssuerId, cmd.TenantId, ct);

        if (issuer is null)
            return Result<Guid>.NotFound(IssuerMessages.NotFound(cmd.IssuerId));

        // Guard against invalid state transition (Requirement 2.7)
        if (issuer.VerificationStatus == IssuerStatus.Rejected)
            return Result<Guid>.Conflict(IssuerMessages.AlreadyRejected);

        var timestamp = DateTimeOffset.UtcNow;

        // Transition to Rejected (Requirement 2.3)
        issuer.VerificationStatus = IssuerStatus.Rejected;

        // Append-only AuditLog entry (Requirement 2.3)
        _auditLogs.Add(new AuditLogEntity
        {
            AuditId = Guid.NewGuid(),
            TenantId = cmd.TenantId,
            ActionType = AuditActions.IssuerRejected,
            EntityId = cmd.IssuerId.ToString(),
            Actor = cmd.AdminActorId,
            Timestamp = timestamp,
            Metadata = cmd.Reason,
            RecordHash = ComputeAuditHash(AuditActions.IssuerRejected, cmd.IssuerId.ToString(), timestamp, cmd.AdminActorId, cmd.Reason)
        });

        // Publish IssuerRejected via outbox (Requirement 2.8)
        await _outboxWriter.WriteAsync(
            cmd.TenantId,
            EventTypes.IssuerRejected,
            new IssuerRejected
            {
                IssuerId = cmd.IssuerId,
                TenantId = cmd.TenantId,
                OccurredAt = timestamp
            },
            ct);

        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Ok(cmd.IssuerId);
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private static string ComputeAuditHash(
        string actionType,
        string entityId,
        DateTimeOffset timestamp,
        string actor,
        string? metadata)
    {
        var raw = actionType
                  + entityId
                  + timestamp.ToString("O")
                  + actor
                  + (metadata ?? string.Empty);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
