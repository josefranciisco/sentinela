using System.Diagnostics;
using System.Management;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sentinela.Automation.Workflows;
using Sentinela.Shared.Core.Interfaces;
using Sentinela.Shared.Domain.Automation;
using Sentinela.Shared.Domain.Monitoring;
using Serilog;

namespace Sentinela.Automation.Actions;

public interface IActionExecutor
{
    Task<ActionResult> ExecuteAction(WorkflowAction action, object triggerEvent);
}

public class ActionExecutor : IActionExecutor
{
    private readonly IScriptExecutor _scriptExecutor;
    private readonly INotificationService _notificationService;
    private readonly IEventBus _eventBus;
    private readonly ILogger<ActionExecutor> _logger;
    private readonly AutomationOptions _options;

    public ActionExecutor(
        IScriptExecutor scriptExecutor,
        INotificationService notificationService,
        IEventBus eventBus,
        IOptions<AutomationOptions> options,
        ILogger<ActionExecutor> logger)
    {
        _scriptExecutor = scriptExecutor;
        _notificationService = notificationService;
        _eventBus = eventBus;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ActionResult> ExecuteAction(WorkflowAction action, object triggerEvent)
    {
        var result = new ActionResult { ActionType = action.Type.ToString() };
        var startTime = DateTime.UtcNow;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.MaxExecutionTimeSeconds));

            result = action.Type switch
            {
                ActionType.SendAlert => await SendAlertAction(action, triggerEvent),
                ActionType.ExecuteScript => await _scriptExecutor.ExecuteScriptAsync(action.Config, cts.Token),
                ActionType.OpenTicket => await OpenTicketAction(action, triggerEvent),
                ActionType.SendEmail => await _notificationService.SendEmailAsync(action.Config, triggerEvent),
                ActionType.SendTeams => await _notificationService.SendTeamsMessageAsync(action.Config, triggerEvent),
                ActionType.SendSlack => await _notificationService.SendSlackMessageAsync(action.Config, triggerEvent),
                ActionType.BlockUSB => await BlockUsbAction(action),
                ActionType.RunPowerShell => await ExecutePowerShellAction(action, cts.Token),
                ActionType.SendWebhook => await SendWebhookAction(action, triggerEvent),
                ActionType.RestartService => await RestartServiceAction(action),
                _ => new ActionResult { ActionType = action.Type.ToString(), Success = false, Error = "Unknown action type" }
            };

            result.Duration = DateTime.UtcNow - startTime;

            _logger.LogInformation("Action {Type} executed: Success={Success}, Duration={Duration}ms",
                action.Type, result.Success, result.Duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            _logger.LogError(ex, "Action {Type} failed", action.Type);
        }

        return result;
    }

    private async Task<ActionResult> SendAlertAction(WorkflowAction action, object triggerEvent)
    {
        await _eventBus.PublishAsync(new AutomationAlertEvent
        {
            Title = "Workflow Alert",
            Description = action.Config,
            Timestamp = DateTime.UtcNow
        });

        return new ActionResult { ActionType = "SendAlert", Success = true };
    }

    private async Task<ActionResult> OpenTicketAction(WorkflowAction action, object triggerEvent)
    {
        return await SendWebhookAction(action, triggerEvent);
    }

    private async Task<ActionResult> BlockUsbAction(WorkflowAction action)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Services\UsbStor");
            key.SetValue("Start", 4, Microsoft.Win32.RegistryValueKind.DWord);
            return new ActionResult { ActionType = "BlockUSB", Success = true, Output = "USB storage disabled" };
        }
        catch (Exception ex)
        {
            return new ActionResult { ActionType = "BlockUSB", Success = false, Error = ex.Message };
        }
    }

    private async Task<ActionResult> ExecutePowerShellAction(WorkflowAction action, CancellationToken ct)
    {
        var config = JsonSerializer.Deserialize<JsonElement>(action.Config);
        var script = config.GetProperty("Script").GetString() ?? "";

        return await _scriptExecutor.ExecutePowerShellAsync(script, ct);
    }

    private async Task<ActionResult> SendWebhookAction(WorkflowAction action, object triggerEvent)
    {
        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var config = JsonSerializer.Deserialize<JsonElement>(action.Config);
            var url = config.GetProperty("Url").GetString() ?? "";
            var payload = config.GetProperty("Payload").GetString() ?? "{}";

            var response = await httpClient.PostAsync(url, new StringContent(payload, Encoding.UTF8, "application/json"));
            return new ActionResult
            {
                ActionType = "SendWebhook",
                Success = response.IsSuccessStatusCode,
                Output = $"Status: {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            return new ActionResult { ActionType = "SendWebhook", Success = false, Error = ex.Message };
        }
    }

    private async Task<ActionResult> RestartServiceAction(WorkflowAction action)
    {
        try
        {
            var config = JsonSerializer.Deserialize<JsonElement>(action.Config);
            var serviceName = config.GetProperty("ServiceName").GetString() ?? "";

            using var sc = new ServiceController(serviceName);
            if (sc.Status != ServiceControllerStatus.Stopped)
            {
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
            }
            sc.Start();
            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));

            return new ActionResult { ActionType = "RestartService", Success = true, Output = $"Service {serviceName} restarted" };
        }
        catch (Exception ex)
        {
            return new ActionResult { ActionType = "RestartService", Success = false, Error = ex.Message };
        }
    }
}
