using System.Security.Claims;

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

        if (IsOperator(Context.User))
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

    private static bool IsOperator(ClaimsPrincipal? user)
    {
        if (user is null) return false;
        if (user.IsInRole("Admin") || user.IsInRole("SuperAdmin")
            || user.IsInRole("Operator") || user.IsInRole("SecurityAnalyst"))
            return true;

        // Fallback: some tokens put role in a custom claim
        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value)
            .Concat(user.FindAll("role").Select(c => c.Value));
        return roles.Any(r => r is "Admin" or "SuperAdmin" or "Operator" or "SecurityAnalyst");
    }
}
