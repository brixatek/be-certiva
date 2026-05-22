namespace Certiva.NotificationSystem.Services;

/// <summary>
/// Abstraction for sending email notifications.
/// The implementation can be swapped for SendGrid, SMTP, etc.
/// </summary>
public interface IEmailDispatchService
{
    Task SendAsync(string toEmail, string subject, string body, CancellationToken ct);
}
