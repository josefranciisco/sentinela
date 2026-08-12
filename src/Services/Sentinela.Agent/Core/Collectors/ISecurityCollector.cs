using System.Management;
using Microsoft.Win32;

namespace Sentinela.Agent.Core.Collectors;

public interface ISecurityCollector
{
    Task<SecurityStatus> CollectSecurityStatusAsync();
    Task<List<SecurityEvent>> CheckSecurityChangesAsync();
    Task<List<MalwareThreatInfo>> GetActiveThreatsAsync();
}

public class SecurityCollector : ISecurityCollector
{
    private readonly HashSet<string> _knownThreatIds = new(StringComparer.OrdinalIgnoreCase);

    public async Task<SecurityStatus> CollectSecurityStatusAsync()
    {
        var defender = CollectDefenderStatus();
        var thirdParty = CollectSecurityCenterProducts();

        return new SecurityStatus
        {
            FirewallEnabled = CheckFirewall(),
            DefenderEnabled = defender.RealTimeProtectionEnabled,
            AntivirusEnabled = defender.AntivirusEnabled || thirdParty.Any(p => p.IsEnabled),
            RealTimeProtectionEnabled = defender.RealTimeProtectionEnabled || thirdParty.Any(p => p.IsEnabled),
            AntivirusSignatureAgeDays = thirdParty.Any(p => p.IsEnabled && p.IsUpToDate) ? 0 : defender.AntivirusSignatureAgeDays,
            AntivirusSignatureLastUpdated = thirdParty.Any(p => p.IsEnabled && p.IsUpToDate) ? DateTime.UtcNow : defender.AntivirusSignatureLastUpdated,
            AntivirusProductName = SimplifyProductName(
                    thirdParty.FirstOrDefault(p => p.IsEnabled)?.DisplayName
                    ?? thirdParty.FirstOrDefault()?.DisplayName)
                ?? defender.ProductName
                ?? "",
            ThirdPartyProducts = thirdParty,
            BitlockerEnabled = await CheckBitlockerAsync(),
            RdpEnabled = CheckRdp(),
            DnsServers = GetDnsServers(),
            DefaultGateway = GetDefaultGateway(),
            Hostname = Environment.MachineName,
            ClockSkew = GetClockSkew(),
            LocalAdmins = GetLocalAdministrators(),
            RunningServices = GetRunningServices(),
            Timestamp = DateTime.UtcNow
        };
    }

    public Task<List<MalwareThreatInfo>> GetActiveThreatsAsync()
    {
        var threats = new List<MalwareThreatInfo>();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\Microsoft\Windows\Defender",
                "SELECT * FROM MSFT_MpThreat");

