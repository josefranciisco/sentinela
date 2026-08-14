using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Sentinela.Agent.Recording;

public class RecordingUploadClient
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<RecordingUploadClient> _logger;

    public RecordingUploadClient(
        IHttpClientFactory httpFactory,
        ILogger<RecordingUploadClient> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task PostStatusAsync(string computerId, RecordingStatus status, CancellationToken ct)
    {
        var client = _httpFactory.CreateClient("SentinelaApi");
        var payload = new
        {
            computerId,
            enabled = status.Enabled,
            fromUtc = status.FromUtc,
            toUtc = status.ToUtc,
            bytes = status.Bytes,
            segmentCount = status.SegmentCount,
            monitors = status.Monitors,
            segments = status.Segments,
            inSchedule = status.InSchedule,
            scheduleSummary = status.ScheduleSummary,
            maxBytes = status.MaxBytes
        };
        var response = await client.PostAsJsonAsync("/api/v1/recordings/ingest/status", payload, ct);
        if (!response.IsSuccessStatusCode)
            _logger.LogWarning("Recording status ingest failed: {Status}", response.StatusCode);
    }

    public async Task PostFrameAsync(string requestId, string computerId, DateTime atUtc, byte[] jpeg, CancellationToken ct)
    {
        var client = _httpFactory.CreateClient("SentinelaApi");
        var payload = new
        {
            requestId,
            computerId,
            capturedAt = atUtc,
            imageBase64 = Convert.ToBase64String(jpeg)
        };
        var response = await client.PostAsJsonAsync("/api/v1/recordings/ingest/frame", payload, ct);
        if (!response.IsSuccessStatusCode)
            _logger.LogWarning("Recording frame ingest failed: {Status}", response.StatusCode);
    }

    public async Task PostExportAsync(string exportId, string computerId, string zipPath, CancellationToken ct)
    {
        var client = _httpFactory.CreateClient("SentinelaApiUpload");
        await using var file = File.OpenRead(zipPath);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(exportId), "ExportId");
        form.Add(new StringContent(computerId), "ComputerId");
        var fileContent = new StreamContent(file);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        form.Add(fileContent, "File", Path.GetFileName(zipPath));

        var response = await client.PostAsync("/api/v1/recordings/ingest/export", form, ct);
        if (!response.IsSuccessStatusCode)
            _logger.LogWarning("Recording export ingest failed: {Status}", response.StatusCode);
    }
}
