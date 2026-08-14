using Sentinela.ScreenCapture.Configuration;
using Sentinela.Agent.Recording;
using Polly.Extensions.Http;

namespace Sentinela.Agent.Configuration;

public static class AgentServiceRegistration
{
    public static IServiceCollection AddAgentServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AgentOptions>(configuration.GetSection("Agent"));
        services.Configure<ServerConnectionOptions>(configuration.GetSection("ServerConnection"));
        
        services.AddSingleton<IActiveWindowCollector, ActiveWindowCollector>();
        services.AddSingleton<IUserSessionCollector, UserSessionCollector>();
        services.AddSingleton<IProcessCollector, ProcessCollector>();
        services.AddSingleton<IUsbCollector, UsbCollector>();
        services.AddSingleton<ISoftwareCollector, SoftwareCollector>();
        services.AddSingleton<ISecurityCollector, SecurityCollector>();
        services.AddSingleton<ISystemEventCollector, SystemEventCollector>();
        services.AddSingleton<IRecordingStore, RecordingStore>();
        services.AddSingleton<RecordingUploadClient>();
        services.AddSingleton<IScreenCaptureService, ScreenCaptureService>();
        
        services.AddSingleton<IAgentHealthService, AgentHealthService>();
        services.AddSingleton<IWatchdogService, WatchdogService>();
        
        services.AddSingleton<IAgentStateService, AgentStateService>();
        services.AddSingleton<IAgentUpdateService, AgentUpdateService>();
        services.AddSingleton<IOfflineCacheService, OfflineCacheService>();
        services.AddSingleton<ICommunicationService, CommunicationService>();
        services.AddSingleton<ICommandService, CommandService>();
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        
        services.AddSingleton<IAgentHubClient, AgentHubClient>();
        
        services.AddSingleton<ICryptominerDetector, CryptominerDetector>();
        services.AddSingleton<IRansomwareDetector, RansomwareDetector>();
        
        services.AddHostedService<ContinuousRecordingWorker>();
        services.AddHostedService<HeartbeatWorker>();
        services.AddHostedService<CollectorWorker>();
        services.AddHostedService<CommunicationWorker>();
        services.AddHostedService<WatchdogWorker>();
        services.AddHostedService<RemoteSessionWorker>();
        services.AddHostedService<CryptominerDetectorHost>();
        services.AddHostedService<RansomwareDetectorHost>();
        
        services.AddHttpClient("SentinelaApi", client =>
        {
            client.BaseAddress = new Uri(configuration["ServerConnection:ApiUrl"] ?? "https://localhost:5001");
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(3, retry => TimeSpan.FromSeconds(Math.Pow(2, retry))));

        services.AddHttpClient("SentinelaApiUpload", client =>
        {
            client.BaseAddress = new Uri(configuration["ServerConnection:ApiUrl"] ?? "https://localhost:5001");
            client.Timeout = TimeSpan.FromMinutes(10);
        });
        
        services.AddSingleton<IIpcService, IpcService>();

        services.AddScreenCaptureServices(configuration);

        return services;
    }
}
