using Microsoft.Win32;

namespace Sentinela.Agent.Core.Collectors;

public interface ISoftwareCollector
{
    List<SoftwareInfo> GetInstalledSoftware();
    void CheckForChanges();
    event EventHandler<SoftwareChangeEventArgs>? SoftwareInstalled;
    event EventHandler<SoftwareChangeEventArgs>? SoftwareUninstalled;
}

public class SoftwareCollector : ISoftwareCollector, IDisposable
{
    private readonly HashSet<string> _knownSoftware = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private bool _initialized;

    public event EventHandler<SoftwareChangeEventArgs>? SoftwareInstalled;
    public event EventHandler<SoftwareChangeEventArgs>? SoftwareUninstalled;

    public SoftwareCollector()
    {
        PopulateKnownSoftware();
        _initialized = true;
    }

    private void PopulateKnownSoftware()
    {
        lock (_lock)
        {
            _knownSoftware.Clear();
            foreach (var sw in GetInstalledSoftware())
            {
                if (!string.IsNullOrWhiteSpace(sw.DisplayName))
                    _knownSoftware.Add(sw.DisplayName);
            }
        }
    }

    public void CheckForChanges()
    {
        if (!_initialized) return;

        List<SoftwareInfo> current;
        try
        {
            current = GetInstalledSoftware();
        }
        catch
        {
            return;
        }

        var currentNames = new HashSet<string>(
            current.Where(s => !string.IsNullOrWhiteSpace(s.DisplayName)).Select(s => s.DisplayName),
            StringComparer.OrdinalIgnoreCase);

        lock (_lock)
        {
            foreach (var sw in current)
            {
                if (string.IsNullOrWhiteSpace(sw.DisplayName)) continue;
                if (_knownSoftware.Contains(sw.DisplayName)) continue;

                SoftwareInstalled?.Invoke(this, new SoftwareChangeEventArgs(SoftwareChangeType.Installed, sw));
                _knownSoftware.Add(sw.DisplayName);
            }

            var removed = _knownSoftware.Except(currentNames, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var name in removed)
            {
                SoftwareUninstalled?.Invoke(this, new SoftwareChangeEventArgs(
                    SoftwareChangeType.Uninstalled,
                    new SoftwareInfo { DisplayName = name }));
                _knownSoftware.Remove(name);
            }
        }
    }

    public List<SoftwareInfo> GetInstalledSoftware()
    {
        var software = new List<SoftwareInfo>();

        software.AddRange(GetSoftwareFromRegistry(RegistryView.Registry32));
        software.AddRange(GetSoftwareFromRegistry(RegistryView.Registry64));

        // Per-user installs
        try
        {
            using var userKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (userKey != null)
                software.AddRange(ReadUninstallKey(userKey));
        }
        catch { }

        var unique = new Dictionary<string, SoftwareInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var sw in software)
        {
            if (!string.IsNullOrEmpty(sw.DisplayName) && !unique.ContainsKey(sw.DisplayName))
                unique[sw.DisplayName] = sw;
        }

        return unique.Values.ToList();
    }

    private List<SoftwareInfo> GetSoftwareFromRegistry(RegistryView view)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var uninstallKey = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstallKey == null) return new List<SoftwareInfo>();
            return ReadUninstallKey(uninstallKey);
        }
        catch
        {
            return new List<SoftwareInfo>();
        }
    }

    private List<SoftwareInfo> ReadUninstallKey(RegistryKey uninstallKey)
    {
        var list = new List<SoftwareInfo>();

        foreach (var subKeyName in uninstallKey.GetSubKeyNames())
        {
            try
            {
                using var subKey = uninstallKey.OpenSubKey(subKeyName);
                if (subKey == null) continue;

                var displayName = subKey.GetValue("DisplayName")?.ToString();
                if (string.IsNullOrEmpty(displayName)) continue;

                var installDateStr = subKey.GetValue("InstallDate")?.ToString() ?? "";
                DateTime? installDate = null;
                if (DateTime.TryParseExact(installDateStr, "yyyyMMdd", null,
                        System.Globalization.DateTimeStyles.None, out var parsed))
                {
                    installDate = parsed;
                }

                list.Add(new SoftwareInfo
                {
                    DisplayName = displayName,
                    Version = subKey.GetValue("DisplayVersion")?.ToString() ?? "",
                    Publisher = subKey.GetValue("Publisher")?.ToString() ?? "",
                    InstallDate = installDate,
                    InstallLocation = subKey.GetValue("InstallLocation")?.ToString() ?? "",
                    UninstallString = subKey.GetValue("UninstallString")?.ToString() ?? "",
                    IsSystemComponent = subKey.GetValue("SystemComponent") is int comp && comp == 1
                });
            }
            catch { }
        }

        return list;
    }

    public void Dispose() { }
}

public class SoftwareInfo
{
    public string DisplayName { get; set; } = "";
    public string Version { get; set; } = "";
    public string Publisher { get; set; } = "";
    public DateTime? InstallDate { get; set; }
    public string InstallLocation { get; set; } = "";
    public string UninstallString { get; set; } = "";
    public bool IsSystemComponent { get; set; }
}

public enum SoftwareChangeType
{
    Installed,
    Uninstalled
}

public class SoftwareChangeEventArgs : EventArgs
{
    public SoftwareChangeType ChangeType { get; }
    public SoftwareInfo Software { get; }

    public SoftwareChangeEventArgs(SoftwareChangeType changeType, SoftwareInfo software)
    {
        ChangeType = changeType;
        Software = software;
    }
}
