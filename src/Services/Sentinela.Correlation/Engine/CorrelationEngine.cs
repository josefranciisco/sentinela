using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sentinela.Shared.Core.Interfaces;
using Sentinela.Shared.Domain.Monitoring.Enums;
using Sentinela.Shared.Domain.Security;
using Sentinela.Shared.Messaging.Events;

namespace Sentinela.Correlation.Engine;

public interface ICorrelationEngine
{
    Task<CorrelationResult?> AnalyzeEventAsync(object securityEvent);
    Task<List<CorrelationResult>> AnalyzeTimeWindowAsync(string computerId, TimeSpan window);
    Task<List<CorrelationResult>> AnalyzePatternAsync(string computerId, Guid ruleId);
}

public class CorrelationEngine : ICorrelationEngine
{
    private readonly IRepository<CorrelationRule> _ruleRepo;
    private readonly IRepository<SecurityEvent> _eventRepo;
    private readonly ICacheService _cache;
    private readonly IEventBus _eventBus;
    private readonly ILogger<CorrelationEngine> _logger;
    private readonly CorrelationOptions _options;

    private static readonly Dictionary<string, CorrelationPattern> BuiltInPatterns = new()
    {
        ["UsbExfiltration"] = new CorrelationPattern
        {
            Name = "USB Data Exfiltration",
            Description = "USB connected followed by file copy and logout",
            TimeWindow = TimeSpan.FromMinutes(5),
            RequiredEvents = new[] { "UsbConnected", "FileCopy", "Logout" },
            MinScore = 80,
            Severity = Severity.Critical,
            Tags = new[] { "exfiltration", "usb", "data-loss" }
        },
        ["OffHoursActivity"] = new CorrelationPattern
        {
            Name = "Off-Hours Suspicious Activity",
            Description = "Login outside business hours with unknown software and PowerShell",
            TimeWindow = TimeSpan.FromMinutes(15),
            RequiredEvents = new[] { "Login", "AppStarted", "PowerShellStarted" },
            Conditions = new Dictionary<string, string>
            {
                ["Login.Hour"] = "< 6 || > 22",
                ["AppStarted.IsUnknown"] = "true"
            },
            MinScore = 70,
            Severity = Severity.High,
            Tags = new[] { "off-hours", "suspicious" }
        },
        ["BruteForce"] = new CorrelationPattern
        {
            Name = "Brute Force Attempt",
            Description = "Multiple failed login attempts in short period",
            TimeWindow = TimeSpan.FromMinutes(10),
            RequiredEvents = new[] { "FailedLogin", "FailedLogin", "FailedLogin", "FailedLogin", "FailedLogin" },
            MinCounts = new Dictionary<string, int> { ["FailedLogin"] = 5 },
            MinScore = 60,
            Severity = Severity.High,
            Tags = new[] { "bruteforce", "authentication" }
        },
        ["NewAdminAnomaly"] = new CorrelationPattern
        {
            Name = "New Local Administrator Anomaly",
            Description = "New admin account created with subsequent security control changes",
            TimeWindow = TimeSpan.FromHours(1),
            RequiredEvents = new[] { "NewLocalAdmin", "FirewallDisabled", "DefenderDisabled" },
            MinScore = 90,
            Severity = Severity.Critical,
            Tags = new[] { "privilege-escalation", "defense-evasion" }
        },
        ["RansomwareIndicators"] = new CorrelationPattern
        {
            Name = "Ransomware Indicators",
            Description = "Mass file renames with suspicious extensions detected",
            TimeWindow = TimeSpan.FromMinutes(5),
            RequiredEvents = new[] { "MassFileRename", "RansomwarePattern" },
            MinCounts = new Dictionary<string, int> { ["MassFileRename"] = 1, ["RansomwarePattern"] = 1 },
            MinScore = 95,
            Severity = Severity.Critical,
            Tags = new[] { "ransomware", "malware", "encryption" }
        },
        ["MassRenameAttack"] = new CorrelationPattern
        {
            Name = "Mass File Rename Attack",
            Description = "Large number of file renames in short period",
            TimeWindow = TimeSpan.FromMinutes(2),
            RequiredEvents = new[] { "MassFileRename" },
            MinCounts = new Dictionary<string, int> { ["MassFileRename"] = 1 },
            MinScore = 90,
            Severity = Severity.Critical,
            Tags = new[] { "ransomware", "file-system", "mass-rename" }
        },
        ["SuspiciousFileExtension"] = new CorrelationPattern
        {
            Name = "Suspicious Ransomware Extension",
            Description = "File created with known ransomware extension",
            TimeWindow = TimeSpan.FromMinutes(10),
            RequiredEvents = new[] { "RansomwarePattern" },
            MinCounts = new Dictionary<string, int> { ["RansomwarePattern"] = 3 },
            MinScore = 85,
            Severity = Severity.High,
            Tags = new[] { "ransomware", "suspicious-file" }
        },
        ["CryptominerDetected"] = new CorrelationPattern
        {
            Name = "Cryptominer Detection",
            Description = "Known mining process or suspicious high CPU activity detected",
            TimeWindow = TimeSpan.FromMinutes(10),
            RequiredEvents = new[] { "CryptominerDetected", "HighCpuProcess" },
            MinCounts = new Dictionary<string, int> { ["CryptominerDetected"] = 1 },
            MinScore = 90,
            Severity = Severity.Critical,
            Tags = new[] { "cryptominer", "malware", "cryptojacking" }
        },
        ["SuspiciousHighCpu"] = new CorrelationPattern
        {
            Name = "Suspicious High CPU Activity",
            Description = "Multiple processes with sustained high CPU usage",
            TimeWindow = TimeSpan.FromMinutes(5),
            RequiredEvents = new[] { "HighCpuProcess", "HighCpuProcess", "HighCpuProcess" },
            MinCounts = new Dictionary<string, int> { ["HighCpuProcess"] = 3 },
            MinScore = 75,
            Severity = Severity.High,
            Tags = new[] { "performance", "suspicious", "cryptominer" }
        }
    };

