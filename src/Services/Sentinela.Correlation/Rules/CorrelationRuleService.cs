using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sentinela.Shared.Core.Interfaces;
using Sentinela.Shared.Domain.Security;
using Sentinela.Shared.Messaging.Events;

namespace Sentinela.Correlation.Rules;

public interface ICorrelationRuleService
{
    Task<CorrelationRule> CreateRuleAsync(CorrelationRule rule);
    Task<CorrelationRule> UpdateRuleAsync(CorrelationRule rule);
    Task<bool> DeleteRuleAsync(Guid ruleId);
    Task<CorrelationRule?> GetRuleAsync(Guid ruleId);
    Task<List<CorrelationRule>> GetAllRulesAsync(bool includeDisabled = false);
    Task<bool> TestRuleAsync(Guid ruleId, Guid computerId);
    Task<bool> ValidateExpressionAsync(string expression);
    List<RuleTemplate> GetRuleTemplates();
    Task<CorrelationRule> ToggleRuleAsync(Guid ruleId, bool enabled);
}

public class CorrelationRuleService : ICorrelationRuleService
{
    private readonly IRepository<CorrelationRule> _ruleRepo;
    private readonly ICacheService _cache;
    private readonly IEventBus _eventBus;
    private readonly ILogger<CorrelationRuleService> _logger;

    private static readonly List<RuleTemplate> RuleTemplates = new()
    {
        new RuleTemplate
        {
            Name = "Multiple Failed Logins",
            Description = "Detect brute force attempts via failed logins",
            ConditionExpression = "Count(FailedLogin) >= 5 AND Count(Login) == 0",
            TimeWindowMinutes = 10,
            Priority = 2,
            Tags = new[] { "bruteforce", "authentication" }
        },
        new RuleTemplate
        {
            Name = "Admin PrivEsc Detection",
            Description = "Detect privilege escalation via admin group changes",
            ConditionExpression = "Count(LocalGroupAdd) >= 1 AND Count(AdminGroupChange) >= 1",
            TimeWindowMinutes = 30,
            Priority = 1,
            Tags = new[] { "privilege-escalation", "security" }
        },
        new RuleTemplate
        {
            Name = "Data Staging",
            Description = "Detect patterns indicating data staging for exfiltration",
            ConditionExpression = "Count(FileCopy) >= 10 AND Count(ArchiveCreated) >= 1",
            TimeWindowMinutes = 15,
            Priority = 1,
            Tags = new[] { "exfiltration", "data-loss" }
        },
        new RuleTemplate
        {
            Name = "Defense Evasion",
            Description = "Detect firewall and AV tampering in sequence",
            ConditionExpression = "Count(FirewallDisabled) >= 1 AND Count(DefenderDisabled) >= 1",
            TimeWindowMinutes = 5,
            Priority = 1,
            Tags = new[] { "defense-evasion", "tampering" }
        },
        new RuleTemplate
        {
            Name = "Lateral Movement",
            Description = "Detect lateral movement via remote connections",
            ConditionExpression = "Count(RemoteConnection) >= 3 AND Severity(High) >= 1",
            TimeWindowMinutes = 20,
            Priority = 1,
            Tags = new[] { "lateral-movement", "network" }
        }
    };

