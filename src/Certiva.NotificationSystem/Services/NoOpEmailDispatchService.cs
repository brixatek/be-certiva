using Microsoft.Extensions.Logging;

namespace Certiva.NotificationSystem.Services;

/// <summary>
/// No-op email dispatch service for Phase 1.
/// Logs the email instead of sending it. Replace with a real implementation (SendGrid, SMTP, etc.).
/// </summary>
public sealed class NoOpEmailDispatchService : IEmailDispatchService
{
    private readonly ILogger<NoOpEmailDispatchService> _logger;

    public NoOpEmailDispatchService(ILogger<NoOpEmailDispatchService> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct)
    {
        _logger.LogInformation(
            "[EMAIL] To={To} Subject={Subject} Body={Body}",
            toEmail, subject, body[..Math.Min(body.Length, 200)]);
        return Task.CompletedTask;
    }
}
