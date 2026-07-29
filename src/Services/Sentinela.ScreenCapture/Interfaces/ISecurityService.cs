namespace Sentinela.ScreenCapture.Interfaces;

public interface ISecurityService
{
    string ComputeHash(byte[] data);
    bool ValidateHash(byte[] data, string expectedHash);
}

public interface IAuditService
{
    Task LogAsync(AuditEntry entry);
    Task<IReadOnlyList<AuditEntry>> QueryAsync(string? computerId = null, DateTime? from = null, DateTime? to = null);
}

public class AuditEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string AdminName { get; set; } = "";
    public string AdminIp { get; set; } = "";
    public string ComputerId { get; set; } = "";
    public string ComputerName { get; set; } = "";
    public string StationUser { get; set; } = "";
    public string Reason { get; set; } = "";
    public string RequestId { get; set; } = "";
    public string Result { get; set; } = "";
    public long CaptureTimeMs { get; set; }
    public long UploadTimeMs { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
