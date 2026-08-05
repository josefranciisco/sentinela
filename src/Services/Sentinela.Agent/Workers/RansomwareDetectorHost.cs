namespace Sentinela.Agent.Workers;

public class RansomwareDetectorHost : BackgroundService
{
    private readonly IRansomwareDetector _detector;
    private readonly ILogger<RansomwareDetectorHost> _logger;
    
    public RansomwareDetectorHost(
        IRansomwareDetector detector,
        ILogger<RansomwareDetectorHost> logger)
    {
        _detector = detector;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RansomwareDetectorHost starting...");
        
        try
        {
            await _detector.StartAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RansomwareDetectorHost failed");
        }
    }
}
