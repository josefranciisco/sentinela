using Sentinela.Shared.Core.Entities;

namespace Sentinela.Shared.Domain.Monitoring;

public class UsbEvent : BaseEntity
{
    protected UsbEvent() : base() { }

    public UsbEvent(
        Guid computerId,
        string deviceId,
        string deviceName,
        string deviceType,
        bool isConnected,
        DateTimeOffset timestamp,
        string? username = null)
        : base()
    {
        ComputerId = computerId;
        DeviceId = deviceId;
        DeviceName = deviceName;
        DeviceType = deviceType;
        Action = isConnected ? UsbAction.Connected : UsbAction.Disconnected;
        Timestamp = timestamp;
        Username = username;
    }

    public Guid ComputerId { get; private set; }
    public string DeviceId { get; private set; }
    public string DeviceName { get; private set; }
    public string DeviceType { get; private set; }
    public string? SerialNumber { get; private set; }
    public string? VendorId { get; private set; }
    public string? ProductId { get; private set; }
    public UsbAction Action { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }
    public int FileCount { get; private set; }
    public string? Username { get; private set; }

    public enum UsbAction
    {
        Connected,
        Disconnected
    }
}
