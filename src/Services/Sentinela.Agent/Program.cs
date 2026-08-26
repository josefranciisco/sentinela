using System.Diagnostics;
using Microsoft.Extensions.Hosting.WindowsServices;
using Serilog;
using Serilog.Events;

Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var showConsole = args.Any(a => string.Equals(a, "--console", StringComparison.OrdinalIgnoreCase));
var interactive = args.Any(a => string.Equals(a, "--interactive", StringComparison.OrdinalIgnoreCase));
var serviceHost = WindowsServiceHelpers.IsWindowsService() && !interactive;

var logConfig = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.SignalR", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.Http.Connections", LogEventLevel.Warning)
    .MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning)
    .WriteTo.File(
        "C:\\ProgramData\\Sentinela\\Agent\\logs\\agent-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        shared: true);

if (showConsole)
    logConfig = logConfig.WriteTo.Console();

Log.Logger = logConfig.CreateLogger();

Mutex? singleton = null;
try
{
    if (!serviceHost)
    {
        singleton = new Mutex(true, @"Global\SentinelaAgent.Interactive", out var created);
        if (!created)
        {
            Log.Information("Outra instância do agente já está na sessão do usuário");
            return;
        }
    }

    Log.Information(
        "Sentinela Agent starting (serviceHost={ServiceHost}, interactive={Interactive}, session={Session})",
        serviceHost, interactive, Process.GetCurrentProcess().SessionId);

    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddSerilog();
    builder.Services.Configure<HostOptions>(opts => opts.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

    if (serviceHost)
    {
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "SentinelaAgent";
        });
        // Sessão 0: presença (Online no painel) + launcher do processo interativo (captura/remoto).
        builder.Services.AddAgentPresenceServices(builder.Configuration);
        builder.Services.AddHostedService<InteractiveSessionLauncher>();
    }
    else
    {
        builder.Services.AddAgentServices(builder.Configuration);
    }

    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Agent terminated unexpectedly");
}
finally
{
    singleton?.Dispose();
    Log.CloseAndFlush();
}