            foreach (var obj in searcher.Get())
            {
                var threatId = obj["ThreatID"]?.ToString() ?? Guid.NewGuid().ToString();
                var status = Convert.ToInt32(obj["ThreatStatus"] ?? 0);
                // Status 0/1/2 typically active/quarantined variants — report non-cleaned
                var severity = Convert.ToInt32(obj["SeverityID"] ?? 0);
                var name = obj["ThreatName"]?.ToString() ?? "Unknown threat";
                var category = obj["CategoryID"]?.ToString() ?? "";

                var info = new MalwareThreatInfo
                {
                    ThreatId = threatId,
                    ThreatName = name,
                    SeverityId = severity,
                    CategoryId = category,
                    Status = status,
                    Resources = obj["Resources"] as string[] ?? Array.Empty<string>(),
                    DetectionTime = DateTime.UtcNow,
                    IsNew = !_knownThreatIds.Contains(threatId)
                };

                threats.Add(info);
                _knownThreatIds.Add(threatId);
            }
        }
        catch
        {
            // Fallback: MSFT_MpThreatDetection if available
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    @"root\Microsoft\Windows\Defender",
                    "SELECT * FROM MSFT_MpThreatDetection");

                foreach (var obj in searcher.Get())
                {
                    var threatId = obj["ThreatID"]?.ToString()
                                   ?? obj["DetectionID"]?.ToString()
                                   ?? Guid.NewGuid().ToString();
                    var name = obj["ThreatName"]?.ToString() ?? "Detected threat";
                    var info = new MalwareThreatInfo
                    {
                        ThreatId = threatId,
                        ThreatName = name,
                        SeverityId = Convert.ToInt32(obj["SeverityID"] ?? 3),
                        Status = Convert.ToInt32(obj["ThreatStatus"] ?? 1),
                        Resources = new[] { obj["Resources"]?.ToString() ?? "" },
                        DetectionTime = DateTime.UtcNow,
                        IsNew = !_knownThreatIds.Contains(threatId)
                    };
                    threats.Add(info);
                    _knownThreatIds.Add(threatId);
                }
            }
            catch { }
        }

        return Task.FromResult(threats);
    }

    public Task<List<SecurityEvent>> CheckSecurityChangesAsync()
    {
        var events = new List<SecurityEvent>();

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NTLogEvent WHERE LogFile = 'Security' AND TimeGenerated >= '"
                + DateTime.UtcNow.AddMinutes(-5).ToString("yyyyMMddHHmmss") + "'");

            foreach (var obj in searcher.Get())
            {
                var eventId = obj["EventIdentifier"]?.ToString() ?? "";
                var eventCode = Convert.ToInt32(eventId);

                if (IsMonitoredEvent(eventCode))
                {
                    events.Add(new SecurityEvent
                    {
                        EventId = eventCode,
                        EventType = GetEventTypeName(eventCode),
                        Username = obj["InsertionStrings"] as string[] != null ? ((string[])obj["InsertionStrings"]).ElementAtOrDefault(1) ?? "" : "",
                        SourceIp = obj["InsertionStrings"] as string[] != null ? ((string[])obj["InsertionStrings"]).ElementAtOrDefault(8) ?? "" : "",
                        Description = obj["Message"]?.ToString() ?? "",
                        Timestamp = obj["TimeGenerated"] is DateTime dt ? dt : DateTime.UtcNow,
                        Severity = GetSeverity(eventCode)
                    });
                }
            }
        }
        catch { }

        return Task.FromResult(events);
    }

    private DefenderStatus CollectDefenderStatus()
    {
        var status = new DefenderStatus();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\Microsoft\Windows\Defender",
                "SELECT * FROM MSFT_MpComputerStatus");

            foreach (var result in searcher.Get())
            {
                status.RealTimeProtectionEnabled = (bool?)result["RealTimeProtectionEnabled"] == true;
                status.AntivirusEnabled = (bool?)result["AntivirusEnabled"] == true;
                status.ProductName = "Microsoft Defender";

                if (result["AntivirusSignatureAge"] != null)
                    status.AntivirusSignatureAgeDays = Convert.ToInt32(result["AntivirusSignatureAge"]);

                if (result["AntivirusSignatureLastUpdated"] is DateTime lastUpdated)
                    status.AntivirusSignatureLastUpdated = DateTime.SpecifyKind(lastUpdated, DateTimeKind.Utc);
                else if (result["AntivirusSignatureLastUpdated"] != null
                         && DateTime.TryParse(result["AntivirusSignatureLastUpdated"].ToString(), out var parsed))
                    status.AntivirusSignatureLastUpdated = parsed.ToUniversalTime();

                break;
            }
        }
        catch { }

        return status;
    }

    private List<AntivirusProductInfo> CollectSecurityCenterProducts()
    {
        var products = new List<AntivirusProductInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\SecurityCenter2",
                "SELECT * FROM AntiVirusProduct");

            foreach (var obj in searcher.Get())
            {
                var productState = Convert.ToUInt32(obj["productState"] ?? 0);
                // productState bits: enabled if (state & 0x1000) != 0 typically; outdated if (state & 0x10) != 0
                var enabled = ((productState >> 12) & 0xF) == 1;
                var upToDate = ((productState >> 4) & 0xF) == 0;

                products.Add(new AntivirusProductInfo
                {
                    DisplayName = obj["displayName"]?.ToString() ?? "Antivirus",
                    InstanceGuid = obj["instanceGuid"]?.ToString() ?? "",
                    ProductState = productState,
                    IsEnabled = enabled,
                    IsUpToDate = upToDate
                });
            }
        }
        catch { }

        return products;
    }

    private bool IsMonitoredEvent(int eventId) => eventId switch
    {
        4624 or 4625 or 4634 or 4647 or 4648 or 4672 or 4720 or 4722 or 4723 or 4724 or 4725 or 4726
            or 4732 or 4733 or 4740 or 4741 or 4742 or 4743 or 4756 or 4776 or 4778 or 4779
            or 4781 or 4798 or 4799 or 4800 or 4801 or 4802 or 4803 or 4825 => true,
        _ => false
    };

    private string GetEventTypeName(int eventId) => eventId switch
    {
        4624 => "Logon",
        4625 => "FailedLogon",
        4634 => "Logoff",
        4647 => "InitiatingLogoff",
        4800 => "WorkstationLock",
        4801 => "WorkstationUnlock",
        4802 => "ScreenSaverInvoked",
        4803 => "ScreenSaverDismissed",
        4720 => "UserCreated",
        4722 => "UserEnabled",
        4723 => "PasswordChange",
        4724 => "PasswordReset",
        4725 => "UserDisabled",
        4726 => "UserDeleted",
        4672 => "AdminLogon",
        4776 => "CredentialValidation",
        _ => $"Event{eventId}"
    };

    private Sentinela.Shared.Domain.Monitoring.Enums.Severity GetSeverity(int eventId) => eventId switch
    {
        4625 or 4725 or 4726 or 4740 => Sentinela.Shared.Domain.Monitoring.Enums.Severity.High,
        4720 or 4722 or 4723 or 4724 => Sentinela.Shared.Domain.Monitoring.Enums.Severity.Medium,
        _ => Sentinela.Shared.Domain.Monitoring.Enums.Severity.Info
    };

    private bool CheckFirewall()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy");
            if (key == null) return false;

            foreach (var profile in new[] { "DomainProfile", "PrivateProfile", "StandardProfile" })
            {
                using var profileKey = key.OpenSubKey(profile);
                if (profileKey?.GetValue("EnableFirewall") is int enabled && enabled == 1)
                    return true;
            }
            return false;
        }
        catch { return false; }
    }

    private async Task<bool> CheckBitlockerAsync()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\CIMV2\Security\MicrosoftVolumeEncryption",
                "SELECT * FROM Win32_EncryptableVolume WHERE DriveLetter = 'C:'");
            foreach (var result in searcher.Get())
            {
                var protectionStatus = (uint?)result["ProtectionStatus"];
                return protectionStatus == 1;
            }
        }
        catch { }
        await Task.CompletedTask;
        return false;
    }

    private bool CheckRdp()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Terminal Server");
            var value = key?.GetValue("fDenyTSConnections");
            return value is int intVal && intVal == 0;
        }
        catch { return false; }
    }

    private string[] GetDnsServers()
    {
        var servers = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = True");
            foreach (var obj in searcher.Get())
            {
                var dns = obj["DNSServerSearchOrder"] as string[];
                if (dns != null) servers.AddRange(dns);
            }
        }
        catch { }
        return servers.ToArray();
    }

    private string GetDefaultGateway()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_IP4RouteTable WHERE Destination = '0.0.0.0'");
            foreach (var obj in searcher.Get())
                return obj["NextHop"]?.ToString() ?? "";
        }
        catch { }
        return "";
    }

    private long GetClockSkew()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
            foreach (var obj in searcher.Get())
            {
                var currentTime = DateTime.UtcNow;
                var localTime = obj["LocalDateTime"] is DateTime dt ? dt : currentTime;
                return (long)(currentTime - localTime.ToUniversalTime()).TotalSeconds;
            }
        }
        catch { }
        return 0;
    }

    private List<string> GetLocalAdministrators()
    {
        var admins = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_GroupUser WHERE GroupComponent = \"Win32_Group.Domain='"
                + Environment.UserDomainName + "',Name='Administrators'\"");

            foreach (var obj in searcher.Get())
            {
                var part = obj["PartComponent"]?.ToString() ?? "";
                foreach (var p in part.Split(','))
                {
                    if (p.StartsWith("Name="))
                        admins.Add(p.Replace("Name=", "").Replace("\"", ""));
                }
            }
        }
        catch { }
        return admins;
    }

    private List<ServiceInfo> GetRunningServices()
    {
        var services = new List<ServiceInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Service WHERE State = 'Running'");
            foreach (var obj in searcher.Get())
            {
                services.Add(new ServiceInfo
                {
                    Name = obj["Name"]?.ToString() ?? "",
                    DisplayName = obj["DisplayName"]?.ToString() ?? "",
                    PathName = obj["PathName"]?.ToString() ?? "",
                    StartMode = obj["StartMode"]?.ToString() ?? "",
                    StartName = obj["StartName"]?.ToString() ?? ""
                });
            }
        }
        catch { }
        return services;
    }

    private static string? SimplifyProductName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (name.Contains("Bitdefender", StringComparison.OrdinalIgnoreCase)) return "Bitdefender";
        return name;
    }

    private class DefenderStatus
    {
        public bool RealTimeProtectionEnabled { get; set; }
        public bool AntivirusEnabled { get; set; }
        public int AntivirusSignatureAgeDays { get; set; }
        public DateTime? AntivirusSignatureLastUpdated { get; set; }
        public string? ProductName { get; set; }
    }
}

