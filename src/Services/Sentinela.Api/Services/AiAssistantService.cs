using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Sentinela.Shared.Domain.Analytics;
using Sentinela.Shared.Domain.Monitoring;

namespace Sentinela.Api.Services;

public class AiAssistantService : IAiAssistantService
{
    private readonly IRepository<Computer> _computerRepo;
    private readonly IRepository<Alert> _alertRepo;
    private readonly IRepository<TimelineEntry> _timelineRepo;
    private readonly IRepository<SecurityEvent> _securityEventRepo;
    private readonly IRepository<ApplicationUsage> _appUsageRepo;
    private readonly ICacheService _cache;
    private readonly ILogger<AiAssistantService> _logger;
    private readonly AiOptions _options;

    public AiAssistantService(
        IRepository<Computer> computerRepo,
        IRepository<Alert> alertRepo,
        IRepository<TimelineEntry> timelineRepo,
        IRepository<SecurityEvent> securityEventRepo,
        IRepository<ApplicationUsage> appUsageRepo,
        ICacheService cache,
        IOptions<AiOptions> options,
        ILogger<AiAssistantService> logger)
    {
        _computerRepo = computerRepo;
        _alertRepo = alertRepo;
        _timelineRepo = timelineRepo;
        _securityEventRepo = securityEventRepo;
        _appUsageRepo = appUsageRepo;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AiResponse> AskAsync(string query, Guid userId, string userName, Dictionary<string, string>? context = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var queryId = Guid.NewGuid();

        try
        {
            var queryLower = query.ToLowerInvariant();

            var response = queryLower switch
            {
                _ when queryLower.Contains("quem instalou") || queryLower.Contains("instalação") => await QuerySoftwareInstallations(query),
                _ when queryLower.Contains("pendrive") || queryLower.Contains("usb") => await QueryUsbActivity(query),
                _ when queryLower.Contains("offline") || queryLower.Contains("desconectado") => await QueryOfflineComputers(query),
                _ when queryLower.Contains("fora do horário") || queryLower.Contains("madrugada") => await QueryOffHoursLogins(query),
                _ when queryLower.Contains("inativo") || queryLower.Contains("ocioso") => await QueryInactiveUsers(query),
                _ when queryLower.Contains("programas mais utilizados") || queryLower.Contains("apps mais") => await QueryTopApplications(query),
                _ when queryLower.Contains("mais alertas") || queryLower.Contains("crítico") => await QueryMostAlertedComputers(query),
                _ when queryLower.Contains("explica") && queryLower.Contains("nota") => await ExplainComputerScore(ExtractGuid(query)),
                _ when queryLower.Contains("resumo") || queryLower.Contains("sumário") => await GenerateSummary(query),
                _ when queryLower.Contains("quantos computadores") => await QueryComputerCounts(),
                _ when queryLower.Contains("login") || queryLower.Contains("logon") => await QueryLogins(query),
                _ when queryLower.Contains("falha") || queryLower.Contains("erro") => await QueryFailures(query),
                _ => await HandleGeneralQuery(query)
            };

            stopwatch.Stop();
            response.QueryId = queryId;
            response.ProcessingTime = stopwatch.Elapsed;

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI query failed: {Query}", query);
            stopwatch.Stop();
            return new AiResponse
            {
                QueryId = queryId,
                Text = $"Desculpe, ocorreu um erro ao processar sua consulta: {ex.Message}",
                ProcessingTime = stopwatch.Elapsed
            };
        }
    }

    private async Task<AiResponse> QuerySoftwareInstallations(string query)
    {
        var since = ExtractDate(query, DateTime.UtcNow.AddDays(-1));
        var installations = await _timelineRepo.Query()
            .Where(t => t.EventType == EventType.SoftwareInstalled && t.Timestamp >= since)
            .GroupBy(t => t.Username)
            .Select(g => new { User = g.Key, Count = g.Count(), Items = g.OrderByDescending(x => x.Timestamp).Take(5).ToList() })
            .ToListAsync();

        if (!installations.Any())
            return new AiResponse { Text = $"Nenhuma instalação de software registrada desde {since:dd/MM/yyyy HH:mm}." };

        var text = $"**Instalações de Software desde {since:dd/MM/yyyy HH:mm}**\n\n";
        foreach (var inst in installations)
        {
            text += $"**{inst.User}** - {inst.Count} instalações\n";
            foreach (var item in inst.Items.Take(3))
            {
                text += $"  • {item.Description} às {item.Timestamp:HH:mm}\n";
            }
            text += "\n";
        }

        return new AiResponse { Text = text };
    }

    private async Task<AiResponse> QueryUsbActivity(string query)
    {
        var since = ExtractDate(query, DateTime.UtcNow.AddDays(-1));
        var usbEvents = await _timelineRepo.Query()
            .Where(t => (t.EventType == EventType.USBConnected || t.EventType == EventType.USBDisconnected) && t.Timestamp >= since)
            .OrderByDescending(t => t.Timestamp)
            .ToListAsync();

        if (!usbEvents.Any())
            return new AiResponse { Text = $"Nenhuma atividade de USB registrada desde {since:dd/MM/yyyy HH:mm}." };

        var count = usbEvents.Count(e => e.EventType == EventType.USBConnected);
        var users = usbEvents.Select(e => e.Username).Distinct();
        var text = $"**Atividade de USB desde {since:dd/MM/yyyy HH:mm}**\n\n";
        text += $"Total de eventos: {usbEvents.Count}\n";
        text += $"Conexões: {count}\n";
        text += $"Usuários: {string.Join(", ", users)}\n\n";
        text += "**Últimos eventos:**\n";
        foreach (var evt in usbEvents.Take(10))
        {
            text += $"  • {evt.Username} - {evt.Description} às {evt.Timestamp:HH:mm}\n";
        }

        return new AiResponse { Text = text };
    }

    private async Task<AiResponse> QueryOfflineComputers(string query)
    {
        var offlineThreshold = DateTime.UtcNow.AddMinutes(-5);
        var offlineComputers = await _computerRepo.Query()
            .Where(c => c.Status == ComputerStatus.Offline || c.LastHeartbeat < offlineThreshold)
            .Select(c => new { c.Hostname, c.IpAddress, c.Department, c.LastHeartbeat, c.CurrentUser })
            .ToListAsync();

        if (!offlineComputers.Any())
            return new AiResponse { Text = "Todos os computadores estão online no momento." };

        var text = $"**Computadores Offline: {offlineComputers.Count}**\n\n";
        foreach (var comp in offlineComputers.Take(20))
        {
            var lastHb = comp.LastHeartbeat.ToString("dd/MM HH:mm");
            text += $"  • **{comp.Hostname}** - {comp.Department ?? "Sem departamento"} (Último heartbeat: {lastHb})\n";
        }
        if (offlineComputers.Count > 20)
            text += $"\n... e mais {offlineComputers.Count - 20} computadores offline.";

        return new AiResponse { Text = text };
    }

    private async Task<AiResponse> QueryOffHoursLogins(string query)
    {
        var since = ExtractDate(query, DateTime.UtcNow.AddDays(-7));
        var offHoursStart = 22;
        var offHoursEnd = 6;

        var offHoursLogins = await _timelineRepo.Query()
            .Where(t => t.EventType == EventType.Login
                && t.Timestamp >= since
                && (t.Timestamp.Hour >= offHoursStart || t.Timestamp.Hour < offHoursEnd))
            .OrderByDescending(t => t.Timestamp)
            .ToListAsync();

        if (!offHoursLogins.Any())
            return new AiResponse { Text = $"Nenhum login fora do horário comercial registrado desde {since:dd/MM}." };

        var users = offHoursLogins.GroupBy(t => t.Username).Select(g => new { User = g.Key, Count = g.Count() });
        var text = $"**Logins Fora do Horário ({offHoursStart}:00 - {offHoursEnd}:00)**\n\n";
        text += $"Total de eventos: {offHoursLogins.Count}\n";
        text += "**Usuários:**\n";
        foreach (var user in users.OrderByDescending(u => u.Count))
        {
            text += $"  • {user.User}: {user.Count} vezes\n";
        }
        text += "\n**Últimos 5 eventos:**\n";
        foreach (var evt in offHoursLogins.Take(5))
        {
            text += $"  • {evt.Username} - {evt.Timestamp:dd/MM HH:mm}\n";
        }

        return new AiResponse
        {
            Text = text,
            SuggestedActions = new List<AiAction>
            {
                new() { Label = "Ver todos na Timeline", Action = "/security/off-hours-logins", Icon = "shield" },
                new() { Label = "Criar Regra de Alerta", Action = "/automation/new?trigger=LoginOutOfHours", Icon = "bell" }
            }
        };
    }

    private async Task<AiResponse> QueryInactiveUsers(string query)
    {
        var threshold = TimeSpan.FromHours(2);
        var inactiveUsers = await _computerRepo.Query()
            .Where(c => c.CurrentUser != null && c.Status == ComputerStatus.Away)
            .Select(c => new { c.Hostname, c.CurrentUser, c.LastHeartbeat })
            .ToListAsync();

        var text = $"**Usuários Inativos por mais de {threshold.TotalHours} horas**\n\n";
        var inactive = inactiveUsers.Where(c => (DateTime.UtcNow - c.LastHeartbeat) > threshold).ToList();

        if (!inactive.Any())
            return new AiResponse { Text = "Nenhum usuário inativo por mais de 2 horas no momento." };

        foreach (var user in inactive)
        {
            var inactiveSince = user.LastHeartbeat.ToString("dd/MM HH:mm");
            text += $"  • **{user.CurrentUser}** em {user.Hostname} (Inativo desde {inactiveSince})\n";
        }

        return new AiResponse { Text = text };
    }

    private async Task<AiResponse> QueryTopApplications(string query)
    {
        var since = ExtractDate(query, DateTime.UtcNow.AddDays(-7));
        var rawApps = await _appUsageRepo.Query()
            .Where(a => a.StartTime >= since)
            .ToListAsync();

        var topApps = rawApps
            .GroupBy(a => a.ProcessName)
            .Select(g => new
            {
                App = g.Key,
                TotalDuration = g.Sum(a => a.Duration?.TotalSeconds ?? 0),
                ExecutionCount = g.Count(),
                Users = g.Select(a => a.Username).Distinct().Count()
            })
            .OrderByDescending(a => a.TotalDuration)
            .Take(15)
            .ToList();

        if (!topApps.Any())
            return new AiResponse { Text = $"Nenhum dado de uso de aplicativos desde {since:dd/MM}." };

        var text = $"**Top {topApps.Count} Aplicativos (desde {since:dd/MM})**\n\n";
        int rank = 1;
        foreach (var app in topApps)
        {
            var hours = TimeSpan.FromSeconds(app.TotalDuration).TotalHours;
            text += $"{rank}. **{app.App}** - {hours:F1}h, {app.ExecutionCount} execuções, {app.Users} usuários\n";
            rank++;
        }

        return new AiResponse { Text = text };
    }

    private async Task<AiResponse> QueryMostAlertedComputers(string query)
    {
        var since = ExtractDate(query, DateTime.UtcNow.AddDays(-7));
        var topAlerted = await _alertRepo.Query()
            .Where(a => a.Timestamp >= since && a.Status != AlertStatus.Resolved && a.Status != AlertStatus.FalsePositive)
            .GroupBy(a => a.ComputerId)
            .Select(g => new { ComputerId = g.Key, Count = g.Count(), MaxSeverity = g.Max(a => a.Severity) })
            .OrderByDescending(a => a.Count)
            .Take(10)
            .ToListAsync();

        if (!topAlerted.Any())
            return new AiResponse { Text = "Nenhum alerta registrado no período." };

        var computerIds = topAlerted.Select(a => a.ComputerId).ToList();
        var computers = await _computerRepo.Query()
            .Where(c => computerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Hostname);

        var text = $"**Computadores com mais alertas (desde {since:dd/MM})**\n\n";
        foreach (var item in topAlerted)
        {
            var hostname = computers.GetValueOrDefault(item.ComputerId ?? Guid.Empty, "Desconhecido");
            text += $"  • **{hostname}** - {item.Count} alertas (Severidade máxima: {item.MaxSeverity})\n";
        }

        return new AiResponse { Text = text };
    }

    private async Task<AiResponse> ExplainComputerScore(Guid computerId)
    {
        if (computerId == Guid.Empty)
            return new AiResponse { Text = "Por favor, especifique qual computador deseja analisar." };

        var computer = await _computerRepo.GetByIdAsync(computerId);
        if (computer == null)
            return new AiResponse { Text = "Computador não encontrado." };

        var weekAgo = DateTime.UtcNow.AddDays(-7);

        var alerts = await _alertRepo.Query()
            .Where(a => a.ComputerId == computerId && a.Timestamp >= weekAgo)
            .ToListAsync();

        var securityEvents = await _securityEventRepo.Query()
            .Where(s => s.ComputerId == computerId && s.Timestamp >= weekAgo)
            .ToListAsync();

        var uptime = (DateTime.UtcNow - computer.LastHeartbeat).TotalHours;

        var score = 100;
        var deductions = new List<string>();

        if (uptime > 1)
        {
            var deduction = Math.Min((int)uptime * 2, 20);
            score -= deduction;
            deductions.Add($"Fora do ar por {uptime:F1}h (-{deduction}pts)");
        }

        var criticalAlerts = alerts.Count(a => a.Severity == Severity.Critical);
        var highAlerts = alerts.Count(a => a.Severity == Severity.High);
        var mediumAlerts = alerts.Count(a => a.Severity == Severity.Medium);

        score -= criticalAlerts * 10;
        if (criticalAlerts > 0) deductions.Add($"{criticalAlerts} alertas críticos (-{criticalAlerts * 10}pts)");

        score -= highAlerts * 5;
        if (highAlerts > 0) deductions.Add($"{highAlerts} alertas altos (-{highAlerts * 5}pts)");

        score -= mediumAlerts * 2;
        if (mediumAlerts > 0) deductions.Add($"{mediumAlerts} alertas médios (-{mediumAlerts * 2}pts)");

        if (securityEvents.Any(s => s.EventType == "FirewallDisabled"))
        { score -= 15; deductions.Add("Firewall desabilitado (-15pts)"); }
        if (securityEvents.Any(s => s.EventType == "DefenderDisabled"))
        { score -= 15; deductions.Add("Defender desabilitado (-15pts)"); }
        if (securityEvents.Any(s => s.EventType == "NewLocalAdmin"))
        { score -= 10; deductions.Add("Novo administrador local (-10pts)"); }

        score = Math.Max(0, score);

        var status = score switch
        {
            >= 80 => "Excelente",
            >= 60 => "Requer Atenção",
            >= 40 => "Problemático",
            _ => "Crítico"
        };

        var text = $"**Análise do Computador: {computer.Hostname}**\n\n";
        text += $"**Score Geral: {score}/100** - {status}\n\n";
        text += $"**Detalhes:**\n";
        text += $"  • Status: {computer.Status}\n";
        text += $"  • IP: {computer.IpAddress}\n";
        text += $"  • Departamento: {computer.Department ?? "N/A"}\n";
        text += $"  • Usuário: {computer.CurrentUser ?? "N/A"}\n";
        text += $"  • Último Heartbeat: {computer.LastHeartbeat:dd/MM HH:mm}\n\n";

        if (deductions.Any())
        {
            text += "**Deduções aplicadas:**\n";
            foreach (var d in deductions) text += $"  • {d}\n";
        }
        else
        {
            text += "Nenhuma dedução aplicada. O computador está em conformidade.\n";
        }

        return new AiResponse
        {
            Text = text,
            SuggestedActions = new List<AiAction>
            {
                new() { Label = "Ver Detalhes do Computador", Action = $"/computers/{computerId}", Icon = "monitor" },
                new() { Label = "Ver Timeline", Action = $"/computers/{computerId}/timeline", Icon = "timeline" }
            }
        };
    }

    private async Task<AiResponse> GenerateSummary(string query)
    {
        var period = query switch
        {
            _ when query.Contains("24h") || query.Contains("hoje") => TimeSpan.FromHours(24),
            _ when query.Contains("7 dias") || query.Contains("semana") => TimeSpan.FromDays(7),
            _ when query.Contains("30 dias") || query.Contains("mês") => TimeSpan.FromDays(30),
            _ => TimeSpan.FromHours(24)
        };

        var since = DateTime.UtcNow - period;

        var computerCount = await _computerRepo.Query().CountAsync();
        var onlineCount = await _computerRepo.Query().CountAsync(c => c.Status == ComputerStatus.Online);
        var alertCount = await _alertRepo.Query().CountAsync(a => a.Timestamp >= since);
        var criticalCount = await _alertRepo.Query().CountAsync(a => a.Timestamp >= since && a.Severity == Severity.Critical);
        var loginCount = await _timelineRepo.Query().CountAsync(t => t.Timestamp >= since && t.EventType == EventType.Login);
        var usbCount = await _timelineRepo.Query().CountAsync(t => t.Timestamp >= since && t.EventType == EventType.USBConnected);
        var installCount = await _timelineRepo.Query().CountAsync(t => t.Timestamp >= since && t.EventType == EventType.SoftwareInstalled);

        var text = $"**Resumo das Últimas {period.TotalHours:F0}h**\n\n";
        text += $"**Computadores:** {computerCount} total, {onlineCount} online ({(computerCount > 0 ? onlineCount * 100 / computerCount : 0)}% disponibilidade)\n";
        text += $"**Alertas:** {alertCount} total, {criticalCount} críticos\n";
        text += $"**Logins:** {loginCount}\n";
        text += $"**USB Conectados:** {usbCount}\n";
        text += $"**Instalações:** {installCount}\n\n";

        if (criticalCount > 0)
        {
            text += "⚠️ **Atenção:** Existem alertas críticos que requerem ação imediata.\n";
        }

        return new AiResponse { Text = text };
    }

    private async Task<AiResponse> QueryComputerCounts()
    {
        var total = await _computerRepo.Query().CountAsync();
        var online = await _computerRepo.Query().CountAsync(c => c.Status == ComputerStatus.Online);
        var offline = await _computerRepo.Query().CountAsync(c => c.Status == ComputerStatus.Offline);
        var away = await _computerRepo.Query().CountAsync(c => c.Status == ComputerStatus.Away);
        var departments = await _computerRepo.Query().Select(c => c.Department).Distinct().CountAsync();

        return new AiResponse
        {
            Text = $"**Visão Geral dos Computadores**\n\n{total} total | {online} online | {offline} offline | {away} ausentes\n{departments} departamentos",
            Charts = new List<AiChart>
            {
                new()
                {
                    Type = "pie",
                    Title = "Status dos Computadores",
                    Data = new { labels = new[] { "Online", "Offline", "Away" }, values = new[] { online, offline, away } }
                }
            }
        };
    }

    private async Task<AiResponse> QueryLogins(string query)
    {
        var since = ExtractDate(query, DateTime.UtcNow.AddDays(-1));
        var logins = await _timelineRepo.Query()
            .Where(t => t.EventType == EventType.Login && t.Timestamp >= since)
            .GroupBy(t => t.Username)
            .Select(g => new { User = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ToListAsync();

        if (!logins.Any())
            return new AiResponse { Text = $"Nenhum login registrado desde {since:dd/MM HH:mm}." };

        var text = $"**Logins desde {since:dd/MM HH:mm}**\n\n";
        foreach (var login in logins.Take(20))
        {
            text += $"  • {login.User}: {login.Count} logins\n";
        }

        return new AiResponse { Text = text };
    }

    private async Task<AiResponse> QueryFailures(string query)
    {
        var since = ExtractDate(query, DateTime.UtcNow.AddDays(-1));
        var failures = await _timelineRepo.Query()
            .Where(t => t.Severity >= Severity.High && t.Timestamp >= since)
            .OrderByDescending(t => t.Timestamp)
            .ToListAsync();

        if (!failures.Any())
            return new AiResponse { Text = $"Nenhuma falha registrada desde {since:dd/MM HH:mm}." };

        var text = $"**Falhas e Erros desde {since:dd/MM HH:mm}**\n\n";
        foreach (var failure in failures.Take(20))
        {
            text += $"  • [{failure.Severity}] {failure.Description} - {failure.Timestamp:HH:mm}\n";
        }

        return new AiResponse { Text = text };
    }

    private async Task<AiResponse> HandleGeneralQuery(string query)
    {
        return new AiResponse
        {
            Text = $"Não entendi completamente sua pergunta. Aqui estão alguns exemplos do que posso fazer:\n\n" +
                   "• \"Quem instalou programas hoje?\"\n" +
                   "• \"Quem utilizou pendrive?\"\n" +
                   "• \"Quais computadores estão offline?\"\n" +
                   "• \"Quem fez login fora do horário?\"\n" +
                   "• \"Quais usuários ficaram mais de 2 horas inativos?\"\n" +
                   "• \"Quais programas foram mais utilizados?\"\n" +
                   "• \"Quais computadores possuem mais alertas?\"\n" +
                   "• \"Explique porque este computador recebeu nota baixa\"\n" +
                   "• \"Gere um resumo das últimas 24h\"",
            SuggestedActions = new List<AiAction>
            {
                new() { Label = "Ver Exemplos de Consultas", Icon = "book", Action = "/help/ai-queries" }
            }
        };
    }

    public async Task<AiResponse> AnalyzeComputerAsync(Guid computerId) => await ExplainComputerScore(computerId);

    public async Task<AiResponse> GenerateReportAsync(ReportType type, Dictionary<string, object> parameters)
    {
        return type switch
        {
            ReportType.DailySummary => await GenerateSummary("últimas 24h"),
            ReportType.SecurityReport => await GenerateSecurityReport(parameters),
            _ => new AiResponse { Text = "Tipo de relatório não suportado." }
        };
    }

    private async Task<AiResponse> GenerateSecurityReport(Dictionary<string, object> parameters)
    {
        var since = DateTime.UtcNow.AddDays(-7);
        var securityEvents = await _securityEventRepo.Query()
            .Where(s => s.Timestamp >= since && s.Severity >= Severity.High)
            .OrderByDescending(s => s.Timestamp)
            .ToListAsync();

        var text = $"**Relatório de Segurança (7 dias)**\n\n";
        text += $"Total de eventos de segurança: {securityEvents.Count}\n";
        text += $"Críticos: {securityEvents.Count(s => s.Severity == Severity.Critical)}\n";
        text += $"Altos: {securityEvents.Count(s => s.Severity == Severity.High)}\n\n";

        var categories = securityEvents.GroupBy(s => s.Category).Select(g => $"{g.Key}: {g.Count()}");
        text += $"**Categorias:**\n{string.Join("\n", categories)}\n\n";

        text += "**Recomendações:**\n";
        if (securityEvents.Any(s => s.EventType == "FirewallDisabled"))
            text += "  • Habilitar firewall em computadores afetados\n";
        if (securityEvents.Any(s => s.EventType == "DefenderDisabled"))
            text += "  • Reativar Microsoft Defender\n";
        if (securityEvents.Any(s => s.EventType == "NewLocalAdmin"))
            text += "  • Revisar contas de administrador locais\n";

        return new AiResponse { Text = text };
    }

    public async Task<List<DashboardInsight>> GenerateInsightsAsync()
    {
        return new List<DashboardInsight>
        {
            new DashboardInsight
            {
                Type = "info",
                Title = "System Running",
                Description = "AI insights module initialized",
                Severity = "low",
                Category = "system",
                Score = 100,
                Recommendation = "Configure data sources for AI analysis"
            }
        };
    }

    public async Task<AiResponse> ExplainAlertAsync(Guid alertId)
    {
        var alert = await _alertRepo.GetByIdAsync(alertId);
        if (alert == null)
            return new AiResponse { Text = "Alerta não encontrado." };

        var computer = await _computerRepo.GetByIdAsync(alert.ComputerId ?? Guid.Empty);
        var computerId = alert.ComputerId ?? Guid.Empty;
        var relatedEvents = await _timelineRepo.Query()
            .Where(t => t.ComputerId == computerId && t.Timestamp >= alert.Timestamp.AddMinutes(-30) && t.Timestamp <= alert.Timestamp.AddMinutes(30))
            .OrderBy(t => t.Timestamp)
            .ToListAsync();

        var text = $"**Análise do Alerta: {alert.Title}**\n\n";
        text += $"**Severidade:** {alert.Severity}\n";
        text += $"**Categoria:** {alert.Category}\n";
        text += $"**Computador:** {computer?.Hostname ?? "Desconhecido"}\n";
        text += $"**Usuário:** {computer?.CurrentUser ?? "N/A"}\n";
        text += $"**Data:** {alert.Timestamp:dd/MM/yyyy HH:mm:ss}\n\n";
        text += $"**Descrição:** {alert.Description}\n\n";

        if (relatedEvents.Any())
        {
            text += "**Eventos Relacionados (30min antes/depois):**\n";
            foreach (var evt in relatedEvents.Take(15))
            {
                text += $"  • {evt.Timestamp:HH:mm} - {evt.Description}\n";
            }
        }

        return new AiResponse
        {
            Text = text,
            SuggestedActions = new List<AiAction>
            {
                new() { Label = "Resolver Alerta", Action = $"/alerts/{alertId}/resolve", Icon = "check" },
                new() { Label = "Ver Computador", Action = $"/computers/{alert.ComputerId}", Icon = "monitor" }
            }
        };
    }

    public async Task<AiResponse> SuggestActionsAsync(Guid computerId, string? issue)
    {
        var computer = await _computerRepo.GetByIdAsync(computerId);
        if (computer == null) return new AiResponse { Text = "Computador não encontrado." };

        var alerts = await _alertRepo.Query()
            .Where(a => a.ComputerId == computerId && a.Status == AlertStatus.Open)
            .ToListAsync();

        var text = $"**Ações Sugeridas para {computer.Hostname}**\n\n";
        var actions = new List<AiAction>();

        if (alerts.Any(a => a.Severity == Severity.Critical))
        {
            text += "⚠️ Alertas críticos detectados. Recomenda-se ação imediata.\n\n";
            actions.Add(new AiAction { Label = "Revisar Alertas Críticos", Action = $"/alerts?computerId={computerId}&severity=Critical", Icon = "alert-triangle" });
        }

        if (computer.Status == ComputerStatus.Offline)
        {
            text += "• Verificar conectividade de rede\n";
            text += "• Confirmar se o agente está em execução\n";
            text += "• Verificar se o Windows Service 'SentinelaAgent' está rodando\n\n";
            actions.Add(new AiAction { Label = "Enviar Comando de Reinicialização", Action = $"api/v1/computers/{computerId}/command", Icon = "refresh-cw" });
        }

        if (alerts.Any(a => a.Category == "Security"))
        {
            text += "• Executar varredura de segurança\n";
            text += "• Revisar eventos de segurança recentes\n\n";
            actions.Add(new AiAction { Label = "Ver Eventos de Segurança", Action = $"/security?computerId={computerId}", Icon = "shield" });
        }

        if (!actions.Any())
        {
            text += "Nenhuma ação específica necessária no momento. O computador parece estar em conformidade.";
        }

        return new AiResponse { Text = text, SuggestedActions = actions };
    }

    public async Task<AiResponse> SummarizeEventsAsync(DateTimeOffset from, DateTimeOffset to, string? computerId)
    {
        throw new NotImplementedException();
    }

    public async Task<AiResponse> PrioritizeIncidentsAsync()
    {
        var criticalOpen = await _alertRepo.Query()
            .Where(a => a.Status == AlertStatus.Open && a.Severity == Severity.Critical)
            .OrderBy(a => a.Timestamp)
            .ToListAsync();

        if (!criticalOpen.Any())
            return new AiResponse { Text = "Não há incidentes críticos abertos no momento." };

        var text = $"**Priorização de Incidentes ({criticalOpen.Count} críticos abertos)**\n\n";
        int priority = 1;
        foreach (var alert in criticalOpen.Take(10))
        {
            var computer = await _computerRepo.GetByIdAsync(alert.ComputerId ?? Guid.Empty);
            text += $"{priority}. **{alert.Title}** - {computer?.Hostname ?? "N/A"}\n";
            text += $"   {alert.Description?[..Math.Min(100, alert.Description?.Length ?? 0)]}\n";
            text += $"   Aberto: {alert.Timestamp:dd/MM HH:mm}\n\n";
            priority++;
        }

        return new AiResponse
        {
            Text = text,
            SuggestedActions = criticalOpen.Take(5).Select(a => new AiAction
            {
                Label = $"Resolver: {a.Title?[..Math.Min(30, a.Title?.Length ?? 0)]}...",
                Action = $"/alerts/{a.Id}",
                Icon = "check-circle"
            }).ToList()
        };
    }

    private static DateTime ExtractDate(string query, DateTime defaultDate)
    {
        if (query.Contains("hoje")) return DateTime.UtcNow.Date;
        if (query.Contains("24h") || query.Contains("últimas 24")) return DateTime.UtcNow.AddHours(-24);
        if (query.Contains("7 dias") || query.Contains("semana")) return DateTime.UtcNow.AddDays(-7);
        if (query.Contains("30 dias") || query.Contains("mês")) return DateTime.UtcNow.AddDays(-30);
        return defaultDate;
    }

    private static Guid ExtractGuid(string input)
    {
        var match = Regex.Match(input, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
        return match.Success ? Guid.Parse(match.Value) : Guid.Empty;
    }
}

public class AiOptions
{
    public bool Enabled { get; set; } = true;
    public string Provider { get; set; } = "BuiltIn";
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }
    public string? Model { get; set; }
    public int MaxTokens { get; set; } = 2048;
    public double Temperature { get; set; } = 0.7;
}
