namespace Certiva.Infrastructure.Constants;

public static class RedisKeys
{
    public static string AuthRateLimit(string ip)     => $"ratelimit:auth:{ip}";
    public static string VerifyRateLimit(string ip)   => $"ratelimit:verify:{ip}";
    public static string CertVerify(Guid tenantId, Guid certificateId) => $"cert:verify:{tenantId}:{certificateId}";
}
