using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sentinela.Caching;
using Sentinela.Correlation.Configuration;
using Sentinela.MessageBus;
using Sentinela.Persistence;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddPersistenceServices(builder.Configuration);
builder.Services.AddCachingServices(builder.Configuration);
builder.Services.AddMessageBus(builder.Configuration);
builder.Services.AddCorrelationServices(builder.Configuration);

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SentinelaDbContext>();
    db.Database.EnsureCreated();
}

await host.RunAsync();
