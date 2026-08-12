using Serilog;
using Sentinela.Caching;
using Sentinela.MessageBus;
using Sentinela.Persistence;
using Sentinela.Api.Configuration;
using Microsoft.EntityFrameworkCore.Infrastructure;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services.AddApiServices(builder.Configuration);
    builder.Services.AddPersistenceServices(builder.Configuration);
    builder.Services.AddMessageBus(builder.Configuration);
    builder.Services.AddCachingServices(builder.Configuration);

    var app = builder.Build();

    app.UseApiPipeline();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<SentinelaDbContext>();
        try
        {
            if (!await db.Database.CanConnectAsync())
            {
                await db.Database.EnsureCreatedAsync();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Database schema initialization warning");
        }
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
