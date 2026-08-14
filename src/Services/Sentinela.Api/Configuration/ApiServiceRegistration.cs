using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
        using Microsoft.Extensions.Options;
        using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Versioning;
using Sentinela.Api.Services;
using Sentinela.Shared.Core.Interfaces;

namespace Sentinela.Api.Configuration;

public static class ApiServiceRegistration
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        });

        var jwtSection = configuration.GetSection("Jwt");
        var secretKey = jwtSection["SecretKey"] ?? "super-secret-key-at-least-32-characters-long";

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSection["Issuer"] ?? "Sentinela",
                    ValidAudience = jwtSection["Audience"] ?? "Sentinela",
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                    ClockSkew = TimeSpan.FromMinutes(2),
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                    NameClaimType = System.Security.Claims.ClaimTypes.Name
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                            context.Token = accessToken;
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            });

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "Sentinela API", Version = "v1" });
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    Array.Empty<string>()
                }
            });
        });

        services.AddSignalR(options =>
            {
                options.MaximumReceiveMessageSize = 16 * 1024 * 1024;
            })
            .AddMessagePackProtocol();

        services.AddMemoryCache();
        services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = 2_147_483_647;
        });
        services.AddCors(options =>
        {
            options.AddPolicy("SentinelaCors", policy =>
            {
                policy.SetIsOriginAllowed(origin => IsAllowedCorsOrigin(origin, configuration))
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        services.AddHealthChecks();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
        services.AddAutoMapper(typeof(Program).Assembly);
        services.AddHttpClient();
        services.Configure<MonitoramentoOptions>(configuration.GetSection("Monitoramento"));
        services.AddHttpClient<MonitoramentoFleetClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<MonitoramentoOptions>>().Value;
            var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl) ? "http://192.168.0.116:8000" : options.BaseUrl.TrimEnd('/');
            client.BaseAddress = new Uri(baseUrl + "/");
            client.Timeout = TimeSpan.FromSeconds(8);
        });
        services.AddResponseCaching();
        services.AddResponseCompression();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddSingleton<RecordingVideoEncoder>();

        services.AddHostedService<ComputerPresenceWorker>();

        return services;
    }

    private static bool IsAllowedCorsOrigin(string? origin, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(origin)) return false;
        var configured = configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
        if (configured.Contains(origin, StringComparer.OrdinalIgnoreCase)) return true;
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
        var host = uri.Host;
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".trycloudflare.com", StringComparison.OrdinalIgnoreCase);
    }
}
