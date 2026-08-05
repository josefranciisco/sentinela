using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Sentinela.Agent.Services;
using Sentinela.Shared.Domain.Monitoring.Enums;

namespace Sentinela.Agent.Workers;

public interface IRansomwareDetector
{
    Task StartAsync(CancellationToken cancellationToken);
}

public class RansomwareDetector : IRansomwareDetector, IDisposable
{
    private readonly ICommunicationService _communicationService;
    private readonly IAgentStateService _agentState;
    private readonly ILogger<RansomwareDetector> _logger;
    private readonly ObservableCollection<FileSystemWatcher> _watchers = new();
    
    private static readonly HashSet<string> SuspiciousExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".locked", ".encrypted", ".crypto", ".crypt", ".enc", ".crypted",
        ".ryk", ".ryuk", ".wncry", ".wncryt", ".wcry", ".petya", ".NotPetya",
        ".maze", ".ryuk", ".darkside", ".revil", ".sodinokibi", ".netwalker",
        ".lobster", ".makop", ".phobos", ".dharma", ".crysis", ".stop",
        ".djvu", ".moia", ".medusa", ".blackcat", ".alphv", ".lockbit",
        ".cl0p", ".hive", ".blackmatter", ".avaddon", ".maze", ".ransom"
    };
    
    private static readonly ConcurrentDictionary<string, RenameTracker> _renameTrackers = new();
    private static readonly ConcurrentDictionary<string, DateTime> _alertCooldowns = new();
    
    private const int RenameThreshold = 100;
    private const int RenameWindowSeconds = 60;
    private const int CooldownMinutes = 5;
    
    private static readonly string[] MonitoredPaths = new[]
    {
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
        Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
    };
    
    public RansomwareDetector(
        ICommunicationService communicationService,
        IAgentStateService agentState,
        ILogger<RansomwareDetector> logger)
    {
        _communicationService = communicationService;
        _agentState = agentState;
        _logger = logger;
    }
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("RansomwareDetector starting...");
        
        foreach (var path in MonitoredPaths)
        {
            if (Directory.Exists(path))
            {
                try
                {
                    var watcher = new FileSystemWatcher(path)
                    {
                        IncludeSubdirectories = true,
                        EnableRaisingEvents = false,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
                    };
                    
                    watcher.Renamed += OnRenamed;
                    watcher.Created += OnCreated;
                    watcher.Changed += OnChanged;
                    watcher.EnableRaisingEvents = true;
                    
                    _watchers.Add(watcher);
                    _logger.LogInformation("Monitoring: {Path}", path);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to monitor: {Path}", path);
                }
            }
        }
        
        return Task.CompletedTask;
    }
    
    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        try
        {
            TrackRename(e.FullPath, e.OldFullPath);
            CheckSuspiciousExtension(e.FullPath);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error processing rename event");
        }
    }
    
    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        try
        {
            CheckSuspiciousExtension(e.FullPath);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error processing create event");
        }
    }
    
    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            CheckSuspiciousExtension(e.FullPath);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error processing change event");
        }
    }
    
    private void TrackRename(string newPath, string oldPath)
    {
        var directory = Path.GetDirectoryName(newPath) ?? "";
        if (string.IsNullOrEmpty(directory)) return;
        
        var tracker = _renameTrackers.GetOrAdd(directory, _ => new RenameTracker());
        
        lock (tracker.Lock)
        {
            var now = DateTime.UtcNow;
            
            tracker.Renames.Add(now);
            
            tracker.Renames.RemoveAll(r => (now - r).TotalSeconds > RenameWindowSeconds);
            
            if (tracker.Renames.Count >= RenameThreshold && !tracker.AlertSent)
            {
                tracker.AlertSent = true;
                _ = ReportMassRename(directory, tracker.Renames.Count);
            }
        }
    }
    
    private void CheckSuspiciousExtension(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(extension)) return;
        
        if (SuspiciousExtensions.Contains(extension))
        {
            if (IsInCooldown(filePath)) return;
            
            _logger.LogWarning("Suspicious file extension detected: {FilePath}", filePath);
            _ = ReportSuspiciousFile(filePath, extension);
        }
    }
    
    private async Task ReportMassRename(string directory, int count)
    {
        if (IsInCooldown(directory)) return;
        SetCooldown(directory);
        
        _logger.LogWarning("Mass rename detected in {Directory}: {Count} renames", directory, count);
        
        await _communicationService.SendTimelineBatchAsync(new List<TimelineEntryData>
        {
            new()
            {
                ComputerId = _agentState.ComputerId,
                EventType = EventType.MassFileRename.ToString(),
                Category = "Security",
                Description = $"Mass file rename detected: {count} files renamed",
                Details = $"Directory: {directory}\nRenames in last {RenameWindowSeconds}s: {count}\nThreshold: {RenameThreshold}\nPossible ransomware activity",
                Severity = "Critical",
                Timestamp = DateTime.UtcNow
            }
        });
    }
    
    private async Task ReportSuspiciousFile(string filePath, string extension)
    {
        SetCooldown(filePath);
        
        await _communicationService.SendTimelineBatchAsync(new List<TimelineEntryData>
        {
            new()
            {
                ComputerId = _agentState.ComputerId,
                EventType = EventType.RansomwarePattern.ToString(),
                Category = "Security",
                Description = $"Suspicious file with ransomware extension: {Path.GetFileName(filePath)}",
                Details = $"File: {filePath}\nExtension: {extension}\nKnown ransomware extension pattern detected",
                Severity = "Critical",
                Timestamp = DateTime.UtcNow
            }
        });
    }
    
    private bool IsInCooldown(string key)
    {
        if (_alertCooldowns.TryGetValue(key, out var lastAlert))
        {
            return (DateTime.UtcNow - lastAlert).TotalMinutes < CooldownMinutes;
        }
        return false;
    }
    
    private void SetCooldown(string key)
    {
        _alertCooldowns[key] = DateTime.UtcNow;
    }
    
    public void Dispose()
    {
        foreach (var watcher in _watchers)
        {
            try
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            catch { }
        }
        _watchers.Clear();
    }
}

internal class RenameTracker
{
    public List<DateTime> Renames { get; } = new();
    public bool AlertSent { get; set; }
    public object Lock { get; } = new();
}
