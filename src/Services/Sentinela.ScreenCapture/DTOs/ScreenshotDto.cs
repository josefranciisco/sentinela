namespace Sentinela.ScreenCapture.DTOs;

public class ScreenshotRequestDto
{
    public string ComputerId { get; set; } = "";
    public string? Reason { get; set; }
    public int Quality { get; set; } = 80;
    public int? MonitorIndex { get; set; }
}

public class ScreenshotResponseDto
{
    public string Id { get; set; } = "";
    public string ComputerId { get; set; } = "";
    public string RequestId { get; set; } = "";
    public string User { get; set; } = "";
    public string MonitorName { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public string MimeType { get; set; } = "";
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
}

public class ScreenshotUploadRequestDto
{
    public string ComputerId { get; set; } = "";
    public string RequestId { get; set; } = "";
    public string MonitorName { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public string User { get; set; } = "";
    public string Hash { get; set; } = "";
    public long TimestampMs { get; set; }
}

public class PaginatedScreenshotsDto
{
    public List<ScreenshotResponseDto> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
