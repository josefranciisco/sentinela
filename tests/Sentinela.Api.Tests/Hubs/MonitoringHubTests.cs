using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.AspNetCore.SignalR;
using Sentinela.Api.Hubs;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Sentinela.Api.Tests.Hubs;

public class MonitoringHubTests
{
    [Fact]
    public async Task OnConnectedAsync_WithAdminUser_AddsToAdminGroup()
    {
        var loggerMock = new Mock<ILogger<MonitoringHub>>();
        var hub = new MonitoringHub(loggerMock.Object);

        var mockClients = new Mock<IHubCallerClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        var mockContext = new Mock<HubCallerContext>();
        var mockGroups = new Mock<IGroupManager>();

        var claims = new List<Claim> { new(ClaimTypes.Role, "Admin") };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);

        mockContext.Setup(c => c.User).Returns(principal);
        mockContext.Setup(c => c.ConnectionId).Returns("conn-1");

        hub.Clients = mockClients.Object;
        hub.Context = mockContext.Object;
        hub.Groups = mockGroups.Object;

        await hub.OnConnectedAsync();

        mockGroups.Verify(g => g.AddToGroupAsync("conn-1", "admins", default), Times.Once);
    }

    [Fact]
    public async Task OnConnectedAsync_WithComputerId_AddsToComputerGroup()
    {
        var loggerMock = new Mock<ILogger<MonitoringHub>>();
        var hub = new MonitoringHub(loggerMock.Object);

        var mockContext = new Mock<HubCallerContext>();
        var mockGroups = new Mock<IGroupManager>();

        var claims = new List<Claim> { new("ComputerId", "comp-123") };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);

        mockContext.Setup(c => c.User).Returns(principal);
        mockContext.Setup(c => c.ConnectionId).Returns("conn-2");

        hub.Context = mockContext.Object;
        hub.Groups = mockGroups.Object;

        await hub.OnConnectedAsync();

        mockGroups.Verify(g => g.AddToGroupAsync("conn-2", "computer:comp-123", default), Times.Once);
    }
}
