public class AutomationOptions
{
    public bool EnableAutomation { get; set; } = true;
    public int MaxExecutionTimeSeconds { get; set; } = 300;
    public int MaxConcurrentWorkflows { get; set; } = 50;
    public bool EnableScriptExecution { get; set; } = true;
    public string[] AllowedScriptPaths { get; set; } = Array.Empty<string>();
    public string[] BlockedScriptCommands { get; set; } = { "Format", "Del", "Remove-Item", "Stop-Service" };
    public int WorkflowRetentionDays { get; set; } = 30;
}
