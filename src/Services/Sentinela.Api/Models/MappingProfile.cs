using AutoMapper;
using Sentinela.Persistence.Models;
using Sentinela.Shared.Domain.Alerting;
using Sentinela.Shared.Domain.Audit;
using Sentinela.Shared.Domain.Automation;
using Sentinela.Shared.Domain.Monitoring;
using Sentinela.Shared.Domain.Security;

namespace Sentinela.Api.Models;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Computer, ComputerDto>()
            .ForMember(d => d.LastHeartbeat, o => o.MapFrom(s => s.LastHeartbeat.UtcDateTime))
            .ForMember(d => d.CreatedAt, o => o.MapFrom(s => s.CreatedAt.UtcDateTime));
        CreateMap<Computer, ComputerDetailDto>()
            .ForMember(d => d.LastHeartbeat, o => o.MapFrom(s => s.LastHeartbeat.UtcDateTime))
            .ForMember(d => d.CreatedAt, o => o.MapFrom(s => s.CreatedAt.UtcDateTime));

        CreateMap<TimelineEntry, TimelineEntryDto>()
            .ForMember(d => d.Timestamp, o => o.MapFrom(s => s.Timestamp.UtcDateTime))
            .ForMember(d => d.EventType, o => o.MapFrom(s => s.EventType.ToString()))
            .ForMember(d => d.Severity, o => o.MapFrom(s => s.Severity.ToString()));

        CreateMap<Alert, AlertDto>();
        CreateMap<Alert, AlertDetailDto>();
        CreateMap<AlertComment, AlertCommentDto>();

        CreateMap<ScreenCaptureRecord, ScreenCaptureDto>();

        CreateMap<SecurityEvent, SecurityEventDto>()
            .ForMember(d => d.Timestamp, o => o.MapFrom(s => s.Timestamp.UtcDateTime))
            .ForMember(d => d.Severity, o => o.MapFrom(s => s.Severity.ToString()));
        CreateMap<CorrelationRule, CorrelationRuleDto>();

        CreateMap<AuditTrail, AuditLogEntryDto>();
        CreateMap<Screenshot, ScreenshotDto>()
            .ForMember(d => d.CreatedAt, o => o.MapFrom(s => s.CreatedAt.UtcDateTime));

        CreateMap<Workflow, WorkflowDto>();
        CreateMap<WorkflowCondition, WorkflowConditionDto>();
        CreateMap<WorkflowAction, WorkflowActionDto>();
        CreateMap<WorkflowExecutionLog, WorkflowExecutionLogDto>();
    }
}
