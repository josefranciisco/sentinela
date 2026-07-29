using Xunit;
using FluentAssertions;
using Moq;
using Sentinela.Correlation.Engine;
using Sentinela.Shared.Domain.Alerting;
using Sentinela.Shared.Domain.Security;
using Sentinela.Shared.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Sentinela.Correlation.Tests;

public class CorrelationEngineTests
{
    private readonly Mock<IRepository<CorrelationRule>> _ruleRepoMock;
    private readonly Mock<IRepository<SecurityEvent>> _eventRepoMock;
    private readonly Mock<IRepository<Alert>> _alertRepoMock;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly Mock<IEventBus> _eventBusMock;
    private readonly Mock<ILogger<CorrelationEngine>> _loggerMock;
    private readonly CorrelationEngine _engine;

    public CorrelationEngineTests()
    {
        _ruleRepoMock = new Mock<IRepository<CorrelationRule>>();
        _eventRepoMock = new Mock<IRepository<SecurityEvent>>();
        _alertRepoMock = new Mock<IRepository<Alert>>();
        _cacheMock = new Mock<ICacheService>();
        _eventBusMock = new Mock<IEventBus>();
        _loggerMock = new Mock<ILogger<CorrelationEngine>>();

        var options = Options.Create(new CorrelationOptions 
        { 
            EnableCorrelation = true,
            EnableBuiltInPatterns = true 
        });

        _engine = new CorrelationEngine(
            _ruleRepoMock.Object,
            _eventRepoMock.Object,
            _alertRepoMock.Object,
            _cacheMock.Object,
            _eventBusMock.Object,
            options,
            _loggerMock.Object);
    }

    [Fact]
    public async Task AnalyzeEventAsync_WithUsbExfiltration_ReturnsCriticalAlert()
    {
        var computerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var recentEvents = new List<SecurityEvent>
        {
            new() { Id = Guid.NewGuid(), ComputerId = computerId, EventType = "UsbConnected", Timestamp = now.AddMinutes(-2), Severity = Severity.Medium },
            new() { Id = Guid.NewGuid(), ComputerId = computerId, EventType = "FileCopy", Timestamp = now.AddMinutes(-1), Severity = Severity.High },
            new() { Id = Guid.NewGuid(), ComputerId = computerId, EventType = "FileCopy", Timestamp = now.AddMinutes(-1), Severity = Severity.High },
            new() { Id = Guid.NewGuid(), ComputerId = computerId, EventType = "Logout", Timestamp = now, Severity = Severity.Info }
        };

        _eventRepoMock.Setup(x => x.Query())
            .Returns(recentEvents.AsQueryable());

        _eventBusMock.Setup(x => x.PublishAsync(It.IsAny<CorrelationAlertEvent>()))
            .Returns(Task.CompletedTask);

        var triggerEvent = recentEvents[3];

        var result = await _engine.AnalyzeEventAsync(triggerEvent);

        result.Should().NotBeNull();
        result!.PatternName.Should().Be("USB Data Exfiltration");
        result.Severity.Should().Be(Severity.Critical);
        result.Score.Should().BeGreaterOrEqualTo(80);
    }

    [Fact]
    public async Task AnalyzeEventAsync_WithNormalActivity_ReturnsNull()
    {
        var computerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var recentEvents = new List<SecurityEvent>
        {
            new() { Id = Guid.NewGuid(), ComputerId = computerId, EventType = "Login", Timestamp = now.AddHours(-8), Severity = Severity.Info },
            new() { Id = Guid.NewGuid(), ComputerId = computerId, EventType = "AppStarted", Timestamp = now.AddHours(-7), Severity = Severity.Info }
        };

        _eventRepoMock.Setup(x => x.Query())
            .Returns(recentEvents.AsQueryable());

        var triggerEvent = recentEvents[1];

        var result = await _engine.AnalyzeEventAsync(triggerEvent);

        result.Should().BeNull();
    }
}
