namespace Sentinela.Api.Services;

public class HeskOptions
{
    public string BaseUrl { get; set; } = "http://192.168.0.116/chamados";
    public string FeedPath { get; set; } = "sentinela-feed.php";
    public string Token { get; set; } = "sentinela-mobi-hesk";
    public string AdminUrl { get; set; } = "http://menu/chamados/admin";
    public int PollSeconds { get; set; } = 8;
}

public class HeskTicketEvent
{
    public int Id { get; set; }
    public string TrackId { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Email { get; set; }
    public int Status { get; set; }
    public string StatusLabel { get; set; } = "";
    public int Priority { get; set; }
    public string PriorityLabel { get; set; } = "";
    public string? Category { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string Event { get; set; } = "updated";
    public string? Url { get; set; }
}

public class HeskFeedSnapshot
{
    public bool Configured { get; set; }
    public bool Reachable { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset? FetchedAt { get; set; }
    public int OpenCount { get; set; }
    public List<HeskTicketEvent> Tickets { get; set; } = [];
}

public class HeskTicketFeedStore
{
    private readonly object _gate = new();
    private HeskFeedSnapshot _snapshot = new();

    public HeskFeedSnapshot Get()
    {
        lock (_gate) return Clone(_snapshot);
    }

    public void Set(HeskFeedSnapshot snapshot)
    {
        lock (_gate) _snapshot = Clone(snapshot);
    }

    private static HeskFeedSnapshot Clone(HeskFeedSnapshot source) => new()
    {
        Configured = source.Configured,
        Reachable = source.Reachable,
        Error = source.Error,
        FetchedAt = source.FetchedAt,
        OpenCount = source.OpenCount,
        Tickets = [.. source.Tickets]
    };
}
