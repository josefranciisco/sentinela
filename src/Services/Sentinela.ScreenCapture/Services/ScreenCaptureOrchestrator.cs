using System.Diagnostics;
using Sentinela.ScreenCapture.Interfaces;
using Sentinela.ScreenCapture.DTOs;

namespace Sentinela.ScreenCapture.Services;

public interface IScreenCaptureOrchestrator
{
    Task<CaptureResultDto> ExecuteCaptureAsync(CaptureCommandDto command, CancellationToken ct = default);
    IReadOnlyList<MonitorInfo> GetMonitors();
}

public class ScreenCaptureOrchestrator : IScreenCaptureOrchestrator
{
    private readonly ICaptureService _captureService;
    private readonly ICompressionService _compressionService;
    private readonly IThumbnailService _thumbnailService;
    private readonly ICacheService _cacheService;
    private readonly IUploadService _uploadService;
    private readonly ISecurityService _securityService;
    private readonly IAuditService _auditService;
    private readonly ILogger<ScreenCaptureOrchestrator> _logger;

    public ScreenCaptureOrchestrator(
        ICaptureService captureService, ICompressionService compressionService,
        IThumbnailService thumbnailService, ICacheService cacheService,
        IUploadService uploadService, ISecurityService securityService,
        IAuditService auditService, ILogger<ScreenCaptureOrchestrator> logger)
    {
        _captureService = captureService;
        _compressionService = compressionService;
        _thumbnailService = thumbnailService;
        _cacheService = cacheService;
        _uploadService = uploadService;
        _securityService = securityService;
        _auditService = auditService;
        _logger = logger;
    }

    public IReadOnlyList<MonitorInfo> GetMonitors() => _captureService.GetMonitors();

    public async Task<CaptureResultDto> ExecuteCaptureAsync(CaptureCommandDto command, CancellationToken ct = default)
    {
        var result = new CaptureResultDto { RequestId = command.RequestId };

        _logger.LogInformation("Starting capture {RequestId} for computer {ComputerId}",
            command.RequestId, command.ComputerId);

        try
        {
            if (await _cacheService.ExistsAsync(command.RequestId))
            {
                var cached = await _cacheService.GetAsync(command.RequestId);
                if (cached != null)
                {
                    _logger.LogInformation("Cache hit for {RequestId}", command.RequestId);
                    return new CaptureResultDto
                    {
                        RequestId = command.RequestId, Success = true,
                        Width = cached.Width, Height = cached.Height,
                        MonitorName = cached.MonitorName,
                        ImageData = cached.ImageData, ThumbnailData = cached.ThumbnailData
                    };
                }
            }

            var capture = await _captureService.CaptureAsync(new CaptureOptions
            {
                MonitorIndex = command.MonitorIndex,
                Quality = command.Quality,
                CaptureAllMonitors = command.CaptureAllMonitors
            }, ct);

            result.CaptureTimeMs = capture.TimestampMs;
            result.Width = capture.Width;
            result.Height = capture.Height;
            result.MonitorName = capture.MonitorName;

            var compressed = _compressionService.Compress(capture.ImageData, new CompressionOptions
            {
                Format = _compressionService.IsWebPSupported ? CompressionFormat.WebP : CompressionFormat.Jpeg,
                Quality = command.Quality,
                MaxWidth = 3840, MaxHeight = 2160
            });
            result.MimeType = compressed.MimeType;

            var thumbnail = _thumbnailService.GenerateThumbnail(capture.ImageData);
            var hash = _securityService.ComputeHash(compressed.Data);

            await _cacheService.SetAsync(command.RequestId, new CachedCapture
            {
                ImageData = compressed.Data, ThumbnailData = thumbnail.Data,
                Width = capture.Width, Height = capture.Height,
                MonitorName = capture.MonitorName, CapturedAt = DateTime.UtcNow
            }, TimeSpan.FromSeconds(30));

            var uploadResult = await _uploadService.UploadAsync(new ScreenshotUpload
            {
                ComputerId = command.ComputerId, RequestId = command.RequestId,
                MonitorName = capture.MonitorName, Width = capture.Width, Height = capture.Height,
                ImageData = compressed.Data, ThumbnailData = thumbnail.Data,
                ImageMimeType = compressed.MimeType, User = Environment.UserName, Hash = hash,
                TimestampMs = capture.TimestampMs
            }, ct);

            result.UploadTimeMs = uploadResult.ElapsedMs;
            result.Success = uploadResult.Success;
            result.ScreenshotId = uploadResult.ScreenshotId;
            result.ErrorMessage = uploadResult.ErrorMessage;
            result.ImageData = compressed.Data;
            result.ThumbnailData = thumbnail.Data;

            await _auditService.LogAsync(new AuditEntry
            {
                RequestId = command.RequestId, AdminName = command.RequestedBy ?? "system",
                ComputerId = command.ComputerId, Reason = command.Reason ?? "On-demand capture",
                Result = result.Success ? "Success" : "Failed",
                CaptureTimeMs = result.CaptureTimeMs, UploadTimeMs = result.UploadTimeMs
            });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Capture failed for {RequestId}", command.RequestId);
            result.ErrorMessage = ex.Message;
            await _auditService.LogAsync(new AuditEntry
            {
                RequestId = command.RequestId, AdminName = command.RequestedBy ?? "system",
                ComputerId = command.ComputerId, Reason = command.Reason ?? "On-demand capture",
                Result = $"Failed: {ex.Message}", CaptureTimeMs = result.CaptureTimeMs
            });
            return result;
        }
    }
}
