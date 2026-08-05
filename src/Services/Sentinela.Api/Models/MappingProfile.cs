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

        CreateMap<RemoteSession, RemoteSessionDto>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.Id))
            .ForMember(d => d.ComputerId, o => o.MapFrom(s => s.ComputerId))
            .ForMember(d => d.RequestedBy, o => o.MapFrom(s => s.RequestedBy))
            .ForMember(d => d.SessionType, o => o.MapFrom(s => s.SessionType))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status))
            .ForMember(d => d.RequestedAt, o => o.MapFrom(s => s.RequestedAt.UtcDateTime))
            .ForMember(d => d.TerminatedAt, o => o.MapFrom(s => s.TerminatedAt.HasValue ? s.TerminatedAt.Value.UtcDateTime : (DateTime?)null))
            .ForMember(d => d.MonitorIndex, o => o.MapFrom(s => s.MonitorIndex));
    }
}
