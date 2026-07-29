using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sentinela.MessageBus.Configuration;
using Sentinela.Shared.Core.Interfaces;

namespace Sentinela.MessageBus;

public static class MessageBusServiceRegistration
{
    public static IServiceCollection AddMessageBus(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMq"));
        services.AddSingleton<RabbitMqEventBus>();
        services.AddSingleton<IEventBus>(sp => sp.GetRequiredService<RabbitMqEventBus>());
        services.AddHostedService<RabbitMqConnectionHealthService>();

        return services;
    }
}
