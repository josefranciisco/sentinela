namespace Sentinela.MessageBus.Configuration;

public class RabbitMqOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public int RetryCount { get; set; } = 5;
    public int NetworkRecoveryIntervalSeconds { get; set; } = 10;
    public string ClientProvidedName { get; set; } = "Sentinela";
    public int PrefetchCount { get; set; } = 50;

    public string EventsExchange { get; set; } = "sentinela.events";
    public string CommandsExchange { get; set; } = "sentinela.commands";
    public string DeadLetterExchange { get; set; } = "sentinela.dlx";

    public string AlertQueue { get; set; } = "sentinela.alerts";
    public string AutomationQueue { get; set; } = "sentinela.automation";
    public string CorrelationQueue { get; set; } = "sentinela.correlation";
    public string AgentCommandsQueue { get; set; } = "sentinela.agent.commands";
    public string AgentEventsQueue { get; set; } = "sentinela.agent.events";
    public string AuditQueue { get; set; } = "sentinela.audit";
    public string ScreenCaptureQueue { get; set; } = "sentinela.screencapture";
}
