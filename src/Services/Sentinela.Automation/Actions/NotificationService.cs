using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sentinela.Automation.Workflows;

namespace Sentinela.Automation.Actions;

public interface INotificationService
{
    Task<ActionResult> SendEmailAsync(string config, object triggerEvent);
    Task<ActionResult> SendTeamsMessageAsync(string config, object triggerEvent);
    Task<ActionResult> SendSlackMessageAsync(string config, object triggerEvent);
}

public class NotificationService : INotificationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(HttpClient httpClient, ILogger<NotificationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ActionResult> SendEmailAsync(string config, object triggerEvent)
    {
        try
        {
            var cfg = JsonSerializer.Deserialize<JsonElement>(config);
            return new ActionResult { ActionType = "SendEmail", Success = true };
        }
        catch (Exception ex)
        {
            return new ActionResult { ActionType = "SendEmail", Success = false, Error = ex.Message };
        }
    }

    public async Task<ActionResult> SendTeamsMessageAsync(string config, object triggerEvent)
    {
        try
        {
            var cfg = JsonSerializer.Deserialize<JsonElement>(config);
            var webhookUrl = cfg.GetProperty("WebhookUrl").GetString() ?? "";
            var message = cfg.GetProperty("Message").GetString() ?? "Alert notification";

            var payload = new
            {
                text = $"Sentinela Alert\n\n{message}\n\nTime: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC"
            };

            var response = await _httpClient.PostAsync(webhookUrl,
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

            return new ActionResult
            {
                ActionType = "SendTeams",
                Success = response.IsSuccessStatusCode,
                Output = $"HTTP {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            return new ActionResult { ActionType = "SendTeams", Success = false, Error = ex.Message };
        }
    }

    public async Task<ActionResult> SendSlackMessageAsync(string config, object triggerEvent)
    {
        try
        {
            var cfg = JsonSerializer.Deserialize<JsonElement>(config);
            var webhookUrl = cfg.GetProperty("WebhookUrl").GetString() ?? "";
            var message = cfg.GetProperty("Message").GetString() ?? "Alert notification";

            var payload = new
            {
                text = $"*Sentinela Alert*\n{message}\n_Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC_",
                mrkdwn = true
            };

            var response = await _httpClient.PostAsync(webhookUrl,
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

            return new ActionResult
            {
                ActionType = "SendSlack",
                Success = response.IsSuccessStatusCode,
                Output = $"HTTP {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            return new ActionResult { ActionType = "SendSlack", Success = false, Error = ex.Message };
        }
    }
}
