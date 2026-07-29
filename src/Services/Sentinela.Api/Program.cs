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
            // EnsureCreated skips when any tables exist (e.g. Identity shares the DB).
            // CreateTables builds missing Sentinela schema in that case.
            if (!await db.Database.CanConnectAsync())
            {
                await db.Database.EnsureCreatedAsync();
            }
            else
            {
                var creator = (Microsoft.EntityFrameworkCore.Storage.RelationalDatabaseCreator)
                    db.GetService<Microsoft.EntityFrameworkCore.Storage.IDatabaseCreator>();
                try
                {
                    await creator.CreateTablesAsync();
                }
                catch (Exception ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase)
                    || ex.ToString().Contains("42P07", StringComparison.Ordinal))
                {
                    // Tables already present — safe to ignore
                }
            }

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "ScreenCaptures" (
                    "Id" uuid NOT NULL,
                    "ComputerId" uuid NOT NULL,
                    "ImageData" bytea,
                    "CapturedAt" timestamptz NOT NULL DEFAULT now(),
                    "Status" integer NOT NULL DEFAULT 0,
                    "RequestedBy" text,
                    "Reason" text,
                    "RequestedAt" timestamptz NOT NULL DEFAULT now(),
                    "IsDeleted" boolean NOT NULL DEFAULT false,
                    "CreatedAt" timestamptz NOT NULL DEFAULT now(),
                    "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
                    CONSTRAINT "PK_ScreenCaptures" PRIMARY KEY ("Id")
                );
                CREATE INDEX IF NOT EXISTS "IX_ScreenCaptures_ComputerId" ON "ScreenCaptures" ("ComputerId");

                CREATE TABLE IF NOT EXISTS screenshots (
                    id uuid NOT NULL,
                    computer_id uuid NOT NULL,
                    request_id text NOT NULL,
                    "user" text,
                    monitor_name text,
                    width integer NOT NULL DEFAULT 0,
                    height integer NOT NULL DEFAULT 0,
                    hash text,
                    image_path text NOT NULL,
                    thumbnail_path text,
                    mime_type text NOT NULL DEFAULT 'image/jpeg',
                    size bigint NOT NULL DEFAULT 0,
                    created_by text,
                    is_deleted boolean NOT NULL DEFAULT false,
                    created_at timestamptz NOT NULL DEFAULT now(),
                    updated_at timestamptz,
                    deleted_at timestamptz,
                    CONSTRAINT pk_screenshots PRIMARY KEY (id)
                );
                CREATE INDEX IF NOT EXISTS ix_screenshots_computer_id ON screenshots (computer_id);
                CREATE UNIQUE INDEX IF NOT EXISTS ix_screenshots_request_id ON screenshots (request_id);
                CREATE INDEX IF NOT EXISTS ix_screenshots_created_at ON screenshots (created_at);
            """);
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
