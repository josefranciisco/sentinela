using Sentinela.AlertEngine.Core;
using Sentinela.Shared.Domain.Monitoring;
using Sentinela.Shared.Domain.Security;
using Sentinela.Shared.Messaging.Events;
using Sentinela.MessageBus.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace Sentinela.AlertEngine.Channels;

public class AlertConsumerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<RabbitMqOptions> _rabbitOptions;
    private readonly ILogger<AlertConsumerService> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    public AlertConsumerService(
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> rabbitOptions,
        ILogger<AlertConsumerService> logger)
    {
        _scopeFactory = scopeFactory;
        _rabbitOptions = rabbitOptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _rabbitOptions.Value.HostName,
            UserName = _rabbitOptions.Value.UserName,
            Password = _rabbitOptions.Value.Password,
            Port = _rabbitOptions.Value.Port,
            VirtualHost = _rabbitOptions.Value.VirtualHost,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(_rabbitOptions.Value.NetworkRecoveryIntervalSeconds),
            ClientProvidedName = _rabbitOptions.Value.ClientProvidedName
        };

        _connection = factory.CreateConnection("Sentinela.AlertEngine");
        _channel = _connection.CreateModel();

        _channel.ExchangeDeclare(_rabbitOptions.Value.EventsExchange, ExchangeType.Topic, durable: true);
        _channel.QueueDeclare(_rabbitOptions.Value.AlertQueue, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueBind(_rabbitOptions.Value.AlertQueue, _rabbitOptions.Value.EventsExchange, "security.#");
        _channel.QueueBind(_rabbitOptions.Value.AlertQueue, _rabbitOptions.Value.EventsExchange, "monitoring.#");

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += OnMessageReceived;

        _channel.BasicConsume(_rabbitOptions.Value.AlertQueue, autoAck: false, consumer: consumer);

        _logger.LogInformation("Alert consumer started, listening on queue: {Queue}", _rabbitOptions.Value.AlertQueue);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private void OnMessageReceived(object? sender, BasicDeliverEventArgs args)
    {
        using var scope = _scopeFactory.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<Sentinela.AlertEngine.Core.AlertEngine>();

        try
        {
            var body = Encoding.UTF8.GetString(args.Body.ToArray());
            var routingKey = args.RoutingKey;

            var @event = DeserializeEvent(body, routingKey);
            if (@event == null)
            {
                _channel!.BasicNack(args.DeliveryTag, false, false);
                return;
            }

            engine.ProcessEventAsync(@event, CancellationToken.None).GetAwaiter().GetResult();
            _channel!.BasicAck(args.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message from queue");
            _channel!.BasicNack(args.DeliveryTag, false, true);
        }
    }

    private static object? DeserializeEvent(string body, string routingKey)
    {
        var json = JsonSerializer.Deserialize<JsonElement>(body);
        if (!json.TryGetProperty("EventType", out var eventTypeProp))
            return null;

        var eventType = eventTypeProp.GetString();
        if (string.IsNullOrEmpty(eventType))
            return null;

        return routingKey switch
        {
            string key when key.StartsWith("security.") => JsonSerializer.Deserialize<SecurityEvent>(body),
            string key when key.StartsWith("monitoring.timeline") => JsonSerializer.Deserialize<TimelineEntry>(body),
            string key when key.StartsWith("monitoring.usb") => JsonSerializer.Deserialize<UsbEvent>(body),
            _ => null
        };
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel?.IsOpen == true)
            _channel.Close();
        if (_connection?.IsOpen == true)
            _connection.Close();

        await base.StopAsync(cancellationToken);
    }
}
