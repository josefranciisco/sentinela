using Sentinela.Api.Controllers.v1;
using Sentinela.Api.Models;
using Sentinela.Persistence;
using Sentinela.Persistence.Models;
using Sentinela.Shared.Core.Interfaces;
using Sentinela.Shared.Domain.Monitoring;
using Sentinela.Shared.Domain.Monitoring.Enums;
using Sentinela.Shared.Domain.Security;
using Sentinela.Shared.Messaging.Events;

namespace Sentinela.Api.Hubs;

[AllowAnonymous]
public class AgentHub : Hub
{
    private static readonly HashSet<string> SecurityRelevantTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "FileCopy", "USBConnected", "USBDisconnected",
        "SoftwareInstalled", "SoftwareUninstalled",
        "MalwareDetected", "AntivirusOutdated", "AntivirusDisabled",
        "FailedLogon", "CryptominerDetected", "HighCpuProcess",
        "MassFileRename", "RansomwarePattern", "SuspiciousNetworkActivity"
    };

    private readonly IRepository<Computer> _computerRepo;
    private readonly IRepository<Heartbeat> _heartbeatRepo;
    private readonly IRepository<TimelineEntry> _timelineRepo;
    private readonly IRepository<ScreenCapture> _captureRepo;
    private readonly IRepository<SecurityEvent> _securityEventRepo;
    private readonly IRepository<SoftwareInventoryItem> _softwareRepo;
    private readonly IRepository<EndpointSecurityStatus> _securityStatusRepo;
    private readonly IEventBus _eventBus;
    private readonly ILogger<AgentHub> _logger;
    private readonly IHubContext<RemoteAssistanceHub> _remoteHubContext;
    private readonly IHubContext<MonitoringHub> _monitoringHubContext;
    private readonly IHubContext<AlertHub> _alertHubContext;
    private readonly TenantAccessor _tenantAccessor;
    private readonly ICacheService _cache;

    public AgentHub(
        IRepository<Computer> computerRepo,
        IRepository<Heartbeat> heartbeatRepo,
        IRepository<TimelineEntry> timelineRepo,
        IRepository<ScreenCapture> captureRepo,
        IRepository<SecurityEvent> securityEventRepo,
        IRepository<SoftwareInventoryItem> softwareRepo,
        IRepository<EndpointSecurityStatus> securityStatusRepo,
        IEventBus eventBus,
        ILogger<AgentHub> logger,
        IHubContext<RemoteAssistanceHub> remoteHubContext,
        IHubContext<MonitoringHub> monitoringHubContext,
        IHubContext<AlertHub> alertHubContext,
        TenantAccessor tenantAccessor,
        ICacheService cache)
    {
        _computerRepo = computerRepo;
        _heartbeatRepo = heartbeatRepo;
        _timelineRepo = timelineRepo;
        _captureRepo = captureRepo;
        _securityEventRepo = securityEventRepo;
        _softwareRepo = softwareRepo;
        _securityStatusRepo = securityStatusRepo;
        _eventBus = eventBus;
        _logger = logger;
        _remoteHubContext = remoteHubContext;
        _monitoringHubContext = monitoringHubContext;
        _alertHubContext = alertHubContext;
        _tenantAccessor = tenantAccessor;
        _cache = cache;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Agent connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public async Task SendHeartbeat(AgentHeartbeatDto dto)
    {
        var computerId = Guid.TryParse(dto.ComputerId, out var id) ? id : Guid.Empty;
        
        if (dto.TenantId.HasValue && dto.TenantId.Value != Guid.Empty)
            _tenantAccessor.SetTenantId(dto.TenantId.Value);
        
        var computer = await ResolveComputerAsync(computerId, dto.Hostname, dto.IpAddress, dto.CurrentUser, dto.TenantId);

        await Groups.AddToGroupAsync(Context.ConnectionId, $"agent:{computer.Id}");

        computer.UpdateStatus(ComputerStatus.Online);
        computer.UpdateHeartbeat(dto.IpAddress, dto.CurrentUser);
        computer.UpdateMonitorCount(dto.MonitorCount);
        await _computerRepo.SaveChangesAsync();

        if (dto.RecordingEnabled || dto.RecordingBytes > 0)
        {
            var key = RecordingsController.MetaKeyPrefix + computer.Id;
            var existing = await _cache.GetAsync<RecordingStatusDto>(key);
            await _cache.SetAsync(key, new RecordingStatusDto
            {
                ComputerId = computer.Id.ToString(),
                Enabled = dto.RecordingEnabled,
                InSchedule = dto.RecordingInSchedule,
                ScheduleSummary = dto.RecordingScheduleSummary ?? existing?.ScheduleSummary,
                FromUtc = dto.RecordingFromUtc ?? existing?.FromUtc,
                ToUtc = dto.RecordingToUtc ?? existing?.ToUtc,
                Bytes = dto.RecordingBytes > 0 ? dto.RecordingBytes : existing?.Bytes ?? 0,
                MaxBytes = dto.RecordingMaxBytes > 0 ? dto.RecordingMaxBytes : existing?.MaxBytes ?? 0,
                Monitors = existing?.Monitors ?? [],
                Segments = existing?.Segments ?? [],
                SegmentCount = existing?.SegmentCount ?? 0
            }, TimeSpan.FromMinutes(5));
        }

        var heartbeat = new Heartbeat(DateTimeOffset.UtcNow, ComputerStatus.Online, 0, 0, 0, 0) { ComputerId = computer.Id };
        await _heartbeatRepo.AddAsync(heartbeat);

        await Clients.Group("admins").SendAsync("ComputerStatusChanged", new
        {
            computer.Id,
            computer.Hostname,
            computer.Status,
            computer.LastHeartbeat
        });
    }

    public async Task SendScreenCapture(ScreenCaptureDataDto dto)
    {
        var computerId = ResolveComputerId(dto.ComputerId);
        if (computerId == Guid.Empty)
        {
            _logger.LogWarning("Screen capture without computer id");
            return;
        }

        var computer = await _computerRepo.GetByIdAsync(computerId);
        if (computer?.TenantId != null && computer.TenantId != Guid.Empty)
            _tenantAccessor.SetTenantId(computer.TenantId);

        ScreenCapture capture;

        if (!string.IsNullOrWhiteSpace(dto.CaptureRequestId) && Guid.TryParse(dto.CaptureRequestId, out var requestId))
        {
            capture = await _captureRepo.GetByIdAsync(requestId);
            if (capture != null)
            {
                capture.ImageData = dto.ImageData;
                capture.CapturedAt = DateTimeOffset.UtcNow;
                capture.Status = CaptureStatus.Captured;
                await _captureRepo.UpdateAsync(capture);
            }
            else
            {
                _logger.LogWarning("Capture request {RequestId} not found, creating new", requestId);
                capture = null;
            }
        }
        else
        {
            capture = null;
        }

        if (capture == null)
        {
            capture = new ScreenCapture
            {
                ComputerId = computerId,
                ImageData = dto.ImageData,
                CapturedAt = DateTimeOffset.UtcNow,
                Status = CaptureStatus.Captured
            };
            capture.MarkAsUpdated();
            await _captureRepo.AddAsync(capture);
        }

        _logger.LogInformation("Screen capture received for computer {ComputerId}: {Size} bytes", computerId, dto.ImageData?.Length ?? 0);

        await Clients.Group("admins").SendAsync("ScreenCaptureReceived", new
        {
            capture.Id,
            capture.ComputerId,
            capture.CapturedAt,
            capture.Status
        });
    }

    public async Task SendTimelineBatch(List<AgentTimelineEntryDto> events)
    {
        if (events is null || events.Count == 0) return;

        foreach (var e in events)
        {
            var computerId = ResolveComputerId(e.ComputerId);
            if (computerId == Guid.Empty)
            {
                _logger.LogWarning("Timeline entry without computer id: {EventType}", e.EventType);
                continue;
            }

            if (_tenantAccessor.TenantId == Guid.Empty)
            {
                var computer = await _computerRepo.GetByIdAsync(computerId);
                if (computer?.TenantId != null && computer.TenantId != Guid.Empty)
                    _tenantAccessor.SetTenantId(computer.TenantId);
            }

            var eventType = ParseEventType(e.EventType);
            var severity = ResolveSeverity(e.EventType, e.Severity);
            var timestamp = e.Timestamp == default ? DateTimeOffset.UtcNow : new DateTimeOffset(e.Timestamp, TimeSpan.Zero);

            var entry = new TimelineEntry(
                timestamp,
                eventType,
                string.IsNullOrWhiteSpace(e.Category) ? "General" : e.Category,
                e.Description ?? "",
                computerId,
                e.Username,
                e.Details,
                severity);

            await _timelineRepo.AddAsync(entry);

            if (SecurityRelevantTypes.Contains(e.EventType) || string.Equals(e.Category, "Security", StringComparison.OrdinalIgnoreCase)
                || string.Equals(e.Category, "Malware", StringComparison.OrdinalIgnoreCase)
                || string.Equals(e.Category, "USB", StringComparison.OrdinalIgnoreCase)
                || string.Equals(e.Category, "Software", StringComparison.OrdinalIgnoreCase))
            {
                await PersistSecurityEventAsync(computerId, e.EventType, e.Category, e.Description, e.Username, e.Details, severity);
            }
        }

        var firstComputerId = ResolveComputerId(events.First().ComputerId);
        if (firstComputerId != Guid.Empty)
        {
            await Clients.Group($"computer:{firstComputerId}")
                .SendAsync("NewTimelineBatch", events.Count);
            await Clients.Group("admins").SendAsync("NewTimelineBatch", events.Count);
        }
    }

    public async Task SendSecurityStatus(AgentSecurityStatusDto status)
    {
        var computerId = ResolveComputerId(status.ComputerId);
        if (computerId == Guid.Empty && !string.IsNullOrWhiteSpace(status.Hostname))
        {
            var existing = _computerRepo.Query().FirstOrDefault(c => c.Hostname == status.Hostname && !c.IsDeleted);
            if (existing != null) computerId = existing.Id;
        }

        if (computerId == Guid.Empty)
        {
            _logger.LogWarning("Security status without computer id from {Hostname}", status.Hostname);
            return;
        }

        if (_tenantAccessor.TenantId == Guid.Empty)
        {
            var computer = await _computerRepo.GetByIdAsync(computerId);
            if (computer?.TenantId != null && computer.TenantId != Guid.Empty)
                _tenantAccessor.SetTenantId(computer.TenantId);
        }

        var existingStatus = _securityStatusRepo.Query()
            .FirstOrDefault(s => s.ComputerId == computerId && !s.IsDeleted);

        if (existingStatus is null)
        {
            existingStatus = MapStatus(new EndpointSecurityStatus(), status, computerId);
            await _securityStatusRepo.AddAsync(existingStatus);
        }
        else
        {
            MapStatus(existingStatus, status, computerId);
            await _securityStatusRepo.UpdateAsync(existingStatus);
        }

        const int outdatedDays = 7;
        if (!status.RealTimeProtectionEnabled || !status.AntivirusEnabled)
        {
            await PersistSecurityEventAsync(
                computerId,
                "AntivirusDisabled",
                "Security",
                $"Antivirus disabled on {status.Hostname} ({status.AntivirusProductName})",
                null,
                $"RTP={status.RealTimeProtectionEnabled}, AV={status.AntivirusEnabled}",
                Severity.High);
        }
        else if (status.AntivirusSignatureAgeDays > outdatedDays)
        {
            await PersistSecurityEventAsync(
                computerId,
                "AntivirusOutdated",
                "Security",
                $"Antivirus signatures outdated ({status.AntivirusSignatureAgeDays} days) on {status.Hostname}",
                null,
                $"Product={status.AntivirusProductName}, LastUpdated={status.AntivirusSignatureLastUpdated}",
                Severity.High);
        }

        await Clients.Group("admins").SendAsync("SecurityStatusUpdated", new
        {
            ComputerId = computerId,
            status.Hostname,
            status.AntivirusEnabled,
            status.RealTimeProtectionEnabled,
            status.AntivirusSignatureAgeDays,
            status.AntivirusProductName
        });
    }

    public async Task SendSoftwareInventory(AgentSoftwareInventoryDto inventory)
    {
        var computerId = ResolveComputerId(inventory.ComputerId);
        if (computerId == Guid.Empty) return;

        if (_tenantAccessor.TenantId == Guid.Empty)
        {
            var computer = await _computerRepo.GetByIdAsync(computerId);
            if (computer?.TenantId != null && computer.TenantId != Guid.Empty)
                _tenantAccessor.SetTenantId(computer.TenantId);
        }

        var now = DateTimeOffset.UtcNow;
        var incoming = inventory.Items ?? new List<AgentSoftwareItemDto>();
        var existing = _softwareRepo.Query()
            .Where(s => s.ComputerId == computerId && !s.IsDeleted)
            .ToList();

        var incomingKeys = new HashSet<string>(
            incoming.Select(i => $"{i.Name}|{i.Version}"),
            StringComparer.OrdinalIgnoreCase);

        foreach (var item in incoming)
        {
            if (string.IsNullOrWhiteSpace(item.Name)) continue;

            var match = existing.FirstOrDefault(e =>
                string.Equals(e.Name, item.Name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(e.Version, item.Version ?? "", StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                await _softwareRepo.AddAsync(new SoftwareInventoryItem
                {
                    ComputerId = computerId,
                    TenantId = _tenantAccessor.TenantId,
                    Name = item.Name,
                    Version = item.Version ?? "",
                    Publisher = item.Publisher ?? "",
                    IsAuthorized = true,
                    Category = "Installed",
                    FirstDetected = now,
                    LastDetected = now,
                    InstallLocation = item.InstallLocation
                });
            }
            else
            {
                match.Publisher = item.Publisher ?? match.Publisher;
                match.LastDetected = now;
                match.InstallLocation = item.InstallLocation ?? match.InstallLocation;
                await _softwareRepo.UpdateAsync(match);
            }
        }

        foreach (var stale in existing.Where(e => !incomingKeys.Contains($"{e.Name}|{e.Version}")))
        {
            await _softwareRepo.DeleteAsync(stale);
        }

        _logger.LogInformation("Software inventory synced for {ComputerId}: {Count} items", computerId, incoming.Count);
    }

    public async Task SendCommand(Guid computerId, AgentCommand command)
    {
        await Clients.Group($"agent:{computerId}")
            .SendAsync("ExecuteCommand", command);
    }

    public async Task UpdateConfiguration(Guid computerId, AgentConfiguration config)
    {
        await Clients.Group($"agent:{computerId}")
            .SendAsync("ApplyConfiguration", config);
    }

    public async Task UpdateAgent(Guid computerId, AgentUpdateDto update)
    {
        await Clients.Group($"agent:{computerId}")
            .SendAsync("UpdateAgent", update);
    }

    public async Task ExecuteScript(Guid computerId, ScriptExecutionDto script)
    {
        await Clients.Group($"agent:{computerId}")
            .SendAsync("ExecuteScript", script);
    }

    public async Task SendRemoteScreenFrame(RemoteScreenFrameDto dto)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.SessionId)) return;

        await _remoteHubContext.Clients.Group($"session:{dto.SessionId}")
            .SendAsync("ScreenFrameReceived", new
            {
                dto.SessionId,
                dto.FrameData,
                dto.FrameNumber,
                dto.Timestamp
            });
    }

    private async Task<Computer> ResolveComputerAsync(Guid computerId, string hostname, string ipAddress, string currentUser, Guid? tenantId = null)
    {
        Computer? computer = null;
        if (computerId != Guid.Empty)
            computer = await _computerRepo.GetByIdAsync(computerId);

        if (computer is null && !string.IsNullOrWhiteSpace(hostname))
            computer = _computerRepo.Query().FirstOrDefault(c => c.Hostname == hostname && !c.IsDeleted);

        if (computer is null)
        {
            computer = computerId != Guid.Empty
                ? new Computer(computerId, hostname, ipAddress, "unknown")
                : new Computer(hostname, ipAddress, "unknown");
            computer.TenantId = tenantId ?? Guid.Empty;
            computer.UpdateHeartbeat(ipAddress, currentUser);
            computer.UpdateStatus(ComputerStatus.Online);
            await _computerRepo.AddAsync(computer);
            _logger.LogInformation("Computer auto-registered: {Hostname} ({Id}) for tenant {TenantId}", hostname, computer.Id, tenantId);
        }
        else
        {
            if (computer.TenantId == Guid.Empty && tenantId.HasValue && tenantId.Value != Guid.Empty)
            {
                computer.TenantId = tenantId.Value;
            }
            computer.UpdateStatus(ComputerStatus.Online);
            computer.UpdateHeartbeat(ipAddress, currentUser);
        }

        return computer;
    }

    private async Task PersistSecurityEventAsync(
        Guid computerId,
        string eventType,
        string? category,
        string? description,
        string? username,
        string? details,
        Severity severity)
    {
        var securityEvent = new SecurityEvent(
            computerId,
            eventType,
            string.IsNullOrWhiteSpace(category) ? "Security" : category,
            description ?? eventType,
            username,
            null,
            severity);

        if (!string.IsNullOrWhiteSpace(details))
            securityEvent.AddMetadata("details", details);

        await _securityEventRepo.AddAsync(securityEvent);

        var computer = await _computerRepo.GetByIdAsync(computerId);

        if (string.Equals(eventType, "USBConnected", StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventType, "USBDisconnected", StringComparison.OrdinalIgnoreCase))
        {
            var tenantId = computer?.TenantId is { } tid && tid != Guid.Empty
                ? tid
                : _tenantAccessor.TenantId;
            if (tenantId != Guid.Empty)
            {
                await _cache.RemoveAsync($"dashboard:overview:{tenantId}");
                await _cache.RemoveAsync($"dashboard:stats:{tenantId}");
            }
        }
        var payload = new
        {
            securityEvent.Id,
            securityEvent.ComputerId,
            ComputerName = computer?.Hostname,
            securityEvent.EventType,
            securityEvent.Category,
            securityEvent.Description,
            Severity = securityEvent.Severity.ToString(),
            securityEvent.Timestamp,
            Details = details
        };

        // Agent hub group (legacy / agents)
        await Clients.Group("admins").SendAsync("SecurityEvent", payload);

        // Web: todos os clientes autenticados dos hubs (não só o grupo "admins")
        await _monitoringHubContext.Clients.All.SendAsync("SecurityEvent", payload);
        await _alertHubContext.Clients.All.SendAsync("SecurityEvent", payload);
        await _alertHubContext.Clients.All.SendAsync("AlertCreated", new
        {
            Id = securityEvent.Id,
            Title = securityEvent.EventType,
            EventType = securityEvent.EventType,
            Description = securityEvent.Description,
            Severity = securityEvent.Severity.ToString(),
            Category = securityEvent.Category,
            ComputerId = securityEvent.ComputerId,
            ComputerName = computer?.Hostname,
            CreatedAt = securityEvent.Timestamp
        });

        await _monitoringHubContext.Clients.Group("admins").SendAsync("SecurityEvent", payload);
        await _alertHubContext.Clients.Group("security").SendAsync("SecurityEvent", payload);

        if (securityEvent.Severity is Severity.High or Severity.Critical)
        {
            await _alertHubContext.Clients.Group($"severity:{securityEvent.Severity}")
                .SendAsync("SecurityEvent", payload);
        }

        try
        {
            await _eventBus.PublishAsync(new SecurityEventBusMessage
            {
                Payload = securityEvent
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish security event to bus");
        }
    }

    private static EndpointSecurityStatus MapStatus(EndpointSecurityStatus entity, AgentSecurityStatusDto status, Guid computerId)
    {
        entity.ComputerId = computerId;
        entity.FirewallEnabled = status.FirewallEnabled;
        entity.DefenderEnabled = status.DefenderEnabled;
        entity.AntivirusEnabled = status.AntivirusEnabled;
        entity.RealTimeProtectionEnabled = status.RealTimeProtectionEnabled;
        entity.AntivirusSignatureAgeDays = status.AntivirusSignatureAgeDays;
        entity.AntivirusSignatureLastUpdated = status.AntivirusSignatureLastUpdated.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(status.AntivirusSignatureLastUpdated.Value, DateTimeKind.Utc))
            : null;
        entity.AntivirusProductName = status.AntivirusProductName ?? "";
        entity.BitlockerEnabled = status.BitlockerEnabled;
        entity.RdpEnabled = status.RdpEnabled;
        entity.CollectedAt = status.Timestamp == default
            ? DateTimeOffset.UtcNow
            : new DateTimeOffset(DateTime.SpecifyKind(status.Timestamp, DateTimeKind.Utc));
        return entity;
    }

    private static Guid ResolveComputerId(string? computerId)
    {
        return Guid.TryParse(computerId, out var id) ? id : Guid.Empty;
    }

    private static Guid ResolveComputerId(Guid computerId) => computerId;

    private static EventType ParseEventType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return EventType.Custom;
        if (Enum.TryParse<EventType>(value, true, out var parsed)) return parsed;
        // Legacy alias from older agents
        if (string.Equals(value, "FileTransfer", StringComparison.OrdinalIgnoreCase))
            return EventType.FileCopy;
        return EventType.Custom;
    }

    private static Severity ParseSeverity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Severity.Info;
        return Enum.TryParse<Severity>(value, true, out var parsed) ? parsed : Severity.Info;
    }

    /// <summary>
    /// Política de severidade: cópia para USB é sempre crítica (exfiltração de dados).
    /// </summary>
    private static Severity ResolveSeverity(string? eventType, string? severityValue)
    {
        if (string.Equals(eventType, "FileCopy", StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventType, "FileTransfer", StringComparison.OrdinalIgnoreCase))
            return Severity.Critical;

        return ParseSeverity(severityValue);
    }
}

public class SecurityEventBusMessage : IEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string EventType => "security.event";
    public string Source => "Sentinela.Api";
    public object Payload { get; init; } = null!;
}
