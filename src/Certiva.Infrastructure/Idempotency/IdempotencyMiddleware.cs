using Certiva.Infrastructure.Constants;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Certiva.Infrastructure.Idempotency;

/// <summary>
/// ASP.NET Core middleware that enforces idempotency for mutating HTTP methods
/// (POST, PUT, PATCH) when the client supplies an <c>Idempotency-Key</c> header.
///
/// Behaviour:
/// <list type="bullet">
///   <item>No header → pass through unchanged.</item>
///   <item>Header present, key found in store → return cached JSON response (HTTP 200) and short-circuit.</item>
///   <item>Header present, key not found → buffer the downstream response; on 2xx, persist the result.</item>
/// </list>
/// </summary>
public sealed class IdempotencyMiddleware
{
    private const string IdempotencyKeyHeader = CertivaHeaderNames.IdempotencyKey;

    private static readonly HashSet<string> MutatingMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post,
        HttpMethods.Put,
        HttpMethods.Patch
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<IdempotencyMiddleware> _logger;

    public IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only intercept mutating methods that carry the idempotency header.
        if (!MutatingMethods.Contains(context.Request.Method)
            || !context.Request.Headers.TryGetValue(IdempotencyKeyHeader, out var keyValues)
            || string.IsNullOrWhiteSpace(keyValues.FirstOrDefault()))
        {
            await _next(context);
            return;
        }

        var idempotencyKey = keyValues.First()!.Trim();

        // TenantId must have been resolved by the tenant middleware upstream.
        // If it hasn't run yet (e.g., unauthenticated request), pass through.
        if (context.Items[HttpContextKeys.TenantId] is not Guid tenantId)
        {
            await _next(context);
            return;
        }

        // Resolve the repository from the request's DI scope.
        var repository = context.RequestServices.GetRequiredService<IIdempotencyKeyRepository>();

        // --- Check for a cached result ---
        var cached = await repository.GetResultAsync(idempotencyKey, tenantId, context.RequestAborted);
        if (cached is not null)
        {
            _logger.LogDebug(
                "Idempotency cache hit for key {Key} tenant {TenantId}",
                idempotencyKey, tenantId);

            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(cached, Encoding.UTF8, context.RequestAborted);
            return;
        }

        // --- Buffer the downstream response so we can capture it ---
        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await _next(context);
        }
        finally
        {
            // Always restore the original body stream.
            context.Response.Body = originalBody;
        }

        // Copy the buffered response to the real output stream.
        buffer.Seek(0, SeekOrigin.Begin);
        await buffer.CopyToAsync(originalBody, context.RequestAborted);

        // Persist the result only for successful (2xx) responses.
        var statusCode = context.Response.StatusCode;
        if (statusCode >= 200 && statusCode < 300)
        {
            buffer.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(buffer, Encoding.UTF8).ReadToEndAsync(context.RequestAborted);

            // Derive the operation type from the request path (best-effort).
            var operationType = $"{context.Request.Method}:{context.Request.Path}";

            try
            {
                await repository.StoreAsync(
                    idempotencyKey,
                    tenantId,
                    operationType,
                    responseBody,
                    context.RequestAborted);
            }
            catch (Exception ex)
            {
                // Storage failure must not break the response already sent to the client.
                _logger.LogWarning(ex,
                    "Failed to store idempotency result for key {Key} tenant {TenantId}",
                    idempotencyKey, tenantId);
            }
        }
    }
}
