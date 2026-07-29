using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sentinela.Caching.Configuration;
using Sentinela.Caching.Services;
using Sentinela.Shared.Core.Interfaces;

namespace Sentinela.Caching;

public static class CachingServiceRegistration
{
    public static IServiceCollection AddCachingServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RedisOptions>(configuration.GetSection("Redis"));

        var redisOptions = configuration.GetSection("Redis").Get<RedisOptions>();
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisOptions?.ConnectionString ?? "localhost:6379";
            options.InstanceName = redisOptions?.InstanceName ?? "Sentinela:";
        });

        services.AddSingleton<ICacheService, RedisCacheService>();

        return services;
    }
}
