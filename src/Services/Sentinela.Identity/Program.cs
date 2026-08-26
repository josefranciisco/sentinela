using Sentinela.Identity.Configuration;
using Sentinela.Identity.Models;
using Sentinela.Identity.Stores;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console();
    });

    builder.Services.AddIdentityServices(builder.Configuration);

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    app.UseCors("AllowFrontend");

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Sentinela Identity API v1");
        });
    }

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        // After PC reboot Postgres can report healthy while still accepting connections briefly —
        // retry so Identity does not crash once and leave nginx pointing at a dead container.
        const int maxDbAttempts = 30;
        for (var attempt = 1; attempt <= maxDbAttempts; attempt++)
        {
            try
            {
                dbContext.Database.EnsureCreated();
                break;
            }
            catch (Exception ex) when (attempt < maxDbAttempts)
            {
                Log.Warning(ex, "Identity DB not ready (attempt {Attempt}/{Max}), retrying…", attempt, maxDbAttempts);
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        foreach (var roleName in new[] { "Admin", "SuperAdmin", "Operator", "Auditor" })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new ApplicationRole { Name = roleName });
                Log.Information("Role '{Role}' created", roleName);
            }
        }

        var adminUser = await userManager.FindByNameAsync("Admin");
        if (adminUser is null)
        {
            adminUser = new ApplicationUser
            {
                UserName = "Admin",
                Email = "admin@sentinela.com",
                FullName = "Administrador",
                EmailConfirmed = true,
                IsActive = true
            };
            var result = await userManager.CreateAsync(adminUser, "4517");
            if (result.Succeeded)
            {
                Log.Information("Admin user created successfully");
            }
        }

        var requiredRoles = new[] { "Admin", "SuperAdmin" };
        var currentRoles = await userManager.GetRolesAsync(adminUser!);
        var missingRoles = requiredRoles.Where(r => !currentRoles.Contains(r)).ToList();
        if (missingRoles.Count > 0)
        {
            await userManager.AddToRolesAsync(adminUser!, missingRoles);
            Log.Information("Roles assigned to Admin: {Roles}", string.Join(", ", missingRoles));
        }
    }

    Log.Information("Sentinela Identity Service starting");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Sentinela Identity Service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
