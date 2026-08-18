namespace Sentinela.Api.Hubs;

[Authorize]
public class MonitoringHub : Hub
{
    private readonly ILogger<MonitoringHub> _logger;

    public MonitoringHub(ILogger<MonitoringHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var computerId = Context.User?.FindFirst("ComputerId")?.Value;
        if (computerId is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"computer:{computerId}");
        }

        if (Context.User?.Identity?.IsAuthenticated == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "admins");
            await Groups.AddToGroupAsync(Context.ConnectionId, "security");
        }

        _logger.LogInformation("Monitoring client connected: {ConnectionId}, User: {User}",
            Context.ConnectionId, Context.User?.Identity?.Name);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Monitoring client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SubscribeToComputer(string computerId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"computer:{computerId}");
    }

    public async Task UnsubscribeFromComputer(string computerId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"computer:{computerId}");
    }
}
