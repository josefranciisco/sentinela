using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sentinela.ScreenCapture.Cache;
using Sentinela.ScreenCapture.Capture;
using Sentinela.ScreenCapture.Compression;
using Sentinela.ScreenCapture.Interfaces;
using Sentinela.ScreenCapture.Security;
using Sentinela.ScreenCapture.Services;
using Sentinela.ScreenCapture.Thumbnail;
using Sentinela.ScreenCapture.Upload;

namespace Sentinela.ScreenCapture.Configuration;

public static class ScreenCaptureServiceRegistration
{
    public static IServiceCollection AddScreenCaptureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ScreenCaptureOptions>(configuration.GetSection("ScreenCapture"));

        services.AddSingleton<ICaptureService, CaptureService>();
        services.AddSingleton<ICompressionService, CompressionService>();
        services.AddSingleton<IThumbnailService, ThumbnailService>();
        services.AddSingleton<ICacheService, CacheService>();
        services.AddSingleton<ISecurityService, SecurityService>();
        services.AddSingleton<IAuditService, AuditService>();

        services.AddHttpClient<IUploadService, UploadService>(client =>
        {
            var baseUrl = configuration["ScreenCapture:ApiBaseUrl"] ?? "http://localhost:5002";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddSingleton<IScreenCaptureOrchestrator, ScreenCaptureOrchestrator>();

        return services;
    }
}
