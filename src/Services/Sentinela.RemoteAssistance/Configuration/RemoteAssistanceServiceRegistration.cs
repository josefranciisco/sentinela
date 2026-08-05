using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sentinela.RemoteAssistance.Core;

namespace Sentinela.RemoteAssistance.Configuration;

public static class RemoteAssistanceServiceRegistration
{
    public static IServiceCollection AddRemoteAssistanceServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RemoteAssistanceOptions>(configuration.GetSection("RemoteAssistance"));
        services.AddSingleton<IRemoteSessionService, RemoteSessionService>();
        services.AddSingleton<ICommandExecutionService, CommandExecutionService>();
        services.AddScoped<IFileTransferService, FileTransferService>();
        return services;
    }
}
