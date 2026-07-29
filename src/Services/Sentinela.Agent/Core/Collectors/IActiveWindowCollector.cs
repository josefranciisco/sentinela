using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Sentinela.Agent.Core.Collectors;

public interface IActiveWindowCollector
{
    string GetForegroundWindowTitle();
    string GetForegroundProcessName();
    string GetForegroundProcessPath();
    uint GetForegroundProcessId();
}

public class ActiveWindowCollector : IActiveWindowCollector
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    
    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    
    public string GetForegroundWindowTitle()
    {
        var handle = GetForegroundWindow();
        var sb = new StringBuilder(256);
        GetWindowText(handle, sb, 256);
        return sb.ToString();
    }
    
    public string GetForegroundProcessName()
    {
        var handle = GetForegroundWindow();
        GetWindowThreadProcessId(handle, out var pid);
        try
        {
            var process = Process.GetProcessById((int)pid);
            return process.ProcessName;
        }
        catch
        {
            return "Unknown";
        }
    }
    
    public string GetForegroundProcessPath()
    {
        var handle = GetForegroundWindow();
        GetWindowThreadProcessId(handle, out var pid);
        try
        {
            var process = Process.GetProcessById((int)pid);
            return process.MainModule?.FileName ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }
    
    public uint GetForegroundProcessId()
    {
        var handle = GetForegroundWindow();
        GetWindowThreadProcessId(handle, out var pid);
        return pid;
    }
}
