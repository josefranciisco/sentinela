using Sentinela.AlertEngine.Core;
using Sentinela.Shared.Core.Interfaces;
using Sentinela.Shared.Domain.Alerting;
using Sentinela.Shared.Domain.Monitoring.Enums;

namespace Sentinela.AlertEngine.Evaluators;

public interface IAlertEvaluator
{
    Task<List<AlertResult>> EvaluateAsync(AlertRule rule, object @event, CancellationToken ct);
}

public class AlertEvaluator : IAlertEvaluator
{
    private readonly ICacheService _cache;
    private readonly ILogger<AlertEvaluator> _logger;

    public AlertEvaluator(ICacheService cache, ILogger<AlertEvaluator> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<AlertResult>> EvaluateAsync(AlertRule rule, object @event, CancellationToken ct)
    {
        var results = new List<AlertResult>();

        try
        {
            var condition = rule.Condition;

            if (EvaluateCondition(condition, @event))
            {
                var cooldownKey = $"alert_cooldown:{rule.Id}:{GetEventKey(@event)}";
                var inCooldown = await _cache.ExistsAsync(cooldownKey);
                if (!inCooldown)
                {
                    results.Add(new AlertResult
                    {
                        RuleId = rule.Id,
                        RuleName = rule.Name,
                        Severity = rule.Severity,
                        Category = rule.Category ?? "Uncategorized",
                        Title = rule.Name,
                        Description = FormatDescription(rule.Description, @event),
                        ComputerId = GetComputerId(@event),
                        Username = GetUsername(@event),
                        Timestamp = DateTime.UtcNow,
                        Metadata = new Dictionary<string, string>
                        {
                            ["SourceEvent"] = @event.GetType().Name,
                            ["RuleId"] = rule.Id.ToString()
                        }
                    });

                    await _cache.SetAsync(cooldownKey, true, rule.CooldownPeriod ?? TimeSpan.FromMinutes(5));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating rule {RuleId}: {RuleName}", rule.Id, rule.Name);
        }

        return results;
    }

    private bool EvaluateCondition(string condition, object @event)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(condition)) return false;

            var eventType = @event.GetType().Name;
            var conditionLower = condition.ToLowerInvariant();

            if (conditionLower.Contains("eventtype") && conditionLower.Contains(eventType.ToLowerInvariant()))
                return true;

            var props = @event.GetType().GetProperties();
            foreach (var prop in props)
            {
                var propName = prop.Name.ToLowerInvariant();
                if (!conditionLower.Contains(propName)) continue;

                var value = prop.GetValue(@event)?.ToString()?.ToLowerInvariant();
                if (value != null && conditionLower.Contains(value))
                    return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private string GetEventKey(object @event)
    {
        var computerId = GetComputerId(@event);
        var eventType = @event.GetType().Name;
        return $"{computerId}:{eventType}";
    }

    private Guid GetComputerId(object @event)
    {
        return @event.GetType().GetProperty("ComputerId")?.GetValue(@event) as Guid? ?? Guid.Empty;
    }

    private string GetUsername(object @event)
    {
        return @event.GetType().GetProperty("Username")?.GetValue(@event)?.ToString() ?? "SYSTEM";
    }

    private string FormatDescription(string? template, object @event)
    {
        if (string.IsNullOrWhiteSpace(template)) return string.Empty;

        var result = template;
        foreach (var prop in @event.GetType().GetProperties())
        {
            result = result.Replace($"{{{prop.Name}}}", prop.GetValue(@event)?.ToString() ?? "");
        }
        return result;
    }
}

public record AlertResult
{
    public Guid RuleId { get; init; }
    public string RuleName { get; init; } = string.Empty;
    public Severity Severity { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public Guid ComputerId { get; init; }
    public string Username { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new();
}
