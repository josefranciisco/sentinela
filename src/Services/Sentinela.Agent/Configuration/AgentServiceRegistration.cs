using Sentinela.Agent.Commands;
using Sentinela.Agent.Core.Collectors;
using Sentinela.Agent.Core.Health;
using Sentinela.Agent.Core.Monitors;
using Sentinela.Agent.Recording;
using Sentinela.Agent.Services;
using Sentinela.Agent.Workers;
using Sentinela.ScreenCapture.Configuration;
using Polly.Extensions.Http;

namespace Sentinela.Agent.Configuration;

public static class AgentServiceRegistration
{
    /// <summary>
    /// Presença mínima (heartbeat/SignalR) no serviço Sessão 0.
    /// Mantém a máquina Online mesmo sem usuário logado / processo interativo.
    /// </summary>
    public static IServiceCollection AddAgentPresenceServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AgentOptions>(configuration.GetSection("Agent"));
        services.Configure<ServerConnectionOptions>(configuration.GetSection("ServerConnection"));

        services.AddSingleton<IAgentStateService, AgentStateService>();
        services.AddSingleton<IOfflineCacheService, OfflineCacheService>();
        services.AddSingleton<ICommunicationService, CommunicationService>();
        services.AddSingleton<IAgentHubClient, AgentHubClient>();
        services.AddSingleton<IScreenCaptureService, Session0ScreenCaptureStub>();
        services.AddSingleton<IRecordingStore, NoopRecordingStore>();

        services.AddHostedService<HeartbeatWorker>();
        services.AddHostedService<CommunicationWorker>();

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

        return services;
    }

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

/// <summary>Evita GDI/BitBlt na Sessão 0; heartbeat de presença reporta 0 monitores.</summary>
internal sealed class Session0ScreenCaptureStub : IScreenCaptureService
{
    public Task<CapturedScreen?> CaptureAsync() => Task.FromResult<CapturedScreen?>(null);
    public Task<byte[]?> CaptureEncryptedAsync() => Task.FromResult<byte[]?>(null);
    public Task<byte[]?> CaptureCompressedAsync(int quality = 50) => Task.FromResult<byte[]?>(null);
    public Task<byte[]?> CaptureForStreamingAsync(int maxWidth = 1920, int quality = 50, int? monitorIndex = null) =>
        Task.FromResult<byte[]?>(null);
    public IReadOnlyList<MonitorInfo> GetMonitors() => Array.Empty<MonitorInfo>();
}

internal sealed class NoopRecordingStore : IRecordingStore
{
    public RecordingStatus GetStatus() => new() { Enabled = false, InSchedule = false };
    public void SetMonitors(IReadOnlyList<RecordingMonitorInfo> monitors) { }
    public void SetSchedule(bool inSchedule, string? summary) { }
    public void CloseOpenSegments() { }
    public void SetQuota(long maxBytes) { }
    public void AppendFrame(DateTime utc, byte[] jpeg, int monitorIndex) { }
    public byte[]? GetFrame(DateTime utc, int monitorIndex = 0) => null;
    public string CreateJpegZip(DateTime fromUtc, DateTime toUtc, int monitorIndex = 0) => string.Empty;
    public void Purge(TimeSpan retention, long maxBytes) { }
}
