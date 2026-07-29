namespace Sentinela.Agent.Services;

public interface IAgentUpdateService
{
    Task<bool> CheckForUpdateAsync();
    Task<bool> DownloadUpdateAsync(string downloadUrl);
    Task<bool> ApplyUpdateAsync();
    Version? GetCurrentVersion();
}

public class AgentUpdateService : IAgentUpdateService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AgentUpdateService> _logger;
    private readonly string _updateDir;
    private string? _downloadedFilePath;

    public AgentUpdateService(IHttpClientFactory httpClientFactory, ILogger<AgentUpdateService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _updateDir = Path.Combine("C:\\ProgramData\\Sentinela\\Agent\\updates");
        Directory.CreateDirectory(_updateDir);
    }

    public Version? GetCurrentVersion()
    {
        return GetType().Assembly.GetName().Version;
    }

    public async Task<bool> CheckForUpdateAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("SentinelaApi");
            var response = await client.GetAsync("/api/agent/version");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                if (Version.TryParse(content, out var serverVersion))
                {
                    var current = GetCurrentVersion();
                    return current == null || serverVersion > current;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check for updates");
        }
        return false;
    }

    public async Task<bool> DownloadUpdateAsync(string downloadUrl)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("SentinelaApi");
            var response = await client.GetAsync(downloadUrl);
            if (!response.IsSuccessStatusCode) return false;

            var fileName = $"Sentinela.Agent.{DateTime.UtcNow:yyyyMMddHHmmss}.zip";
            _downloadedFilePath = Path.Combine(_updateDir, fileName);

            await using var fs = new FileStream(_downloadedFilePath, FileMode.Create, FileAccess.Write);
            await response.Content.CopyToAsync(fs);

            _logger.LogInformation("Update downloaded to {Path}", _downloadedFilePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download update");
            return false;
        }
    }

    public async Task<bool> ApplyUpdateAsync()
    {
        if (string.IsNullOrEmpty(_downloadedFilePath) || !File.Exists(_downloadedFilePath))
        {
            _logger.LogWarning("No update file to apply");
            return false;
        }

        try
        {
            var extractDir = Path.Combine(_updateDir, "extracted");
            if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
            Directory.CreateDirectory(extractDir);

            System.IO.Compression.ZipFile.ExtractToDirectory(_downloadedFilePath, extractDir);

            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            foreach (var file in Directory.GetFiles(extractDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(extractDir, file);
                var destPath = Path.Combine(appDir, relativePath);
                var destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);
                File.Copy(file, destPath, overwrite: true);
            }

            _logger.LogInformation("Update applied successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply update");
            return false;
        }
    }
}
