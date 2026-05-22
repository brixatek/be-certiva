using Certiva.Infrastructure.Constants;
using Certiva.Infrastructure.Domain;
using Certiva.Infrastructure.Persistence;
using Certiva.Infrastructure.Persistence.Entities;
using Certiva.Infrastructure.Persistence.Repositories;
using Certiva.Infrastructure.Redis;
using Certiva.VerificationEngine.Resources;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Certiva.VerificationEngine.Services;

/// <summary>
/// Resolves certificate verification with Redis/PostgreSQL fallback,
/// VerificationLog recording, rate limiting, and latency-exceeded logging.
/// </summary>
public sealed class VerificationEngineService : IVerificationEngineService
{
    private const int RateLimitMaxRequests = 100;
    private const int RateLimitWindowSeconds = 60;
    private const int ResyncDeadlineSeconds = 300; // 5 minutes

    private readonly CertivaDbContext _db;
    private readonly IVerificationStoreRepository _verificationStore;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<VerificationEngineService> _logger;
    private readonly ICertificateRepository _certificates;
    private readonly IVerificationLogRepository _verificationLogs;

    public VerificationEngineService(
        CertivaDbContext db,
        IVerificationStoreRepository verificationStore,
        IConnectionMultiplexer redis,
        ILogger<VerificationEngineService> logger,
        ICertificateRepository certificates,
        IVerificationLogRepository verificationLogs)
    {
        _db = db;
        _verificationStore = verificationStore;
        _redis = redis;
        _logger = logger;
        _certificates = certificates;
        _verificationLogs = verificationLogs;
    }

    /// <inheritdoc/>
    public async Task<Result<VerificationResult>> VerifyCertificateAsync(
        Guid certificateId,
        string requestingIp,
        CancellationToken ct)
    {
        // ── Rate limiting: 100 req/min/IP ────────────────────────────────────
        if (!await CheckRateLimitAsync(requestingIp, ct))
            return Result<VerificationResult>.TooManyRequests(VerificationMessages.RateLimitExceeded);

        var now = DateTimeOffset.UtcNow;
        VerificationResult? result = null;
        string source = "postgresql";

        // ── Redis path ───────────────────────────────────────────────────────
        var redisAvailable = await _verificationStore.IsAvailableAsync(ct);
        if (redisAvailable)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Redis does not have a TenantId in the lookup key for public verification.
            // We resolve via a full-scan search: try to get by certificateId from known tenants.
            // For public verification, we do a DB lookup just to get the TenantId, then Redis lookup.
            var tenantId = await _certificates.FindTenantIdAsync(certificateId, ct);

            if (tenantId.HasValue)
            {
                var view = await _verificationStore.GetAsync(certificateId, tenantId.Value, ct);
                sw.Stop();

                if (sw.ElapsedMilliseconds > 50)
                {
                    _logger.LogWarning(
                        "VerificationEngine: Redis latency exceeded 50ms for CertificateId={CertificateId} (elapsed={Elapsed}ms).",
                        certificateId, sw.ElapsedMilliseconds);
                }

                if (view is not null)
                {
                    result = MapViewToResult(view);
                    source = "redis";
                }
            }
        }

        // ── PostgreSQL fallback ──────────────────────────────────────────────
        if (result is null)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            result = await LoadFromPostgresAsync(certificateId, ct);
            sw.Stop();

            if (sw.ElapsedMilliseconds > 500)
            {
                _logger.LogWarning(
                    "VerificationEngine: PostgreSQL latency exceeded 500ms for CertificateId={CertificateId} (elapsed={Elapsed}ms).",
                    certificateId, sw.ElapsedMilliseconds);
            }

