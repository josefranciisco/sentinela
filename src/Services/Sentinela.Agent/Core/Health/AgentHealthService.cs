using System.Diagnostics;
using System.Management;
using Microsoft.Extensions.Options;
using Sentinela.Agent.Configuration;
using Sentinela.Agent.Services;

namespace Sentinela.Agent.Core.Health;

public interface IAgentHealthService
{
    AgentHealthStatus GetCurrentHealth();
    Task<AgentHealthReport> GenerateHealthReportAsync();
    bool SelfDiagnose();
}

public class AgentHealthService : IAgentHealthService
{
    private readonly IAgentStateService _state;
    private readonly AgentOptions _options;
    private readonly ILogger<AgentHealthService> _logger;
    private readonly Process _currentProcess;
    private DateTime _lastDiagnosticTime;
    
    public AgentHealthService(IAgentStateService state, IOptions<AgentOptions> options, ILogger<AgentHealthService> logger)
    {
        _state = state;
        _options = options.Value;
        _logger = logger;
        _currentProcess = Process.GetCurrentProcess();
    }
    
    public AgentHealthStatus GetCurrentHealth()
    {
        _currentProcess.Refresh();
        
        return new AgentHealthStatus
        {
            MemoryUsageBytes = _currentProcess.WorkingSet64,
            MemoryUsageMb = Math.Round(_currentProcess.WorkingSet64 / 1024.0 / 1024.0, 2),
            CpuUsagePercent = GetCpuUsage(),
            UptimeSeconds = (int)(DateTime.UtcNow - _state.StartTime).TotalSeconds,
            ThreadCount = _currentProcess.Threads.Count,
            HandleCount = _currentProcess.HandleCount,
            QueueSize = _state.OfflineQueueSize,
            LastHeartbeat = _state.LastHeartbeat,
            LastCommunication = _state.LastSuccessfulCommunication,
            ConnectionStatus = _state.ConnectionStatus,
            IsHealthy = true,
            Timestamp = DateTime.UtcNow
        };
    }
    
    public async Task<AgentHealthReport> GenerateHealthReportAsync()
    {
        var health = GetCurrentHealth();
        
        return new AgentHealthReport
        {
            HealthStatus = health,
            DiskFreeSpace = GetDiskFreeSpace(),
            TotalDiskSpace = GetTotalDiskSpace(),
            SystemUptimeSeconds = (int)(TimeSpan.FromMilliseconds(Environment.TickCount64).TotalSeconds),
            IsServiceRunning = CheckServiceRunning(),
            IsNetworkAvailable = await CheckNetworkAsync(),
            PendingEventCount = _state.OfflineQueueSize,
            LastSyncTimestamp = _state.LastSyncTimestamp,
            Issues = GetIssues(health)
        };
    }
    
    public bool SelfDiagnose()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastDiagnosticTime).TotalSeconds < 30) return true;
        _lastDiagnosticTime = now;
        
        var issues = new List<string>();
        
        if (_currentProcess.WorkingSet64 > 500 * 1024 * 1024)
            issues.Add("High memory usage");
            
        if (_state.LastHeartbeat != DateTime.MinValue && (now - _state.LastHeartbeat).TotalMinutes > 5)
            issues.Add("Missed heartbeats");
            
        if (_state.ConnectionStatus == "Disconnected" && (now - _state.StartTime).TotalMinutes > 1)
            issues.Add("Connection lost");
            
        if (GetDiskFreeSpace() < 1024 * 1024 * 1024)
            issues.Add("Low disk space");
        
        if (issues.Count > 0)
        {
            _logger.LogWarning("Self-diagnosis issues: {Issues}", string.Join(", ", issues));
            return false;
        }
        
        return true;
    }
    
    private double GetCpuUsage()
    {
        try
        {
            using var cpuCounter = new PerformanceCounter("Process", "% Processor Time", _currentProcess.ProcessName, true);
            cpuCounter.NextValue();
            Thread.Sleep(100);
            return Math.Round(cpuCounter.NextValue() / Environment.ProcessorCount, 2);
        }
        catch { return 0; }
    }
    
    private long GetDiskFreeSpace()
    {
        try
        {
            var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.Name == "C:\\");
            return drive?.AvailableFreeSpace ?? 0;
        }
        catch { return 0; }
    }
    
    private long GetTotalDiskSpace()
    {
        try
        {
            var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.Name == "C:\\");
            return drive?.TotalSize ?? 0;
        }
        catch { return 0; }
    }
    
    private bool CheckServiceRunning()
    {
        try
        {
            using var sc = new ServiceController("SentinelaAgent");
            return sc.Status == ServiceControllerStatus.Running;
        }
        catch { return false; }
    }
    
    private async Task<bool> CheckNetworkAsync()
    {
        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await httpClient.GetAsync("https://clients3.google.com/generate_204");
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }
    
    private List<string> GetIssues(AgentHealthStatus health)
    {
        var issues = new List<string>();
        if (health.MemoryUsageMb > 200) issues.Add($"High memory: {health.MemoryUsageMb}MB");
        if (health.QueueSize > 1000) issues.Add($"Large queue: {health.QueueSize}");
        if (health.ConnectionStatus != "Connected") issues.Add("Not connected");
        return issues;
    }
}

public class AgentHealthStatus
{
    public long MemoryUsageBytes { get; set; }
    public double MemoryUsageMb { get; set; }
    public double CpuUsagePercent { get; set; }
    public int UptimeSeconds { get; set; }
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }
    public int QueueSize { get; set; }
    public DateTime LastHeartbeat { get; set; }
    public DateTime LastCommunication { get; set; }
    public string ConnectionStatus { get; set; } = "";
    public bool IsHealthy { get; set; }
    public DateTime Timestamp { get; set; }
}

public class AgentHealthReport
{
    public AgentHealthStatus HealthStatus { get; set; } = new();
    public long DiskFreeSpace { get; set; }
    public long TotalDiskSpace { get; set; }
    public int SystemUptimeSeconds { get; set; }
    public bool IsServiceRunning { get; set; }
    public bool IsNetworkAvailable { get; set; }
    public int PendingEventCount { get; set; }
    public DateTime LastSyncTimestamp { get; set; }
    public List<string> Issues { get; set; } = new();
}
