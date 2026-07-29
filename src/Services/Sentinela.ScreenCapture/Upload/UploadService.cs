using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Sentinela.ScreenCapture.Interfaces;

namespace Sentinela.ScreenCapture.Upload;

public class UploadService : IUploadService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UploadService> _logger;

    public UploadService(HttpClient httpClient, ILogger<UploadService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<UploadResult> UploadAsync(ScreenshotUpload upload, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var form = new MultipartFormDataContent
            {
                { new StringContent(upload.ComputerId), "ComputerId" },
                { new StringContent(upload.RequestId), "RequestId" },
                { new StringContent(upload.MonitorName), "MonitorName" },
                { new StringContent(upload.Width.ToString()), "Width" },
                { new StringContent(upload.Height.ToString()), "Height" },
                { new StringContent(upload.User), "User" },
                { new StringContent(upload.Hash), "Hash" },
                { new StringContent(upload.TimestampMs.ToString()), "TimestampMs" },
            };

            var imageContent = new ByteArrayContent(upload.ImageData);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue(upload.ImageMimeType);
            form.Add(imageContent, "Image", $"capture.{upload.ImageMimeType.Split('/')[1]}");

            var thumbContent = new ByteArrayContent(upload.ThumbnailData);
            thumbContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            form.Add(thumbContent, "Thumbnail", "thumb.jpg");

            var response = await _httpClient.PostAsync("/api/v1/screencapture/upload", form, ct);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<UploadResponse>(ct);
                _logger.LogInformation("Uploaded screenshot {RequestId}: {Id}", upload.RequestId, result?.Id);
                return new UploadResult(true, result?.Id, null, sw.ElapsedMilliseconds);
            }

            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Upload failed for {RequestId}: {Status} {Error}", upload.RequestId, response.StatusCode, error);
            return new UploadResult(false, null, $"HTTP {response.StatusCode}: {error}", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Upload exception for {RequestId}", upload.RequestId);
            return new UploadResult(false, null, ex.Message, sw.ElapsedMilliseconds);
        }
    }

    public Task<int> GetPendingCountAsync() => Task.FromResult(0);

    private class UploadResponse
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
    }
}
