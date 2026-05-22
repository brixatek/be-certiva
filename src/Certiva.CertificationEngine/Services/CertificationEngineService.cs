using Certiva.CertificationEngine.Commands;
using Certiva.CertificationEngine.Models;
using Certiva.CertificationEngine.Resources;
using Certiva.Infrastructure.Constants;
using Certiva.Infrastructure.Domain;
using Certiva.Infrastructure.Events;
using Certiva.Infrastructure.Idempotency;
using Certiva.Infrastructure.Outbox;
using Certiva.Infrastructure.Persistence;
using Certiva.Infrastructure.Persistence.Entities;
using Certiva.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Certiva.CertificationEngine.Services;

/// <summary>
/// Implements certificate issuance with full validation, hash chain computation,
/// transactional outbox, and idempotency support.
/// </summary>
public sealed class CertificationEngineService : ICertificationEngineService
{
    private readonly CertivaDbContext _db;
    private readonly IOutboxWriter _outboxWriter;
    private readonly ICertificateHashService _hashService;
    private readonly IIdempotencyKeyRepository _idempotencyRepo;
    private readonly IProfessionalRepository _professionals;
    private readonly IIssuerRepository _issuers;
    private readonly ICertificateTemplateRepository _templates;
    private readonly ICertificateRepository _certificates;
    private readonly IAuditLogRepository _auditLogs;
    private readonly ILogger<CertificationEngineService> _logger;

    private const string OperationType = OperationTypes.IssueCertificate;

