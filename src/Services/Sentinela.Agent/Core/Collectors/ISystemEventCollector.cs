using System.Diagnostics.Eventing.Reader;

namespace Sentinela.Agent.Core.Collectors;

public interface ISystemEventCollector
{
    List<SystemEventEntry> GetEvents(string logName, DateTime? since = null);
    List<SystemEventEntry> GetSecurityEvents(DateTime? since = null);
    List<SystemEventEntry> GetSystemEvents(DateTime? since = null);
    List<SystemEventEntry> GetApplicationEvents(DateTime? since = null);
    event EventHandler<SystemEventEventArgs>? EventWritten;
}

public class SystemEventCollector : ISystemEventCollector, IDisposable
{
    private EventLogWatcher? _securityWatcher;
    private EventLogWatcher? _systemWatcher;
    private EventLogWatcher? _applicationWatcher;
    
    public event EventHandler<SystemEventEventArgs>? EventWritten;
    
    public SystemEventCollector()
    {
        InitializeWatchers();
    }
    
    private void InitializeWatchers()
    {
        try
        {
            SubscribeToLog("Security");
            SubscribeToLog("System");
            SubscribeToLog("Application");
        }
        catch { }
    }
    
    private void SubscribeToLog(string logName)
    {
        try
        {
            var query = new EventLogQuery(logName, PathType.LogName) { ReverseDirection = true };
            var reader = new EventLogReader(query);
            
            var watcher = new EventLogWatcher(query);
            watcher.EventRecordWritten += OnEventRecordWritten;
            watcher.Enabled = true;
            
            switch (logName)
            {
                case "Security": _securityWatcher = watcher; break;
                case "System": _systemWatcher = watcher; break;
                case "Application": _applicationWatcher = watcher; break;
            }
        }
        catch { }
    }
    
    private void OnEventRecordWritten(object sender, EventRecordWrittenEventArgs e)
    {
        if (e.EventRecord == null) return;
        
        try
        {
            var entry = MapEventRecord(e.EventRecord);
            EventWritten?.Invoke(this, new SystemEventEventArgs(entry));
        }
        catch { }
    }
    
    public List<SystemEventEntry> GetEvents(string logName, DateTime? since = null)
    {
        var events = new List<SystemEventEntry>();
        try
        {
            var query = new EventLogQuery(logName, PathType.LogName) { ReverseDirection = true };
            if (since.HasValue)
            {
                query.Session = new EventLogSession();
            }
            
            using var reader = new EventLogReader(query);
            EventRecord? record;
            while ((record = reader.ReadEvent()) != null)
            {
                if (since.HasValue && record.TimeCreated.HasValue && record.TimeCreated.Value < since.Value)
                    break;
                    
                events.Add(MapEventRecord(record));
                
                if (events.Count >= 1000) break;
            }
        }
        catch { }
        return events;
    }
    
    public List<SystemEventEntry> GetSecurityEvents(DateTime? since = null)
    {
        return GetEvents("Security", since);
    }
    
    public List<SystemEventEntry> GetSystemEvents(DateTime? since = null)
    {
        return GetEvents("System", since);
    }
    
    public List<SystemEventEntry> GetApplicationEvents(DateTime? since = null)
    {
        return GetEvents("Application", since);
    }
    
    private SystemEventEntry MapEventRecord(EventRecord record)
    {
        var entry = new SystemEventEntry
        {
            LogName = record.LogName ?? "",
            EventId = (int)record.Id,
            Level = record.Level.HasValue ? GetLevelName(record.Level.Value) : "",
            ProviderName = record.ProviderName ?? "",
            MachineName = record.MachineName ?? "",
            TimeCreated = record.TimeCreated ?? DateTime.UtcNow,
            Qualifiers = (int)(record.Qualifiers.HasValue ? record.Qualifiers.Value : 0),
            Task = (int)(record.Task.HasValue ? record.Task.Value : 0),
            Keywords = record.Keywords.HasValue ? ((long)record.Keywords.Value).ToString("X") : ""
        };
        
        try
        {
            entry.Message = record.FormatDescription() ?? "";
        }
        catch { entry.Message = ""; }
        
        try
        {
            entry.Properties = record.Properties.Select(p => p.Value?.ToString() ?? "").ToList();
        }
        catch { entry.Properties = new List<string>(); }
        
        return entry;
    }
    
    private string GetLevelName(byte level)
    {
        return level switch
        {
            1 => "Critical",
            2 => "Error",
            3 => "Warning",
            4 => "Information",
            5 => "Verbose",
            _ => $"Level{level}"
        };
    }
    
    public void Dispose()
    {
        _securityWatcher?.Dispose();
        _systemWatcher?.Dispose();
        _applicationWatcher?.Dispose();
    }
}

public class SystemEventEntry
{
    public string LogName { get; set; } = "";
    public int EventId { get; set; }
    public string Level { get; set; } = "";
    public string ProviderName { get; set; } = "";
    public string MachineName { get; set; } = "";
    public DateTime TimeCreated { get; set; }
    public int Qualifiers { get; set; }
    public int Task { get; set; }
    public string Keywords { get; set; } = "";
    public string Message { get; set; } = "";
    public List<string> Properties { get; set; } = new();
}

public class SystemEventEventArgs : EventArgs
{
    public SystemEventEntry Entry { get; }
    public SystemEventEventArgs(SystemEventEntry entry) => Entry = entry;
}
