using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Options;
using Sentinela.Automation.Workflows;

namespace Sentinela.Automation.Actions;

public interface IScriptExecutor
{
    Task<ActionResult> ExecuteScriptAsync(string config, CancellationToken ct);
    Task<ActionResult> ExecutePowerShellAsync(string script, CancellationToken ct);
    Task<ActionResult> ExecuteBatchAsync(string script, CancellationToken ct);
    Task<ActionResult> ExecutePythonAsync(string script, CancellationToken ct);
}

public class ScriptExecutor : IScriptExecutor
{
    private readonly AutomationOptions _options;
    private readonly ILogger<ScriptExecutor> _logger;

    public ScriptExecutor(IOptions<AutomationOptions> options, ILogger<ScriptExecutor> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ActionResult> ExecutePowerShellAsync(string script, CancellationToken ct)
    {
        if (!_options.EnableScriptExecution)
            return new ActionResult { ActionType = "PowerShell", Success = false, Error = "Script execution disabled" };

        ValidateScript(script);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script.Replace("\"", "\\\"")}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        };

        var output = new StringBuilder();
        var error = new StringBuilder();

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        var completed = process.WaitForExit((int)TimeSpan.FromSeconds(_options.MaxExecutionTimeSeconds).TotalMilliseconds);

        if (!completed)
        {
            process.Kill();
            return new ActionResult { ActionType = "PowerShell", Success = false, Error = "Script execution timed out" };
        }

        output.Append(await outputTask);
        error.Append(await errorTask);

        return new ActionResult
        {
            ActionType = "PowerShell",
            Success = process.ExitCode == 0,
            Output = output.ToString(),
            Error = error.ToString()
        };
    }

    private void ValidateScript(string script)
    {
        foreach (var blocked in _options.BlockedScriptCommands)
        {
            if (script.Contains(blocked, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Blocked command: {blocked}");
            }
        }
    }

    public Task<ActionResult> ExecuteScriptAsync(string config, CancellationToken ct) => throw new NotImplementedException();
    public Task<ActionResult> ExecuteBatchAsync(string script, CancellationToken ct) => throw new NotImplementedException();
    public Task<ActionResult> ExecutePythonAsync(string script, CancellationToken ct) => throw new NotImplementedException();
}
