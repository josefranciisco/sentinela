namespace Sentinela.Caching.Configuration;

public class RedisOptions
{
    public string ConnectionString { get; set; } = "localhost:6379";
    public string InstanceName { get; set; } = "Sentinela:";
    public int DefaultCacheMinutes { get; set; } = 60;
    public int HealthCheckIntervalSeconds { get; set; } = 30;
}
