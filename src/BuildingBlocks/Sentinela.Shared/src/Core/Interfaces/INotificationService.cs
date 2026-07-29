namespace Sentinela.Shared.Core.Interfaces;

public interface INotificationService
{
    Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
    Task SendTeamsAsync(string webhookUrl, string title, string message, CancellationToken cancellationToken = default);
    Task SendSlackAsync(string webhookUrl, string channel, string message, CancellationToken cancellationToken = default);
    Task SendWebhookAsync(string url, object payload, CancellationToken cancellationToken = default);
    Task SendPushAsync(string deviceToken, string title, string body, CancellationToken cancellationToken = default);
}
