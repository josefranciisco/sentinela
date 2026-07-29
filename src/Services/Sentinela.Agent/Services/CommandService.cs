using System.Diagnostics;
using System.Management;
using System.Text;
using System.Text.Json;
using Sentinela.ScreenCapture.DTOs;
using Sentinela.ScreenCapture.Services;

namespace Sentinela.Agent.Services;

public interface ICommandService
{
    Task<CommandResult> ExecuteCommandAsync(CommandData command, CancellationToken ct = default);
    Task<CommandResult> RunPowerShellAsync(string script, int timeoutSec = 60);
    Task<CommandResult> RunCmdAsync(string command, int timeoutSec = 60);
    Task<bool> RestartSystemAsync();
    Task<bool> ShutdownSystemAsync();
    Task<bool> LogoffUserAsync();
    Task<bool> LockWorkstationAsync();
    Task<bool> SendMessageAsync(string title, string message);
    Task<CommandResult> ExecuteScriptAsync(string scriptContent, string scriptType, int timeoutSec = 60);
    Task<CommandResult> TransferFileAsync(string sourcePath, string destinationPath);
}

public class CommandService : ICommandService
{
    private readonly ILogger<CommandService> _logger;
    private readonly IAgentStateService _state;
    private readonly IScreenCaptureService _screenCaptureService;
    private readonly ICommunicationService _communicationService;
    private readonly IScreenCaptureOrchestrator _orchestrator;

    public CommandService(
        ILogger<CommandService> logger,
        IAgentStateService state,
        IScreenCaptureService screenCaptureService,
        ICommunicationService communicationService,
        IScreenCaptureOrchestrator orchestrator)
    {
        _logger = logger;
        _state = state;
        _screenCaptureService = screenCaptureService;
        _communicationService = communicationService;
        _orchestrator = orchestrator;
    }

    public async Task<CommandResult> ExecuteCommandAsync(CommandData command, CancellationToken ct = default)
    {
        _logger.LogInformation("Executing command: {Type} [{Id}]", command.CommandType, command.CommandId);

        return command.CommandType.ToLowerInvariant() switch
        {
            "runpowershell" => await RunPowerShellAsync(command.Parameters),
            "runcmd" => await RunCmdAsync(command.Parameters),
            "restart" => new CommandResult { Success = await RestartSystemAsync(), Output = "Restart initiated" },
            "shutdown" => new CommandResult { Success = await ShutdownSystemAsync(), Output = "Shutdown initiated" },
            "logoff" => new CommandResult { Success = await LogoffUserAsync(), Output = "Logoff initiated" },
            "lock" => new CommandResult { Success = await LockWorkstationAsync(), Output = "Lock initiated" },
            "sendmessage" => await HandleSendMessageAsync(command.Parameters),
            "updateagent" => new CommandResult { Success = false, Output = "Update not implemented" },
            "executescript" => await HandleExecuteScriptAsync(command.Parameters),
            "transferfile" => await HandleTransferFileAsync(command.Parameters),
            "capturescreen" => await HandleCaptureScreenAsync(command.Parameters),
            _ => new CommandResult { Success = false, Output = $"Unknown command type: {command.CommandType}" }
        };
    }

