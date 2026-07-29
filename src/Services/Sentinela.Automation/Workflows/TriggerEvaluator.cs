using System.Text.Json;
using Sentinela.Shared.Domain.Automation;

namespace Sentinela.Automation.Workflows;

public interface ITriggerEvaluator
{
    Task<bool> MatchesTrigger(Workflow workflow, object triggerEvent);
}

public class TriggerEvaluator : ITriggerEvaluator
{
    public Task<bool> MatchesTrigger(Workflow workflow, object triggerEvent)
    {
        var triggerType = triggerEvent.GetType().Name;

        var workflowTriggerType = workflow.TriggerType;

        var matches = string.Equals(workflowTriggerType, triggerType, StringComparison.OrdinalIgnoreCase)
            || string.Equals(workflowTriggerType, "Any", StringComparison.OrdinalIgnoreCase)
            || workflowTriggerType.Contains(triggerType, StringComparison.OrdinalIgnoreCase);

        if (matches && !string.IsNullOrEmpty(workflow.TriggerConfig))
        {
            matches = CheckTriggerConfig(workflow.TriggerConfig, triggerEvent);
        }

        return Task.FromResult(matches);
    }

    private bool CheckTriggerConfig(string config, object triggerEvent)
    {
        try
        {
            var configObj = JsonSerializer.Deserialize<JsonElement>(config);
            if (configObj.TryGetProperty("EventType", out var eventType))
            {
                var actualEventType = triggerEvent.GetType().Name;
                if (!eventType.GetString()?.Equals(actualEventType, StringComparison.OrdinalIgnoreCase) ?? false)
                    return false;
            }
            if (configObj.TryGetProperty("Severity", out var severity))
            {
                var severityProp = triggerEvent.GetType().GetProperty("Severity");
                if (severityProp != null)
                {
                    var actualSeverity = severityProp.GetValue(triggerEvent)?.ToString();
                    if (!severity.GetString()?.Equals(actualSeverity, StringComparison.OrdinalIgnoreCase) ?? false)
                        return false;
                }
            }
            return true;
        }
        catch
        {
            return true;
        }
    }
}
