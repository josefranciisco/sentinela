using System.Collections.Concurrent;
using System.Management;

namespace Sentinela.Agent.Core.Collectors;

public interface IUsbCollector
{
    List<UsbDeviceInfo> GetConnectedDevices();
    event EventHandler<UsbDeviceEventArgs>? DeviceArrived;
    event EventHandler<UsbDeviceEventArgs>? DeviceRemoved;
    event EventHandler<FileCopyEventArgs>? FileCopied;
}

public class UsbCollector : IUsbCollector, IDisposable
{
    private ManagementEventWatcher? _arrivalWatcher;
    private ManagementEventWatcher? _removalWatcher;
    private readonly Dictionary<string, FileSystemWatcher> _driveWatchers = new();
    private readonly ConcurrentDictionary<string, DateTime> _recentFiles = new();
    private readonly object _lock = new();
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromSeconds(3);

    public event EventHandler<UsbDeviceEventArgs>? DeviceArrived;
    public event EventHandler<UsbDeviceEventArgs>? DeviceRemoved;
    public event EventHandler<FileCopyEventArgs>? FileCopied;

    public UsbCollector()
    {
        InitializeWatchers();
        MonitorExistingDrives();
    }

    private void InitializeWatchers()
    {
        try
        {
            _arrivalWatcher = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 2"));
            _arrivalWatcher.EventArrived += OnDeviceArrived;
            _arrivalWatcher.Start();

            _removalWatcher = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 3"));
            _removalWatcher.EventArrived += OnDeviceRemoved;
            _removalWatcher.Start();
        }
        catch { }
    }

    private void OnDeviceArrived(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var driveName = e.NewEvent["DriveName"]?.ToString() ?? "";
            var deviceInfo = GetDeviceInfo(driveName);
            if (deviceInfo != null)
            {
                StartDriveMonitoring(driveName);
                DeviceArrived?.Invoke(this, new UsbDeviceEventArgs(deviceInfo));
            }
        }
        catch { }
    }

    private void OnDeviceRemoved(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var driveName = e.NewEvent["DriveName"]?.ToString() ?? "";
            StopDriveMonitoring(driveName);
            DeviceRemoved?.Invoke(this, new UsbDeviceEventArgs(new UsbDeviceInfo { DriveLetter = driveName }));
        }
        catch { }
    }

    private UsbDeviceInfo? GetDeviceInfo(string driveLetter)
    {
        try
        {
            var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.Name.TrimEnd('\\') == driveLetter.TrimEnd('\\'));
            if (drive == null || !drive.IsReady) return null;

            using var searcher = new ManagementObjectSearcher($"SELECT * FROM Win32_LogicalDisk WHERE DeviceID = '{driveLetter.Replace("\\", "")}'");
            foreach (var obj in searcher.Get())
            {
                return new UsbDeviceInfo
                {
                    DeviceId = obj["DeviceID"]?.ToString() ?? "",
                    DriveLetter = driveLetter,
                    VolumeName = drive.VolumeLabel,
                    TotalSize = drive.TotalSize,
                    AvailableFreeSpace = drive.AvailableFreeSpace,
                    DriveFormat = drive.DriveFormat
                };
            }
        }
        catch { }
        return null;
    }

    private void MonitorExistingDrives()
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType == DriveType.Removable && drive.IsReady)
            {
                StartDriveMonitoring(drive.Name);
            }
        }
    }

    private void StartDriveMonitoring(string driveName)
    {
        try
        {
            var dir = driveName.TrimEnd('\\') + "\\";
            if (!Directory.Exists(dir)) return;

            lock (_lock)
            {
                if (_driveWatchers.ContainsKey(driveName)) return;

                var watcher = new FileSystemWatcher(dir)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
                    EnableRaisingEvents = true
                };

                // Prefer Created; Changed is noisy and often duplicates Created
                watcher.Created += (s, e) => OnFileCopied(e.FullPath, e.Name ?? "", "Created", driveName);

                _driveWatchers[driveName] = watcher;
            }
        }
        catch { }
    }

    private void StopDriveMonitoring(string driveName)
    {
        lock (_lock)
        {
            if (_driveWatchers.TryGetValue(driveName, out var watcher))
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                _driveWatchers.Remove(driveName);
            }
        }
    }

    private void OnFileCopied(string fullPath, string fileName, string changeType, string driveLetter)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileName) || fileName.StartsWith('.'))
                return;

            var key = fullPath.ToLowerInvariant();
            var now = DateTime.UtcNow;
            if (_recentFiles.TryGetValue(key, out var last) && now - last < DebounceWindow)
                return;

            _recentFiles[key] = now;

            // Prune old debounce entries
            if (_recentFiles.Count > 500)
            {
                foreach (var stale in _recentFiles.Where(kv => now - kv.Value > TimeSpan.FromMinutes(5)).Select(kv => kv.Key).ToList())
                    _recentFiles.TryRemove(stale, out _);
            }

            var fileInfo = new FileInfo(fullPath);
            FileCopied?.Invoke(this, new FileCopyEventArgs
            {
                FileName = fileName,
                FullPath = fullPath,
                FileSize = fileInfo.Exists ? fileInfo.Length : 0,
                ChangeType = changeType,
                DriveLetter = driveLetter,
                Timestamp = now
            });
        }
        catch { }
    }

    public List<UsbDeviceInfo> GetConnectedDevices()
    {
        var devices = new List<UsbDeviceInfo>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType == DriveType.Removable)
                {
                    devices.Add(new UsbDeviceInfo
                    {
                        DriveLetter = drive.Name,
                        VolumeName = drive.IsReady ? drive.VolumeLabel : "",
                        TotalSize = drive.IsReady ? drive.TotalSize : 0,
                        AvailableFreeSpace = drive.IsReady ? drive.AvailableFreeSpace : 0,
                        DriveFormat = drive.IsReady ? drive.DriveFormat : "",
                        IsReady = drive.IsReady
                    });
                }
            }
        }
        catch { }
        return devices;
    }

    public void Dispose()
    {
        _arrivalWatcher?.Dispose();
        _removalWatcher?.Dispose();
        lock (_lock)
        {
            foreach (var watcher in _driveWatchers.Values)
                watcher.Dispose();
            _driveWatchers.Clear();
        }
    }
}

public class UsbDeviceInfo
{
    public string DeviceId { get; set; } = "";
    public string DriveLetter { get; set; } = "";
    public string VolumeName { get; set; } = "";
    public long TotalSize { get; set; }
    public long AvailableFreeSpace { get; set; }
    public string DriveFormat { get; set; } = "";
    public bool IsReady { get; set; }
}

public class UsbDeviceEventArgs : EventArgs
{
    public UsbDeviceInfo DeviceInfo { get; }
    public UsbDeviceEventArgs(UsbDeviceInfo deviceInfo) => DeviceInfo = deviceInfo;
}

public class FileCopyEventArgs : EventArgs
{
    public string FileName { get; set; } = "";
    public string FullPath { get; set; } = "";
    public long FileSize { get; set; }
    public string ChangeType { get; set; } = "";
    public string DriveLetter { get; set; } = "";
    public DateTime Timestamp { get; set; }
}