    public async Task<CommandResult> RunPowerShellAsync(string script, int timeoutSec = 60)
    {
        return await RunProcessAsync("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{script.Replace("\"", "\\\"")}\"", timeoutSec);
    }

    public async Task<CommandResult> RunCmdAsync(string command, int timeoutSec = 60)
    {
        return await RunProcessAsync("cmd.exe", $"/C \"{command}\"", timeoutSec);
    }

    public async Task<bool> RestartSystemAsync()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem WHERE Primary = True");
            foreach (ManagementObject obj in searcher.Get())
            {
                obj.InvokeMethod("Win32Shutdown", new object[] { 6 });
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart system");
        }
        return false;
    }

    public async Task<bool> ShutdownSystemAsync()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem WHERE Primary = True");
            foreach (ManagementObject obj in searcher.Get())
            {
                obj.InvokeMethod("Win32Shutdown", new object[] { 5 });
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to shutdown system");
        }
        return false;
    }

    public async Task<bool> LogoffUserAsync()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem WHERE Primary = True");
            foreach (ManagementObject obj in searcher.Get())
            {
                obj.InvokeMethod("Win32Shutdown", new object[] { 4 });
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to logoff user");
        }
        return false;
    }

    public async Task<bool> LockWorkstationAsync()
    {
        try
        {
            [DllImport("user32.dll")]
            static extern bool LockWorkStation();
            return LockWorkStation();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to lock workstation");
        }
        return false;
    }

    public async Task<bool> SendMessageAsync(string title, string message)
    {
        try
        {
            var result = await RunCmdAsync($"msg * /TIME:60 \"{message.Replace("\"", "\\\"")}\"");
            return result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message");
        }
        return false;
    }

    public async Task<CommandResult> ExecuteScriptAsync(string scriptContent, string scriptType, int timeoutSec = 60)
    {
        return scriptType.ToLowerInvariant() switch
        {
            "powershell" => await RunPowerShellAsync(scriptContent, timeoutSec),
            "cmd" or "batch" => await RunCmdAsync(scriptContent, timeoutSec),
            _ => new CommandResult { Success = false, Output = $"Unsupported script type: {scriptType}" }
        };
    }

    public async Task<CommandResult> TransferFileAsync(string sourcePath, string destinationPath)
    {
        try
        {
            if (!File.Exists(sourcePath))
                return new CommandResult { Success = false, Output = $"Source file not found: {sourcePath}" };

            var destDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destDir))
                Directory.CreateDirectory(destDir);

            File.Copy(sourcePath, destinationPath, overwrite: true);
            return new CommandResult
            {
                Success = true,
                Output = $"File copied from {sourcePath} to {destinationPath}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to transfer file");
            return new CommandResult { Success = false, Output = ex.Message };
        }
    }

    private async Task<CommandResult> RunProcessAsync(string fileName, string arguments, int timeoutSec)
    {
        var result = new CommandResult();
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                Verb = "runas"
            };

            using var process = new Process();
            process.StartInfo = psi;

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (s, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var exited = process.WaitForExit(timeoutSec * 1000);
            if (!exited)
            {
                process.Kill(entireProcessTree: true);
                return new CommandResult { Success = false, Output = $"Process timed out after {timeoutSec}s" };
            }

            result.Success = process.ExitCode == 0;
            result.Output = outputBuilder.ToString();
            result.Error = errorBuilder.ToString();
            result.ExitCode = process.ExitCode;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }
        return result;
    }

    private async Task<CommandResult> HandleSendMessageAsync(string parameters)
    {
        var parts = parameters.Split('|', 2);
        var title = parts.Length > 0 ? parts[0] : "Sentinela Agent";
        var message = parts.Length > 1 ? parts[1] : parameters;
        var success = await SendMessageAsync(title, message);
        return new CommandResult { Success = success, Output = success ? "Message sent" : "Failed to send message" };
    }

    private async Task<CommandResult> HandleExecuteScriptAsync(string parameters)
    {
        var parts = parameters.Split('|', 2);
        var scriptType = parts.Length > 0 ? parts[0] : "powershell";
        var scriptContent = parts.Length > 1 ? parts[1] : parameters;
        return await ExecuteScriptAsync(scriptContent, scriptType);
    }

    private async Task<CommandResult> HandleTransferFileAsync(string parameters)
    {
        var parts = parameters.Split('|');
        if (parts.Length < 2)
            return new CommandResult { Success = false, Output = "Invalid parameters. Expected: sourcePath|destinationPath" };
        return await TransferFileAsync(parts[0], parts[1]);
    }

    private async Task<CommandResult> HandleCaptureScreenAsync(string parameters)
    {
        try
        {
            string requestId;
            bool captureAllMonitors = false;
            try
            {
                using var doc = JsonDocument.Parse(parameters);
                requestId = doc.RootElement.GetProperty("requestId").GetString() ?? parameters;
                captureAllMonitors = doc.RootElement.GetProperty("captureAllMonitors").GetBoolean();
            }
            catch
            {
                requestId = parameters;
            }

            var cmd = new CaptureCommandDto
            {
                Command = "CaptureScreen",
                ComputerId = _state.ComputerId,
                RequestId = requestId,
                Quality = 80,
                CaptureAllMonitors = captureAllMonitors
            };

            var result = await _orchestrator.ExecuteCaptureAsync(cmd);
            if (result.Success)
            {
                _logger.LogInformation("Capture {RequestId} completed: {Width}x{Height} on {Monitor}",
                    result.RequestId, result.Width, result.Height, result.MonitorName);
                return new CommandResult { Success = true, Output = $"Capture completed: {result.ScreenshotId}" };
            }

            _logger.LogWarning("Capture {RequestId} failed: {Error}", result.RequestId, result.ErrorMessage);
            return new CommandResult { Success = false, Output = result.ErrorMessage ?? "Capture failed" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle screen capture command");
            return new CommandResult { Success = false, Output = ex.Message };
        }
    }
}

public class CommandResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = "";
    public string Error { get; set; } = "";
    public int ExitCode { get; set; }
}
