using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Sentinela.Api.Services;

public class HeskTicketClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _http;
    private readonly HeskOptions _options;
    private readonly ILogger<HeskTicketClient> _logger;

    public HeskTicketClient(HttpClient http, IOptions<HeskOptions> options, ILogger<HeskTicketClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.BaseUrl);

    public async Task<HeskFeedSnapshot> FetchAsync(CancellationToken ct = default)
    {
        var snapshot = new HeskFeedSnapshot { Configured = IsConfigured };
        if (!IsConfigured)
        {
            snapshot.Error = "HESK não configurado.";
            return snapshot;
        }

        try
        {
            var baseUrl = _options.BaseUrl.TrimEnd('/');
            var path = string.IsNullOrWhiteSpace(_options.FeedPath) ? "sentinela-feed.php" : _options.FeedPath.TrimStart('/');
            var url = $"{baseUrl}/{path}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrWhiteSpace(_options.Token))
                request.Headers.TryAddWithoutValidation("X-Sentinela-Token", _options.Token);

            using var response = await _http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                snapshot.Error = response.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? "Instale sentinela-feed.php na pasta do HESK."
                    : $"HESK respondeu {(int)response.StatusCode}.";
                _logger.LogWarning("HESK feed {Url} returned {Status}", url, (int)response.StatusCode);
                return snapshot;
            }

            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var root = doc.RootElement;
            if (root.TryGetProperty("ok", out var okEl) && okEl.ValueKind == JsonValueKind.False)
            {
                snapshot.Error = root.TryGetProperty("error", out var err) ? err.GetString() : "Feed HESK recusou a leitura.";
                return snapshot;
            }

            var tickets = ParseTickets(root, AdminTicketBase());
            if (root.TryGetProperty("openCount", out var openEl) && openEl.TryGetInt32(out var openFromFeed))
                snapshot.OpenCount = openFromFeed;
            else
                snapshot.OpenCount = tickets.Count(t => t.Status != 3);
            snapshot.Reachable = true;
            snapshot.FetchedAt = DateTimeOffset.UtcNow;
            snapshot.Tickets = tickets;
            return snapshot;
        }
        catch (Exception ex)
        {
            snapshot.Error = "Não foi possível alcançar o HESK.";
            _logger.LogWarning(ex, "HESK feed fetch failed");
            return snapshot;
        }
    }

    private static List<HeskTicketEvent> ParseTickets(JsonElement root, string adminBase)
    {
        if (!root.TryGetProperty("tickets", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return [];

        var list = new List<HeskTicketEvent>();
        foreach (var item in arr.EnumerateArray())
        {
            var track = ReadString(item, "trackId", "trackid") ?? "";
            if (string.IsNullOrWhiteSpace(track)) continue;
            var created = ReadDate(item, "createdAt", "dt") ?? DateTimeOffset.UtcNow;
            var updated = ReadDate(item, "updatedAt", "lastchange") ?? created;
            var url = $"{adminBase}/admin_ticket.php?track={Uri.EscapeDataString(track)}";

            list.Add(new HeskTicketEvent
            {
                Id = ReadInt(item, "id"),
                TrackId = track,
                Subject = ReadString(item, "subject") ?? "",
                Name = ReadString(item, "name") ?? "",
                Email = ReadString(item, "email"),
                Status = ReadInt(item, "status"),
                StatusLabel = ReadString(item, "statusLabel") ?? "",
                Priority = ReadInt(item, "priority"),
                PriorityLabel = ReadString(item, "priorityLabel") ?? "",
                Category = ReadString(item, "category"),
                CreatedAt = created,
                UpdatedAt = updated,
                Event = ReadString(item, "event") ?? "updated",
                Url = url
            });
        }
        return list
            .OrderBy(t => t.Status == 3)
            .ThenBy(t => t.Status == 3 ? DateTimeOffset.MaxValue : t.CreatedAt)
            .ThenByDescending(t => t.UpdatedAt)
            .ToList();
    }

    private string AdminTicketBase()
    {
        var admin = string.IsNullOrWhiteSpace(_options.AdminUrl)
            ? "http://menu/chamados/admin"
            : _options.AdminUrl.TrimEnd('/');
        return admin;
    }

    private static string? ReadString(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
        }
        return null;
    }

    private static int ReadInt(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var v)) return 0;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n)) return n;
        if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out n)) return n;
        return 0;
    }

    private static DateTimeOffset? ReadDate(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (!el.TryGetProperty(name, out var v)) continue;
            if (v.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(v.GetString(), out var parsed))
                return parsed;
        }
        return null;
    }
}
