using System.Diagnostics;
using System.Management;
using System.Text;
using System.Text.Json;
using Sentinela.Agent.Core.Collectors;
using Sentinela.Agent.Recording;
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
    private readonly ISoftwareCollector _softwareCollector;
    private readonly ISecurityCollector _securityCollector;
    private readonly IRecordingStore _recordingStore;
    private readonly RecordingUploadClient _recordingUpload;

    public CommandService(
        ILogger<CommandService> logger,
        IAgentStateService state,
        IScreenCaptureService screenCaptureService,
        ICommunicationService communicationService,
        IScreenCaptureOrchestrator orchestrator,
        ISoftwareCollector softwareCollector,
        ISecurityCollector securityCollector,
        IRecordingStore recordingStore,
        RecordingUploadClient recordingUpload)
    {
        _logger = logger;
        _state = state;
        _screenCaptureService = screenCaptureService;
        _communicationService = communicationService;
        _orchestrator = orchestrator;
        _softwareCollector = softwareCollector;
        _securityCollector = securityCollector;
        _recordingStore = recordingStore;
        _recordingUpload = recordingUpload;
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
            "syncinventory" or "refreshsecurity" or "syncsecurity" => await HandleSyncInventoryAsync(ct),
            "listrecording" => await HandleListRecordingAsync(command.Parameters, ct),
            "getrecordingframe" => await HandleGetRecordingFrameAsync(command.Parameters, ct),
            "exportrecording" => await HandleExportRecordingAsync(command.Parameters, ct),
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

    private async Task<CommandResult> HandleSyncInventoryAsync(CancellationToken ct)
    {
        try
        {
            _softwareCollector.CheckForChanges();

            var inventory = _softwareCollector.GetInstalledSoftware()
                .Where(s => !s.IsSystemComponent)
                .Select(s => new SoftwareInventoryItem
                {
                    Name = s.DisplayName,
                    Version = s.Version,
                    Publisher = s.Publisher,
                    InstallDate = s.InstallDate,
                    InstallLocation = s.InstallLocation
                })
                .ToList();

            await _communicationService.SendSoftwareInventoryAsync(new SoftwareInventoryData
            {
                ComputerId = _state.ComputerId,
                Hostname = Environment.MachineName,
                Items = inventory,
                Timestamp = DateTime.UtcNow
            }, ct);

            var status = await _securityCollector.CollectSecurityStatusAsync();
            await _communicationService.SendSecurityStatusAsync(new SecurityStatusData
            {
                ComputerId = _state.ComputerId,
                FirewallEnabled = status.FirewallEnabled,
                DefenderEnabled = status.DefenderEnabled,
                AntivirusEnabled = status.AntivirusEnabled,
                RealTimeProtectionEnabled = status.RealTimeProtectionEnabled,
                AntivirusSignatureAgeDays = status.AntivirusSignatureAgeDays,
                AntivirusSignatureLastUpdated = status.AntivirusSignatureLastUpdated,
                AntivirusProductName = status.AntivirusProductName,
                BitlockerEnabled = status.BitlockerEnabled,
                RdpEnabled = status.RdpEnabled,
                Hostname = status.Hostname,
                Timestamp = status.Timestamp
            }, ct);

            _logger.LogInformation("Forced security/inventory sync: {Count} software items", inventory.Count);
            return new CommandResult
            {
                Success = true,
                Output = $"Synced {inventory.Count} software items and security status"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync inventory/security status");
            return new CommandResult { Success = false, Output = ex.Message };
        }
    }

    private async Task<CommandResult> HandleCaptureScreenAsync(string parameters)
    {
        try
        {
            string requestId;
            bool captureAllMonitors = false;
            int? monitorIndex = null;
            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(parameters) ? "{}" : parameters);
                requestId = doc.RootElement.TryGetProperty("requestId", out var rid)
                    ? rid.GetString() ?? parameters
                    : parameters;
                if (doc.RootElement.TryGetProperty("captureAllMonitors", out var allEl)
                    && (allEl.ValueKind is JsonValueKind.True or JsonValueKind.False))
                    captureAllMonitors = allEl.GetBoolean();
                if (doc.RootElement.TryGetProperty("monitorIndex", out var idxEl) && idxEl.TryGetInt32(out var idx))
                    monitorIndex = idx;
            }
            catch
            {
                requestId = parameters;
            }

            if (monitorIndex.HasValue)
                captureAllMonitors = false;

            var cmd = new CaptureCommandDto
            {
                Command = "CaptureScreen",
                ComputerId = _state.ComputerId,
                RequestId = requestId,
                Quality = 100,
                CaptureAllMonitors = captureAllMonitors,
                MonitorIndex = monitorIndex
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

    private async Task<CommandResult> HandleListRecordingAsync(string parameters, CancellationToken ct)
    {
        try
        {
            var status = _recordingStore.GetStatus();
            await _recordingUpload.PostStatusAsync(ResolveServerComputerId(parameters), status, ct);
            return new CommandResult { Success = true, Output = $"segments={status.SegmentCount}" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list recording");
            return new CommandResult { Success = false, Output = ex.Message };
        }
    }

    private async Task<CommandResult> HandleGetRecordingFrameAsync(string parameters, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(parameters) ? "{}" : parameters);
            var requestId = doc.RootElement.TryGetProperty("requestId", out var rid) ? rid.GetString() : Guid.NewGuid().ToString();
            var at = doc.RootElement.TryGetProperty("at", out var atEl) && atEl.TryGetDateTime(out var parsed)
                ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
                : DateTime.UtcNow;
            var monitorIndex = ReadInt(doc.RootElement, "monitorIndex", 0);

            var jpeg = _recordingStore.GetFrame(at, monitorIndex);
            if (jpeg is null || jpeg.Length == 0)
            {
                _logger.LogWarning("No recording frame at {At} monitor {Monitor}", at, monitorIndex);
                return new CommandResult { Success = false, Output = "No recording frame for that time" };
            }

            await _recordingUpload.PostFrameAsync(requestId!, ResolveServerComputerId(parameters), at, jpeg, ct);
            return new CommandResult { Success = true, Output = $"frame {jpeg.Length} bytes" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recording frame");
            return new CommandResult { Success = false, Output = ex.Message };
        }
    }

    private async Task<CommandResult> HandleExportRecordingAsync(string parameters, CancellationToken ct)
    {
        string? zipPath = null;
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(parameters) ? "{}" : parameters);
            var exportId = doc.RootElement.TryGetProperty("exportId", out var eid) ? eid.GetString() : Guid.NewGuid().ToString();
            var from = doc.RootElement.TryGetProperty("from", out var fromEl) && fromEl.TryGetDateTime(out var fromDt)
                ? DateTime.SpecifyKind(fromDt, DateTimeKind.Utc)
                : DateTime.UtcNow.AddMinutes(-30);
            var to = doc.RootElement.TryGetProperty("to", out var toEl) && toEl.TryGetDateTime(out var toDt)
                ? DateTime.SpecifyKind(toDt, DateTimeKind.Utc)
                : DateTime.UtcNow;

            if (to - from > TimeSpan.FromHours(2))
                from = to.AddHours(-2);

            var monitorIndex = ReadInt(doc.RootElement, "monitorIndex", 0);
            zipPath = _recordingStore.CreateJpegZip(from, to, monitorIndex);
            await _recordingUpload.PostExportAsync(exportId ?? Guid.NewGuid().ToString("N"), ResolveServerComputerId(parameters), zipPath, ct);
            return new CommandResult { Success = true, Output = exportId };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export recording");
            return new CommandResult { Success = false, Output = ex.Message };
        }
        finally
        {
            if (zipPath is not null)
            {
                try { File.Delete(zipPath); } catch { /* ignore */ }
            }
        }
    }

    private string ResolveServerComputerId(string parameters)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(parameters) ? "{}" : parameters);
            if (doc.RootElement.TryGetProperty("computerId", out var idEl))
            {
                var value = idEl.ValueKind == JsonValueKind.String ? idEl.GetString() : idEl.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
        }
        catch
        {
            /* fall back to local id */
        }

        return _state.ComputerId;
    }

    private static int ReadInt(JsonElement root, string name, int fallback)
    {
        if (!root.TryGetProperty(name, out var el)) return fallback;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n)) return n;
        if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var parsed)) return parsed;
        return fallback;
    }
}

public class CommandResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = "";
    public string Error { get; set; } = "";
    public int ExitCode { get; set; }
}
