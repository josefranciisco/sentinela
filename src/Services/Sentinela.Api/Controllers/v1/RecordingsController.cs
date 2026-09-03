using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Sentinela.Api.Hubs;
using Sentinela.Api.Services;
using Sentinela.Shared.Core.Interfaces;

namespace Sentinela.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
public class RecordingsController : ControllerBase
{
    public const string MetaKeyPrefix = "recording:meta:";
    public const string FrameKeyPrefix = "recording:frame:";
    public const string ExportKeyPrefix = "recording:export:";

    private readonly IHubContext<AgentHub> _hub;
    private readonly ICacheService _cache;
    private readonly IWebHostEnvironment _env;
    private readonly RecordingVideoEncoder _videoEncoder;
    private readonly ILogger<RecordingsController> _logger;

    public RecordingsController(
        IHubContext<AgentHub> hub,
        ICacheService cache,
        IWebHostEnvironment env,
        RecordingVideoEncoder videoEncoder,
        ILogger<RecordingsController> logger)
    {
        _hub = hub;
        _cache = cache;
        _env = env;
        _videoEncoder = videoEncoder;
        _logger = logger;
    }

    [HttpGet("computers/{id:guid}/recording")]
    [Authorize]
    [RequirePermission("screenshots.view")]
    public async Task<IActionResult> GetStatus(Guid id)
    {
        var cached = await _cache.GetAsync<RecordingStatusDto>(MetaKeyPrefix + id);
        var listParams = JsonSerializer.Serialize(new { computerId = id });
        if (cached is null)
        {
            await SendCommand(id, "ListRecording", listParams);
            cached = await WaitFor(MetaKeyPrefix + id, TimeSpan.FromSeconds(8), () => _cache.GetAsync<RecordingStatusDto>(MetaKeyPrefix + id));
        }
        else
        {
            await SendCommand(id, "ListRecording", listParams);
        }

        return Ok(cached ?? new RecordingStatusDto { ComputerId = id.ToString(), Enabled = false });
    }

    [HttpGet("computers/{id:guid}/recording/frame")]
    [Authorize]
    [RequirePermission("screenshots.view")]
    public async Task<IActionResult> GetFrame(Guid id, [FromQuery] DateTime? at = null, [FromQuery] int monitorIndex = 0)
    {
        var requestId = Guid.NewGuid().ToString("N");
        var atUtc = (at ?? DateTime.UtcNow).ToUniversalTime();
        var parameters = JsonSerializer.Serialize(new { requestId, at = atUtc, computerId = id, monitorIndex });
        await SendCommand(id, "GetRecordingFrame", parameters);

        var frame = await WaitFor(FrameKeyPrefix + requestId, TimeSpan.FromSeconds(20),
            () => _cache.GetAsync<RecordingFrameDto>(FrameKeyPrefix + requestId));

        if (frame is null || string.IsNullOrEmpty(frame.ImageBase64))
            return NotFound(new { message = "Nenhum quadro de gravação nesse horário. A máquina precisa estar online e com o Agent atualizado." });

        return Ok(frame);
    }

    [HttpPost("computers/{id:guid}/recording/export")]
    [Authorize]
    [RequirePermission("screenshots.view")]
    public async Task<IActionResult> StartExport(Guid id, [FromBody] RecordingExportRequest dto)
    {
        var exportId = Guid.NewGuid().ToString("N");
        var to = (dto.To ?? DateTime.UtcNow).ToUniversalTime();
        var from = (dto.From ?? to.AddMinutes(-30)).ToUniversalTime();
        if (to - from > TimeSpan.FromHours(2))
            from = to.AddHours(-2);

        await _cache.SetAsync(ExportKeyPrefix + exportId, new RecordingExportDto
        {
            ExportId = exportId,
            ComputerId = id.ToString(),
            Status = "pending"
        }, TimeSpan.FromHours(2));

        var parameters = JsonSerializer.Serialize(new { exportId, from, to, computerId = id, monitorIndex = dto.MonitorIndex });
        await SendCommand(id, "ExportRecording", parameters);
        return Accepted(new { exportId, from, to });
    }

    [HttpGet("computers/{id:guid}/recording/exports/{exportId}")]
    [Authorize]
    [RequirePermission("screenshots.view")]
    public async Task<IActionResult> ExportStatus(Guid id, string exportId)
    {
        var item = await _cache.GetAsync<RecordingExportDto>(ExportKeyPrefix + exportId);
        if (item is null) return NotFound();
        return Ok(item);
    }

    [HttpGet("computers/{id:guid}/recording/exports/{exportId}/download")]
    [Authorize]
    [RequirePermission("screenshots.view")]
    public async Task<IActionResult> Download(Guid id, string exportId)
    {
        var item = await _cache.GetAsync<RecordingExportDto>(ExportKeyPrefix + exportId);
        if (item is null || string.IsNullOrWhiteSpace(item.FilePath) || !System.IO.File.Exists(item.FilePath))
            return NotFound();

        return PhysicalFile(item.FilePath, "video/mp4", $"sentinela-gravacao-{id:N}-{exportId}.mp4", enableRangeProcessing: true);
    }

