using Sentinela.Automation.Actions;
using Sentinela.Automation.Channels;
using Sentinela.Automation.Workflows;

public static class AutomationServiceRegistration
{
    public static IServiceCollection AddAutomationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AutomationOptions>(configuration.GetSection("Automation"));

        services.AddSingleton<IWorkflowEngine, WorkflowEngine>();
        services.AddSingleton<IActionExecutor, ActionExecutor>();
        services.AddSingleton<ITriggerEvaluator, TriggerEvaluator>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IScriptExecutor, ScriptExecutor>();

        services.AddHostedService<WorkflowConsumerService>();
        services.AddHostedService<WorkflowSchedulerService>();

        return services;
    }
}
