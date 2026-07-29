using System.ComponentModel;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Sentinela.Agent.Core.Collectors;

public interface IUserSessionCollector
{
    string GetCurrentUserName();
    bool IsSessionLocked();
    TimeSpan GetIdleTime();
    List<UserSessionInfo> GetActiveSessions();
    event EventHandler<SessionChangeEventArgs>? SessionChanged;
}

public class UserSessionCollector : IUserSessionCollector
{
    private ManagementEventWatcher? _sessionWatcher;
    private readonly HashSet<string> _knownUsers = new(StringComparer.OrdinalIgnoreCase);
    
    public event EventHandler<SessionChangeEventArgs>? SessionChanged;
    
    public UserSessionCollector()
    {
        InitializeWatcher();
    }
    
    private void InitializeWatcher()
    {
        try
        {
            var query = new WqlEventQuery("SELECT * FROM Win32_LogonSession");
            _sessionWatcher = new ManagementEventWatcher(query);
            _sessionWatcher.EventArrived += OnSessionEvent;
            _sessionWatcher.Start();
        }
        catch { }
    }
    
    private void OnSessionEvent(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var eventClass = e.NewEvent.ClassPath.ClassName;
            SessionChangeType type = eventClass.Contains("Start") ? SessionChangeType.Logon : SessionChangeType.Logoff;
            SessionChanged?.Invoke(this, new SessionChangeEventArgs(type, GetCurrentUserName(), DateTime.UtcNow));
        }
        catch { }
    }
    
    public string GetCurrentUserName()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT UserName FROM Win32_ComputerSystem");
            foreach (var obj in searcher.Get())
            {
                return obj["UserName"]?.ToString() ?? Environment.UserName;
            }
        }
        catch { }
        return Environment.UserName;
    }
    
    public bool IsSessionLocked()
    {
        try
        {
            var sessionId = WTSGetActiveConsoleSessionId();
            if (sessionId == 0xFFFFFFFF) return false;
            
            var sb = new StringBuilder();
            WTSQuerySessionInformation(IntPtr.Zero, sessionId, WTS_INFO_CLASS.WTSConnectState, out var ptr, out _);
            var state = Marshal.PtrToStructure<WTS_CONNECTSTATE_CLASS>(ptr);
            WTSFreeMemory(ptr);
            
            return state == WTS_CONNECTSTATE_CLASS.WTSDisconnected;
        }
        catch { return false; }
    }
    
    public TimeSpan GetIdleTime()
    {
        var lastInput = new LASTINPUTINFO();
        lastInput.cbSize = Marshal.SizeOf(lastInput);
        if (GetLastInputInfo(ref lastInput))
        {
            var ticks = (uint)Environment.TickCount;
            var msIdle = ticks - lastInput.dwTime;
            return TimeSpan.FromMilliseconds(msIdle);
        }
        return TimeSpan.Zero;
    }
    
    public List<UserSessionInfo> GetActiveSessions()
    {
        var sessions = new List<UserSessionInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_LogonSession WHERE LogonType = 2 OR LogonType = 10");
            foreach (var obj in searcher.Get())
            {
                sessions.Add(new UserSessionInfo
                {
                    SessionId = obj["LogonId"]?.ToString() ?? "",
                    Username = obj["User"]?.ToString() ?? "",
                    StartTime = obj["StartTime"] is DateTime dt ? dt : DateTime.MinValue,
                    IsActive = true
                });
            }
        }
        catch { }
        return sessions;
    }
    
    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
    
    [DllImport("kernel32.dll")]
    private static extern uint WTSGetActiveConsoleSessionId();
    
    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQuerySessionInformation(IntPtr hServer, uint sessionId, WTS_INFO_CLASS wtsInfoClass, out IntPtr ppBuffer, out uint pcbBytesReturned);
    
    [DllImport("wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr pMemory);
    
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public int cbSize;
        public uint dwTime;
    }
    
    private enum WTS_INFO_CLASS
    {
        WTSConnectState = 8
    }
    
    private enum WTS_CONNECTSTATE_CLASS
    {
        WTSActive,
        WTSConnected,
        WTSConnectQuery,
        WTSShadow,
        WTSDisconnected,
        WTSIdle,
        WTSListen,
        WTSReset,
        WTSDown,
        WTSInit
    }
}

public class UserSessionInfo
{
    public string SessionId { get; set; } = "";
    public string Username { get; set; } = "";
    public DateTime StartTime { get; set; }
    public bool IsActive { get; set; }
}

public enum SessionChangeType
{
    Logon,
    Logoff,
    Lock,
    Unlock
}

public class SessionChangeEventArgs : EventArgs
{
    public SessionChangeType ChangeType { get; }
    public string Username { get; }
    public DateTime Timestamp { get; }
    
    public SessionChangeEventArgs(SessionChangeType changeType, string username, DateTime timestamp)
    {
        ChangeType = changeType;
        Username = username;
        Timestamp = timestamp;
    }
}
