namespace Sentinela.Agent.Workers;

public class CryptominerDetectorHost : BackgroundService
{
    private readonly ICryptominerDetector _detector;
    private readonly ILogger<CryptominerDetectorHost> _logger;
    
    public CryptominerDetectorHost(
        ICryptominerDetector detector,
        ILogger<CryptominerDetectorHost> logger)
    {
        _detector = detector;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CryptominerDetectorHost starting...");
        
        try
        {
            await _detector.StartAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CryptominerDetectorHost failed");
        }
    }
}
