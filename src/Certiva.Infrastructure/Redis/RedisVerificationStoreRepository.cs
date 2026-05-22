using Certiva.Infrastructure.Constants;
using System.Text.Json;
using StackExchange.Redis;

namespace Certiva.Infrastructure.Redis;

/// <summary>
/// Redis-backed implementation of <see cref="IVerificationStoreRepository"/>.
/// Key format: cert:verify:{tenantId}:{certificateId}
/// TTL: 3600 seconds (1 hour), reset on every write.
/// </summary>
public sealed class RedisVerificationStoreRepository : IVerificationStoreRepository
{
    private const int TtlSeconds = 3600;

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _connection;

    public RedisVerificationStoreRepository(IConnectionMultiplexer connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    // -----------------------------------------------------------------------
    // IVerificationStoreRepository
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<CertificateVerificationView?> GetAsync(
        Guid certificateId,
        Guid tenantId,
        CancellationToken ct)
    {
        var db = _connection.GetDatabase();
        var key = BuildKey(tenantId, certificateId);

        RedisValue value = await db.StringGetAsync(key).WaitAsync(ct);

        if (value.IsNullOrEmpty)
            return null;

        return JsonSerializer.Deserialize<CertificateVerificationView>(value!, _jsonOptions);
    }

    /// <inheritdoc/>
    public async Task UpsertAsync(CertificateVerificationView view, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(view);

        var db = _connection.GetDatabase();
        var key = BuildKey(view.TenantId, view.CertificateId);
        var json = JsonSerializer.Serialize(view, _jsonOptions);

        await db.StringSetAsync(key, json, TimeSpan.FromSeconds(TtlSeconds)).WaitAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        try
        {
            // Use the first available server to send a PING.
            var server = _connection.GetServers().FirstOrDefault(s => s.IsConnected);
            if (server is null)
                return false;

            await server.PingAsync().WaitAsync(ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static string BuildKey(Guid tenantId, Guid certificateId)
        => RedisKeys.CertVerify(tenantId, certificateId);
}
