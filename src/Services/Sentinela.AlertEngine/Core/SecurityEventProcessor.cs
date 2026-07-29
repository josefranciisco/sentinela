using Sentinela.Shared.Domain.Monitoring;
using Sentinela.Shared.Domain.Monitoring.Enums;
using Sentinela.Shared.Domain.Security;

namespace Sentinela.AlertEngine.Core;

public interface ISecurityEventProcessor
{
    object Enrich(object @event);
}

public class SecurityEventProcessor : ISecurityEventProcessor
{
    private readonly ILogger<SecurityEventProcessor> _logger;

    public SecurityEventProcessor(ILogger<SecurityEventProcessor> logger)
    {
        _logger = logger;
    }

    public object Enrich(object @event)
    {
        return @event switch
        {
            SecurityEvent securityEvent => EnrichSecurityEvent(securityEvent),
            TimelineEntry timelineEntry => EnrichTimelineEntry(timelineEntry),
            UsbEvent usbEvent => EnrichUsbEvent(usbEvent),
            _ => @event
        };
    }

    private static SecurityEvent EnrichSecurityEvent(SecurityEvent @event)
    {
        @event.AddMetadata("EnrichedBy", nameof(SecurityEventProcessor));
        @event.AddMetadata("ClassificationMethod", "RuleBased");

        return @event;
    }

    private static TimelineEntry EnrichTimelineEntry(TimelineEntry entry)
    {
        return entry;
    }

    private static UsbEvent EnrichUsbEvent(UsbEvent @event)
    {
        return @event;
    }
}
