using System.Diagnostics;
using System.Management;

namespace Sentinela.Agent.Core.Collectors;

public interface IProcessCollector
{
    List<ProcessInfo> GetRunningProcesses();
    ProcessInfo? GetProcessById(int processId);
    event EventHandler<ProcessChangeEventArgs>? ProcessStarted;
    event EventHandler<ProcessChangeEventArgs>? ProcessStopped;
}

public class ProcessCollector : IProcessCollector, IDisposable
{
    private ManagementEventWatcher? _startWatcher;
    private ManagementEventWatcher? _stopWatcher;
    private readonly Dictionary<int, ProcessInfo> _processCache = new();
    private readonly object _lock = new();
    
    public event EventHandler<ProcessChangeEventArgs>? ProcessStarted;
    public event EventHandler<ProcessChangeEventArgs>? ProcessStopped;
    
    public ProcessCollector()
    {
        InitializeWatchers();
    }
    
    private void InitializeWatchers()
    {
        try
        {
            _startWatcher = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace"));
            _startWatcher.EventArrived += (s, e) =>
            {
                var processName = e.NewEvent["ProcessName"]?.ToString() ?? "";
                var processId = Convert.ToInt32(e.NewEvent["ProcessID"]);
                var info = new ProcessInfo
                {
                    ProcessId = processId,
                    ProcessName = processName,
                    StartTime = DateTime.Now
                };
                lock (_lock) _processCache[processId] = info;
                ProcessStarted?.Invoke(this, new ProcessChangeEventArgs(ProcessChangeType.Started, info));
            };
            _startWatcher.Start();
            
            _stopWatcher = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStopTrace"));
            _stopWatcher.EventArrived += (s, e) =>
            {
                var processId = Convert.ToInt32(e.NewEvent["ProcessID"]);
                lock (_lock)
                {
                    if (_processCache.TryGetValue(processId, out var info))
                    {
                        info.EndTime = DateTime.Now;
                        info.Duration = info.EndTime - info.StartTime;
                        ProcessStopped?.Invoke(this, new ProcessChangeEventArgs(ProcessChangeType.Stopped, info));
                        _processCache.Remove(processId);
                    }
                }
            };
            _stopWatcher.Start();
        }
        catch { }
    }
    
    public List<ProcessInfo> GetRunningProcesses()
    {
        var processes = new List<ProcessInfo>();
        try
        {
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    processes.Add(new ProcessInfo
                    {
                        ProcessId = process.Id,
                        ProcessName = process.ProcessName,
                        WindowTitle = process.MainWindowTitle,
                        ExecutablePath = process.MainModule?.FileName ?? "",
                        StartTime = process.StartTime.ToLocalTime(),
                        CpuUsage = 0,
                        MemoryUsage = process.WorkingSet64,
                        Username = GetProcessOwner(process.Id)
                    });
                }
                catch { }
            }
        }
        catch { }
        return processes;
    }
    
    public ProcessInfo? GetProcessById(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            return new ProcessInfo
            {
                ProcessId = process.Id,
                ProcessName = process.ProcessName,
                WindowTitle = process.MainWindowTitle,
                ExecutablePath = process.MainModule?.FileName ?? "",
                StartTime = process.StartTime.ToLocalTime(),
                MemoryUsage = process.WorkingSet64,
                Username = GetProcessOwner(process.Id)
            };
        }
        catch { return null; }
    }
    
    private string GetProcessOwner(int processId)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT * FROM Win32_Process WHERE ProcessId = {processId}");
            foreach (ManagementObject obj in searcher.Get())
            {
                var args = new string[] { "", "" };
                var result = (uint)obj.InvokeMethod("GetOwner", args);
                if (result == 0) return args[1] + "\\" + args[0];
            }
        }
        catch { }
        return "";
    }
    
    public void Dispose()
    {
        _startWatcher?.Dispose();
        _stopWatcher?.Dispose();
    }
}

public class ProcessInfo
{
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = "";
    public string WindowTitle { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan? Duration { get; set; }
    public double CpuUsage { get; set; }
    public long MemoryUsage { get; set; }
    public string Username { get; set; } = "";
}

public enum ProcessChangeType
{
    Started,
    Stopped
}

public class ProcessChangeEventArgs : EventArgs
{
    public ProcessChangeType ChangeType { get; }
    public ProcessInfo ProcessInfo { get; }
    
    public ProcessChangeEventArgs(ProcessChangeType changeType, ProcessInfo processInfo)
    {
        ChangeType = changeType;
        ProcessInfo = processInfo;
    }
}
