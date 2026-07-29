using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sentinela.Automation.Workflows;
using Sentinela.Shared.Core.Interfaces;
using Sentinela.Shared.Domain.Automation;
using Serilog;

namespace Sentinela.Automation.Channels;

public class WorkflowSchedulerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkflowSchedulerService> _logger;
    private readonly AutomationOptions _options;

    public WorkflowSchedulerService(
        IServiceScopeFactory scopeFactory,
        IOptions<AutomationOptions> options,
        ILogger<WorkflowSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Workflow Scheduler Service starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessScheduledWorkflows(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing scheduled workflows");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task ProcessScheduledWorkflows(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var workflowRepo = scope.ServiceProvider.GetRequiredService<IRepository<Workflow>>();
        var workflowEngine = scope.ServiceProvider.GetRequiredService<IWorkflowEngine>();

        var scheduledWorkflows = await workflowRepo.Query()
            .Where(w => w.IsEnabled && !w.IsDeleted && w.TriggerType == "Schedule")
            .ToListAsync(ct);

        var now = DateTime.UtcNow;

        foreach (var workflow in scheduledWorkflows)
        {
            if (string.IsNullOrEmpty(workflow.CronExpression)) continue;

            if (IsCronDue(workflow.CronExpression, (workflow.LastExecutedAt ?? DateTime.MinValue).DateTime, now))
            {
                _logger.LogInformation("Executing scheduled workflow: {Name} (Cron: {Cron})",
                    workflow.Name, workflow.CronExpression);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await workflowEngine.ExecuteWorkflow(workflow, new ScheduledTriggerEvent
                        {
                            WorkflowId = workflow.Id,
                            TriggeredAt = now
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Scheduled workflow {Name} execution failed", workflow.Name);
                    }
                }, ct);
            }
        }
    }

    private bool IsCronDue(string cronExpression, DateTime lastExecuted, DateTime now)
    {
        var parts = cronExpression.Split(' ');
        if (parts.Length != 5) return false;

        var currentMinute = now.Minute;
        var currentHour = now.Hour;
        var currentDay = now.Day;
        var currentMonth = now.Month;
        var currentDayOfWeek = (int)now.DayOfWeek;

        var lastRunMinute = lastExecuted.Minute;

        if (!CronFieldMatches(parts[0], currentMinute)) return false;
        if (!CronFieldMatches(parts[1], currentHour)) return false;
        if (!CronFieldMatches(parts[2], currentDay)) return false;
        if (!CronFieldMatches(parts[3], currentMonth)) return false;
        if (!CronFieldMatches(parts[4], currentDayOfWeek)) return false;

        if (lastExecuted > now.AddMinutes(-1)) return false;

        return true;
    }

    private bool CronFieldMatches(string field, int value)
    {
        if (field == "*") return true;

        if (field.Contains('/'))
        {
            var parts = field.Split('/');
            if (int.TryParse(parts[1], out var step))
            {
                return value % step == 0;
            }
        }

        if (field.Contains(','))
        {
            var values = field.Split(',');
            foreach (var v in values)
            {
                if (int.TryParse(v.Trim(), out var intVal) && intVal == value)
                    return true;
            }
            return false;
        }

        if (field.Contains('-'))
        {
            var parts = field.Split('-');
            if (int.TryParse(parts[0], out var low) && int.TryParse(parts[1], out var high))
            {
                return value >= low && value <= high;
            }
        }

        if (int.TryParse(field, out var exact))
        {
            return exact == value;
        }

        return false;
    }
}

public class ScheduledTriggerEvent
{
    public Guid WorkflowId { get; set; }
    public DateTime TriggeredAt { get; set; }
}
