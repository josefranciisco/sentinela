using Sentinela.Shared.Core.Entities;
using Sentinela.Shared.Domain.Monitoring.Enums;

namespace Sentinela.Shared.Domain.Monitoring;

public class Heartbeat : BaseEntity
{
    protected Heartbeat() : base() { }

    public Heartbeat(DateTimeOffset timestamp, ComputerStatus status, double cpuUsage, double memoryUsage, double diskUsage, long uptime)
        : base()
    {
        Timestamp = timestamp;
        Status = status;
        CpuUsage = cpuUsage;
        MemoryUsage = memoryUsage;
        DiskUsage = diskUsage;
        Uptime = uptime;
    }

    public Guid ComputerId { get; set; }
    public DateTimeOffset Timestamp { get; private set; }
    public ComputerStatus Status { get; private set; }
    public double CpuUsage { get; private set; }
    public double MemoryUsage { get; private set; }
    public double DiskUsage { get; private set; }
    public long Uptime { get; private set; }
    public int ConnectedUsers { get; private set; }
}
