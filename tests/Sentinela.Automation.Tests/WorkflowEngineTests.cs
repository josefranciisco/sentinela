using Xunit;
using FluentAssertions;
using Moq;
using Sentinela.Automation.Workflows;
using Sentinela.Shared.Domain.Automation;
using Sentinela.Shared.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sentinela.Automation.Configuration;

namespace Sentinela.Automation.Tests;

public class WorkflowEngineTests
{
    private readonly Mock<IRepository<Workflow>> _workflowRepoMock;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly Mock<IActionExecutor> _actionExecutorMock;
    private readonly Mock<ITriggerEvaluator> _triggerEvaluatorMock;
    private readonly Mock<ILogger<WorkflowEngine>> _loggerMock;
    private readonly WorkflowEngine _engine;

    public WorkflowEngineTests()
    {
        _workflowRepoMock = new Mock<IRepository<Workflow>>();
        _cacheMock = new Mock<ICacheService>();
        _actionExecutorMock = new Mock<IActionExecutor>();
        _triggerEvaluatorMock = new Mock<ITriggerEvaluator>();
        _loggerMock = new Mock<ILogger<WorkflowEngine>>();

        var options = Options.Create(new AutomationOptions { EnableAutomation = true });

        _engine = new WorkflowEngine(
            _workflowRepoMock.Object,
            _cacheMock.Object,
            _actionExecutorMock.Object,
            _triggerEvaluatorMock.Object,
            options,
            _loggerMock.Object);
    }

    [Fact]
    public async Task GetApplicableWorkflows_ReturnsMatchingWorkflows()
    {
        var workflows = new List<Workflow>
        {
            new() { Id = Guid.NewGuid(), Name = "USB Alert", IsEnabled = true, TriggerType = "USBConnected" },
            new() { Id = Guid.NewGuid(), Name = "Login Alert", IsEnabled = true, TriggerType = "Login" },
            new() { Id = Guid.NewGuid(), Name = "Disabled", IsEnabled = false, TriggerType = "USBConnected" }
        };

        _cacheMock.Setup(x => x.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<Func<Task<List<Workflow>>>>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync(workflows);

        _triggerEvaluatorMock.Setup(x => x.MatchesTrigger(It.Is<Workflow>(w => w.Name == "USB Alert"), It.IsAny<object>()))
            .ReturnsAsync(true);
        _triggerEvaluatorMock.Setup(x => x.MatchesTrigger(It.Is<Workflow>(w => w.Name == "Login Alert"), It.IsAny<object>()))
            .ReturnsAsync(false);

        var triggerEvent = new { EventType = "USBConnected" };

        var result = await _engine.GetApplicableWorkflows(triggerEvent);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("USB Alert");
    }

    [Fact]
    public async Task ExecuteWorkflow_WithValidConditions_ExecutesActions()
    {
        var workflow = new Workflow
        {
            Id = Guid.NewGuid(),
            Name = "Test Workflow",
            IsEnabled = true,
            Conditions = new List<WorkflowCondition> { new() { Field = "EventType", Operator = ConditionOperator.Equals, Value = "USBConnected" } },
            Actions = new List<WorkflowAction>
            {
                new() { ActionType = ActionType.SendAlert, Config = "{}", Order = 1 }
            }
        };

        _actionExecutorMock.Setup(x => x.ExecuteAction(It.IsAny<WorkflowAction>(), It.IsAny<object>()))
            .ReturnsAsync(new ActionResult { ActionType = "SendAlert", Success = true });

        _workflowRepoMock.Setup(x => x.UpdateAsync(It.IsAny<Workflow>())).Returns(Task.CompletedTask);

        var triggerEvent = new { EventType = "USBConnected", ComputerId = Guid.NewGuid() };

        var result = await _engine.ExecuteWorkflow(workflow, triggerEvent);

        result.Status.Should().Be(ExecutionStatus.Success);
        result.ActionResults.Should().HaveCount(1);
        result.ActionResults[0].Success.Should().BeTrue();
    }
}
