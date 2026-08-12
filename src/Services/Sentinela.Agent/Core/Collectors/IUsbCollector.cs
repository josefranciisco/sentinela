using System.Collections.Concurrent;
using System.Management;
using Microsoft.Extensions.Logging;

namespace Sentinela.Agent.Core.Collectors;

public interface IUsbCollector
{
    List<UsbDeviceInfo> GetConnectedDevices();
    /// <summary>Reescaneia drives removíveis (backup se o WMI falhar / race de mount).</summary>
    void PollRemovableDrives();
    event EventHandler<UsbDeviceEventArgs>? DeviceArrived;
    event EventHandler<UsbDeviceEventArgs>? DeviceRemoved;
    event EventHandler<FileCopyEventArgs>? FileCopied;
}

public class UsbCollector : IUsbCollector, IDisposable
{
    private ManagementEventWatcher? _arrivalWatcher;
    private ManagementEventWatcher? _removalWatcher;
    private readonly Dictionary<string, FileSystemWatcher> _driveWatchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _knownDrives = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> _recentFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private readonly ILogger<UsbCollector>? _logger;
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromSeconds(2);

    public event EventHandler<UsbDeviceEventArgs>? DeviceArrived;
    public event EventHandler<UsbDeviceEventArgs>? DeviceRemoved;
    public event EventHandler<FileCopyEventArgs>? FileCopied;

    public UsbCollector(ILogger<UsbCollector>? logger = null)
    {
        _logger = logger;
        InitializeWatchers();
        PollRemovableDrives();
    }

    private void InitializeWatchers()
    {
        try
        {
            _arrivalWatcher = new ManagementEventWatcher(
                new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 2"));
            _arrivalWatcher.EventArrived += OnDeviceArrived;
            _arrivalWatcher.Start();

            _removalWatcher = new ManagementEventWatcher(
                new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 3"));
            _removalWatcher.EventArrived += OnDeviceRemoved;
            _removalWatcher.Start();

            _logger?.LogInformation("USB WMI volume watchers started");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to start USB WMI watchers — relying on poll");
        }
    }

    private static string NormalizeDrive(string? drive)
    {
        if (string.IsNullOrWhiteSpace(drive)) return "";
        var d = drive.Trim().TrimEnd('\\');
        if (d.Length == 1 && char.IsLetter(d[0]))
            d += ":";
        return d.ToUpperInvariant();
    }

    private void OnDeviceArrived(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var driveName = NormalizeDrive(e.NewEvent["DriveName"]?.ToString());
            if (string.IsNullOrEmpty(driveName)) return;

            _logger?.LogInformation("USB volume arrived (WMI): {Drive}", driveName);

            // Não depender do drive estar Ready — monitora e notifica com retry
            StartDriveMonitoring(driveName);
            _ = NotifyArrivedWithRetryAsync(driveName);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "OnDeviceArrived failed");
        }
    }

    private async Task NotifyArrivedWithRetryAsync(string driveName)
    {
        UsbDeviceInfo? info = null;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            info = GetDeviceInfo(driveName);
            if (info != null && info.IsReady)
                break;
            await Task.Delay(500);
        }

        info ??= new UsbDeviceInfo
        {
            DriveLetter = driveName + "\\",
            DeviceId = driveName,
            IsReady = false
        };

        lock (_lock)
        {
            if (!_knownDrives.Add(driveName))
                return;
        }

