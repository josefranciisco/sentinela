using Sentinela.AlertEngine.Configuration;
using Sentinela.AlertEngine.Evaluators;
using Sentinela.Shared.Core.Interfaces;
using Sentinela.Shared.Domain.Alerting;
using Sentinela.Shared.Domain.Security;
using Microsoft.Extensions.Options;

namespace Sentinela.AlertEngine.Core;

public class AlertEngine
{
    private readonly IAlertEvaluator _evaluator;
    private readonly IAlertPublisher _publisher;
    private readonly ICorrelationEngine _correlation;
    private readonly ISecurityEventProcessor _eventProcessor;
    private readonly IRepository<AlertRule> _ruleRepo;
    private readonly ICacheService _cache;
    private readonly IOptions<AlertEngineOptions> _options;
    private readonly ILogger<AlertEngine> _logger;

    public AlertEngine(
        IAlertEvaluator evaluator,
        IAlertPublisher publisher,
        ICorrelationEngine correlation,
        ISecurityEventProcessor eventProcessor,
        IRepository<AlertRule> ruleRepo,
        ICacheService cache,
        IOptions<AlertEngineOptions> options,
        ILogger<AlertEngine> logger)
    {
        _evaluator = evaluator;
        _publisher = publisher;
        _correlation = correlation;
        _eventProcessor = eventProcessor;
        _ruleRepo = ruleRepo;
        _cache = cache;
        _options = options;
        _logger = logger;
    }

    public async Task ProcessEventAsync(object @event, CancellationToken ct)
    {
        var enrichedEvent = _eventProcessor.Enrich(@event);

        var rules = await GetActiveRulesAsync(ct);
        var applicableRules = FilterApplicableRules(rules, enrichedEvent);

        foreach (var rule in applicableRules)
        {
            if (await IsThrottledAsync(rule, enrichedEvent))
                continue;

            var results = await _evaluator.EvaluateAsync(rule, enrichedEvent, ct);

            if (results.Count == 0)
                continue;

            var alerts = results.Select(r => MapToAlert(r)).ToList();

            if (_options.Value.EnableCorrelation)
            {
                var correlated = await _correlation.CorrelateAsync(alerts, ct);
                await _publisher.PublishAlertBatchAsync(correlated);
            }
            else
            {
                await _publisher.PublishAlertBatchAsync(alerts);
            }

            await TrackAlertRateAsync(rule, enrichedEvent);
        }
    }

    private async Task<List<AlertRule>> GetActiveRulesAsync(CancellationToken ct)
    {
        return await _cache.GetOrCreateAsync("alertengine:active_rules", async () =>
        {
            var rules = await _ruleRepo.GetAllAsync(ct);
            return rules.Where(r => r.IsEnabled).ToList();
        }, TimeSpan.FromMinutes(5));
    }

    private List<AlertRule> FilterApplicableRules(List<AlertRule> rules, object @event)
    {
        var suppressed = _options.Value.SuppressedAlertCategories;
        return rules
            .Where(r => r.IsEnabled)
            .Where(r => !suppressed.Contains(r.Category ?? string.Empty))
            .ToList();
    }

    private async Task<bool> IsThrottledAsync(AlertRule rule, object @event)
    {
        var computerId = GetComputerId(@event);
        var hourlyKey = $"alert_rate:{rule.Id}:{computerId}:{DateTime.UtcNow:yyyyMMddHH}";
        var count = await _cache.GetAsync<int>(hourlyKey);
        return count >= _options.Value.MaxAlertsPerRulePerHour;
    }

    private async Task TrackAlertRateAsync(AlertRule rule, object @event)
    {
        var computerId = GetComputerId(@event);
        var hourlyKey = $"alert_rate:{rule.Id}:{computerId}:{DateTime.UtcNow:yyyyMMddHH}";
        var count = await _cache.GetAsync<int>(hourlyKey);
        await _cache.SetAsync(hourlyKey, count + 1, TimeSpan.FromHours(1));
    }

    private static SecurityAlert MapToAlert(AlertResult result)
    {
        return new SecurityAlert(
            result.Title,
            result.Description,
            result.Severity,
            result.Category,
            "AlertEngine",
            result.ComputerId,
            result.Username);
    }

    private static Guid GetComputerId(object @event)
    {
        return @event.GetType().GetProperty("ComputerId")?.GetValue(@event) as Guid? ?? Guid.Empty;
    }
}

public record class SecurityAlertResult
{
    public AlertResult Alert { get; init; } = null!;
    public double CorrelationScore { get; init; }
    public List<Guid> RelatedAlertIds { get; init; } = new();
}
