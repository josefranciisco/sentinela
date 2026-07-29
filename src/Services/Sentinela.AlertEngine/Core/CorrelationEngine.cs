using Sentinela.Shared.Core.Interfaces;
using Sentinela.Shared.Domain.Security;

namespace Sentinela.AlertEngine.Core;

public interface ICorrelationEngine
{
    Task<List<SecurityAlert>> CorrelateAsync(List<SecurityAlert> alerts, CancellationToken ct);
}

public class CorrelationEngine : ICorrelationEngine
{
    private readonly ICacheService _cache;
    private readonly IRepository<CorrelationRule> _correlationRuleRepo;
    private readonly ILogger<CorrelationEngine> _logger;

    public CorrelationEngine(
        ICacheService cache,
        IRepository<CorrelationRule> correlationRuleRepo,
        ILogger<CorrelationEngine> logger)
    {
        _cache = cache;
        _correlationRuleRepo = correlationRuleRepo;
        _logger = logger;
    }

    public async Task<List<SecurityAlert>> CorrelateAsync(List<SecurityAlert> alerts, CancellationToken ct)
    {
        var correlatedAlerts = new List<SecurityAlert>();
        var rules = await GetCorrelationRulesAsync(ct);

        foreach (var alert in alerts)
        {
            var matchedRules = rules
                .Where(r => r.IsEnabled)
                .Where(r => EvaluateCorrelationCondition(r, alert))
                .ToList();

            correlatedAlerts.Add(alert);

            if (matchedRules.Count == 0)
                continue;

            foreach (var rule in matchedRules)
            {
                var windowKey = $"correlation_window:{rule.Id}:{alert.ComputerId}";
                var recentAlerts = await _cache.GetAsync<List<Guid>>(windowKey) ?? new List<Guid>();

                recentAlerts.Add(alert.Id);

                if (recentAlerts.Count >= 2)
                {
                    _logger.LogInformation(
                        "Alert correlated: {AlertTitle} matched rule {RuleName} (score: {Score})",
                        alert.Title, rule.Name, rule.Score);
                }

                await _cache.SetAsync(windowKey, recentAlerts, rule.TimeWindow);
            }
        }

        return correlatedAlerts;
    }

    private async Task<List<CorrelationRule>> GetCorrelationRulesAsync(CancellationToken ct)
    {
        return await _cache.GetOrCreateAsync("alertengine:correlation_rules", async () =>
        {
            var rules = await _correlationRuleRepo.GetAllAsync(ct);
            return rules.Where(r => r.IsEnabled).ToList();
        }, TimeSpan.FromMinutes(10));
    }

    private static bool EvaluateCorrelationCondition(CorrelationRule rule, SecurityAlert alert)
    {
        try
        {
            var condition = rule.ConditionExpression.ToLowerInvariant();
            var category = alert.Category.ToLowerInvariant();
            var severity = alert.Severity.ToString().ToLowerInvariant();

            if (condition.Contains("category") && condition.Contains(category))
                return true;

            if (condition.Contains("severity") && condition.Contains(severity))
                return true;

            return false;
        }
        catch
        {
            return false;
        }
    }
}
