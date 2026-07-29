using Sentinela.Shared.Core.ValueObjects;

namespace Sentinela.Shared.Domain.Automation;

public enum ActionType
{
    SendAlert,
    ExecuteScript,
    OpenTicket,
    SendEmail,
    SendTeams,
    SendSlack,
    BlockUSB,
    RestartService,
    RunPowerShell,
    SendWebhook
}

public class WorkflowAction : ValueObject
{
    private WorkflowAction() { }

    public WorkflowAction(ActionType actionType, string? config = null, int order = 0)
    {
        Type = actionType;
        Config = config;
        Order = order;
    }

    public ActionType Type { get; }
    public string? Config { get; }
    public int Order { get; }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Type;
        yield return Config ?? string.Empty;
        yield return Order;
    }
}
