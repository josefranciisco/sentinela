using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Sentinela.Automation.Actions;
using Sentinela.Automation.Workflows;
using Sentinela.MessageBus.Configuration;
using Sentinela.Shared.Core.Interfaces;
using Sentinela.Shared.Domain.Monitoring;
using Sentinela.Shared.Domain.Security;
using Serilog;

namespace Sentinela.Automation.Channels;

public class WorkflowConsumerService : BackgroundService
{
    private readonly IWorkflowEngine _workflowEngine;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkflowConsumerService> _logger;
    private readonly RabbitMqOptions _rabbitOptions;
    private IConnection? _connection;
    private IModel? _channel;

    public WorkflowConsumerService(
        IWorkflowEngine workflowEngine,
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> rabbitOptions,
        ILogger<WorkflowConsumerService> logger)
    {
        _workflowEngine = workflowEngine;
        _scopeFactory = scopeFactory;
        _rabbitOptions = rabbitOptions.Value;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Workflow Consumer Service starting");

        var connectionFactory = new ConnectionFactory
        {
            HostName = _rabbitOptions.HostName,
            Port = _rabbitOptions.Port,
            UserName = _rabbitOptions.UserName,
            Password = _rabbitOptions.Password,
            VirtualHost = _rabbitOptions.VirtualHost
        };
        _connection = connectionFactory.CreateConnection("Sentinela.Automation");
        _channel = _connection.CreateModel();

        _channel.ExchangeDeclare(_rabbitOptions.EventsExchange, ExchangeType.Topic, durable: true);
        var queueDeclare = _channel.QueueDeclare(_rabbitOptions.AutomationQueue, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueBind(queueDeclare.QueueName, _rabbitOptions.EventsExchange, "automation.#");

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                _logger.LogInformation("Received workflow trigger event: {RoutingKey}", ea.RoutingKey);

                var triggerEvent = DeserializeTriggerEvent(ea.RoutingKey, message);
                if (triggerEvent == null) return;

                var scope = _scopeFactory.CreateScope();
                var actionExecutor = scope.ServiceProvider.GetRequiredService<IActionExecutor>();

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var workflows = await _workflowEngine.GetApplicableWorkflows(triggerEvent);
                        foreach (var workflow in workflows)
                        {
                            await _workflowEngine.ExecuteWorkflow(workflow, triggerEvent);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing workflow trigger event");
                    }
                    finally
                    {
                        scope.Dispose();
                    }
                });

                _channel.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing workflow trigger event");
            }
        };

        _channel.BasicConsume("sentinela.automation", autoAck: false, consumer: consumer);

        return Task.CompletedTask;
    }

    private object? DeserializeTriggerEvent(string routingKey, string message)
    {
        try
        {
            var routingParts = routingKey.Split('.');
            var eventType = routingParts.Length > 1 ? routingParts[1] : "Unknown";

            return eventType switch
            {
                "usb" => JsonSerializer.Deserialize<UsbEvent>(message),
                "security" => JsonSerializer.Deserialize<SecurityEvent>(message),
                _ => null
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize trigger event: {RoutingKey}", routingKey);
            return null;
        }
    }

    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> SemaphorePool = new();

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Workflow Consumer Service stopping");

        _channel?.Close();
        _connection?.Close();

        foreach (var semaphore in SemaphorePool.Values)
        {
            semaphore.Dispose();
        }
        SemaphorePool.Clear();

        return base.StopAsync(cancellationToken);
    }
}
