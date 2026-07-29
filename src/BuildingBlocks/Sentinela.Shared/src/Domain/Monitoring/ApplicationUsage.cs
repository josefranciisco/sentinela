using Sentinela.Shared.Core.Entities;

namespace Sentinela.Shared.Domain.Monitoring;

public class ApplicationUsage : BaseEntity
{
    protected ApplicationUsage() : base() { }

    public ApplicationUsage(
        Guid computerId,
        string processName,
        string windowTitle,
        string executablePath,
        DateTimeOffset startTime,
        string username)
        : base()
    {
        ComputerId = computerId;
        ProcessName = processName;
        WindowTitle = windowTitle;
        ExecutablePath = executablePath;
        StartTime = startTime;
        Username = username;
        IsForeground = false;
    }

    public Guid ComputerId { get; private set; }
    public string ProcessName { get; private set; }
    public string WindowTitle { get; private set; }
    public string ExecutablePath { get; private set; }
    public DateTimeOffset StartTime { get; private set; }
    public DateTimeOffset? EndTime { get; private set; }
    public TimeSpan? Duration { get; private set; }
    public bool IsForeground { get; private set; }
    public string Username { get; private set; }

    public void SetEndTime(DateTimeOffset endTime)
    {
        EndTime = endTime;
        Duration = endTime - StartTime;
    }
}
