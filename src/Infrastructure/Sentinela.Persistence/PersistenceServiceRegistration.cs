using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sentinela.Persistence.Repositories;
using Sentinela.Shared.Core.Interfaces;
using Sentinela.Shared.Infrastructure.Time;

namespace Sentinela.Persistence;

public static class PersistenceServiceRegistration
{
    public static IServiceCollection AddPersistenceServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<SentinelaDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("SentinelaDb"),
                b => b.MigrationsAssembly(typeof(SentinelaDbContext).Assembly.FullName))
                .UseSnakeCaseNamingConvention());

        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<SentinelaDbContext>());
        services.AddScoped<IDateTime, UtcTimeProvider>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(PersistenceServiceRegistration).Assembly));

        return services;
    }
}
