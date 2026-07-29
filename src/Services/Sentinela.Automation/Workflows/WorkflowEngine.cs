using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sentinela.Automation.Actions;
using Sentinela.Shared.Core.Interfaces;
using Sentinela.Shared.Domain.Automation;
using Serilog;
using ConditionOperator = Sentinela.Shared.Domain.Automation.WorkflowCondition.ComparisonOperator;

namespace Sentinela.Automation.Workflows;

public interface IWorkflowEngine
{
    Task<IReadOnlyList<Workflow>> GetApplicableWorkflows(object triggerEvent);
    Task<WorkflowExecutionResult> ExecuteWorkflow(Workflow workflow, object triggerEvent);
    Task<bool> EvaluateConditions(Workflow workflow, object triggerEvent);
}

public class WorkflowEngine : IWorkflowEngine
{
    private readonly IRepository<Workflow> _workflowRepo;
    private readonly ICacheService _cache;
    private readonly IActionExecutor _actionExecutor;
    private readonly ITriggerEvaluator _triggerEvaluator;
    private readonly ILogger<WorkflowEngine> _logger;
    private readonly AutomationOptions _options;

    public WorkflowEngine(
        IRepository<Workflow> workflowRepo,
        ICacheService cache,
        IActionExecutor actionExecutor,
        ITriggerEvaluator triggerEvaluator,
        IOptions<AutomationOptions> options,
        ILogger<WorkflowEngine> logger)
    {
        _workflowRepo = workflowRepo;
        _cache = cache;
        _actionExecutor = actionExecutor;
        _triggerEvaluator = triggerEvaluator;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Workflow>> GetApplicableWorkflows(object triggerEvent)
    {
        var workflows = await _cache.GetOrCreateAsync("automation:active_workflows", async () =>
        {
            return await _workflowRepo.Query()
                .Where(w => w.IsEnabled && !w.IsDeleted)
                .ToListAsync();
        }, TimeSpan.FromMinutes(5));

        var applicable = new List<Workflow>();
        foreach (var workflow in workflows)
        {
            if (await _triggerEvaluator.MatchesTrigger(workflow, triggerEvent))
            {
                applicable.Add(workflow);
            }
        }

        return applicable;
    }

    public async Task<WorkflowExecutionResult> ExecuteWorkflow(Workflow workflow, object triggerEvent)
    {
        var result = new WorkflowExecutionResult
        {
            WorkflowId = workflow.Id,
            WorkflowName = workflow.Name,
            StartedAt = DateTime.UtcNow
        };

        if (!_options.EnableAutomation)
        {
            result.Status = ExecutionStatus.Disabled;
            return result;
        }

        try
        {
            if (!await EvaluateConditions(workflow, triggerEvent))
            {
                result.Status = ExecutionStatus.ConditionsNotMet;
                return result;
            }

            foreach (var action in workflow.Actions.OrderBy(a => a.Order))
            {
                var actionResult = await _actionExecutor.ExecuteAction(action, triggerEvent);
                result.ActionResults.Add(actionResult);
            }

            result.Status = result.ActionResults.All(r => r.Success)
                ? ExecutionStatus.Success
                : ExecutionStatus.PartialSuccess;

            workflow.RecordExecution();
            await _workflowRepo.UpdateAsync(workflow);

            _logger.LogInformation("Workflow {Name} executed with status {Status}",
                workflow.Name, result.Status);
        }
        catch (Exception ex)
        {
            result.Status = ExecutionStatus.Failed;
            result.Error = ex.Message;
            _logger.LogError(ex, "Workflow {Name} execution failed", workflow.Name);
        }

        result.CompletedAt = DateTime.UtcNow;
        return result;
    }

    public async Task<bool> EvaluateConditions(Workflow workflow, object triggerEvent)
    {
        if (workflow.Conditions.Count == 0) return true;

        foreach (var condition in workflow.Conditions)
        {
            if (!EvaluateCondition(condition, triggerEvent)) return false;
        }
        return true;
    }

    private bool EvaluateCondition(WorkflowCondition condition, object triggerEvent)
    {
        try
        {
            var value = GetPropertyValue(triggerEvent, condition.Field);
            if (value == null) return false;

            return condition.Operator switch
            {
                ConditionOperator.Equals => value.Equals(condition.Value, StringComparison.OrdinalIgnoreCase),
                ConditionOperator.NotEquals => !value.Equals(condition.Value, StringComparison.OrdinalIgnoreCase),
                ConditionOperator.GreaterThan => decimal.TryParse(value, out var v) && decimal.TryParse(condition.Value, out var c) && v > c,
                ConditionOperator.LessThan => decimal.TryParse(value, out var v) && decimal.TryParse(condition.Value, out var c) && v < c,
                ConditionOperator.Contains => value.Contains(condition.Value, StringComparison.OrdinalIgnoreCase),
                ConditionOperator.StartsWith => value.StartsWith(condition.Value, StringComparison.OrdinalIgnoreCase),
                ConditionOperator.EndsWith => value.EndsWith(condition.Value, StringComparison.OrdinalIgnoreCase),
                ConditionOperator.Regex => Regex.IsMatch(value, condition.Value, RegexOptions.IgnoreCase),
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }

    private string? GetPropertyValue(object obj, string propertyPath)
    {
        var parts = propertyPath.Split('.');
        object? current = obj;

        foreach (var part in parts)
        {
            if (current == null) return null;
            var prop = current.GetType().GetProperty(part, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (prop == null) return null;
            current = prop.GetValue(current);
        }

        return current?.ToString();
    }
}

public class WorkflowExecutionResult
{
    public Guid WorkflowId { get; set; }
    public string WorkflowName { get; set; } = string.Empty;
    public ExecutionStatus Status { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public string? Error { get; set; }
    public List<ActionResult> ActionResults { get; set; } = new();
}

public enum ExecutionStatus
{
    Pending,
    Running,
    Success,
    PartialSuccess,
    Failed,
    Disabled,
    ConditionsNotMet,
    TimedOut
}

public class ActionResult
{
    public string ActionType { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
    public TimeSpan Duration { get; set; }
}