public class SecurityStatus
{
    public bool FirewallEnabled { get; set; }
    public bool DefenderEnabled { get; set; }
    public bool AntivirusEnabled { get; set; }
    public bool RealTimeProtectionEnabled { get; set; }
    public int AntivirusSignatureAgeDays { get; set; }
    public DateTime? AntivirusSignatureLastUpdated { get; set; }
    public string AntivirusProductName { get; set; } = "";
    public List<AntivirusProductInfo> ThirdPartyProducts { get; set; } = new();
    public bool BitlockerEnabled { get; set; }
    public bool RdpEnabled { get; set; }
    public string[] DnsServers { get; set; } = Array.Empty<string>();
    public string DefaultGateway { get; set; } = "";
    public string Hostname { get; set; } = "";
    public long ClockSkew { get; set; }
    public List<string> LocalAdmins { get; set; } = new();
    public List<ServiceInfo> RunningServices { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

public class AntivirusProductInfo
{
    public string DisplayName { get; set; } = "";
    public string InstanceGuid { get; set; } = "";
    public uint ProductState { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsUpToDate { get; set; }
}

public class MalwareThreatInfo
{
    public string ThreatId { get; set; } = "";
    public string ThreatName { get; set; } = "";
    public int SeverityId { get; set; }
    public string CategoryId { get; set; } = "";
    public int Status { get; set; }
    public string[] Resources { get; set; } = Array.Empty<string>();
    public DateTime DetectionTime { get; set; }
    public bool IsNew { get; set; }
}

public class SecurityEvent
{
    public int EventId { get; set; }
    public string EventType { get; set; } = "";
    public string Username { get; set; } = "";
    public string SourceIp { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public Sentinela.Shared.Domain.Monitoring.Enums.Severity Severity { get; set; }
}

public class ServiceInfo
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PathName { get; set; } = "";
    public string StartMode { get; set; } = "";
    public string StartName { get; set; } = "";
}
