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
        var role = Context.User?.FindFirst(ClaimTypes.Role)?.Value;
        if (role is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"role:{role}");

            if (role is "Admin" or "SuperAdmin" or "SecurityAnalyst")
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "security");
            }
        }

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

    public async Task AlertCreated(object alert)
    {
        await Clients.Group("security").SendAsync("AlertCreated", alert);

        var severity = alert?.GetType().GetProperty("Severity")?.GetValue(alert)?.ToString();
        if (severity is not null)
        {
            await Clients.Group($"severity:{severity}").SendAsync("AlertCreated", alert);
        }
    }

    public async Task AlertUpdated(object alert)
    {
        await Clients.Group("security").SendAsync("AlertUpdated", alert);
    }

    public async Task AlertAcknowledged(object alert)
    {
        await Clients.Group("security").SendAsync("AlertAcknowledged", alert);
    }

    public async Task AlertResolved(object alert)
    {
        await Clients.Group("security").SendAsync("AlertResolved", alert);
    }
}
