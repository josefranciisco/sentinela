using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Sentinela.Api.Services;

public class HeskTicketClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
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

    private bool HasHesk => !string.IsNullOrWhiteSpace(_options.HeskFeedUrl);
    private bool HasSgm => !string.IsNullOrWhiteSpace(_options.ConnectionString);
    public bool IsConfigured => HasHesk || HasSgm;

    public async Task<HeskFeedSnapshot> FetchAsync(CancellationToken ct = default)
    {
        var snapshot = new HeskFeedSnapshot { Configured = IsConfigured };
        if (!IsConfigured)
        {
            snapshot.Error = "Nenhuma fonte de chamados configurada.";
            return snapshot;
        }

        HeskFeedSnapshot? hesk = null;
        HeskFeedSnapshot? sgm = null;
        if (HasHesk)
            hesk = await FetchHeskAsync(ct);
        if (HasSgm)
            sgm = await FetchSgmAsync(ct);

        // Com feed HESK ok, ele manda no status aberto/fechado. SGM só completa
        // buracos quando o HESK está indisponível (senão ficam "fantasmas" abertos).
        var tickets = Merge(
            hesk?.Tickets ?? [],
            sgm?.Tickets ?? [],
            heskAuthoritative: hesk?.Reachable == true);
        snapshot.Reachable = hesk?.Reachable == true || sgm?.Reachable == true;
        snapshot.FetchedAt = DateTimeOffset.UtcNow;
        snapshot.Tickets = tickets;
        snapshot.OpenCount = tickets.Count(t => t.Status != 3);
        if (!snapshot.Reachable)
            snapshot.Error = hesk?.Error ?? sgm?.Error ?? "Não foi possível ler os chamados.";
        return snapshot;
    }

    private async Task<HeskFeedSnapshot> FetchHeskAsync(CancellationToken ct)
    {
        var snapshot = new HeskFeedSnapshot { Configured = true };
        try
        {
            var url = _options.HeskFeedUrl.Trim();
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            var token = _options.HeskFeedToken?.Trim();
            if (!string.IsNullOrEmpty(token))
                request.Headers.TryAddWithoutValidation("X-Sentinela-Token", token);

            using var response = await _http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var dto = await response.Content.ReadFromJsonAsync<HeskFeedDto>(JsonOptions, ct);
            if (dto is not { Ok: true })
            {
                snapshot.Error = "Feed do HESK inválido.";
                return snapshot;
            }

            snapshot.Reachable = true;
            snapshot.OpenCount = dto.OpenCount;
            snapshot.Tickets = dto.Tickets ?? [];
            return snapshot;
        }
        catch (Exception ex)
        {
            snapshot.Error = "Não foi possível ler os chamados no HESK.";
            _logger.LogWarning(ex, "HESK feed query failed");
            return snapshot;
        }
    }

    private async Task<HeskFeedSnapshot> FetchSgmAsync(CancellationToken ct)
    {
        var snapshot = new HeskFeedSnapshot { Configured = true };
        try
        {
            await using var db = new SqlConnection(WithConnectTimeout(_options.ConnectionString, 8));
            await db.OpenAsync(ct);

            var open = await QueryAsync(db, openOnly: true, ct);
            var closed = await QueryAsync(db, openOnly: false, ct);
            var openCount = await CountOpenAsync(db, ct);

            snapshot.Reachable = true;
            snapshot.OpenCount = openCount;
            snapshot.Tickets = open.Concat(closed)
                .GroupBy(t => t.Id)
                .Select(g => g.First())
                .ToList();
            return snapshot;
        }
        catch (Exception ex)
        {
            snapshot.Error = "Não foi possível ler os chamados no SGM.";
            _logger.LogWarning(ex, "SGM chamados query failed");
            return snapshot;
        }
    }

    private static List<HeskTicketEvent> Merge(
        List<HeskTicketEvent> hesk,
        List<HeskTicketEvent> sgm,
        bool heskAuthoritative)
    {
        var map = new Dictionary<string, HeskTicketEvent>(StringComparer.OrdinalIgnoreCase);

        foreach (var ticket in hesk)
        {
            var key = TicketKey(ticket);
            if (key.Length > 0)
                map[key] = ticket;
        }

        foreach (var ticket in sgm)
        {
            var key = TicketKey(ticket);
            if (key.Length == 0)
                continue;

            if (map.TryGetValue(key, out var existing))
            {
                // Se qualquer fonte marcar resolvido, some da lista de abertos.
                if (existing.Status != 3 && ticket.Status == 3)
                    map[key] = ticket;
                else if (existing.Status == 3)
                    continue;
                else if (ticket.UpdatedAt > existing.UpdatedAt)
                    map[key] = PreferHeskMetadata(existing, ticket);
                continue;
            }

            // Sem HESK confiável: inclui SGM. Com HESK ok: não inventa aberto só do SGM.
            if (!heskAuthoritative)
                map[key] = ticket;
        }

        return map.Values
            .OrderBy(t => t.Status == 3)
            .ThenBy(t => t.Status == 3 ? DateTimeOffset.MaxValue : t.CreatedAt)
            .ThenByDescending(t => t.UpdatedAt)
            .ToList();
    }

    /// <summary>Atualiza metadados do SGM mantendo identidade/URL do HESK quando possível.</summary>
    private static HeskTicketEvent PreferHeskMetadata(HeskTicketEvent hesk, HeskTicketEvent sgm) => new()
    {
        Id = hesk.Id > 0 ? hesk.Id : sgm.Id,
        TrackId = string.IsNullOrWhiteSpace(hesk.TrackId) ? sgm.TrackId : hesk.TrackId,
        Subject = string.IsNullOrWhiteSpace(sgm.Subject) ? hesk.Subject : sgm.Subject,
        Name = string.IsNullOrWhiteSpace(sgm.Name) ? hesk.Name : sgm.Name,
        Email = sgm.Email ?? hesk.Email,
        Status = sgm.Status,
        StatusLabel = sgm.StatusLabel,
        Priority = hesk.Priority,
        PriorityLabel = hesk.PriorityLabel,
        Category = sgm.Category ?? hesk.Category,
        CreatedAt = hesk.CreatedAt != default ? hesk.CreatedAt : sgm.CreatedAt,
        UpdatedAt = sgm.UpdatedAt,
        Event = sgm.Event,
        Url = hesk.Url ?? sgm.Url
    };

    private static string TicketKey(HeskTicketEvent ticket)
    {
        if (!string.IsNullOrWhiteSpace(ticket.TrackId))
            return ticket.TrackId.Trim();
        return ticket.Id > 0 ? $"id:{ticket.Id}" : "";
    }

    private async Task<int> CountOpenAsync(SqlConnection db, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand();
        cmd.CommandTimeout = 15;
        cmd.CommandText = "SELECT COUNT(*) FROM dbo.sgm_chamados WHERE status <> N'resolvido'";
        var raw = await cmd.ExecuteScalarAsync(ct);
        return raw is int n ? n : Convert.ToInt32(raw);
    }

    private async Task<List<HeskTicketEvent>> QueryAsync(SqlConnection db, bool openOnly, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand();
        cmd.CommandTimeout = 15;
        cmd.CommandText = openOnly
            ? """
              SELECT TOP 100
                c.id, c.protocolo, c.categoria, c.status, c.assunto,
                c.aberto_por_nome, c.aberto_por_email, c.aberto_em, c.atualizado_em,
                c.trackid_hesk
              FROM dbo.sgm_chamados c
              WHERE c.status <> N'resolvido'
              ORDER BY c.aberto_em ASC
              """
            : """
              SELECT TOP 12
                c.id, c.protocolo, c.categoria, c.status, c.assunto,
                c.aberto_por_nome, c.aberto_por_email, c.aberto_em, c.atualizado_em,
                c.trackid_hesk
              FROM dbo.sgm_chamados c
              WHERE c.status = N'resolvido'
              ORDER BY c.atualizado_em DESC
              """;

        var list = new List<HeskTicketEvent>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetInt32(0);
            var protocolo = reader.IsDBNull(1) ? "" : reader.GetString(1);
            var categoria = reader.IsDBNull(2) ? null : reader.GetString(2);
            var statusRaw = reader.IsDBNull(3) ? "" : reader.GetString(3);
            var assunto = reader.IsDBNull(4) ? "" : reader.GetString(4);
            var nome = reader.IsDBNull(5) ? "" : reader.GetString(5);
            var email = reader.IsDBNull(6) ? null : reader.GetString(6);
            var abertoEm = ReadDate(reader, 7);
            var atualizadoEm = ReadDate(reader, 8);
            var trackHesk = reader.IsDBNull(9) ? null : reader.GetString(9);

            var (status, label, evt) = MapStatus(statusRaw);
            if (status != 3 && atualizadoEm > abertoEm.AddMinutes(1))
                evt = "reply";

            list.Add(new HeskTicketEvent
            {
                Id = id,
                TrackId = !string.IsNullOrWhiteSpace(trackHesk) ? trackHesk
                    : (string.IsNullOrWhiteSpace(protocolo) ? id.ToString() : protocolo),
                Subject = assunto,
                Name = nome,
                Email = email,
                Status = status,
                StatusLabel = label,
                Priority = 0,
                PriorityLabel = "",
                Category = categoria,
                CreatedAt = abertoEm,
                UpdatedAt = atualizadoEm,
                Event = evt,
                Url = BuildUrl(id, trackHesk)
            });
        }

        return list;
    }

    private static string WithConnectTimeout(string connectionString, int seconds)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            ConnectTimeout = seconds
        };
        return builder.ConnectionString;
    }

    private string? BuildUrl(int id, string? trackHesk)
    {
        var app = _options.AppBaseUrl?.Trim().TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(app))
            return $"{app}/ti/chamados?id={id}";

        if (!string.IsNullOrWhiteSpace(trackHesk))
        {
            var admin = string.IsNullOrWhiteSpace(_options.HeskAdminUrl)
                ? "http://menu/chamados/admin"
                : _options.HeskAdminUrl.TrimEnd('/');
            return $"{admin}/admin_ticket.php?track={Uri.EscapeDataString(trackHesk)}";
        }

        return null;
    }

    private static (int Status, string Label, string Event) MapStatus(string raw)
    {
        return raw.Trim().ToLowerInvariant() switch
        {
            "aberto" => (0, "Aberto", "new"),
            "em_andamento" => (4, "Em andamento", "progress"),
            "em_revisao" => (5, "Em revisão", "waiting"),
            "resolvido" => (3, "Resolvido", "resolved"),
            _ => (1, string.IsNullOrWhiteSpace(raw) ? "Atualizado" : raw, "updated")
        };
    }

    private static DateTimeOffset ReadDate(SqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            return DateTimeOffset.UtcNow;
        var value = reader.GetDateTime(ordinal);
        return DateTime.SpecifyKind(value, DateTimeKind.Local);
    }

    private sealed class HeskFeedDto
    {
        public bool Ok { get; set; }
        public int OpenCount { get; set; }
        public List<HeskTicketEvent>? Tickets { get; set; }
    }
}
