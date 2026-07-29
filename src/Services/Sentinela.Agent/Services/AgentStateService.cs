using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;

namespace Sentinela.Agent.Services;

public interface IAgentStateService
{
    string ComputerId { get; }
    string CurrentUser { get; set; }
    DateTime StartTime { get; }
    DateTime LastHeartbeat { get; set; }
    DateTime LastSuccessfulCommunication { get; set; }
    DateTime LastSyncTimestamp { get; set; }
    string ConnectionStatus { get; set; }
    string AgentVersion { get; }
    int OfflineQueueSize { get; set; }
    Dictionary<string, object> Metadata { get; }
    
    T? Get<T>(string key);
    void Set<T>(string key, T value);
    bool TryGet<T>(string key, out T? value);
}

public class AgentStateService : IAgentStateService
{
    private readonly ConcurrentDictionary<string, object> _state = new();
    private readonly object _lock = new();
    private readonly string _computerId;
    private string _currentUser = "";
    private string _connectionStatus = "Disconnected";
    private DateTime _lastHeartbeat;
    private DateTime _lastSuccessfulCommunication;
    private DateTime _lastSyncTimestamp;
    private int _offlineQueueSize;

    private static string GenerateStableComputerId()
    {
        var hostname = Environment.MachineName;
        var mac = NetworkInterface
            .GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                        n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Select(n => n.GetPhysicalAddress().ToString())
            .FirstOrDefault(m => !string.IsNullOrEmpty(m)) ?? "unknown";
        var input = $"{hostname}-{mac}";
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return new Guid(hash).ToString();
    }

    public AgentStateService()
    {
        _computerId = GenerateStableComputerId();
    }
    
    public string ComputerId => _computerId;
    
    public string CurrentUser
    {
        get => _currentUser;
        set { lock (_lock) _currentUser = value; }
    }
    
    public DateTime StartTime { get; } = DateTime.UtcNow;
    
    public DateTime LastHeartbeat
    {
        get => _lastHeartbeat;
        set { lock (_lock) _lastHeartbeat = value; }
    }
    
    public DateTime LastSuccessfulCommunication
    {
        get => _lastSuccessfulCommunication;
        set { lock (_lock) _lastSuccessfulCommunication = value; }
    }
    
    public DateTime LastSyncTimestamp
    {
        get => _lastSyncTimestamp;
        set { lock (_lock) _lastSyncTimestamp = value; }
    }
    
    public string ConnectionStatus
    {
        get => _connectionStatus;
        set { lock (_lock) _connectionStatus = value; }
    }
    
    public string AgentVersion => GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0";
    
    public int OfflineQueueSize
    {
        get => _offlineQueueSize;
        set { lock (_lock) _offlineQueueSize = value; }
    }
    
    public Dictionary<string, object> Metadata => new(_state);
    
    public T? Get<T>(string key)
    {
        if (_state.TryGetValue(key, out var value) && value is T typed)
            return typed;
        return default;
    }
    
    public void Set<T>(string key, T value)
    {
        _state[key] = value!;
    }
    
    public bool TryGet<T>(string key, out T? value)
    {
        if (_state.TryGetValue(key, out var obj) && obj is T typed)
        {
            value = typed;
            return true;
        }
        value = default;
        return false;
    }
}
