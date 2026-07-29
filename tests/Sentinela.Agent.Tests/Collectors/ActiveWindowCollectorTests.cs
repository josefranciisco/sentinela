using Xunit;
using FluentAssertions;
using Sentinela.Agent.Core.Collectors;

namespace Sentinela.Agent.Tests.Collectors;

public class ActiveWindowCollectorTests
{
    [Fact]
    public void GetForegroundProcessId_ReturnsPositiveId()
    {
        var collector = new ActiveWindowCollector();
        var pid = collector.GetForegroundProcessId();
        pid.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetForegroundProcessName_ReturnsNonEmpty()
    {
        var collector = new ActiveWindowCollector();
        var name = collector.GetForegroundProcessName();
        name.Should().NotBeNullOrEmpty();
    }
}