    public CertificationEngineService(
        CertivaDbContext db,
        IOutboxWriter outboxWriter,
        ICertificateHashService hashService,
        IIdempotencyKeyRepository idempotencyRepo,
        IProfessionalRepository professionals,
        IIssuerRepository issuers,
        ICertificateTemplateRepository templates,
        ICertificateRepository certificates,
        IAuditLogRepository auditLogs,
        ILogger<CertificationEngineService> logger)
    {
        _db = db;
        _outboxWriter = outboxWriter;
        _hashService = hashService;
        _idempotencyRepo = idempotencyRepo;
        _professionals = professionals;
        _issuers = issuers;
        _templates = templates;
        _certificates = certificates;
        _auditLogs = auditLogs;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<IssueCertificateResult>> IssueCertificateAsync(
        IssueCertificateCommand cmd,
        CancellationToken ct)
    {
        // ── Step 1: Idempotency check ────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(cmd.IdempotencyKey))
        {
            var cached = await _idempotencyRepo.GetResultAsync(cmd.IdempotencyKey, cmd.TenantId, ct);
            if (cached is not null)
            {
                var cachedResult = JsonSerializer.Deserialize<IssueCertificateResult>(cached);
                if (cachedResult is not null)
                    return Result<IssueCertificateResult>.Ok(cachedResult);
            }
        }

        // ── Step 2: Load Professional ────────────────────────────────────────
        var professional = await _professionals.FindByIdAsync(cmd.ProfessionalId, cmd.TenantId, ct);

        if (professional is null)
            return Result<IssueCertificateResult>.NotFound(
                CertificateMessages.ProfessionalNotFound(cmd.ProfessionalId, cmd.TenantId));

        // ── Step 3: Load Template ────────────────────────────────────────────
        var template = await _templates.FindByIdAsync(cmd.TemplateId, cmd.TenantId, ct);

        if (template is null)
            return Result<IssueCertificateResult>.NotFound(
                CertificateMessages.TemplateNotFound(cmd.TemplateId, cmd.TenantId));

        // ── Step 4: Load Issuer ──────────────────────────────────────────────
        var issuer = await _issuers.FindByIdAsync(cmd.IssuerId, cmd.TenantId, ct);

        if (issuer is null)
            return Result<IssueCertificateResult>.NotFound(
                CertificateMessages.IssuerNotFound(cmd.IssuerId, cmd.TenantId));

        // ── Step 5: Template belongs to this Issuer ──────────────────────────
        if (template.IssuerId != cmd.IssuerId)
            return Result<IssueCertificateResult>.Forbidden(
                CertificateMessages.TemplateNotOwnedByIssuer);

        // ── Step 6: Issuer must be Verified ──────────────────────────────────
        if (issuer.VerificationStatus != IssuerStatus.Verified)
            return Result<IssueCertificateResult>.Forbidden(
                CertificateMessages.IssuerNotVerified);

        // ── Step 7: Cross-entity TenantId consistency ────────────────────────
        if (professional.TenantId != cmd.TenantId ||
            template.TenantId != cmd.TenantId ||
            issuer.TenantId != cmd.TenantId)
        {
            return Result<IssueCertificateResult>.Forbidden(
                CertificateMessages.CrossTenantMismatch);
        }

        // ── Step 8: Duplicate Active certificate check ───────────────────────
        var existing = await _certificates.FindActiveAsync(cmd.ProfessionalId, cmd.TemplateId, cmd.TenantId, ct);

        if (existing is not null)
            return Result<IssueCertificateResult>.Conflict(
                CertificateMessages.DuplicateActive(existing.CertificateId));

        // ── Step 9: Compute dates ────────────────────────────────────────────
        var issueDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var expiryDate = template.ValidityPeriodDays > 0
            ? issueDate.AddDays(template.ValidityPeriodDays)
            : (DateOnly?)null;

        // ── Step 10: Compute hash chain ──────────────────────────────────────
        var lastCert = await _certificates.FindLastByProfessionalAsync(cmd.ProfessionalId, cmd.TenantId, ct);
        var previousHash = lastCert?.CertificateHash ?? _hashService.GetGenesisHash();

        var newCertificateId = Guid.NewGuid();

        var fields = new CertificateFields(
            CertificateId: newCertificateId,
            ExpiryDate: expiryDate,
            IssuerId: cmd.IssuerId,
            IssuerName: issuer.OrganizationName,
            IssueDate: issueDate,
            Name: template.Name,
            ProfessionalId: cmd.ProfessionalId,
            TenantId: cmd.TenantId);

        var certificateHash = _hashService.ComputeHash(fields, previousHash);

        // ── Step 11: Create Certificate entity ───────────────────────────────
        var now = DateTimeOffset.UtcNow;
        var entity = new CertificateEntity
        {
            CertificateId = newCertificateId,
            TenantId = cmd.TenantId,
            ProfessionalId = cmd.ProfessionalId,
            IssuerId = cmd.IssuerId,
            TemplateId = cmd.TemplateId,
            Name = template.Name,
            Status = CertificateStatus.Active,
            IssueDate = issueDate,
            ExpiryDate = expiryDate,
            CertificateHash = certificateHash,
            CreatedAt = now,
            UpdatedAt = now
        };

        _certificates.Add(entity);

        // ── Step 12: Write outbox message ────────────────────────────────────
        await _outboxWriter.WriteAsync(
            cmd.TenantId,
            EventTypes.CertificateIssued,
            new CertificateIssued
            {
                EventId = Guid.NewGuid(),
                CertificateId = newCertificateId,
                ProfessionalId = cmd.ProfessionalId,
                IssuerId = cmd.IssuerId,
                TenantId = cmd.TenantId,
                SequenceNumber = 1,
                OccurredAt = now
            },
            ct);

        // ── Step 13: Persist in one transaction ──────────────────────────────
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Certificate operation: {OperationType} CertificateId={CertificateId} ProfessionalId={ProfessionalId} IssuerId={IssuerId} Timestamp={Timestamp}",
            OperationTypes.IssueCertificate, newCertificateId, cmd.ProfessionalId, cmd.IssuerId, now);

        // ── Step 14: Store idempotency result ────────────────────────────────
        var result = new IssueCertificateResult(newCertificateId, true);

        if (!string.IsNullOrWhiteSpace(cmd.IdempotencyKey))
        {
            var payload = JsonSerializer.Serialize(result);
            await _idempotencyRepo.StoreAsync(cmd.IdempotencyKey, cmd.TenantId, OperationType, payload, ct);
        }

        return Result<IssueCertificateResult>.Ok(result);
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> RevokeCertificateAsync(
        RevokeCertificateCommand cmd,
        CancellationToken ct)
    {
        // ── Step 1: Validate RevocationReason ────────────────────────────────
        if (string.IsNullOrWhiteSpace(cmd.RevocationReason) || cmd.RevocationReason.Length > 500)
            return Result<Guid>.UnprocessableEntity(CertificateMessages.RevocationReasonRequired);

        // ── Step 2: Load Certificate ─────────────────────────────────────────
        var certificate = await _certificates.FindByIdAsync(cmd.CertificateId, cmd.TenantId, ct);

        if (certificate is null)
            return Result<Guid>.NotFound(
                CertificateMessages.NotFound(cmd.CertificateId, cmd.TenantId));

        // ── Step 3: Validate Issuer ownership ───────────────────────────────
        if (certificate.IssuerId != cmd.IssuerId)
            return Result<Guid>.Forbidden(
                CertificateMessages.RevocationNotAuthorized);

        // ── Step 4: Validate current Status ─────────────────────────────────
        if (certificate.Status is CertificateStatus.Revoked or CertificateStatus.Expired)
            return Result<Guid>.Conflict(
                CertificateMessages.AlreadyTerminated(cmd.CertificateId, certificate.Status));

        // ── Step 5: Apply revocation ─────────────────────────────────────────
        var now = DateTimeOffset.UtcNow;
        certificate.Status = CertificateStatus.Revoked;
        certificate.RevocationReason = cmd.RevocationReason;
        certificate.RevokedAt = now;
        certificate.UpdatedAt = now;

        // ── Step 6: Build AuditLog entry ─────────────────────────────────────
        const string actionType = AuditActions.CertificateRevoked;
        var entityId = cmd.CertificateId.ToString();
        var actor = cmd.IssuerId.ToString();
        var metadataJson = JsonSerializer.Serialize(new { revocationReason = cmd.RevocationReason });
        var recordHash = ComputeAuditRecordHash(actionType, entityId, now, actor, metadataJson);

        _auditLogs.Add(new AuditLogEntity
        {
            AuditId = Guid.NewGuid(),
            TenantId = cmd.TenantId,
            ActionType = actionType,
            EntityId = entityId,
            Actor = actor,
            Timestamp = now,
            Metadata = metadataJson,
            RecordHash = recordHash
        });

        // ── Step 7: Write outbox message ─────────────────────────────────────
        await _outboxWriter.WriteAsync(
            cmd.TenantId,
            EventTypes.CertificateRevoked,
            new CertificateRevoked
            {
                EventId = Guid.NewGuid(),
                CertificateId = cmd.CertificateId,
                TenantId = cmd.TenantId,
                SequenceNumber = 1,
                OccurredAt = now
            },
            ct);

        // ── Step 8: Persist in one transaction ───────────────────────────────
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Certificate operation: {OperationType} CertificateId={CertificateId} ProfessionalId={ProfessionalId} IssuerId={IssuerId} Timestamp={Timestamp}",
            OperationTypes.RevokeCertificate, cmd.CertificateId, certificate.ProfessionalId, cmd.IssuerId, now);

        return Result<Guid>.Ok(cmd.CertificateId);
    }

