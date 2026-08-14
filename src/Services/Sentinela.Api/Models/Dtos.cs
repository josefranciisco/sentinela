namespace Sentinela.Api.Models;

public class PaginatedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class ComputerDto
{
    public Guid Id { get; set; }
    public string Hostname { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string CurrentUser { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public DateTime? LastHeartbeat { get; set; }
    public DateTime CreatedAt { get; set; }
    public int MonitorCount { get; set; } = 1;
}

public class ComputerDetailDto : ComputerDto
{
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string CpuInfo { get; set; } = string.Empty;
    public int RamMb { get; set; }
    public List<DiskInfoDto> Disks { get; set; } = new();
    public string MacAddress { get; set; } = string.Empty;
    public bool? FirewallEnabled { get; set; }
    public bool? DefenderEnabled { get; set; }
    public bool? AntivirusEnabled { get; set; }
    public bool? RealTimeProtectionEnabled { get; set; }
    public bool? BitlockerEnabled { get; set; }
    public bool? RdpEnabled { get; set; }
    public string? AntivirusProductName { get; set; }
    public int? AntivirusSignatureAgeDays { get; set; }
    public DateTime? AntivirusSignatureLastUpdated { get; set; }
    public DateTime? SecurityCollectedAt { get; set; }
}

public class UpdateComputerDto
{
    public string? Hostname { get; set; }
    public string? Department { get; set; }
}

public class ComputerSoftwareItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public bool IsAuthorized { get; set; }
    public string Category { get; set; } = string.Empty;
    public DateTimeOffset FirstDetected { get; set; }
    public DateTimeOffset LastDetected { get; set; }
    public string? InstallLocation { get; set; }
}

public class DiskInfoDto
{
    public string Drive { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public long TotalSpaceMb { get; set; }
    public long UsedSpaceMb { get; set; }
    public long FreeSpaceMb { get; set; }
}

public class TimelineEntryDto
{
    public Guid Id { get; set; }
    public Guid ComputerId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? ComputerName { get; set; }
}

public class AgentTimelineEntryDto
{
    public string ComputerId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Details { get; set; }
    public string Severity { get; set; } = "Info";
    public DateTime Timestamp { get; set; }
}

public class AgentSecurityStatusDto
{
    public string ComputerId { get; set; } = string.Empty;
    public bool FirewallEnabled { get; set; }
    public bool DefenderEnabled { get; set; }
    public bool AntivirusEnabled { get; set; }
    public bool RealTimeProtectionEnabled { get; set; }
    public int AntivirusSignatureAgeDays { get; set; }
    public DateTime? AntivirusSignatureLastUpdated { get; set; }
    public string AntivirusProductName { get; set; } = string.Empty;
    public bool BitlockerEnabled { get; set; }
    public bool RdpEnabled { get; set; }
    public string Hostname { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class AgentSoftwareInventoryDto
{
    public string ComputerId { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public List<AgentSoftwareItemDto> Items { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

public class AgentSoftwareItemDto
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public DateTime? InstallDate { get; set; }
    public string? InstallLocation { get; set; }
}

public class ApplicationUsageDto
{
    public string ProcessName { get; set; } = string.Empty;
    public string Name => ProcessName;
    public long TotalDuration { get; set; }
    public int ExecutionCount { get; set; }
    public int Count => ExecutionCount;
    public DateTimeOffset FirstSeen { get; set; }
    public DateTimeOffset LastSeen { get; set; }
}

public class AlertDto
{
    public Guid Id { get; set; }
    public Guid? ComputerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public string AssignedTo { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public class AlertDetailDto : AlertDto
{
    public List<AlertCommentDto> Comments { get; set; } = new();
}

public class AlertCommentDto
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class DashboardStatsDto
{
    public int TotalComputers { get; set; }
    public int OnlineComputers { get; set; }
    public int OfflineComputers { get; set; }
    public int AwayComputers { get; set; }
    public int DisabledComputers { get; set; }
    public int TotalUsers { get; set; }
    public int TotalDepartments { get; set; }
    public int TotalAlerts { get; set; }
    public int CriticalAlerts { get; set; }
    public int HighAlerts { get; set; }
}

public class HeatmapDto
{
    public DateTime Date { get; set; }
    public int Hour { get; set; }
    public int Count { get; set; }
    public int Value => Count;
}

public class AvailabilityDto
{
    public DateTime Date { get; set; }
    public int OnlineCount { get; set; }
    public int TotalCount { get; set; }
    public double Percentage => TotalCount > 0 ? Math.Round((double)OnlineCount / TotalCount * 100, 1) : 0;
}

public class TopUserDto
{
    public string Username { get; set; } = string.Empty;
    public int EventCount { get; set; }
    public DateTime LastActivity { get; set; }
}

public class SecurityOverviewDto
{
    public int OpenAlerts { get; set; }
    public int AcknowledgedAlerts { get; set; }
    public int ResolvedAlerts { get; set; }
    public int CriticalAlerts { get; set; }
    public int HighAlerts { get; set; }
    public int MediumAlerts { get; set; }
    public int LowAlerts { get; set; }
    public double AvgResponseTime { get; set; }
}

public class WorkflowDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Trigger { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public List<WorkflowConditionDto> Conditions { get; set; } = new();
    public List<WorkflowActionDto> Actions { get; set; } = new();
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class WorkflowConditionDto
{
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class WorkflowActionDto
{
    public string Type { get; set; } = string.Empty;
    public string Parameters { get; set; } = string.Empty;
    public int Order { get; set; }
}

public class WorkflowExecutionLogDto
{
    public Guid Id { get; set; }
    public Guid WorkflowId { get; set; }
    public string TriggeredBy { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Details { get; set; } = string.Empty;
}

public class AgentCommandDto
{
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
}

public class ScreenCaptureDto
{
    public Guid Id { get; set; }
    public Guid ComputerId { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? CapturedAt { get; set; }
}

public class ScreenCaptureRequestDto
{
    public Guid ComputerId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool CaptureAllMonitors { get; set; }
    public int? MonitorIndex { get; set; }
}

public class ScreenshotDto
{
    public Guid Id { get; set; }
    public Guid ComputerId { get; set; }
    public string RequestId { get; set; } = "";
    public string User { get; set; } = "";
    public string MonitorName { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public string MimeType { get; set; } = "";
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public string? ImageUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
}

public class CaptureConfigDto
{
    public bool Enabled { get; set; }
    public int IntervalSeconds { get; set; }
    public int Quality { get; set; }
    public int MaxStorageDays { get; set; }
    public bool RequireReason { get; set; }
    public bool NotifyUser { get; set; }
}

public class RemoteSessionDto
{
    public Guid Id { get; set; }
    public Guid ComputerId { get; set; }
    public string ComputerName { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public string SessionType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? TerminatedAt { get; set; }
    public int? MonitorIndex { get; set; }
}

public class RequestSessionDto
{
    public Guid ComputerId { get; set; }
    public string SessionType { get; set; } = string.Empty;
    public int? MonitorIndex { get; set; }
}

public class RemoteCommandDto
{
    public string Command { get; set; } = string.Empty;
    public string Parameters { get; set; } = string.Empty;
}

public class RemoteScreenFrameDto
{
    public string SessionId { get; set; } = string.Empty;
    public byte[] FrameData { get; set; } = Array.Empty<byte>();
    public long FrameNumber { get; set; }
    public DateTime Timestamp { get; set; }
}

public class SoftwareInventoryDto
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public int InstallCount { get; set; }
    public bool IsAuthorized { get; set; }
    public string Category { get; set; } = string.Empty;
    public DateTimeOffset FirstSeen { get; set; }
    public DateTimeOffset LastSeen { get; set; }
}

public class SecurityEventDto
{
    public Guid Id { get; set; }
    public Guid ComputerId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? SourceIp { get; set; }
    public DateTime Timestamp { get; set; }
    public string? ComputerName { get; set; }
}

public class EndpointSecurityStatusDto
{
    public Guid ComputerId { get; set; }
    public bool FirewallEnabled { get; set; }
    public bool DefenderEnabled { get; set; }
    public bool AntivirusEnabled { get; set; }
    public bool RealTimeProtectionEnabled { get; set; }
    public int AntivirusSignatureAgeDays { get; set; }
    public DateTime? AntivirusSignatureLastUpdated { get; set; }
    public string AntivirusProductName { get; set; } = string.Empty;
    public bool BitlockerEnabled { get; set; }
    public bool RdpEnabled { get; set; }
    public DateTime CollectedAt { get; set; }
}

public class SecurityComplianceDto
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
}

public class CorrelationRuleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string EventPattern { get; set; } = string.Empty;
    public int TimeWindow { get; set; }
    public int Threshold { get; set; }
    public string Severity { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

public class CreateCorrelationRuleDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string EventPattern { get; set; } = string.Empty;
    public int TimeWindow { get; set; }
    public int Threshold { get; set; }
    public string Severity { get; set; } = string.Empty;
}

public class SecuritySummaryDto
{
    public int EventsLast24h { get; set; }
    public int EventsLast7d { get; set; }
    public int CriticalEvents { get; set; }
    public int HighEvents { get; set; }
    public int OpenIncidents { get; set; }
    public int ComputersAtRisk { get; set; }
    public int ActiveCorrelationRules { get; set; }
    public List<ThreatCategoryDto> TopThreatCategories { get; set; } = new();
    public List<SecurityComplianceDto> Compliance { get; set; } = new();
}

public class ThreatCategoryDto
{
    public string Category { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class AuditLogEntryDto
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class KpiDashboardDto
{
    public int TotalEndpoints { get; set; }
    public int OnlineEndpoints { get; set; }
    public double EndpointComplianceRate { get; set; }
    public int TotalAlerts { get; set; }
    public int CriticalAlerts { get; set; }
    public double AlertResolutionRate { get; set; }
    public double AvgTimeToResolution { get; set; }
    public double SecurityScore { get; set; }
    public int DepartmentCount { get; set; }
    public int UserCount { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class AvailabilitySlaDto
{
    public double SlaTarget { get; set; }
    public double CurrentAvailability { get; set; }
    public List<MonthlyAvailabilityDto> MonthlyData { get; set; } = new();
}

public class MonthlyAvailabilityDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public double UptimePercentage { get; set; }
}

public class SecurityScoreDto
{
    public double CurrentScore { get; set; }
    public double AverageScore { get; set; }
    public List<DailySecurityScoreDto> DailyScores { get; set; } = new();
}

public class DailySecurityScoreDto
{
    public DateTime Date { get; set; }
    public double Score { get; set; }
    public int AlertCount { get; set; }
    public int CriticalCount { get; set; }
}

public class ResolveAlertDto
{
    public string? Comment { get; set; }
}

public class AssignAlertDto
{
    public string AssignedTo { get; set; } = string.Empty;
}

public class AddCommentDto
{
    public string Content { get; set; } = string.Empty;
}

public class UpdateAlertStatusDto
{
    public string Status { get; set; } = string.Empty;
}

public class CreateWorkflowDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Trigger { get; set; } = string.Empty;
    public List<CreateWorkflowConditionDto> Conditions { get; set; } = new();
    public List<CreateWorkflowActionDto> Actions { get; set; } = new();
}

public class CreateWorkflowConditionDto
{
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class CreateWorkflowActionDto
{
    public string Type { get; set; } = string.Empty;
    public string Parameters { get; set; } = string.Empty;
    public int Order { get; set; }
}

public class UpdateWorkflowDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Trigger { get; set; } = string.Empty;
}

public class HeartbeatDto
{
    public Guid ComputerId { get; set; }
    public string Hostname { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string CurrentUser { get; set; } = string.Empty;
}

/// <summary>
/// Accepts agent payloads where ComputerId is a string GUID.
/// </summary>
public class AgentHeartbeatDto
{
    public string ComputerId { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string CurrentUser { get; set; } = string.Empty;
    public int MonitorCount { get; set; } = 1;
    public Guid? TenantId { get; set; }
    public bool RecordingEnabled { get; set; }
    public DateTime? RecordingFromUtc { get; set; }
    public DateTime? RecordingToUtc { get; set; }
    public long RecordingBytes { get; set; }
    public bool RecordingInSchedule { get; set; } = true;
    public string? RecordingScheduleSummary { get; set; }
    public long RecordingMaxBytes { get; set; }
}

public class ScreenCaptureDataDto
{
    public Guid ComputerId { get; set; }
    public byte[] ImageData { get; set; } = Array.Empty<byte>();
    public string? CaptureRequestId { get; set; }
}

public class UpdateStatusDto
{
    public Guid ComputerId { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class AgentUpdateDto
{
    public string Version { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public bool Force { get; set; }
}

public class ScriptExecutionDto
{
    public string ScriptContent { get; set; } = string.Empty;
    public string ScriptType { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
}

public class InputEventDto
{
    public string Type { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public int Button { get; set; }
    public string Key { get; set; } = string.Empty;
    public bool Pressed { get; set; }
}

public class FileChunkDto
{
    public string SessionId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public int TotalChunks { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
}

public class IncidentDto
{
    public Guid Id { get; set; }
    public Guid ComputerId { get; set; }
    public string ComputerName { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<IncidentEventDto> Events { get; set; } = new();
    public DateTime Timestamp { get; set; }
    public int EventCount { get; set; }
}

public class IncidentEventDto
{
    public string EventType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