    public CorrelationEngine(
        IRepository<CorrelationRule> ruleRepo,
        IRepository<SecurityEvent> eventRepo,
        ICacheService cache,
        IEventBus eventBus,
        IOptions<CorrelationOptions> options,
        ILogger<CorrelationEngine> logger)
    {
        _ruleRepo = ruleRepo;
        _eventRepo = eventRepo;
        _cache = cache;
        _eventBus = eventBus;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CorrelationResult?> AnalyzeEventAsync(object securityEvent)
    {
        if (!_options.EnableCorrelation) return null;

        var computerId = GetPropertyValue<Guid>(securityEvent, "ComputerId");
        if (computerId == Guid.Empty) return null;

        foreach (var (key, pattern) in BuiltInPatterns)
        {
            if (!pattern.RequiredEvents.Contains(securityEvent.GetType().Name, StringComparer.OrdinalIgnoreCase))
                continue;

            var windowStart = DateTime.UtcNow - pattern.TimeWindow;
            var recentEvents = await _eventRepo.Query()
                .Where(e => e.ComputerId == computerId && e.Timestamp >= windowStart)
                .OrderByDescending(e => e.Timestamp)
                .ToListAsync();

            if (!HasRequiredEvents(pattern, recentEvents)) continue;

            var score = CalculateCorrelationScore(pattern, recentEvents);
            if (score < pattern.MinScore) continue;

            var result = new CorrelationResult
            {
                PatternName = pattern.Name,
                Description = pattern.Description,
                Score = score,
                Severity = pattern.Severity,
                ComputerId = computerId,
                RelatedEvents = recentEvents.Take(10).Select(e => e.Id).ToList(),
                Timestamp = DateTime.UtcNow,
                Tags = pattern.Tags.ToList()
            };

            await PublishCorrelationAlert(result);
            return result;
        }

        var rules = await _cache.GetOrCreateAsync("correlation:active_rules", async () =>
        {
            return await _ruleRepo.Query()
                .Where(r => r.IsEnabled && !r.IsDeleted)
                .ToListAsync();
        }, TimeSpan.FromMinutes(5));

        foreach (var rule in rules)
        {
            var result = await EvaluateCustomRule(rule, computerId);
            if (result != null) return result;
        }

        return null;
    }

    private async Task<CorrelationResult?> EvaluateCustomRule(CorrelationRule rule, Guid computerId)
    {
        var windowStart = DateTime.UtcNow - rule.TimeWindow;

        var recentEvents = await _eventRepo.Query()
            .Where(e => e.ComputerId == computerId && e.Timestamp >= windowStart)
            .ToListAsync();

        var score = EvaluateRuleExpression(rule.ConditionExpression, recentEvents);
        if (score >= rule.Score)
        {
            return new CorrelationResult
            {
                PatternName = rule.Name,
                Description = rule.Description,
                Score = score,
                Severity = rule.Priority switch
                {
                    1 => Severity.Critical,
                    2 => Severity.High,
                    3 => Severity.Medium,
                    _ => Severity.Low
                },
                ComputerId = computerId,
                RelatedEvents = recentEvents.Take(10).Select(e => e.Id).ToList(),
                Timestamp = DateTime.UtcNow,
                Tags = rule.Tags?.ToList() ?? new()
            };
        }

        return null;
    }

    private bool HasRequiredEvents(CorrelationPattern pattern, List<SecurityEvent> events)
    {
        var counts = events.GroupBy(e => e.EventType)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var requiredEvent in pattern.RequiredEvents)
        {
            var minCount = pattern.MinCounts?.GetValueOrDefault(requiredEvent, 1) ?? 1;
            if (!counts.TryGetValue(requiredEvent, out var actualCount) || actualCount < minCount)
                return false;
        }

        return true;
    }

    private int CalculateCorrelationScore(CorrelationPattern pattern, List<SecurityEvent> events)
    {
        var score = 0;

        score += Math.Min(events.Count * 10, 40);

        score += events.Sum(e => (int)e.Severity) * 5;

        if (events.Count >= 2)
        {
            var timeSpan = (events.Max(e => e.Timestamp) - events.Min(e => e.Timestamp)).TotalMinutes;
            score += Math.Max(0, 20 - (int)timeSpan);
        }

        return Math.Min(score, 100);
    }

