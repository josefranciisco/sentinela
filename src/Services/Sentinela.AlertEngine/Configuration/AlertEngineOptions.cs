namespace Sentinela.AlertEngine.Configuration;

public class AlertEngineOptions
{
    public int AlertEvaluationBatchSize { get; set; } = 100;
    public int AlertProcessingIntervalMs { get; set; } = 1000;
    public bool EnableCorrelation { get; set; } = true;
    public int MaxAlertsPerRulePerHour { get; set; } = 10;
    public int AlertRetentionDays { get; set; } = 90;
    public string[] SuppressedAlertCategories { get; set; } = Array.Empty<string>();
}
