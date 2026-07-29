using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Sentinela.Agent.Configuration;

namespace Sentinela.Agent.Services;

public interface IOfflineCacheService
{
    Task InitializeAsync();
    Task QueueEventAsync(string eventType, string payload);
    Task QueueScreenshotAsync(string payload);
    Task<List<CachedEvent>> GetPendingEventsAsync();
    Task<List<CachedScreenshot>> GetPendingScreenshotsAsync();
    Task MarkEventsAsSentAsync(IEnumerable<long> eventIds);
    Task MarkScreenshotsAsSentAsync(IEnumerable<long> screenshotIds);
    Task<int> GetQueueCountAsync();
    Task CleanupOldDataAsync(TimeSpan maxAge);
    Task SetLastSyncAsync(DateTime timestamp);
    DateTime? GetLastSync();
}

public class OfflineCacheService : IOfflineCacheService, IDisposable
{
    private readonly string _dbPath;
    private SqliteConnection? _connection;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private DateTime? _lastSync;
    
    public OfflineCacheService(IOptions<AgentOptions> options)
    {
        var dbDir = Path.Combine("C:\\ProgramData\\Sentinela\\Agent");
        Directory.CreateDirectory(dbDir);
        _dbPath = Path.Combine(dbDir, "cache.db");
    }
    
    public async Task InitializeAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _connection = new SqliteConnection($"Data Source={_dbPath}");
            await _connection.OpenAsync();
            
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS pending_events (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    EventType TEXT NOT NULL,
                    Payload TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    IsSent INTEGER DEFAULT 0
                );
                
                CREATE TABLE IF NOT EXISTS pending_screenshots (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Payload TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    IsSent INTEGER DEFAULT 0
                );
                
                CREATE TABLE IF NOT EXISTS config_cache (
                    Key TEXT PRIMARY KEY,
                    Value TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL
                );
                
                CREATE TABLE IF NOT EXISTS sync_status (
                    Key TEXT PRIMARY KEY,
                    Value TEXT NOT NULL
                );
            """;
            await cmd.ExecuteNonQueryAsync();
            
            await LoadLastSyncAsync();
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public async Task QueueEventAsync(string eventType, string payload)
    {
        await _lock.WaitAsync();
        try
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = "INSERT INTO pending_events (EventType, Payload, CreatedAt) VALUES (@type, @payload, @now)";
            cmd.Parameters.AddWithValue("@type", eventType);
            cmd.Parameters.AddWithValue("@payload", payload);
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public async Task QueueScreenshotAsync(string payload)
    {
        await _lock.WaitAsync();
        try
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = "INSERT INTO pending_screenshots (Payload, CreatedAt) VALUES (@payload, @now)";
            cmd.Parameters.AddWithValue("@payload", payload);
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public async Task<List<CachedEvent>> GetPendingEventsAsync()
    {
        var events = new List<CachedEvent>();
        await _lock.WaitAsync();
        try
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = "SELECT Id, EventType, Payload, CreatedAt FROM pending_events WHERE IsSent = 0 ORDER BY Id ASC LIMIT 100";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                events.Add(new CachedEvent
                {
                    Id = reader.GetInt64(0),
                    EventType = reader.GetString(1),
                    Payload = reader.GetString(2),
                    CreatedAt = DateTime.Parse(reader.GetString(3))
                });
            }
        }
        finally
        {
            _lock.Release();
        }
        return events;
    }
    
    public async Task<List<CachedScreenshot>> GetPendingScreenshotsAsync()
    {
        var screenshots = new List<CachedScreenshot>();
        await _lock.WaitAsync();
        try
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = "SELECT Id, Payload, CreatedAt FROM pending_screenshots WHERE IsSent = 0 ORDER BY Id ASC LIMIT 10";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                screenshots.Add(new CachedScreenshot
                {
                    Id = reader.GetInt64(0),
                    Payload = reader.GetString(1),
                    CreatedAt = DateTime.Parse(reader.GetString(2))
                });
            }
        }
        finally
        {
            _lock.Release();
        }
        return screenshots;
    }
    
    public async Task MarkEventsAsSentAsync(IEnumerable<long> eventIds)
    {
        await _lock.WaitAsync();
        try
        {
            foreach (var id in eventIds)
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = "UPDATE pending_events SET IsSent = 1 WHERE Id = @id";
                cmd.Parameters.AddWithValue("@id", id);
                await cmd.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public async Task MarkScreenshotsAsSentAsync(IEnumerable<long> screenshotIds)
    {
        await _lock.WaitAsync();
        try
        {
            foreach (var id in screenshotIds)
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = "UPDATE pending_screenshots SET IsSent = 1 WHERE Id = @id";
                cmd.Parameters.AddWithValue("@id", id);
                await cmd.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public async Task<int> GetQueueCountAsync()
    {
        await _lock.WaitAsync();
        try
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM pending_events WHERE IsSent = 0";
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public async Task CleanupOldDataAsync(TimeSpan maxAge)
    {
        await _lock.WaitAsync();
        try
        {
            var cutoff = DateTime.UtcNow - maxAge;
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = "DELETE FROM pending_events WHERE IsSent = 1 AND CreatedAt < @cutoff";
            cmd.Parameters.AddWithValue("@cutoff", cutoff.ToString("O"));
            await cmd.ExecuteNonQueryAsync();
            
            cmd.CommandText = "DELETE FROM pending_screenshots WHERE IsSent = 1 AND CreatedAt < @cutoff";
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public async Task SetLastSyncAsync(DateTime timestamp)
    {
        await _lock.WaitAsync();
        try
        {
            _lastSync = timestamp;
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = "INSERT OR REPLACE INTO sync_status (Key, Value) VALUES ('lastSync', @value)";
            cmd.Parameters.AddWithValue("@value", timestamp.ToString("O"));
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public DateTime? GetLastSync() => _lastSync;
    
    private async Task LoadLastSyncAsync()
    {
        try
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = "SELECT Value FROM sync_status WHERE Key = 'lastSync'";
            var result = await cmd.ExecuteScalarAsync();
            if (result is string str && DateTime.TryParse(str, out var dt))
            {
                _lastSync = dt;
            }
        }
        catch { }
    }
    
    public void Dispose()
    {
        _connection?.Dispose();
        _lock.Dispose();
    }
}

public class CachedEvent
{
    public long Id { get; set; }
    public string EventType { get; set; } = "";
    public string Payload { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class CachedScreenshot
{
    public long Id { get; set; }
    public string Payload { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
