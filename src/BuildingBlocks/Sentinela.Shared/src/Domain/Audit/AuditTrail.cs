using Sentinela.Shared.Core.Entities;

namespace Sentinela.Shared.Domain.Audit;

public class AuditTrail : BaseEntity
{
    protected AuditTrail() : base() { }

    public AuditTrail(
        string userId,
        string username,
        string action,
        string resource,
        string? resourceId = null,
        string? details = null,
        string? ipAddress = null,
        string? userAgent = null)
        : base()
    {
        UserId = userId;
        Username = username;
        Action = action;
        Resource = resource;
        ResourceId = resourceId;
        Details = details;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        Timestamp = DateTimeOffset.UtcNow;
    }

    public string UserId { get; private set; }
    public string Username { get; private set; }
    public string Action { get; private set; }
    public string Resource { get; private set; }
    public string? ResourceId { get; private set; }
    public string? Details { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }
}
