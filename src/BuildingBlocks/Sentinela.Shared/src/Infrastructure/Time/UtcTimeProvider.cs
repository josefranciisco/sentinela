using Sentinela.Shared.Core.Interfaces;

namespace Sentinela.Shared.Infrastructure.Time;

public class UtcTimeProvider : IDateTime
{
    public DateTime Now => DateTime.Now;
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime Today => DateTime.Today;
}
