using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Sentinela.Api.Controllers.v1;
using Sentinela.Shared.Core.Interfaces;
using Sentinela.Shared.Domain.Monitoring;
using Sentinela.Api.Models;
using AutoMapper;

namespace Sentinela.Api.Tests;

public class ComputersControllerTests
{
    private readonly Mock<IRepository<Computer>> _computerRepoMock;
    private readonly Mock<IRepository<TimelineEntry>> _timelineRepoMock;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly Mock<IMapper> _mapperMock;
    private readonly Mock<ILogger<ComputersController>> _loggerMock;
    private readonly ComputersController _controller;

    public ComputersControllerTests()
    {
        _computerRepoMock = new Mock<IRepository<Computer>>();
        _timelineRepoMock = new Mock<IRepository<TimelineEntry>>();
        _cacheMock = new Mock<ICacheService>();
        _mapperMock = new Mock<IMapper>();
        _loggerMock = new Mock<ILogger<ComputersController>>();

        _controller = new ComputersController(
            _computerRepoMock.Object,
            _timelineRepoMock.Object,
            _cacheMock.Object,
            _mapperMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task GetComputers_ReturnsPaginatedResult()
    {
        var computers = new List<Computer>
        {
            new() { Id = Guid.NewGuid(), Hostname = "PC-001", Status = ComputerStatus.Online },
            new() { Id = Guid.NewGuid(), Hostname = "PC-002", Status = ComputerStatus.Offline }
        };

        var computerDtos = computers.Select(c => new ComputerDto 
        { 
            Id = c.Id, 
            Hostname = c.Hostname, 
            Status = c.Status.ToString() 
        }).ToList();

        _computerRepoMock.Setup(x => x.Query())
            .Returns(computers.AsQueryable());
        _mapperMock.Setup(x => x.Map<List<ComputerDto>>(It.IsAny<List<Computer>>()))
            .Returns(computerDtos);

        var result = await _controller.GetComputers();

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var paginatedResult = okResult.Value.Should().BeOfType<PaginatedResult<ComputerDto>>().Subject;
        paginatedResult.Items.Should().HaveCount(2);
        paginatedResult.Total.Should().Be(2);
    }

    [Fact]
    public async Task GetComputer_WithValidId_ReturnsComputer()
    {
        var computerId = Guid.NewGuid();
        var computer = new Computer { Id = computerId, Hostname = "PC-001" };
        var computerDto = new ComputerDetailDto { Id = computerId, Hostname = "PC-001" };

        _computerRepoMock.Setup(x => x.GetByIdAsync(computerId)).ReturnsAsync(computer);
        _mapperMock.Setup(x => x.Map<ComputerDetailDto>(computer)).Returns(computerDto);

        var result = await _controller.GetComputer(computerId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var computerResult = okResult.Value.Should().BeOfType<ComputerDetailDto>().Subject;
        computerResult.Hostname.Should().Be("PC-001");
    }

    [Fact]
    public async Task GetComputer_WithInvalidId_ReturnsNotFound()
    {
        _computerRepoMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Computer)null);

        var result = await _controller.GetComputer(Guid.NewGuid());

        result.Result.Should().BeOfType<NotFoundResult>();
    }
}
