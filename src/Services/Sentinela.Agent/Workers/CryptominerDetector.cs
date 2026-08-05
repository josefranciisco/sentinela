using System.Collections.Concurrent;
using Sentinela.Agent.Core.Collectors;
using Sentinela.Agent.Services;
using Sentinela.Shared.Domain.Monitoring.Enums;

namespace Sentinela.Agent.Workers;

public interface ICryptominerDetector
{
    Task StartAsync(CancellationToken cancellationToken);
}

public class CryptominerDetector : ICryptominerDetector, IDisposable
{
    private readonly IProcessCollector _processCollector;
    private readonly ICommunicationService _communicationService;
    private readonly IAgentStateService _agentState;
    private readonly ILogger<CryptominerDetector> _logger;
    private Timer? _monitorTimer;
    
    private static readonly HashSet<string> KnownMinerProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "xmrig", "xmrig.exe", "minerd", "minerd.exe", "cpuminer", "cpuminer.exe",
        "cgminer", "cgminer.exe", "bfgminer", "bfgminer.exe", "ethminer", "ethminer.exe",
        "nicehash", "nicehash.exe", "claymore", "claymore.exe", "phoenix", "phoenix.exe",
        "gminer", "gminer.exe", "t-rex", "t-rex.exe", "lolminer", "lolminer.exe",
        "nanominer", "nanominer.exe", "nbminer", "nbminer.exe", "excavator", "excavator.exe",
        "xmr-stak", "xmr-stak.exe", "monero", "monero.exe", "aeon", "aeon.exe",
        "sumokoin", "sumokoin.exe", "haven", "haven.exe", "masari", "masari.exe",
        "qrl", "qrl.exe", "ryo", "ryo.exe", "wow", "wow.exe", "arqma", "arqma.exe",
        "dero", "dero.exe", "turtlecoin", "turtlecoin.exe", "rtm", "rtm.exe"
    };
    
    private static readonly HashSet<string> KnownMinerDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "xmrpool.eu", "pool.minexmr.com", "monerohash.com", "moneropool.com",
        "nanopool.org", "flypool.org", "ethermine.org", "f2pool.com",
        "poolin.com", "antpool.com", "viabtc.com", "binance.com"
    };
    
    private static readonly ConcurrentDictionary<int, DateTime> _alertCooldowns = new();
    private static readonly TimeSpan CooldownPeriod = TimeSpan.FromMinutes(5);
    
    public CryptominerDetector(
        IProcessCollector processCollector,
        ICommunicationService communicationService,
        IAgentStateService agentState,
        ILogger<CryptominerDetector> logger)
    {
        _processCollector = processCollector;
        _communicationService = communicationService;
        _agentState = agentState;
        _logger = logger;
    }
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("CryptominerDetector starting...");
        
        _monitorTimer = new Timer(
            async _ => await MonitorProcesses(),
            null,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(30));
        
        return Task.CompletedTask;
    }
    
    private async Task MonitorProcesses()
    {
        try
        {
            var processes = _processCollector.GetRunningProcesses();
            
            foreach (var process in processes)
            {
                await CheckForKnownMiners(process);
                await CheckForHighCpuUsage(process);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error monitoring processes for cryptominers");
        }
    }
    
    private async Task CheckForKnownMiners(ProcessInfo process)
    {
        if (KnownMinerProcesses.Contains(process.ProcessName))
        {
            if (IsInCooldown(process.ProcessId))
                return;
            
            _logger.LogWarning("Known miner process detected: {ProcessName} (PID: {Pid})", 
                process.ProcessName, process.ProcessId);
            
            await ReportCryptominerDetected(process, "Known mining process detected");
        }
    }
    
    private async Task CheckForHighCpuUsage(ProcessInfo process)
    {
        if (process.CpuUsage < 80)
            return;
        
        if (IsInCooldown(process.ProcessId))
            return;
        
        bool isSuspicious = false;
        string reason = "";
        
        if (string.IsNullOrEmpty(process.WindowTitle) && process.CpuUsage > 90)
        {
            isSuspicious = true;
            reason = $"High CPU ({process.CpuUsage}%) with no window title";
        }
        
        if (!string.IsNullOrEmpty(process.ExecutablePath))
        {
            var path = process.ExecutablePath.ToLower();
            if (path.Contains("\\appdata\\") || path.Contains("\\temp\\") || path.Contains("\\downloads\\"))
            {
                if (process.CpuUsage > 85)
                {
                    isSuspicious = true;
                    reason = $"High CPU ({process.CpuUsage}%) from suspicious location";
                }
            }
        }
        
        if (isSuspicious)
        {
            _logger.LogWarning("Suspicious high CPU process: {ProcessName} (PID: {Pid}) - {Reason}", 
                process.ProcessName, process.ProcessId, reason);
            
            await ReportHighCpuProcess(process, reason);
        }
    }
    
    private async Task ReportCryptominerDetected(ProcessInfo process, string reason)
    {
        SetCooldown(process.ProcessId);
        
        await _communicationService.SendTimelineBatchAsync(new List<TimelineEntryData>
        {
            new()
            {
                ComputerId = _agentState.ComputerId,
                EventType = EventType.CryptominerDetected.ToString(),
                Category = "Security",
                Description = $"Cryptominer detected: {process.ProcessName}",
                Details = $"Process: {process.ProcessName}\nPID: {process.ProcessId}\nPath: {process.ExecutablePath}\nCPU: {process.CpuUsage}%\nReason: {reason}",
                Username = process.Username,
                Severity = "Critical",
                Timestamp = DateTime.UtcNow
            }
        });
    }
    
    private async Task ReportHighCpuProcess(ProcessInfo process, string reason)
    {
        SetCooldown(process.ProcessId);
        
        await _communicationService.SendTimelineBatchAsync(new List<TimelineEntryData>
        {
            new()
            {
                ComputerId = _agentState.ComputerId,
                EventType = EventType.HighCpuProcess.ToString(),
                Category = "Security",
                Description = $"Suspicious high CPU process: {process.ProcessName}",
                Details = $"Process: {process.ProcessName}\nPID: {process.ProcessId}\nPath: {process.ExecutablePath}\nCPU: {process.CpuUsage}%\nMemory: {process.MemoryUsage / 1024 / 1024}MB\nReason: {reason}",
                Username = process.Username,
                Severity = "High",
                Timestamp = DateTime.UtcNow
            }
        });
    }
    
    private bool IsInCooldown(int processId)
    {
        if (_alertCooldowns.TryGetValue(processId, out var lastAlert))
        {
            return DateTime.UtcNow - lastAlert < CooldownPeriod;
        }
        return false;
    }
    
    private void SetCooldown(int processId)
    {
        _alertCooldowns[processId] = DateTime.UtcNow;
    }
    
    public void Dispose()
    {
        _monitorTimer?.Dispose();
    }
}
