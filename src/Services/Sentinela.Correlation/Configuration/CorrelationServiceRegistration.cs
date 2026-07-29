using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sentinela.Correlation.Engine;
using Sentinela.Correlation.Rules;

namespace Sentinela.Correlation.Configuration;

public static class CorrelationServiceRegistration
{
    public static IServiceCollection AddCorrelationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CorrelationOptions>(configuration.GetSection("Correlation"));
        services.AddSingleton<ICorrelationEngine, CorrelationEngine>();
        services.AddSingleton<ICorrelationRuleService, CorrelationRuleService>();
        services.AddHostedService<CorrelationBackgroundService>();
        return services;
    }
}