    [HttpPost("recordings/ingest/status")]
    [AllowAnonymous]
    public async Task<IActionResult> IngestStatus([FromBody] RecordingStatusDto dto)
    {
        if (!Guid.TryParse(dto.ComputerId, out var computerId) || computerId == Guid.Empty)
            return BadRequest();
        dto.ComputerId = computerId.ToString();
        await _cache.SetAsync(MetaKeyPrefix + computerId, dto, TimeSpan.FromMinutes(5));
        return Ok();
    }

    [HttpPost("recordings/ingest/frame")]
    [AllowAnonymous]
    public async Task<IActionResult> IngestFrame([FromBody] RecordingFrameDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.RequestId))
            return BadRequest();
        await _cache.SetAsync(FrameKeyPrefix + dto.RequestId, dto, TimeSpan.FromMinutes(2));
        return Ok();
    }

    [HttpPost("recordings/ingest/export")]
    [AllowAnonymous]
    [RequestSizeLimit(2_000_000_000)]
    public async Task<IActionResult> IngestExport()
    {
        var form = await Request.ReadFormAsync();
        var exportId = form["ExportId"].FirstOrDefault();
        var computerId = form["ComputerId"].FirstOrDefault();
        var file = form.Files.GetFile("File");
        if (string.IsNullOrWhiteSpace(exportId) || file is null)
            return BadRequest();

        await _cache.SetAsync(ExportKeyPrefix + exportId, new RecordingExportDto
        {
            ExportId = exportId,
            ComputerId = computerId ?? "",
            Status = "encoding"
        }, TimeSpan.FromHours(2));

        var dir = Path.Combine(_env.ContentRootPath, "Storage", "Recordings");
        Directory.CreateDirectory(dir);
        var zipPath = Path.Combine(dir, $"{exportId}.zip");
        var mp4Path = Path.Combine(dir, $"{exportId}.mp4");
        await using (var stream = System.IO.File.Create(zipPath))
            await file.CopyToAsync(stream);

        try
        {
            await _videoEncoder.ZipToMp4Async(zipPath, mp4Path);
            try { System.IO.File.Delete(zipPath); } catch { /* ignore */ }

            await _cache.SetAsync(ExportKeyPrefix + exportId, new RecordingExportDto
            {
                ExportId = exportId,
                ComputerId = computerId ?? "",
                Status = "ready",
                FilePath = mp4Path,
                Bytes = new FileInfo(mp4Path).Length
            }, TimeSpan.FromHours(2));

            _logger.LogInformation("Recording export {ExportId} encoded to MP4 ({Bytes} bytes)", exportId, new FileInfo(mp4Path).Length);
            return Ok(new { exportId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encode recording export {ExportId}", exportId);
            await _cache.SetAsync(ExportKeyPrefix + exportId, new RecordingExportDto
            {
                ExportId = exportId,
                ComputerId = computerId ?? "",
                Status = "failed"
            }, TimeSpan.FromHours(1));
            return StatusCode(500, new { message = ex.Message });
        }
    }

    private async Task SendCommand(Guid computerId, string type, string parameters)
    {
        var command = new
        {
            CommandId = Guid.NewGuid().ToString(),
            CommandType = type,
            Parameters = parameters,
            ReceivedAt = DateTime.UtcNow
        };
        await _hub.Clients.Group($"agent:{computerId}")
            .SendAsync("ExecuteCommand", JsonSerializer.Serialize(command));
    }

    private static async Task<T?> WaitFor<T>(string _, TimeSpan timeout, Func<Task<T?>> factory) where T : class
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var value = await factory();
            if (value is not null) return value;
            await Task.Delay(200);
        }
        return await factory();
    }
}

public class RecordingStatusDto
{
    public string ComputerId { get; set; } = "";
    public bool Enabled { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public long Bytes { get; set; }
    public int SegmentCount { get; set; }
    public bool InSchedule { get; set; } = true;
    public string? ScheduleSummary { get; set; }
    public long MaxBytes { get; set; }
    public List<RecordingMonitorDto> Monitors { get; set; } = [];
    public List<RecordingSegmentDto> Segments { get; set; } = [];
}

public class RecordingSegmentDto
{
    public int MonitorIndex { get; set; }
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
}

public class RecordingMonitorDto
{
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsPrimary { get; set; }
}

public class RecordingFrameDto
{
    public string RequestId { get; set; } = "";
    public string ComputerId { get; set; } = "";
    public DateTime CapturedAt { get; set; }
    public string ImageBase64 { get; set; } = "";
}

public class RecordingExportRequest
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int MonitorIndex { get; set; }
}

public class RecordingExportDto
{
    public string ExportId { get; set; } = "";
    public string ComputerId { get; set; } = "";
    public string Status { get; set; } = "pending";
    public string? FilePath { get; set; }
    public long Bytes { get; set; }
}
