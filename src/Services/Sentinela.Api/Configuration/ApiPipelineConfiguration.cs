using Sentinela.Api.Hubs;


namespace Sentinela.Api.Configuration;

public static class ApiPipelineConfiguration
{
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseResponseCompression();
        app.UseResponseCaching();
        app.UseCors("SentinelaCors");
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHub<MonitoringHub>("/hubs/monitoring");
        app.MapHub<AgentHub>("/hubs/agent");
        app.MapHub<AlertHub>("/hubs/alerts");
        app.MapHub<RemoteAssistanceHub>("/hubs/remote");

        app.MapHealthChecks("/health");

        return app;
    }
}
