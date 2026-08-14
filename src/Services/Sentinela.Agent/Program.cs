using Serilog;
using Serilog.Events;

var showConsole = args.Any(a => string.Equals(a, "--console", StringComparison.OrdinalIgnoreCase));

var logConfig = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.SignalR", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.Http.Connections", LogEventLevel.Warning)
    .MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning)
    .WriteTo.File(
        "C:\\ProgramData\\Sentinela\\Agent\\logs\\agent-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7);

if (showConsole)
    logConfig = logConfig.WriteTo.Console();

Log.Logger = logConfig.CreateLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddSerilog();
    
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "SentinelaAgent";
    });
    
    builder.Services.AddAgentServices(builder.Configuration);
    
    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Agent terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