    /// <inheritdoc />
    public async Task<Result<HashVerificationResult>> VerifyCertificateHashAsync(
        Guid certificateId,
        Guid tenantId,
        CancellationToken ct)
    {
        // ── Step 1: Load Certificate ─────────────────────────────────────────
        var certificate = await _certificates.FindByIdAsync(certificateId, tenantId, ct);

        if (certificate is null)
            return Result<HashVerificationResult>.NotFound(
                CertificateMessages.NotFound(certificateId, tenantId));

        // ── Step 2: Load Issuer to get IssuerName ────────────────────────────
        var issuer = await _issuers.FindByIdAsync(certificate.IssuerId, tenantId, ct);

        if (issuer is null)
            return Result<HashVerificationResult>.NotFound(
                CertificateMessages.IssuerNotFound(certificate.IssuerId, tenantId));

        // ── Step 3: Build CertificateFields from stored data ─────────────────
        var fields = new CertificateFields(
            CertificateId: certificate.CertificateId,
            ExpiryDate: certificate.ExpiryDate,
            IssuerId: certificate.IssuerId,
            IssuerName: issuer.OrganizationName,
            IssueDate: certificate.IssueDate,
            Name: certificate.Name,
            ProfessionalId: certificate.ProfessionalId,
            TenantId: certificate.TenantId);

        // ── Step 4: Resolve preceding hash ───────────────────────────────────
        var preceding = await _certificates.FindPrecedingAsync(
            certificate.ProfessionalId, tenantId, certificate.CreatedAt, ct);

        var previousHash = preceding?.CertificateHash ?? _hashService.GetGenesisHash();

        // ── Step 5: Recompute hash ────────────────────────────────────────────
        var recomputedHash = _hashService.ComputeHash(fields, previousHash);
        var storedHash = certificate.CertificateHash;

        // ── Step 6: Compare ───────────────────────────────────────────────────
        if (string.Equals(storedHash, recomputedHash, StringComparison.OrdinalIgnoreCase))
        {
            return Result<HashVerificationResult>.Ok(
                new HashVerificationResult(certificateId, true, storedHash, recomputedHash));
        }

        // ── Step 7: Mismatch — mark Tampered and write AuditLog ──────────────
        var now = DateTimeOffset.UtcNow;
        certificate.Status = CertificateStatus.Tampered;
        certificate.UpdatedAt = now;

        const string actionType = AuditActions.TamperDetected;
        var entityId = certificateId.ToString();
        const string actor = Actors.System;
        var metadataJson = JsonSerializer.Serialize(new
        {
            storedHash,
            recomputedHash,
            detectionTimestamp = now.ToString("O")
        });
        var recordHash = ComputeAuditRecordHash(actionType, entityId, now, actor, metadataJson);

        _auditLogs.Add(new AuditLogEntity
        {
            AuditId = Guid.NewGuid(),
            TenantId = tenantId,
            ActionType = actionType,
            EntityId = entityId,
            Actor = actor,
            Timestamp = now,
            Metadata = metadataJson,
            RecordHash = recordHash
        });

        await _db.SaveChangesAsync(ct);

        return Result<HashVerificationResult>.Ok(
            new HashVerificationResult(certificateId, false, storedHash, recomputedHash));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string ComputeAuditRecordHash(
        string actionType,
        string entityId,
        DateTimeOffset timestamp,
        string actor,
        string metadataJson)
    {
        var raw = actionType + entityId + timestamp.ToString("O") + actor + metadataJson;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
