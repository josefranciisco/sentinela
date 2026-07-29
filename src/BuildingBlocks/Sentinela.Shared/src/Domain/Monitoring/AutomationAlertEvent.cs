using Sentinela.Shared.Messaging.Events;

namespace Sentinela.Shared.Domain.Monitoring;

public class AutomationAlertEvent : IEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string EventType => "AutomationAlert";
    public string Source => "Sentinela.Automation";
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
