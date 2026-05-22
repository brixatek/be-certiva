using System.Text.Json;
using Certiva.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Certiva.Infrastructure.Outbox;

/// <summary>
/// Background service that polls the <c>OutboxMessages</c> table for unpublished messages,
/// publishes each one to the MassTransit event bus, and marks it as published.
/// Polling interval: 5 seconds. Batch size: 100 messages per iteration.
/// </summary>
public sealed class OutboxRelayWorker : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 100;

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxRelayWorker> _logger;

    public OutboxRelayWorker(IServiceScopeFactory scopeFactory, ILogger<OutboxRelayWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxRelayWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown — exit the loop.
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in OutboxRelayWorker polling loop.");
            }

            await Task.Delay(PollingInterval, stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("OutboxRelayWorker stopped.");
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<CertivaDbContext>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var messages = await db.OutboxMessages
            .Where(m => !m.Published)
            .OrderBy(m => m.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (messages.Count == 0)
            return;

        _logger.LogDebug("OutboxRelayWorker: processing {Count} message(s).", messages.Count);

        foreach (var message in messages)
        {
            try
            {
                // Resolve the CLR type from the EventType name so MassTransit can route correctly.
                // Fall back to publishing as a plain Dictionary when the type cannot be resolved.
                var messageType = ResolveMessageType(message.EventType);

                object deserialized = messageType is not null
                    ? JsonSerializer.Deserialize(message.Payload, messageType, _jsonOptions)!
                    : JsonSerializer.Deserialize<Dictionary<string, object>>(message.Payload, _jsonOptions)!;

                await publishEndpoint.Publish(deserialized, deserialized.GetType(), ct);

                message.Published = true;
                message.PublishedAt = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "OutboxRelayWorker: failed to publish message {MessageId} (EventType={EventType}). Skipping.",
                    message.MessageId,
                    message.EventType);
                // Continue processing remaining messages in the batch.
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Attempts to resolve a CLR <see cref="Type"/> from the event type name.
    /// Searches all loaded assemblies for a type whose short name matches <paramref name="eventType"/>.
    /// Returns <c>null</c> if no match is found.
    /// </summary>
    private static Type? ResolveMessageType(string eventType)
    {
        // Try exact full-name match first, then short-name match across loaded assemblies.
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch { return Array.Empty<Type>(); }
            })
            .FirstOrDefault(t => t.Name == eventType || t.FullName == eventType);
    }
}
