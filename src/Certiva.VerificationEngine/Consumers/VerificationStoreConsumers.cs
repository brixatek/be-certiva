using Certiva.Infrastructure.Events;
using Certiva.Infrastructure.Persistence;
using Certiva.Infrastructure.Persistence.Entities;
using Certiva.Infrastructure.Persistence.Repositories;
using Certiva.Infrastructure.Redis;
using MassTransit;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace Certiva.VerificationEngine.Consumers;

/// <summary>
/// Updates the Redis Verification_Store when a certificate is issued.
/// Enforces sequence number ordering: applies only when SequenceNumber == lastApplied + 1.
/// </summary>
public sealed class VerificationCertificateIssuedConsumer : IConsumer<CertificateIssued>
{
    private readonly IVerificationStoreRepository _store;
    private readonly CertivaDbContext _db;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<VerificationCertificateIssuedConsumer> _logger;
    private readonly ICertificateRepository _certificates;
    private readonly IAuditLogRepository _auditLogs;

    public VerificationCertificateIssuedConsumer(
        IVerificationStoreRepository store,
        CertivaDbContext db,
        IConnectionMultiplexer redis,
        ILogger<VerificationCertificateIssuedConsumer> logger,
        ICertificateRepository certificates,
        IAuditLogRepository auditLogs)
    {
        _store = store;
        _db = db;
        _redis = redis;
        _logger = logger;
        _certificates = certificates;
        _auditLogs = auditLogs;
    }

    public async Task Consume(ConsumeContext<CertificateIssued> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        if (!await CheckAndAdvanceSequenceAsync(msg.TenantId, msg.CertificateId, msg.SequenceNumber, ct))
        {
            await WriteOutOfOrderAuditAsync(msg.TenantId, msg.CertificateId, msg.SequenceNumber, "CertificateIssued", ct);
            return;
        }

        var cert = await _certificates.FindByIdWithNavigationsAsync(msg.CertificateId, ct);

        if (cert is null || cert.TenantId != msg.TenantId) return;

        var view = new CertificateVerificationView
        {
            CertificateId = cert.CertificateId,
            TenantId = cert.TenantId,
            Status = cert.Status,
            ExpiryDate = cert.ExpiryDate,
            ProfessionalName = cert.Professional.Name,
            IssuerName = cert.Issuer.OrganizationName,
            QrCodeUrl = cert.QRCodeUrl
        };

        await _store.UpsertAsync(view, ct);
    }

    private async Task<bool> CheckAndAdvanceSequenceAsync(Guid tenantId, Guid certId, long seq, CancellationToken ct)
    {
        var redisDb = _redis.GetDatabase();
        var seqKey = $"seq:{tenantId}:{certId}";

        var last = await redisDb.StringGetAsync(seqKey).WaitAsync(ct);
        long lastSeq = last.HasValue ? (long)last : 0;

        if (seq != lastSeq + 1) return false;

        await redisDb.StringSetAsync(seqKey, seq, TimeSpan.FromHours(25)).WaitAsync(ct);
        return true;
    }

    private async Task WriteOutOfOrderAuditAsync(Guid tenantId, Guid certId, long seq, string eventType, CancellationToken ct)
    {
        _logger.LogWarning(
            "VerificationEngine: Out-of-order event discarded. CertificateId={CertId}, EventType={Type}, SequenceNumber={Seq}.",
            certId, eventType, seq);

        var redisDb = _redis.GetDatabase();
        var seqKey = $"seq:{tenantId}:{certId}";
        var lastRaw = await redisDb.StringGetAsync(seqKey).WaitAsync(ct);
        long lastSeq = lastRaw.HasValue ? (long)lastRaw : 0;

        var metadata = JsonSerializer.Serialize(new
        {
            expectedSequence = lastSeq + 1,
            receivedSequence = seq,
            eventType
        });

        var now = DateTimeOffset.UtcNow;
        var raw = "OutOfOrderEvent" + certId.ToString() + now.ToString("O") + "System" + metadata;
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();

        _auditLogs.Add(new AuditLogEntity
        {
            AuditId = Guid.NewGuid(),
            TenantId = tenantId,
            ActionType = "OutOfOrderEvent",
            EntityId = certId.ToString(),
            Actor = "System",
            Timestamp = now,
            Metadata = metadata,
            RecordHash = hash
        });

        await _db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Updates the Redis Verification_Store when a certificate is revoked.
/// </summary>
public sealed class VerificationCertificateRevokedConsumer : IConsumer<CertificateRevoked>
{
    private readonly IVerificationStoreRepository _store;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<VerificationCertificateRevokedConsumer> _logger;

    public VerificationCertificateRevokedConsumer(
        IVerificationStoreRepository store,
        IConnectionMultiplexer redis,
        ILogger<VerificationCertificateRevokedConsumer> logger)
    {
        _store = store;
        _redis = redis;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CertificateRevoked> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        if (!await CheckAndAdvanceSequenceAsync(msg.TenantId, msg.CertificateId, msg.SequenceNumber, ct))
        {
            _logger.LogWarning(
                "VerificationEngine: Out-of-order CertificateRevoked discarded. CertificateId={CertId}.", msg.CertificateId);
            return;
        }

        var existing = await _store.GetAsync(msg.CertificateId, msg.TenantId, ct);
        if (existing is null) return;

        await _store.UpsertAsync(existing with { Status = "Revoked" }, ct);
    }

    private async Task<bool> CheckAndAdvanceSequenceAsync(Guid tenantId, Guid certId, long seq, CancellationToken ct)
    {
        var redisDb = _redis.GetDatabase();
        var seqKey = $"seq:{tenantId}:{certId}";
        var last = await redisDb.StringGetAsync(seqKey).WaitAsync(ct);
        long lastSeq = last.HasValue ? (long)last : 0;
        if (seq != lastSeq + 1) return false;
        await redisDb.StringSetAsync(seqKey, seq, TimeSpan.FromHours(25)).WaitAsync(ct);
        return true;
    }
}

/// <summary>
/// Updates the Redis Verification_Store when a certificate expires.
/// </summary>
public sealed class VerificationCertificateExpiredConsumer : IConsumer<CertificateExpired>
{
    private readonly IVerificationStoreRepository _store;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<VerificationCertificateExpiredConsumer> _logger;

    public VerificationCertificateExpiredConsumer(
        IVerificationStoreRepository store,
        IConnectionMultiplexer redis,
        ILogger<VerificationCertificateExpiredConsumer> logger)
    {
        _store = store;
        _redis = redis;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CertificateExpired> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        if (!await CheckAndAdvanceSequenceAsync(msg.TenantId, msg.CertificateId, msg.SequenceNumber, ct))
        {
            _logger.LogWarning(
                "VerificationEngine: Out-of-order CertificateExpired discarded. CertificateId={CertId}.", msg.CertificateId);
            return;
        }

        var existing = await _store.GetAsync(msg.CertificateId, msg.TenantId, ct);
        if (existing is null) return;

        await _store.UpsertAsync(existing with { Status = "Expired" }, ct);
    }

    private async Task<bool> CheckAndAdvanceSequenceAsync(Guid tenantId, Guid certId, long seq, CancellationToken ct)
    {
        var redisDb = _redis.GetDatabase();
        var seqKey = $"seq:{tenantId}:{certId}";
        var last = await redisDb.StringGetAsync(seqKey).WaitAsync(ct);
        long lastSeq = last.HasValue ? (long)last : 0;
        if (seq != lastSeq + 1) return false;
        await redisDb.StringSetAsync(seqKey, seq, TimeSpan.FromHours(25)).WaitAsync(ct);
        return true;
    }
}