            // Repopulate Redis on fallback hit
            if (result is not null && redisAvailable)
            {
                var tenantId = await _certificates.FindTenantIdAsync(certificateId, ct);
                if (tenantId.HasValue)
                {
                    await _verificationStore.UpsertAsync(new CertificateVerificationView
                    {
                        CertificateId = result.CertificateId,
                        TenantId = tenantId.Value,
                        Status = result.Status,
                        ExpiryDate = result.ExpiryDate,
                        ProfessionalName = result.ProfessionalName,
                        IssuerName = result.IssuerName,
                        QrCodeUrl = result.QrCodeUrl
                    }, ct);
                }
            }
        }

        // ── Record VerificationLog ───────────────────────────────────────────
        var statusReturned = result?.Status ?? "NotFound";
        _verificationLogs.Add(new VerificationLogEntity
        {
            LogId = Guid.NewGuid(),
            TenantId = result is not null
                ? await _certificates.FindTenantIdAsync(certificateId, ct) ?? Guid.Empty
                : Guid.Empty,
            CertificateId = certificateId,
            RequestingIp = requestingIp.Length > 45 ? requestingIp[..45] : requestingIp,
            StatusReturned = statusReturned,
            Timestamp = now
        });

        await _db.SaveChangesAsync(ct);

        // ── Not found ────────────────────────────────────────────────────────
        if (result is null)
        {
            _logger.LogInformation(
                "VerificationEngine: Certificate {CertificateId} not found. IP={Ip}.",
                certificateId, requestingIp);
            return Result<VerificationResult>.NotFound(VerificationMessages.CertificateNotFound(certificateId));
        }

        return Result<VerificationResult>.Ok(result with { Source = source });
    }

    /// <inheritdoc/>
    public async Task ResynchronizeVerificationStoreAsync(CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(ResyncDeadlineSeconds));

        _logger.LogInformation("VerificationEngine: Starting Redis Verification_Store resynchronization.");

        int upserted = 0;
        const int batchSize = 500;
        Guid lastId = Guid.Empty;

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var batch = await _certificates.GetBatchAsync(lastId, batchSize, cts.Token);

                if (batch.Count == 0) break;

                foreach (var cert in batch)
                {
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

                    await _verificationStore.UpsertAsync(view, cts.Token);
                    upserted++;
                }

                lastId = batch[^1].CertificateId;

                if (batch.Count < batchSize) break;
            }

            _logger.LogInformation(
                "VerificationEngine: Resynchronization complete. Upserted {Count} records.", upserted);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "VerificationEngine: Resynchronization hit 5-minute deadline after {Count} records. Resuming Redis reads.", upserted);
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task<bool> CheckRateLimitAsync(string ip, CancellationToken ct)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = RedisKeys.VerifyRateLimit(ip);
            var count = await db.StringIncrementAsync(key).WaitAsync(ct);
            if (count == 1)
                await db.KeyExpireAsync(key, TimeSpan.FromSeconds(RateLimitWindowSeconds)).WaitAsync(ct);
            return count <= RateLimitMaxRequests;
        }
        catch
        {
            // If Redis is unavailable, allow the request (fail-open)
            return true;
        }
    }

    private async Task<VerificationResult?> LoadFromPostgresAsync(Guid certificateId, CancellationToken ct)
    {
        var cert = await _certificates.FindByIdWithNavigationsAsync(certificateId, ct);

        if (cert is null) return null;

        return new VerificationResult
        {
            Valid = cert.Status == CertificateStatus.Active,
            CertificateId = cert.CertificateId,
            Status = cert.Status,
            ProfessionalName = cert.Professional.Name,
            CertificateName = cert.Name,
            IssuerName = cert.Issuer.OrganizationName,
            IssueDate = cert.IssueDate,
            ExpiryDate = cert.ExpiryDate,
            QrCodeUrl = cert.QRCodeUrl,
            Source = "postgresql"
        };
    }

    private static VerificationResult MapViewToResult(CertificateVerificationView view)
    {
        return new VerificationResult
        {
            Valid = view.Status == CertificateStatus.Active,
            CertificateId = view.CertificateId,
            Status = view.Status,
            ProfessionalName = view.ProfessionalName,
            CertificateName = string.Empty, // not stored in Redis view
            IssuerName = view.IssuerName,
            IssueDate = default, // not stored in Redis view — enriched from PG if needed
            ExpiryDate = view.ExpiryDate,
            QrCodeUrl = view.QrCodeUrl,
            Source = "redis"
        };
    }
}
