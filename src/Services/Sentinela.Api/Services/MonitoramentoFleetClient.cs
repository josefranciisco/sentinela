using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sentinela.Shared.Core.Interfaces;
using Sentinela.Shared.Domain.Monitoring;
using Sentinela.Shared.Domain.Monitoring.Enums;

namespace Sentinela.Api.Services;

public class MonitoramentoOptions
{
    public string BaseUrl { get; set; } = "http://192.168.0.116:8000";
}

public class MonitoramentoFleetClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly HttpClient _http;
    private readonly IRepository<Computer> _computers;
    private readonly ITenantAccessor _tenant;
    private readonly ILogger<MonitoramentoFleetClient> _logger;

    public MonitoramentoFleetClient(
        HttpClient http,
        IRepository<Computer> computers,
        ITenantAccessor tenant,
        ILogger<MonitoramentoFleetClient> logger)
    {
        _http = http;
        _computers = computers;
        _tenant = tenant;
        _logger = logger;
    }

    public async Task<List<MonitoramentoMachineDto>> GetMachinesAsync(CancellationToken ct = default)
    {
        var raw = await GetAsync<List<MobiMachine>>("/machines", ct) ?? [];
        return await MergeAsync(raw, ct);
    }

    public async Task<MonitoramentoMachineDto?> GetMachineAsync(string hostname, CancellationToken ct = default)
    {
        var encoded = Uri.EscapeDataString(hostname);
        var raw = await GetAsync<MobiMachine>($"/machines/{encoded}", ct);
        if (raw is null) return null;
        var merged = await MergeAsync([raw], ct);
        return merged.FirstOrDefault();
    }

    public async Task<List<JsonElement>> GetInventoryAsync(CancellationToken ct = default)
    {
        return await GetAsync<List<JsonElement>>("/inventory", ct) ?? [];
    }

    private async Task<List<MonitoramentoMachineDto>> MergeAsync(List<MobiMachine> raw, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var known = await _computers.Query()
            .Where(c => !c.IsDeleted && c.TenantId == tenantId)
            .Select(c => new { c.Id, c.Hostname, c.Status })
            .ToListAsync(ct);

        var byHost = known
            .Where(c => !string.IsNullOrWhiteSpace(c.Hostname))
            .GroupBy(c => c.Hostname.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        return raw.Select(m =>
        {
            var host = (m.Hostname ?? "").Trim();
            byHost.TryGetValue(host.ToLowerInvariant(), out var match);
            var agentStatus = match is null
                ? "none"
                : match.Status == ComputerStatus.Online ? "online" : "offline";
            return new MonitoramentoMachineDto
            {
                Hostname = host,
                Alias = m.Alias,
                Status = m.Status ?? "unknown",
                LastSeen = m.LastSeen,
                HealthScore = m.HealthScore,
                Metrics = m.Metrics,
                Inventory = m.Inventory,
                SentinelaComputerId = match?.Id,
                AgentStatus = agentStatus
            };
        }).OrderBy(m => m.Hostname, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken ct)
    {
        using var response = await _http.GetAsync(path, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return default;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
    }

    private sealed class MobiMachine
    {
        public string Hostname { get; set; } = "";
        public string? Alias { get; set; }
        public string? Status { get; set; }
        public double LastSeen { get; set; }
        public double HealthScore { get; set; } = 100;
        public MobiMetrics? Metrics { get; set; }
        public JsonElement? Inventory { get; set; }
    }
}

public class MonitoramentoMachineDto
{
    public string Hostname { get; set; } = "";
    public string? Alias { get; set; }
    public string Status { get; set; } = "unknown";
    public double LastSeen { get; set; }
    public double HealthScore { get; set; }
    public MobiMetrics? Metrics { get; set; }
    public JsonElement? Inventory { get; set; }
    public Guid? SentinelaComputerId { get; set; }
    public string AgentStatus { get; set; } = "none";
}

public class MobiMetrics
{
    public double CpuPercent { get; set; }
    public double RamUsedGb { get; set; }
    public double RamTotalGb { get; set; }
    public double RamPercent { get; set; }
    public double DiskUsedGb { get; set; }
    public double DiskTotalGb { get; set; }
    public double DiskFreeGb { get; set; }
    public double DiskPercent { get; set; }
    public string? Uptime { get; set; }
    public string? Ip { get; set; }
    public string? User { get; set; }
    public string? TopProcess { get; set; }
    public double TopProcessCpu { get; set; }
}
