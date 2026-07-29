using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Sentinela.Api.Hubs;
using Sentinela.Persistence.Models;

namespace Sentinela.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/screencapture")]
[Authorize]
public class ScreenCaptureController : ControllerBase
{
    private readonly IRepository<Screenshot> _screenshotRepo;
    private readonly IRepository<Computer> _computerRepo;
    private readonly ICacheService _cache;
    private readonly IMapper _mapper;
    private readonly IHubContext<AgentHub> _hubContext;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ScreenCaptureController> _logger;

    public ScreenCaptureController(
        IRepository<Screenshot> screenshotRepo,
        IRepository<Computer> computerRepo,
        ICacheService cache,
        IMapper mapper,
        IHubContext<AgentHub> hubContext,
        IWebHostEnvironment env,
        ILogger<ScreenCaptureController> logger)
    {
        _screenshotRepo = screenshotRepo;
        _computerRepo = computerRepo;
        _cache = cache;
        _mapper = mapper;
        _hubContext = hubContext;
        _env = env;
        _logger = logger;
    }

    [HttpPost("request")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> RequestCapture([FromBody] ScreenCaptureRequestDto dto)
    {
        var requestId = Guid.NewGuid();
        var username = User.Identity?.Name ?? "unknown";
        var adminIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";

        var parameters = JsonSerializer.Serialize(new
        {
            requestId = requestId.ToString(),
            captureAllMonitors = dto.CaptureAllMonitors
        });

        var command = new
        {
            CommandId = requestId.ToString(),
            CommandType = "CaptureScreen",
            Parameters = parameters,
            ReceivedAt = DateTime.UtcNow
        };
        var commandJson = JsonSerializer.Serialize(command);

        await _hubContext.Clients.Group($"agent:{dto.ComputerId}")
            .SendAsync("ExecuteCommand", commandJson);

        _logger.LogInformation("Screen capture requested for computer {ComputerId} by {User}, requestId={RequestId}",
            dto.ComputerId, username, requestId);

        return Accepted(new { requestId });
    }

    [HttpPost("upload")]
    [AllowAnonymous]
    public async Task<IActionResult> UploadScreenshot()
    {
        var form = await Request.ReadFormAsync();
        var computerId = form["ComputerId"].FirstOrDefault() ?? "";
        var requestId = form["RequestId"].FirstOrDefault() ?? Guid.NewGuid().ToString();
        var monitorName = form["MonitorName"].FirstOrDefault() ?? "Primary";
        var width = int.TryParse(form["Width"].FirstOrDefault(), out var w) ? w : 0;
        var height = int.TryParse(form["Height"].FirstOrDefault(), out var h) ? h : 0;
        var user = form["User"].FirstOrDefault() ?? "";
        var hash = form["Hash"].FirstOrDefault() ?? "";
        var imageFile = form.Files.GetFile("Image");
        var thumbnailFile = form.Files.GetFile("Thumbnail");

        if (imageFile == null)
            return BadRequest("Image file is required");

        var storagePath = Path.Combine(_env.ContentRootPath, "Storage", "Screenshots");
        Directory.CreateDirectory(storagePath);

        var fileName = $"{requestId}_{DateTime.UtcNow:yyyyMMddHHmmss}";
        var imagePath = Path.Combine(storagePath, $"{fileName}.jpg");
        var thumbnailPath = Path.Combine(storagePath, $"{fileName}_thumb.jpg");

        using (var stream = new FileStream(imagePath, FileMode.Create))
            await imageFile.CopyToAsync(stream);

        if (thumbnailFile != null)
        {
            using var stream = new FileStream(thumbnailPath, FileMode.Create);
            await thumbnailFile.CopyToAsync(stream);
        }

        var screenshot = new Screenshot
        {
            ComputerId = Guid.TryParse(computerId, out var cid) ? cid : Guid.Empty,
            RequestId = requestId,
            MonitorName = monitorName,
            Width = width,
            Height = height,
            Hash = hash,
            ImagePath = imagePath,
            ThumbnailPath = thumbnailFile != null ? thumbnailPath : null,
            MimeType = "image/jpeg",
            Size = imageFile.Length,
            User = user,
            CreatedBy = form["CreatedBy"].FirstOrDefault() ?? "agent"
        };

        await _screenshotRepo.AddAsync(screenshot);

        _logger.LogInformation("Screenshot stored: {Id} for computer {ComputerId}, size={Size}", screenshot.Id, computerId, imageFile.Length);

        return Ok(new { id = screenshot.Id.ToString(), screenshot.ImagePath, screenshot.ThumbnailPath });
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedResult<ScreenshotDto>>> GetScreenshots(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] Guid? computerId = null,
        [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var query = _screenshotRepo.Query().Where(s => !s.IsDeleted);

        if (computerId.HasValue)
            query = query.Where(s => s.ComputerId == computerId.Value);
        if (from.HasValue)
            query = query.Where(s => s.CreatedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(s => s.CreatedAt <= to.Value);

        query = query.OrderByDescending(s => s.CreatedAt);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var result = _mapper.Map<List<ScreenshotDto>>(items);
        foreach (var item in result)
        {
            item.ImageUrl = $"/api/v1/screencapture/{item.Id}/image";
            item.ThumbnailUrl = $"/api/v1/screencapture/thumbnail/{item.Id}";
        }

        return Ok(new PaginatedResult<ScreenshotDto>
        {
            Items = result,
            Total = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ScreenshotDto>> GetScreenshot(Guid id)
    {
        var screenshot = await _screenshotRepo.GetByIdAsync(id);
        if (screenshot == null) return NotFound();

        var dto = _mapper.Map<ScreenshotDto>(screenshot);
        dto.ImageUrl = Url.Action("GetImage", new { id });
        dto.ThumbnailUrl = Url.Action("GetThumbnail", new { id });
        return Ok(dto);
    }

    [HttpGet("{id}/image")]
    [AllowAnonymous]
    public async Task<IActionResult> GetImage(Guid id)
    {
        var screenshot = await _screenshotRepo.GetByIdAsync(id);
        if (screenshot == null) return NotFound();
        if (!System.IO.File.Exists(screenshot.ImagePath))
            return NotFound("Image file not found");

        var stream = new FileStream(screenshot.ImagePath, FileMode.Open, FileAccess.Read);
        return File(stream, screenshot.MimeType ?? "image/jpeg");
    }

    [HttpGet("thumbnail/{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetThumbnail(Guid id)
    {
        var screenshot = await _screenshotRepo.GetByIdAsync(id);
        if (screenshot == null) return NotFound();

        var thumbPath = screenshot.ThumbnailPath ?? screenshot.ImagePath;
        if (!System.IO.File.Exists(thumbPath))
            return NotFound("Thumbnail not found");

        var stream = new FileStream(thumbPath, FileMode.Open, FileAccess.Read);
        return File(stream, "image/jpeg");
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> DeleteScreenshot(Guid id)
    {
        var screenshot = await _screenshotRepo.GetByIdAsync(id);
        if (screenshot == null) return NotFound();

        if (System.IO.File.Exists(screenshot.ImagePath))
            System.IO.File.Delete(screenshot.ImagePath);
        if (screenshot.ThumbnailPath != null && System.IO.File.Exists(screenshot.ThumbnailPath))
            System.IO.File.Delete(screenshot.ThumbnailPath);

        await _screenshotRepo.DeleteAsync(screenshot);
        return NoContent();
    }

    [HttpGet("config")]
    public async Task<ActionResult<CaptureConfigDto>> GetConfiguration()
    {
        var config = await _cache.GetOrCreateAsync("screencapture:config", async () =>
        {
            return new CaptureConfigDto
            {
                Enabled = true,
                IntervalSeconds = 30,
                Quality = 80,
                MaxStorageDays = 90,
                RequireReason = true,
                NotifyUser = true
            };
        }, TimeSpan.FromMinutes(5));

        return Ok(config);
    }
}
