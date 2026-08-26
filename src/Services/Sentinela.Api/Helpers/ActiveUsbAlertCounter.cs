using System.Text.RegularExpressions;

namespace Sentinela.Api.Helpers;

public static class ActiveUsbAlertCounter
{
    private static readonly Regex DriveLetter = new(@"([A-Za-z]):", RegexOptions.Compiled);

    public static async Task<int> CountAsync(
        IQueryable<TimelineEntry> timeline,
        IReadOnlyCollection<Guid> computerIds,
        CancellationToken cancellationToken = default)
    {
        if (computerIds.Count == 0)
            return 0;

        var since = DateTimeOffset.UtcNow.AddDays(-7);
        var rows = await timeline
            .Where(t => !t.IsDeleted
                && computerIds.Contains(t.ComputerId)
                && t.Timestamp >= since
                && (t.EventType == EventType.USBConnected || t.EventType == EventType.USBDisconnected))
            .Select(t => new { t.ComputerId, t.EventType, t.Timestamp, t.Description })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(t => (t.ComputerId, Drive: ExtractDrive(t.Description)))
            .Count(g => g.OrderByDescending(x => x.Timestamp).First().EventType == EventType.USBConnected);
    }

    private static string ExtractDrive(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return string.Empty;

        var match = DriveLetter.Match(description);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : description.Trim();
    }
}
