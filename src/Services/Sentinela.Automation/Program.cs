using Serilog;
using Sentinela.Persistence;
using Sentinela.MessageBus;
using Sentinela.Caching;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddSerilog();
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
    builder.Services.AddHttpClient();
    builder.Services.AddTransient<HttpClient>();
    builder.Services.AddPersistenceServices(builder.Configuration);
    builder.Services.AddMessageBus(builder.Configuration);
    builder.Services.AddCachingServices(builder.Configuration);
    builder.Services.AddAutomationServices(builder.Configuration);

    var host = builder.Build();

    using (var scope = host.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<SentinelaDbContext>();
        db.Database.EnsureCreated();
    }

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Automation Engine terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