        DeviceArrived?.Invoke(this, new UsbDeviceEventArgs(info));
    }

    private void OnDeviceRemoved(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var driveName = NormalizeDrive(e.NewEvent["DriveName"]?.ToString());
            if (string.IsNullOrEmpty(driveName)) return;

            _logger?.LogInformation("USB volume removed (WMI): {Drive}", driveName);
            StopDriveMonitoring(driveName);

            lock (_lock)
            {
                _knownDrives.Remove(driveName);
            }

            DeviceRemoved?.Invoke(this, new UsbDeviceEventArgs(new UsbDeviceInfo
            {
                DriveLetter = driveName + "\\",
                DeviceId = driveName
            }));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "OnDeviceRemoved failed");
        }
    }

    public void PollRemovableDrives()
    {
        try
        {
            var current = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType != DriveType.Removable)
                    continue;

                var key = NormalizeDrive(drive.Name);
                if (string.IsNullOrEmpty(key)) continue;
                current.Add(key);

                bool isNew;
                lock (_lock)
                {
                    isNew = _knownDrives.Add(key);
                }

                StartDriveMonitoring(key);

                if (isNew)
                {
                    _logger?.LogInformation("USB drive detected via poll: {Drive}", key);
                    var info = GetDeviceInfo(key) ?? new UsbDeviceInfo
                    {
                        DriveLetter = drive.Name,
                        IsReady = drive.IsReady,
                        VolumeName = drive.IsReady ? drive.VolumeLabel : "",
                        TotalSize = drive.IsReady ? drive.TotalSize : 0,
                        AvailableFreeSpace = drive.IsReady ? drive.AvailableFreeSpace : 0,
                        DriveFormat = drive.IsReady ? drive.DriveFormat : ""
                    };
                    DeviceArrived?.Invoke(this, new UsbDeviceEventArgs(info));
                }
            }

            List<string> removed;
            lock (_lock)
            {
                removed = _knownDrives.Where(d => !current.Contains(d)).ToList();
                foreach (var d in removed)
                    _knownDrives.Remove(d);
            }

            foreach (var d in removed)
            {
                StopDriveMonitoring(d);
                DeviceRemoved?.Invoke(this, new UsbDeviceEventArgs(new UsbDeviceInfo
                {
                    DriveLetter = d + "\\",
                    DeviceId = d
                }));
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "PollRemovableDrives failed");
        }
    }

    private UsbDeviceInfo? GetDeviceInfo(string driveLetter)
    {
        try
        {
            var normalized = NormalizeDrive(driveLetter);
            var drive = DriveInfo.GetDrives()
                .FirstOrDefault(d => NormalizeDrive(d.Name) == normalized);

            if (drive == null)
                return null;

            return new UsbDeviceInfo
            {
                DeviceId = normalized,
                DriveLetter = drive.Name,
                VolumeName = drive.IsReady ? drive.VolumeLabel : "",
                TotalSize = drive.IsReady ? drive.TotalSize : 0,
                AvailableFreeSpace = drive.IsReady ? drive.AvailableFreeSpace : 0,
                DriveFormat = drive.IsReady ? drive.DriveFormat : "",
                IsReady = drive.IsReady
            };
        }
        catch
        {
            return null;
        }
    }

    private void StartDriveMonitoring(string driveName)
    {
        try
        {
            var normalized = NormalizeDrive(driveName);
            var dir = normalized + "\\";
            if (!Directory.Exists(dir))
                return;

            lock (_lock)
            {
                if (_driveWatchers.ContainsKey(normalized))
                    return;

                var watcher = new FileSystemWatcher(dir)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                    InternalBufferSize = 64 * 1024,
                    EnableRaisingEvents = true
                };

                watcher.Created += (_, e) => OnFileCopied(e.FullPath, e.Name ?? "", "Created", normalized);
                watcher.Renamed += (_, e) => OnFileCopied(e.FullPath, e.Name ?? "", "Renamed", normalized);
                watcher.Changed += (_, e) =>
                {
                    // Cópias grandes: Created chega com 0 bytes; Changed confirma o arquivo
                    try
                    {
                        var fi = new FileInfo(e.FullPath);
                        if (fi.Exists && fi.Length > 0)
                            OnFileCopied(e.FullPath, e.Name ?? fi.Name, "Changed", normalized);
                    }
                    catch { /* arquivo ainda em uso */ }
                };
                watcher.Error += (_, e) =>
                    _logger?.LogWarning(e.GetException(), "FileSystemWatcher error on {Drive}", normalized);

                _driveWatchers[normalized] = watcher;
                _logger?.LogInformation("File watcher started on {Drive}", normalized);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "StartDriveMonitoring failed for {Drive}", driveName);
        }
    }

    private void StopDriveMonitoring(string driveName)
    {
        var normalized = NormalizeDrive(driveName);
        lock (_lock)
        {
            if (_driveWatchers.TryGetValue(normalized, out var watcher))
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                _driveWatchers.Remove(normalized);
            }
        }
    }

    private void OnFileCopied(string fullPath, string fileName, string changeType, string driveLetter)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return;

            // Ignora lixo de sistema / pastas
            if (fileName.StartsWith('.') ||
                fileName.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase) ||
                fullPath.Contains(@"\System Volume Information\", StringComparison.OrdinalIgnoreCase) ||
                fullPath.Contains(@"\$RECYCLE.BIN\", StringComparison.OrdinalIgnoreCase))
                return;

            // Só arquivos (não diretórios)
            if (Directory.Exists(fullPath) && !File.Exists(fullPath))
                return;

            long size = 0;
            try
            {
                var fileInfo = new FileInfo(fullPath);
                if (fileInfo.Exists) size = fileInfo.Length;
            }
            catch { /* locked */ }

            // Created costuma chegar com 0 bytes durante a cópia — espera Changed
            if (changeType == "Created" && size == 0)
                return;

            var key = fullPath.ToLowerInvariant();
            var now = DateTime.UtcNow;
            if (_recentFiles.TryGetValue(key, out var last) && now - last < DebounceWindow)
                return;

            _recentFiles[key] = now;

            _logger?.LogInformation("USB file activity: {Change} {File} on {Drive} ({Size} bytes)",
                changeType, fileName, driveLetter, size);

            FileCopied?.Invoke(this, new FileCopyEventArgs
            {
                FileName = fileName,
                FullPath = fullPath,
                FileSize = size,
                ChangeType = changeType,
                DriveLetter = driveLetter,
                Timestamp = now
            });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "OnFileCopied failed for {Path}", fullPath);
        }
    }

    public List<UsbDeviceInfo> GetConnectedDevices()
    {
        var devices = new List<UsbDeviceInfo>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType != DriveType.Removable) continue;
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
