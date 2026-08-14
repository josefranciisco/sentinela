using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Options;
using Sentinela.Agent.Configuration;
using Sentinela.Agent.Services;

namespace Sentinela.Agent.Core.Health;

public interface IWatchdogService
{
    bool IsAgentHealthy();
    bool RestartAgent();
    void ReportCrash(string component);
    Dictionary<string, bool> CheckCollectorsHealth();
    void ReportCollectorRun(string collectorName);
}

public class WatchdogService : IWatchdogService, IDisposable
{
    private readonly IAgentHealthService _healthService;
    private readonly IAgentStateService _state;
    private readonly AgentOptions _options;
    private readonly ILogger<WatchdogService> _logger;
    private readonly FileSystemWatcher? _configWatcher;
    private readonly Dictionary<string, DateTime> _collectorLastRun = new();
    private readonly object _lock = new();
    private int _missedHeartbeats;
    private const int MaxMissedHeartbeats = 3;
    
    public event EventHandler? ConfigurationChanged;
    
    public WatchdogService(IAgentHealthService healthService, IAgentStateService state,
        IOptions<AgentOptions> options, ILogger<WatchdogService> logger)
    {
        _healthService = healthService;
        _state = state;
        _options = options.Value;
        _logger = logger;
        
        try
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            if (File.Exists(configPath))
            {
                _configWatcher = new FileSystemWatcher(Path.GetDirectoryName(configPath)!, "appsettings.json")
                {
                    NotifyFilter = NotifyFilters.LastWrite,
                    EnableRaisingEvents = true
                };
                _configWatcher.Changed += (s, e) =>
                {
                    _logger.LogInformation("Configuration file changed");
                    ConfigurationChanged?.Invoke(this, EventArgs.Empty);
                };
            }
        }
        catch { }
    }
    
    public bool IsAgentHealthy()
    {
        var health = _healthService.GetCurrentHealth();
        
        if (health.MemoryUsageMb > 500)
        {
            _logger.LogWarning("High memory usage: {Memory}MB", health.MemoryUsageMb);
            return false;
        }
        
        if (CheckMissedHeartbeats())
        {
            _logger.LogWarning("Missed heartbeats detected: {Count}", _missedHeartbeats);
            return false;
        }
        
        if (_state.ConnectionStatus == "Disconnected" && 
            (DateTime.UtcNow - _state.StartTime).TotalMinutes > 2)
        {
            _logger.LogWarning("Agent disconnected for extended period");
        }
        
        return true;
    }
    
    private bool CheckMissedHeartbeats()
    {
        var last = _state.LastSuccessfulCommunication != default
            ? _state.LastSuccessfulCommunication
            : _state.LastHeartbeat;
        if (last == DateTime.MinValue || last == default)
            return false;

        var intervalSec = Math.Max(1, _options.HeartbeatIntervalMs / 1000.0);
        var elapsed = (DateTime.UtcNow - last).TotalSeconds;
        if (elapsed > intervalSec * 3)
        {
            _missedHeartbeats++;
            return _missedHeartbeats >= MaxMissedHeartbeats;
        }

        _missedHeartbeats = 0;
        return false;
    }
    
    public bool RestartAgent()
    {
        try
        {
            _logger.LogWarning("Attempting agent restart");
            
            var process = Process.GetCurrentProcess();
            var exe = process.MainModule?.FileName ?? Environment.ProcessPath ?? "";
            var startInfo = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = Path.GetDirectoryName(exe) ?? Environment.CurrentDirectory
            };

            Process.Start(startInfo);
            
            var shutdownTimeout = TimeSpan.FromSeconds(5);
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < shutdownTimeout)
            {
                Thread.Sleep(100);
            }
            
            Environment.Exit(0);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart agent");
            return false;
        }
    }
    
    public void ReportCrash(string component)
    {
        _logger.LogError("Component crash reported: {Component}", component);
        
        try
        {
            var crashDir = Path.Combine("C:\\ProgramData\\Sentinela\\Agent\\crashes");
            Directory.CreateDirectory(crashDir);
            
            var crashFile = Path.Combine(crashDir, $"crash_{component}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.log");
            File.WriteAllText(crashFile, 
                $"Component: {component}\n" +
                $"Time: {DateTime.UtcNow:O}\n" +
                $"Memory: {Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024}MB\n" +
                $"Uptime: {(DateTime.UtcNow - _state.StartTime).TotalMinutes:F1}min\n");
        }
        catch { }
    }
    
    public Dictionary<string, bool> CheckCollectorsHealth()
    {
        var results = new Dictionary<string, bool>();
        var now = DateTime.UtcNow;
        
        lock (_lock)
        {
            foreach (var (collector, lastRun) in _collectorLastRun)
            {
                var elapsed = (now - lastRun).TotalSeconds;
                results[collector] = elapsed < _options.CollectorIntervalMs / 1000.0 * 10;
            }
        }
        
        return results;
    }
    
    public void ReportCollectorRun(string collectorName)
    {
        lock (_lock)
        {
            _collectorLastRun[collectorName] = DateTime.UtcNow;
        }
    }
    
    public void Dispose()
    {
        _configWatcher?.Dispose();
    }
}