    private int EvaluateRuleExpression(string expression, List<SecurityEvent> events)
    {
        if (string.IsNullOrWhiteSpace(expression)) return 0;

        try
        {
            var tokens = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var score = 0;

            for (int i = 0; i < tokens.Length; i++)
            {
                if (tokens[i].Equals("AND", StringComparison.OrdinalIgnoreCase)) continue;
                if (tokens[i].Equals("OR", StringComparison.OrdinalIgnoreCase)) continue;

                if (tokens[i].StartsWith("Count(", StringComparison.OrdinalIgnoreCase))
                {
                    var match = Regex.Match(tokens[i], @"Count\((\w+)\)\s*([><=!]+)\s*(\d+)", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var eventType = match.Groups[1].Value;
                        var op = match.Groups[2].Value;
                        var threshold = int.Parse(match.Groups[3].Value);
                        var count = events.Count(e => e.EventType.Equals(eventType, StringComparison.OrdinalIgnoreCase));

                        if (EvaluateComparison(count, op, threshold))
                            score += 25;
                    }
                }
                else if (tokens[i].StartsWith("Severity(", StringComparison.OrdinalIgnoreCase))
                {
                    var match = Regex.Match(tokens[i], @"Severity\((\w+)\)\s*([><=!]+)\s*(\d+)", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var severityStr = match.Groups[1].Value;
                        var op = match.Groups[2].Value;
                        var threshold = int.Parse(match.Groups[3].Value);

                        if (Enum.TryParse<Severity>(severityStr, true, out var severity))
                        {
                            var count = events.Count(e => e.Severity == severity);
                            if (EvaluateComparison(count, op, threshold))
                                score += 35;
                        }
                    }
                }
            }

            return Math.Min(score, 100);
        }
        catch
        {
            return 0;
        }
    }

    private bool EvaluateComparison(int value, string op, int threshold) => op switch
    {
        ">=" => value >= threshold,
        "<=" => value <= threshold,
        ">" => value > threshold,
        "<" => value < threshold,
        "==" or "=" => value == threshold,
        "!=" => value != threshold,
        _ => false
    };

    private async Task PublishCorrelationAlert(CorrelationResult result)
    {
        var alert = new SecurityAlert(
            $"Correlation: {result.PatternName}",
            result.Description,
            result.Severity,
            "Correlation",
            "CorrelationEngine",
            result.ComputerId);

        await _eventBus.PublishAsync(new CorrelationAlertEvent
        {
            Alert = alert,
            RelatedEventIds = result.RelatedEvents,
            Score = result.Score,
            Tags = result.Tags
        });

        _logger.LogWarning("Correlation alert: {Name} (Score: {Score}) on computer {ComputerId}",
            result.PatternName, result.Score, result.ComputerId);
    }

    private static Guid GetPropertyValue<T>(object obj, string propertyName)
    {
        var prop = obj.GetType().GetProperty(propertyName);
        if (prop == null) return Guid.Empty;
        var value = prop.GetValue(obj);
        return value is Guid guid ? guid : Guid.Empty;
    }

    public async Task<List<CorrelationResult>> AnalyzeTimeWindowAsync(string computerId, TimeSpan window)
    {
        throw new NotImplementedException();
    }

    public async Task<List<CorrelationResult>> AnalyzePatternAsync(string computerId, Guid ruleId)
    {
        throw new NotImplementedException();
    }
}

public class CorrelationPattern
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TimeSpan TimeWindow { get; set; } = TimeSpan.FromMinutes(10);
    public string[] RequiredEvents { get; set; } = Array.Empty<string>();
    public Dictionary<string, int>? MinCounts { get; set; }
    public Dictionary<string, string>? Conditions { get; set; }
    public int MinScore { get; set; } = 50;
    public Severity Severity { get; set; } = Severity.Medium;
    public string[] Tags { get; set; } = Array.Empty<string>();
}

public class CorrelationResult
{
    public string PatternName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Score { get; set; }
    public Severity Severity { get; set; }
    public Guid ComputerId { get; set; }
    public List<Guid> RelatedEvents { get; set; } = new();
    public DateTimeOffset Timestamp { get; set; }
    public List<string> Tags { get; set; } = new();
}

public class CorrelationOptions
{
    public bool EnableCorrelation { get; set; } = true;
    public int MaxEventsPerWindow { get; set; } = 1000;
    public int DefaultTimeWindowMinutes { get; set; } = 60;
    public bool EnableBuiltInPatterns { get; set; } = true;
}

public class CorrelationAlertEvent : IEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string EventType => nameof(CorrelationAlertEvent);
    public string Source => "Sentinela.Correlation";

    public SecurityAlert Alert { get; init; } = null!;
    public List<Guid> RelatedEventIds { get; init; } = new();
    public int Score { get; init; }
    public List<string> Tags { get; init; } = new();
}
