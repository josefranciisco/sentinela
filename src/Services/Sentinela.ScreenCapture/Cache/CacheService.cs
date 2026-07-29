using System.Collections.Concurrent;
using Sentinela.ScreenCapture.Interfaces;

namespace Sentinela.ScreenCapture.Cache;

public class CacheService : ICacheService, IDisposable
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly int _defaultTtlSeconds;
    private readonly Timer _cleanupTimer;
    private readonly ILogger<CacheService> _logger;

    public CacheService(ILogger<CacheService> logger)
    {
        _logger = logger;
        _defaultTtlSeconds = 10;
        _cleanupTimer = new Timer(Cleanup, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public Task<CachedCapture?> GetAsync(string requestId)
    {
        if (_cache.TryGetValue(requestId, out var entry) && !entry.IsExpired)
        {
            _logger.LogDebug("Cache hit for {RequestId}", requestId);
            entry.LastAccess = DateTime.UtcNow;
            return Task.FromResult<CachedCapture?>(entry.Capture);
        }
        _logger.LogDebug("Cache miss for {RequestId}", requestId);
        return Task.FromResult<CachedCapture?>(null);
    }

    public Task SetAsync(string requestId, CachedCapture capture, TimeSpan ttl)
    {
        _cache[requestId] = new CacheEntry
        {
            Capture = capture,
            ExpiresAt = DateTime.UtcNow.Add(ttl != TimeSpan.Zero ? ttl : TimeSpan.FromSeconds(_defaultTtlSeconds)),
            LastAccess = DateTime.UtcNow
        };
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string requestId)
    {
        _cache.TryRemove(requestId, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string requestId)
    {
        return Task.FromResult(_cache.TryGetValue(requestId, out var entry) && !entry.IsExpired);
    }

    private void Cleanup(object? state)
    {
        var now = DateTime.UtcNow;
        var expired = _cache.Where(kvp => kvp.Value.IsExpired).Select(kvp => kvp.Key).ToList();
        foreach (var key in expired)
            if (_cache.TryRemove(key, out _))
                _logger.LogTrace("Evicted expired cache entry {Key}", key);
    }

    public void Dispose() => _cleanupTimer.Dispose();

    private class CacheEntry
    {
        public CachedCapture Capture { get; set; } = new();
        public DateTime ExpiresAt { get; set; }
        public DateTime LastAccess { get; set; }
        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    }
}
