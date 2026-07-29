using Xunit;
using FluentAssertions;
using Moq;
using Sentinela.AlertEngine.Evaluators;
using Sentinela.Shared.Domain.Alerting;
using Sentinela.Shared.Domain.Monitoring.Enums;
using Sentinela.Shared.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sentinela.AlertEngine.Configuration;

namespace Sentinela.AlertEngine.Tests;

public class AlertEvaluatorTests
{
    private readonly Mock<ICacheService> _cacheMock;
    private readonly Mock<ILogger<AlertEvaluator>> _loggerMock;
    private readonly AlertEvaluator _evaluator;

    public AlertEvaluatorTests()
    {
        _cacheMock = new Mock<ICacheService>();
        _loggerMock = new Mock<ILogger<AlertEvaluator>>();
        _evaluator = new AlertEvaluator(_cacheMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task EvaluateAsync_WithMatchingCondition_ReturnsAlertResult()
    {
        var rule = new AlertRule
        {
            Id = Guid.NewGuid(),
            Name = "USB Detected",
            Condition = "USBConnected",
            Severity = Severity.Medium,
            Category = "Security",
            CooldownPeriod = TimeSpan.FromMinutes(5)
        };

        var usbEvent = new { EventType = "USBConnected", ComputerId = Guid.NewGuid(), Username = "user1" };

        _cacheMock.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _cacheMock.Setup(x => x.SetAsync(It.IsAny<string>(), true, It.IsAny<TimeSpan>())).Returns(Task.CompletedTask);

        var results = await _evaluator.EvaluateAsync(rule, usbEvent, CancellationToken.None);

        results.Should().HaveCount(1);
        results[0].RuleId.Should().Be(rule.Id);
        results[0].Severity.Should().Be(Severity.Medium);
    }

    [Fact]
    public async Task EvaluateAsync_WithCooldown_ReturnsNoResult()
    {
        var rule = new AlertRule
        {
            Id = Guid.NewGuid(),
            Name = "USB Detected",
            Condition = "USBConnected",
            Severity = Severity.Medium,
            CooldownPeriod = TimeSpan.FromMinutes(5)
        };

        var usbEvent = new { EventType = "USBConnected", ComputerId = Guid.NewGuid(), Username = "user1" };

        _cacheMock.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

        var results = await _evaluator.EvaluateAsync(rule, usbEvent, CancellationToken.None);

        results.Should().BeEmpty();
    }
}