    private static readonly Regex ExpressionValidator = new(
        @"^(Count\(\w+\)\s*[><=!]+\s*\d+|Severity\(\w+\)\s*[><=!]+\s*\d+)(\s+(AND|OR)\s+(Count\(\w+\)\s*[><=!]+\s*\d+|Severity\(\w+\)\s*[><=!]+\s*\d+))*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public CorrelationRuleService(
        IRepository<CorrelationRule> ruleRepo,
        ICacheService cache,
        IEventBus eventBus,
        ILogger<CorrelationRuleService> logger)
    {
        _ruleRepo = ruleRepo;
        _cache = cache;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<CorrelationRule> CreateRuleAsync(CorrelationRule rule)
    {
        if (!await ValidateExpressionAsync(rule.ConditionExpression))
            throw new InvalidOperationException("Invalid rule expression.");

        var newRule = new CorrelationRule(
            rule.Name,
            rule.ConditionExpression,
            rule.TimeWindow,
            rule.Score,
            rule.Priority,
            rule.Description);

        await _ruleRepo.AddAsync(newRule);
        await _cache.RemoveAsync("correlation:active_rules");

        _logger.LogInformation("Correlation rule created: {RuleName} ({RuleId})", newRule.Name, newRule.Id);

        await _eventBus.PublishAsync(new CorrelationRuleChangedEvent
        {
            RuleId = newRule.Id,
            Action = "created",
            Timestamp = DateTimeOffset.UtcNow
        });

        return newRule;
    }

    public async Task<CorrelationRule> UpdateRuleAsync(CorrelationRule rule)
    {
        var existing = await _ruleRepo.GetByIdAsync(rule.Id);
        if (existing == null)
            throw new KeyNotFoundException($"Rule {rule.Id} not found.");

        if (!string.IsNullOrWhiteSpace(rule.ConditionExpression) && !await ValidateExpressionAsync(rule.ConditionExpression))
            throw new InvalidOperationException("Invalid rule expression.");

        existing.Update(
            rule.Name,
            rule.Description,
            rule.ConditionExpression,
            rule.Priority,
            rule.Score,
            rule.MinCounts,
            rule.TimeWindow,
            rule.Tags);
        existing.MarkAsUpdated();

        await _ruleRepo.UpdateAsync(existing);
        await _cache.RemoveAsync("correlation:active_rules");

        _logger.LogInformation("Correlation rule updated: {RuleName} ({RuleId})", existing.Name, existing.Id);

        await _eventBus.PublishAsync(new CorrelationRuleChangedEvent
        {
            RuleId = existing.Id,
            Action = "updated",
            Timestamp = DateTimeOffset.UtcNow
        });

        return existing;
    }

    public async Task<bool> DeleteRuleAsync(Guid ruleId)
    {
        var rule = await _ruleRepo.GetByIdAsync(ruleId);
        if (rule == null) return false;

        rule.MarkAsDeleted();
        rule.MarkAsUpdated();

        await _ruleRepo.UpdateAsync(rule);
        await _cache.RemoveAsync("correlation:active_rules");

        _logger.LogInformation("Correlation rule deleted: {RuleName} ({RuleId})", rule.Name, rule.Id);

        await _eventBus.PublishAsync(new CorrelationRuleChangedEvent
        {
            RuleId = rule.Id,
            Action = "deleted",
            Timestamp = DateTimeOffset.UtcNow
        });

        return true;
    }

    public async Task<CorrelationRule?> GetRuleAsync(Guid ruleId)
    {
        return await _ruleRepo.GetByIdAsync(ruleId);
    }

    public async Task<List<CorrelationRule>> GetAllRulesAsync(bool includeDisabled = false)
    {
        var query = _ruleRepo.Query().Where(r => !r.IsDeleted);

        if (includeDisabled)
            query = query.IgnoreQueryFilters();

        return await query.OrderByDescending(r => r.Priority)
            .ThenByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> TestRuleAsync(Guid ruleId, Guid computerId)
    {
        var rule = await _ruleRepo.GetByIdAsync(ruleId);
        if (rule == null) return false;

        _logger.LogInformation("Testing rule {RuleName} on computer {ComputerId}", rule.Name, computerId);
        return true;
    }

    public async Task<bool> ValidateExpressionAsync(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return false;

        return await Task.FromResult(ExpressionValidator.IsMatch(expression.Trim()));
    }

    public async Task<CorrelationRule> ToggleRuleAsync(Guid ruleId, bool enabled)
    {
        var rule = await _ruleRepo.GetByIdAsync(ruleId);
        if (rule == null)
            throw new KeyNotFoundException($"Rule {ruleId} not found.");

        if (enabled)
            rule.Enable();
        else
            rule.Disable();
        rule.MarkAsUpdated();

        await _ruleRepo.UpdateAsync(rule);
        await _cache.RemoveAsync("correlation:active_rules");

        _logger.LogInformation("Correlation rule {RuleName} enabled: {Enabled}", rule.Name, enabled);

        return rule;
    }

    public List<RuleTemplate> GetRuleTemplates()
    {
        return RuleTemplates;
    }
}

public class RuleTemplate
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ConditionExpression { get; set; } = string.Empty;
    public int TimeWindowMinutes { get; set; } = 10;
    public int Priority { get; set; } = 3;
    public string[] Tags { get; set; } = Array.Empty<string>();
}

public class CorrelationRuleChangedEvent : IEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(CorrelationRuleChangedEvent);
    public string Source => "Sentinela.Correlation";

    public Guid RuleId { get; init; }
    public string Action { get; init; } = string.Empty;
}
