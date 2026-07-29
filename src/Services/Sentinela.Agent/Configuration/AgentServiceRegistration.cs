using Sentinela.ScreenCapture.Configuration;

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
        
        services.AddHostedService<HeartbeatWorker>();
        services.AddHostedService<CollectorWorker>();
        services.AddHostedService<CommunicationWorker>();
        services.AddHostedService<WatchdogWorker>();
        
        services.AddHttpClient("SentinelaApi", client =>
        {
            client.BaseAddress = new Uri(configuration["ServerConnection:ApiUrl"] ?? "https://localhost:5001");
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(3, retry => TimeSpan.FromSeconds(Math.Pow(2, retry))));
        
        services.AddSingleton<IIpcService, IpcService>();

        services.AddScreenCaptureServices(configuration);

        return services;
    }
}
