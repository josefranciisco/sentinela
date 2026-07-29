using Sentinela.Shared.Core.Entities;
using Sentinela.Shared.Domain.Monitoring.Enums;

namespace Sentinela.Shared.Domain.Monitoring;

public class Computer : AggregateRoot
{
    private readonly List<Heartbeat> _heartbeats = new();
    private readonly List<TimelineEntry> _timeline = new();
    private readonly List<string> _tags = new();

    protected Computer() : base() { }

    public Computer(string hostname, string ipAddress, string macAddress) : base()
    {
        Hostname = hostname;
        IpAddress = ipAddress;
        MacAddress = macAddress;
        Status = ComputerStatus.Offline;
        LastHeartbeat = DateTimeOffset.UtcNow;
    }

    public Computer(Guid id, string hostname, string ipAddress, string macAddress) : base(id)
    {
        Hostname = hostname;
        IpAddress = ipAddress;
        MacAddress = macAddress;
        Status = ComputerStatus.Offline;
        LastHeartbeat = DateTimeOffset.UtcNow;
    }

    public string Hostname { get; private set; }
    public string? Domain { get; private set; }
    public string IpAddress { get; private set; }
    public string MacAddress { get; private set; }
    public string? OsVersion { get; private set; }
    public DateTimeOffset LastHeartbeat { get; private set; }
    public ComputerStatus Status { get; private set; }
    public string? Department { get; private set; }
    public string? CurrentUser { get; private set; }
    public DateTimeOffset? LastBootTime { get; private set; }
    public string? AgentVersion { get; private set; }
    public bool IsAgentUpdated { get; private set; }
    public string? Notes { get; private set; }

    public IReadOnlyList<Heartbeat> Heartbeats => _heartbeats.AsReadOnly();
    public IReadOnlyList<TimelineEntry> Timeline => _timeline.AsReadOnly();
    public IReadOnlyList<string> Tags => _tags.AsReadOnly();

    public void UpdateStatus(ComputerStatus status)
    {
        Status = status;
        LastHeartbeat = DateTimeOffset.UtcNow;
    }

    public void UpdateHeartbeat(string ipAddress, string currentUser)
    {
        IpAddress = ipAddress;
        CurrentUser = currentUser;
        LastHeartbeat = DateTimeOffset.UtcNow;
    }

    public void AddHeartbeat(Heartbeat heartbeat)
    {
        _heartbeats.Add(heartbeat);
    }
}
