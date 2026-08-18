using System.Security.Claims;

namespace Sentinela.Api.Hubs;

[Authorize]
public class AlertHub : Hub
{
    private readonly ILogger<AlertHub> _logger;

    public AlertHub(ILogger<AlertHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var roles = Context.User?.FindAll(ClaimTypes.Role).Select(c => c.Value)
            .Concat(Context.User?.FindAll("role").Select(c => c.Value) ?? Array.Empty<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        foreach (var role in roles)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"role:{role}");
        }

        if (Context.User?.Identity?.IsAuthenticated == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "security");
            await Groups.AddToGroupAsync(Context.ConnectionId, "admins");
        }

        _logger.LogInformation("Alert hub client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public async Task SubscribeToSeverity(string severity)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"severity:{severity}");
    }

    public async Task UnsubscribeFromSeverity(string severity)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"severity:{severity}");
    }
}
