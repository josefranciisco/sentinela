using Microsoft.Extensions.Options;

namespace Sentinela.Api.Services;

public class HeskTicketPoller : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly HeskTicketFeedStore _store;
    private readonly HeskOptions _options;
    private readonly ILogger<HeskTicketPoller> _logger;

    public HeskTicketPoller(
        IServiceScopeFactory scopes,
        HeskTicketFeedStore store,
        IOptions<HeskOptions> options,
        ILogger<HeskTicketPoller> logger)
    {
        _scopes = scopes;
        _store = store;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seconds = Math.Clamp(_options.PollSeconds, 5, 60);
        _logger.LogInformation("HESK ticket poller started every {Seconds}s ({Url})", seconds, _options.BaseUrl);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));
        await RefreshAsync(stoppingToken);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshAsync(stoppingToken);
        }
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<HeskTicketClient>();
            _store.Set(await client.FetchAsync(ct));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HESK poll failed");
        }
    }
}
