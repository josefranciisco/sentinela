using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Sentinela.Identity.Models;
using Sentinela.Identity.Services;
using Sentinela.Identity.Stores;
using Sentinela.Persistence;
using Sentinela.Shared.Core.Interfaces;
using Sentinela.Shared.Infrastructure.Time;

namespace Sentinela.Identity.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityServices(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtConfig = configuration.GetSection("Jwt").Get<JwtConfiguration>()
            ?? throw new InvalidOperationException("JWT configuration is required.");

        var ldapConfig = configuration.GetSection("Ldap").Get<LdapConfiguration>();
        var ssoConfig = configuration.GetSection("Sso").Get<SsoConfiguration>();

        services.Configure<JwtConfiguration>(configuration.GetSection("Jwt"));
        services.Configure<LdapConfiguration>(configuration.GetSection("Ldap"));
        services.Configure<SsoConfiguration>(configuration.GetSection("Sso"));

        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("IdentityConnection")));

        services.AddDbContext<SentinelaDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("SentinelaDb"))
                .UseSnakeCaseNamingConvention());

        services.AddScoped<ITenantAccessor, TenantAccessor>();
        services.AddScoped<IDateTime, UtcTimeProvider>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(SentinelaDbContext).Assembly));

        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            options.Password.RequireDigit = false;
            options.Password.RequiredLength = 4;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;
            options.User.RequireUniqueEmail = false;
            options.SignIn.RequireConfirmedEmail = false;
        })
        .AddEntityFrameworkStores<IdentityDbContext>()
        .AddDefaultTokenProviders();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtConfig.Issuer,
                ValidAudience = jwtConfig.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtConfig.Secret)),
                ClockSkew = TimeSpan.Zero
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            };
        });

        if (ssoConfig?.Enabled == true)
        {
            services.AddAuthentication()
                .AddOpenIdConnect(ssoConfig.DefaultScheme, options =>
                {
                    options.Authority = ssoConfig.Authority;
                    options.ClientId = ssoConfig.ClientId;
                    options.ClientSecret = ssoConfig.ClientSecret;
                    options.CallbackPath = ssoConfig.CallbackPath;
                    options.ResponseType = "code";
                    options.SaveTokens = true;
                    options.GetClaimsFromUserInfoEndpoint = true;
                    foreach (var scope in ssoConfig.Scopes)
                    {
                        options.Scope.Add(scope);
                    }
                });
        }

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin", "SuperAdmin"));
            options.AddPolicy("TwoFactorRequired", policy => policy.RequireClaim("amr", "mfa"));
        });

        services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend", builder =>
            {
                builder.WithOrigins(configuration.GetSection("Cors:Origins").Get<string[]>() ?? new[] { "http://localhost:3000" })
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            });
        });

        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Sentinela Identity API", Version = "v1" });
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme.",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer"
            });
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
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

        services.AddSignalR();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<ITwoFactorService, TwoFactorService>();

        if (ldapConfig is not null && !string.IsNullOrEmpty(ldapConfig.Server))
        {
            services.AddScoped<ILdapService, LdapService>();
        }

        if (ssoConfig?.Enabled == true)
        {
            services.AddScoped<ISsoService, SsoService>();
        }

        services.AddHttpContextAccessor();

        return services;
    }
}
