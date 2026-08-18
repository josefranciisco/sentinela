using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Sentinela.Api.Services;

public class HeskTicketClient
{
    private readonly HeskOptions _options;
    private readonly ILogger<HeskTicketClient> _logger;

    public HeskTicketClient(IOptions<HeskOptions> options, ILogger<HeskTicketClient> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ConnectionString);

    public async Task<HeskFeedSnapshot> FetchAsync(CancellationToken ct = default)
    {
        var snapshot = new HeskFeedSnapshot { Configured = IsConfigured };
        if (!IsConfigured)
        {
            snapshot.Error = "SGM não configurado.";
            return snapshot;
        }

        try
        {
            await using var db = new SqlConnection(_options.ConnectionString);
            await db.OpenAsync(ct);

            var open = await QueryAsync(db, openOnly: true, ct);
            var closed = await QueryAsync(db, openOnly: false, ct);
            var openCount = await CountOpenAsync(db, ct);

            var tickets = open.Concat(closed)
                .GroupBy(t => t.Id)
                .Select(g => g.First())
                .OrderBy(t => t.Status == 3)
                .ThenBy(t => t.Status == 3 ? DateTimeOffset.MaxValue : t.CreatedAt)
                .ThenByDescending(t => t.UpdatedAt)
                .ToList();

            snapshot.Reachable = true;
            snapshot.FetchedAt = DateTimeOffset.UtcNow;
            snapshot.OpenCount = openCount;
            snapshot.Tickets = tickets;
            return snapshot;
        }
        catch (Exception ex)
        {
            snapshot.Error = "Não foi possível ler os chamados no SGM.";
            _logger.LogWarning(ex, "SGM chamados query failed");
            return snapshot;
        }
    }

    private async Task<int> CountOpenAsync(SqlConnection db, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM dbo.sgm_chamados WHERE status <> N'resolvido'";
        var raw = await cmd.ExecuteScalarAsync(ct);
        return raw is int n ? n : Convert.ToInt32(raw);
    }

    private async Task<List<HeskTicketEvent>> QueryAsync(SqlConnection db, bool openOnly, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand();
        cmd.CommandText = openOnly
            ? """
              SELECT TOP 100
                c.id, c.protocolo, c.categoria, c.status, c.assunto,
                c.aberto_por_nome, c.aberto_por_email, c.aberto_em, c.atualizado_em,
                c.resolvido_em, c.trackid_hesk,
                (SELECT MAX(m.criado_em) FROM dbo.sgm_chamado_mensagens m WHERE m.chamado_id = c.id) AS last_msg
              FROM dbo.sgm_chamados c
              WHERE c.status <> N'resolvido'
              ORDER BY c.aberto_em ASC
              """
            : """
              SELECT TOP 12
                c.id, c.protocolo, c.categoria, c.status, c.assunto,
                c.aberto_por_nome, c.aberto_por_email, c.aberto_em, c.atualizado_em,
                c.resolvido_em, c.trackid_hesk,
                (SELECT MAX(m.criado_em) FROM dbo.sgm_chamado_mensagens m WHERE m.chamado_id = c.id) AS last_msg
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
            var trackHesk = reader.IsDBNull(10) ? null : reader.GetString(10);
            var lastMsg = reader.IsDBNull(11) ? (DateTimeOffset?)null : ReadDate(reader, 11);

            var (status, label, evt) = MapStatus(statusRaw);
            if (lastMsg is { } msg
                && status != 3
                && msg > abertoEm.AddMinutes(1))
            {
                evt = "reply";
            }

            list.Add(new HeskTicketEvent
            {
                Id = id,
                TrackId = string.IsNullOrWhiteSpace(protocolo) ? id.ToString() : protocolo,
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
        var value = reader.GetDateTime(ordinal);
        return DateTime.SpecifyKind(value, DateTimeKind.Local);
    }
}
