using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Sentinela.Agent.Configuration;
using Sentinela.Agent.Core.Collectors;
using Sentinela.Agent.Core.Health;
using Sentinela.Agent.Core.Monitors;
using Sentinela.Agent.Services;

namespace Sentinela.Agent.Workers;

public class CollectorWorker : BackgroundService
{
    private const int SoftwarePollSeconds = 60;
    private const int SecurityStatusSeconds = 300;
    private const int MalwarePollSeconds = 60;
    private const int AntivirusOutdatedDays = 7;

    private readonly IActiveWindowCollector _windowCollector;
    private readonly IUserSessionCollector _sessionCollector;
    private readonly IProcessCollector _processCollector;
    private readonly IUsbCollector _usbCollector;
    private readonly ISoftwareCollector _softwareCollector;
    private readonly ISecurityCollector _securityCollector;
    private readonly ISystemEventCollector _systemEventCollector;
    private readonly IScreenCaptureService _screenCaptureService;
    private readonly ICommunicationService _communication;
    private readonly IConfigurationService _configService;
    private readonly IAgentStateService _state;
    private readonly IWatchdogService _watchdog;
    private readonly AgentOptions _options;
    private readonly ILogger<CollectorWorker> _logger;
    private readonly ConcurrentQueue<TimelineEntryData> _eventQueue = new();

    private string _lastWindowTitle = "";
    private int _lastProcessId;
    private bool _wasLocked;
    private DateTime _lastCaptureTime = DateTime.MinValue;
    private DateTime _lastSoftwarePoll = DateTime.MinValue;
    private DateTime _lastSecurityStatus = DateTime.MinValue;
    private DateTime _lastMalwarePoll = DateTime.MinValue;
    private DateTime _lastInventorySync = DateTime.MinValue;

