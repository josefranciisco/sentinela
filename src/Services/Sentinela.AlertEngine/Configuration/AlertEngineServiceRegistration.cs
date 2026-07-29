using Sentinela.AlertEngine.Channels;
using Sentinela.AlertEngine.Core;
using Sentinela.AlertEngine.Evaluators;

namespace Sentinela.AlertEngine.Configuration;

public static class AlertEngineServiceRegistration
{
    public static IServiceCollection AddAlertEngineServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AlertEngineOptions>(configuration.GetSection("AlertEngine"));

        services.AddSingleton<IAlertEvaluator, AlertEvaluator>();
        services.AddSingleton<ICorrelationEngine, CorrelationEngine>();
        services.AddSingleton<IAlertPublisher, AlertPublisher>();
        services.AddSingleton<ISecurityEventProcessor, SecurityEventProcessor>();

        services.AddScoped<Sentinela.AlertEngine.Core.AlertEngine>();

        services.AddHostedService<AlertConsumerService>();
        services.AddHostedService<AlertCooldownService>();

        return services;
    }
}
