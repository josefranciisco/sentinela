using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using Sentinela.MessageBus.Configuration;
using Sentinela.Shared.Core.Interfaces;
using Sentinela.Shared.Messaging.Events;
using ILogger = Serilog.ILogger;

namespace Sentinela.MessageBus;

public class RabbitMqEventBus : IEventBus, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqEventBus> _logger;
    private readonly ConcurrentDictionary<string, List<Type>> _handlers;
    private readonly ConcurrentDictionary<string, IDisposable> _consumerTags;

    public RabbitMqEventBus(
        Microsoft.Extensions.Options.IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqEventBus> logger)
    {
        _options = options.Value;
        _logger = logger;
        _handlers = new ConcurrentDictionary<string, List<Type>>();
        _consumerTags = new ConcurrentDictionary<string, IDisposable>();

        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost,
            ClientProvidedName = _options.ClientProvidedName,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(_options.NetworkRecoveryIntervalSeconds),
            TopologyRecoveryEnabled = true
        };

        _connection = factory.CreateConnection("Sentinela.EventBus");
        _channel = _connection.CreateModel();

        _channel.BasicQos(0, (ushort)_options.PrefetchCount, false);

        _channel.ExchangeDeclare(_options.EventsExchange, ExchangeType.Topic, durable: true);
        _channel.ExchangeDeclare(_options.CommandsExchange, ExchangeType.Direct, durable: true);
        _channel.ExchangeDeclare(_options.DeadLetterExchange, ExchangeType.Fanout, durable: true);

        _logger.LogInformation("RabbitMQ exchanges declared: {Events}, {Commands}, {Dlx}",
            _options.EventsExchange, _options.CommandsExchange, _options.DeadLetterExchange);
    }

    public Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : IEvent
    {
        var exchange = _options.EventsExchange;
        var routingKey = typeof(T).Name;
        var body = SerializeMessage(@event);

        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.MessageId = Guid.NewGuid().ToString();
        properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        properties.Type = routingKey;

        _channel.BasicPublish(exchange, routingKey, true, properties, body);
        _logger.LogDebug("Published event {EventType} to {Exchange}/{RoutingKey}", routingKey, exchange, routingKey);

        return Task.CompletedTask;
    }

    public Task SubscribeAsync<T>(Func<T, CancellationToken, Task> handler, CancellationToken cancellationToken = default) where T : IEvent
    {
        var eventType = typeof(T).Name;

        var queueName = $"{_options.EventsExchange}.{eventType}";
        _channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueBind(queueName, _options.EventsExchange, eventType);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (_, args) =>
        {
            try
            {
                var message = DeserializeMessage<T>(args.Body.ToArray());
                if (message is not null)
                {
                    await handler(message, cancellationToken);
                    _channel.BasicAck(args.DeliveryTag, false);
                }
                else
                {
                    _logger.LogWarning("Failed to deserialize message of type {EventType}", eventType);
                    _channel.BasicNack(args.DeliveryTag, false, false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message of type {EventType}", eventType);
                _channel.BasicNack(args.DeliveryTag, false, true);
            }
        };

        var tag = _channel.BasicConsume(queueName, autoAck: false, consumer: consumer);
        _consumerTags.TryAdd(eventType, new ConsumerDisposable(_channel, tag));

        _handlers.AddOrUpdate(eventType, _ => [typeof(T)], (_, list) =>
        {
            list.Add(typeof(T));
            return list;
        });

        _logger.LogInformation("Subscribed to {EventType} on queue {QueueName}", eventType, queueName);

        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync<T>(Func<T, CancellationToken, Task> handler, CancellationToken cancellationToken = default) where T : IEvent
    {
        var eventType = typeof(T).Name;

        if (_consumerTags.TryRemove(eventType, out var disposable))
        {
            disposable.Dispose();
        }

        if (_handlers.TryGetValue(eventType, out var handlers))
        {
            handlers.RemoveAll(h => h == typeof(T));
            if (handlers.Count == 0)
            {
                _handlers.TryRemove(eventType, out _);
            }
        }

        _logger.LogInformation("Unsubscribed from {EventType}", eventType);

        return Task.CompletedTask;
    }

    private byte[] SerializeMessage<T>(T message)
    {
        return JsonSerializer.SerializeToUtf8Bytes(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });
    }

    private T? DeserializeMessage<T>(byte[] body)
    {
        return JsonSerializer.Deserialize<T>(body, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    public void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
    }

    private sealed class ConsumerDisposable : IDisposable
    {
        private readonly IModel _channel;
        private readonly string _consumerTag;

        public ConsumerDisposable(IModel channel, string consumerTag)
        {
            _channel = channel;
            _consumerTag = consumerTag;
        }

        public void Dispose()
        {
            try
            {
                if (_channel.IsOpen)
                {
                    _channel.BasicCancel(_consumerTag);
                }
            }
            catch (AlreadyClosedException)
            {
            }
        }
    }
}