    public CollectorWorker(
        IActiveWindowCollector windowCollector,
        IUserSessionCollector sessionCollector,
        IProcessCollector processCollector,
        IUsbCollector usbCollector,
        ISoftwareCollector softwareCollector,
        ISecurityCollector securityCollector,
        ISystemEventCollector systemEventCollector,
        IScreenCaptureService screenCaptureService,
        ICommunicationService communication,
        IConfigurationService configService,
        IAgentStateService state,
        IWatchdogService watchdog,
        IOptions<AgentOptions> options,
        ILogger<CollectorWorker> logger)
    {
        _windowCollector = windowCollector;
        _sessionCollector = sessionCollector;
        _processCollector = processCollector;
        _usbCollector = usbCollector;
        _softwareCollector = softwareCollector;
        _securityCollector = securityCollector;
        _systemEventCollector = systemEventCollector;
        _screenCaptureService = screenCaptureService;
        _communication = communication;
        _configService = configService;
        _state = state;
        _watchdog = watchdog;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CollectorWorker started");

        var collectorTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(_options.CollectorIntervalMs));
        var batchTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(_options.BatchSendIntervalMs));
        var processTimer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        SubscribeToEvents();

        var collectorTask = RunCollectorLoop(collectorTimer, stoppingToken);
        var batchTask = RunBatchLoop(batchTimer, stoppingToken);
        var processTask = RunProcessLoop(processTimer, stoppingToken);

        await Task.WhenAll(collectorTask, batchTask, processTask);
    }

    private void SubscribeToEvents()
    {
        _sessionCollector.SessionChanged += (s, e) =>
        {
            _eventQueue.Enqueue(new TimelineEntryData
            {
                EventType = e.ChangeType.ToString(),
                Category = "Session",
                Description = $"User session {e.ChangeType.ToString().ToLower()}",
                Username = e.Username,
                Timestamp = e.Timestamp,
                Severity = "Info",
                ComputerId = _state.ComputerId
            });
        };

        _processCollector.ProcessStarted += (s, e) =>
        {
            _eventQueue.Enqueue(new TimelineEntryData
            {
                EventType = "AppStarted",
                Category = "Process",
                Description = $"Process started: {e.ProcessInfo.ProcessName}",
                Details = e.ProcessInfo.ExecutablePath,
                Username = e.ProcessInfo.Username,
                Timestamp = DateTime.UtcNow,
                Severity = "Info",
                ComputerId = _state.ComputerId
            });
        };

        _processCollector.ProcessStopped += (s, e) =>
        {
            _eventQueue.Enqueue(new TimelineEntryData
            {
                EventType = "AppClosed",
                Category = "Process",
                Description = $"Process stopped: {e.ProcessInfo.ProcessName}",
                Details = $"Duration: {e.ProcessInfo.Duration?.TotalMinutes:F1}min",
                Username = e.ProcessInfo.Username,
                Timestamp = DateTime.UtcNow,
                Severity = "Info",
                ComputerId = _state.ComputerId
            });
        };

        if (_options.EnableUsbTracking)
        {
            _usbCollector.DeviceArrived += (s, e) =>
            {
                _eventQueue.Enqueue(new TimelineEntryData
                {
                    EventType = "USBConnected",
                    Category = "USB",
                    Description = $"USB device connected: {e.DeviceInfo.DriveLetter}",
                    Details = $"{e.DeviceInfo.VolumeName} ({e.DeviceInfo.TotalSize / 1024 / 1024}MB)",
                    Timestamp = DateTime.UtcNow,
                    Severity = "Info",
                    ComputerId = _state.ComputerId
                });
            };

            _usbCollector.DeviceRemoved += (s, e) =>
            {
                _eventQueue.Enqueue(new TimelineEntryData
                {
                    EventType = "USBDisconnected",
                    Category = "USB",
                    Description = "USB device disconnected",
                    Details = e.DeviceInfo.DriveLetter,
                    Timestamp = DateTime.UtcNow,
                    Severity = "Info",
                    ComputerId = _state.ComputerId
                });
            };

            _usbCollector.FileCopied += (s, e) =>
            {
                _eventQueue.Enqueue(new TimelineEntryData
                {
                    EventType = "FileCopy",
                    Category = "USB",
                    Description = $"File copied: {e.FileName}",
                    Details = $"Drive: {e.DriveLetter}, Size: {e.FileSize / 1024}KB, Path: {e.FullPath}",
                    Username = Environment.UserName,
                    Timestamp = DateTime.UtcNow,
                    Severity = "Medium",
                    ComputerId = _state.ComputerId
                });
            };
        }

        _softwareCollector.SoftwareInstalled += (s, e) =>
        {
            _eventQueue.Enqueue(new TimelineEntryData
            {
                EventType = "SoftwareInstalled",
                Category = "Software",
                Description = $"Software installed: {e.Software.DisplayName}",
                Details = $"Version: {e.Software.Version}, Publisher: {e.Software.Publisher}",
                Timestamp = DateTime.UtcNow,
                Severity = "Medium",
                ComputerId = _state.ComputerId
            });
        };

        _softwareCollector.SoftwareUninstalled += (s, e) =>
        {
            _eventQueue.Enqueue(new TimelineEntryData
            {
                EventType = "SoftwareUninstalled",
                Category = "Software",
                Description = $"Software uninstalled: {e.Software.DisplayName}",
                Timestamp = DateTime.UtcNow,
                Severity = "Medium",
                ComputerId = _state.ComputerId
            });
        };
    }

    private async Task RunCollectorLoop(PeriodicTimer timer, CancellationToken ct)
    {
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                _watchdog.ReportCollectorRun("CollectorWorker");
                await CollectActiveWindowAsync();
                await CollectSessionStateAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in collector loop");
            }
        }
    }

    private async Task RunBatchLoop(PeriodicTimer timer, CancellationToken ct)
    {
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                await FlushEventQueueAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in batch loop");
            }
        }
    }

    private async Task RunProcessLoop(PeriodicTimer timer, CancellationToken ct)
    {
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                await CollectSecurityEventsAsync();
                await PollSoftwareAsync(ct);
                await PollSecurityStatusAsync(ct);
                await PollMalwareAsync();

                var config = _configService.GetCurrentConfiguration();
                if (config.EnableScreenCapture)
                    await CaptureScreenIfNeededAsync(config, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in process loop");
            }
        }
    }

    private Task CollectActiveWindowAsync()
    {
        var title = _windowCollector.GetForegroundWindowTitle();
        var processId = (int)_windowCollector.GetForegroundProcessId();

        if (title != _lastWindowTitle || processId != _lastProcessId)
        {
            var procName = _windowCollector.GetForegroundProcessName();

            if (processId != _lastProcessId && _lastProcessId > 0)
            {
                _eventQueue.Enqueue(new TimelineEntryData
                {
                    EventType = "AppFocus",
                    Category = "Application",
                    Description = $"Mudou para {procName}",
                    Timestamp = DateTime.UtcNow,
                    Severity = "Info",
                    ComputerId = _state.ComputerId
                });
            }

            _lastWindowTitle = title;
            _lastProcessId = processId;
        }

        return Task.CompletedTask;
    }

    private Task CollectSessionStateAsync()
    {
        var isLocked = _sessionCollector.IsSessionLocked();
        var username = _sessionCollector.GetCurrentUserName();

        _state.CurrentUser = username;

        if (isLocked != _wasLocked)
        {
            _eventQueue.Enqueue(new TimelineEntryData
            {
                EventType = isLocked ? "Lock" : "Unlock",
                Category = "Session",
                Description = isLocked ? "Workstation locked" : "Workstation unlocked",
                Username = username,
                Timestamp = DateTime.UtcNow,
                Severity = "Info",
                ComputerId = _state.ComputerId
            });
            _wasLocked = isLocked;
        }

        return Task.CompletedTask;
    }

    private async Task CollectSecurityEventsAsync()
    {
        try
        {
            var events = await _securityCollector.CheckSecurityChangesAsync();
            foreach (var evt in events)
            {
                _eventQueue.Enqueue(new TimelineEntryData
                {
                    EventType = evt.EventType,
                    Category = "Security",
                    Description = evt.Description,
                    Details = $"User: {evt.Username}, IP: {evt.SourceIp}",
                    Timestamp = evt.Timestamp,
                    Severity = evt.Severity.ToString(),
                    ComputerId = _state.ComputerId
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect security events");
        }
    }

    private async Task PollSoftwareAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastSoftwarePoll).TotalSeconds < SoftwarePollSeconds)
            return;

        _lastSoftwarePoll = now;
        try
        {
            _softwareCollector.CheckForChanges();

            if ((now - _lastInventorySync).TotalMinutes >= 15)
            {
                _lastInventorySync = now;
                var inventory = _softwareCollector.GetInstalledSoftware()
                    .Where(s => !s.IsSystemComponent)
                    .Select(s => new SoftwareInventoryItem
                    {
                        Name = s.DisplayName,
                        Version = s.Version,
                        Publisher = s.Publisher,
                        InstallDate = s.InstallDate,
                        InstallLocation = s.InstallLocation
                    })
                    .ToList();

                await _communication.SendSoftwareInventoryAsync(new SoftwareInventoryData
                {
                    ComputerId = _state.ComputerId,
                    Hostname = Environment.MachineName,
                    Items = inventory,
                    Timestamp = now
                }, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to poll software inventory");
        }
    }

    private async Task PollSecurityStatusAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastSecurityStatus).TotalSeconds < SecurityStatusSeconds)
            return;

        _lastSecurityStatus = now;
        try
        {
            var status = await _securityCollector.CollectSecurityStatusAsync();
            var data = new SecurityStatusData
            {
                ComputerId = _state.ComputerId,
                FirewallEnabled = status.FirewallEnabled,
                DefenderEnabled = status.DefenderEnabled,
                AntivirusEnabled = status.AntivirusEnabled,
                RealTimeProtectionEnabled = status.RealTimeProtectionEnabled,
                AntivirusSignatureAgeDays = status.AntivirusSignatureAgeDays,
                AntivirusSignatureLastUpdated = status.AntivirusSignatureLastUpdated,
                AntivirusProductName = status.AntivirusProductName,
                BitlockerEnabled = status.BitlockerEnabled,
                RdpEnabled = status.RdpEnabled,
                Hostname = status.Hostname,
                Timestamp = status.Timestamp
            };

            await _communication.SendSecurityStatusAsync(data, ct);

            if (!status.RealTimeProtectionEnabled || !status.AntivirusEnabled)
            {
                _eventQueue.Enqueue(new TimelineEntryData
                {
                    EventType = "AntivirusDisabled",
                    Category = "Security",
                    Description = $"Antivirus protection disabled ({status.AntivirusProductName})",
                    Details = $"RTP={status.RealTimeProtectionEnabled}, AV={status.AntivirusEnabled}",
                    Timestamp = now,
                    Severity = "High",
                    ComputerId = _state.ComputerId
                });
            }
            else if (status.AntivirusSignatureAgeDays > AntivirusOutdatedDays
                     || status.ThirdPartyProducts.Any(p => p.IsEnabled && !p.IsUpToDate))
            {
                _eventQueue.Enqueue(new TimelineEntryData
                {
                    EventType = "AntivirusOutdated",
                    Category = "Security",
                    Description = $"Antivirus signatures outdated ({status.AntivirusSignatureAgeDays} days)",
                    Details = $"Product: {status.AntivirusProductName}, LastUpdated: {status.AntivirusSignatureLastUpdated}",
                    Timestamp = now,
                    Severity = "High",
                    ComputerId = _state.ComputerId
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to poll security status");
        }
    }

    private async Task PollMalwareAsync()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastMalwarePoll).TotalSeconds < MalwarePollSeconds)
            return;

        _lastMalwarePoll = now;
        try
        {
            var threats = await _securityCollector.GetActiveThreatsAsync();
            foreach (var threat in threats.Where(t => t.IsNew))
            {
                var severity = threat.SeverityId switch
                {
                    >= 5 => "Critical",
                    >= 4 => "High",
                    >= 2 => "Medium",
                    _ => "Low"
                };

                _eventQueue.Enqueue(new TimelineEntryData
                {
                    EventType = "MalwareDetected",
                    Category = "Malware",
                    Description = $"Malware detected: {threat.ThreatName}",
                    Details = $"ThreatId={threat.ThreatId}, Resources={string.Join(';', threat.Resources.Take(3))}",
                    Timestamp = threat.DetectionTime,
                    Severity = severity,
                    ComputerId = _state.ComputerId
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to poll malware threats");
        }
    }

    private async Task CaptureScreenIfNeededAsync(ConfigurationData config, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastCaptureTime).TotalMilliseconds >= config.ScreenCaptureIntervalMs)
        {
            _lastCaptureTime = now;
            var captured = await _screenCaptureService.CaptureCompressedAsync(config.ScreenCaptureQuality);
            if (captured != null)
                await _communication.SendScreenCaptureAsync(new ScreenCaptureData { ComputerId = _state.ComputerId, ImageData = captured }, ct);
        }
    }

    private async Task FlushEventQueueAsync(CancellationToken ct)
    {
        var batch = new List<TimelineEntryData>();
        while (_eventQueue.TryDequeue(out var entry))
        {
            if (string.IsNullOrEmpty(entry.ComputerId))
                entry.ComputerId = _state.ComputerId;
            batch.Add(entry);
            if (batch.Count >= 50) break;
        }

        if (batch.Count > 0)
            await _communication.SendTimelineBatchAsync(batch, ct);
    }
}
