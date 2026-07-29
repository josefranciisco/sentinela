using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Sentinela.MessageBus.Configuration;

namespace Sentinela.MessageBus;

public class RabbitMqConnectionHealthService : BackgroundService
{
    private readonly IOptions<RabbitMqOptions> _options;
    private readonly ILogger<RabbitMqConnectionHealthService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);
    private IConnection? _connection;

    public RabbitMqConnectionHealthService(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqConnectionHealthService> logger)
    {
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RabbitMQ connection health service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_connection is null || !_connection.IsOpen)
                {
                    await AttemptReconnect(stoppingToken);
                }
                else
                {
                    _logger.LogDebug("RabbitMQ connection is healthy");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error checking RabbitMQ connection health");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task AttemptReconnect(CancellationToken cancellationToken)
    {
        var retryDelay = TimeSpan.FromSeconds(1);
        var maxRetryDelay = TimeSpan.FromSeconds(30);

        for (var attempt = 1; attempt <= _options.Value.RetryCount; attempt++)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            try
            {
                _logger.LogInformation("Attempting RabbitMQ reconnection (attempt {Attempt}/{Max})",
                    attempt, _options.Value.RetryCount);

                var factory = new ConnectionFactory
                {
                    HostName = _options.Value.HostName,
                    Port = _options.Value.Port,
                    UserName = _options.Value.UserName,
                    Password = _options.Value.Password,
                    VirtualHost = _options.Value.VirtualHost,
                    ClientProvidedName = _options.Value.ClientProvidedName,
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(_options.Value.NetworkRecoveryIntervalSeconds),
                    TopologyRecoveryEnabled = true
                };

                _connection = factory.CreateConnection("Sentinela.HealthCheck");
                _logger.LogInformation("Successfully reconnected to RabbitMQ");
                return;
            }
            catch (BrokerUnreachableException ex)
            {
                _logger.LogWarning(ex, "RabbitMQ reconnection attempt {Attempt} failed", attempt);

                if (attempt < _options.Value.RetryCount)
                {
                    await Task.Delay(retryDelay, cancellationToken);
                    retryDelay = TimeSpan.FromMilliseconds(
                        Math.Min(retryDelay.TotalMilliseconds * 2, maxRetryDelay.TotalMilliseconds));
                }
            }
        }

        _logger.LogError("Failed to reconnect to RabbitMQ after {MaxRetries} attempts", _options.Value.RetryCount);
    }

    public override void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
        base.Dispose();
    }
}
