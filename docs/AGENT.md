# Sentinela Windows Agent

## Overview

The Sentinela Windows Agent is a lightweight .NET-based background service that runs on each monitored Windows endpoint. It collects telemetry data, enforces security policies, executes remote commands, and maintains real-time communication with the Sentinela server via SignalR.

## Architecture

```
┌──────────────────────────────────────────────────────┐
│                Sentinela Agent Service                │
│                                                      │
│  ┌────────────────────────────────────────────────┐  │
│  │                   Core Services                │  │
│  │  ┌──────────┐ ┌──────────┐ ┌────────────────┐  │  │
│  │  │ Collector│ │  Health  │ │ Command        │  │  │
│  │  │ Manager  │ │ Watchdog │ │ Dispatcher     │  │  │
│  │  └────┬─────┘ └────┬─────┘ └───────┬────────┘  │  │
│  │       │             │               │           │  │
│  │  ┌────┴─────┐ ┌────┴─────┐ ┌───────┴────────┐  │  │
│  │  │ Collectors│ │ Monitors│ │ Event Bus      │  │  │
│  │  │          │ │         │ │ (In-process)    │  │  │
│  │  └──────────┘ └─────────┘ └────────────────┘  │  │
│  └────────────────────────────────────────────────┘  │
│                                                      │
│  ┌────────────────────────────────────────────────┐  │
│  │           Communication Layer                  │  │
│  │  ┌──────────────┐ ┌────────────────────────┐  │  │
│  │  │ SignalR      │ │ HTTP Fallback          │  │  │
│  │  │ Client       │ │ (When WS unavailable)  │  │  │
│  │  └──────┬───────┘ └───────────┬────────────┘  │  │
│  │         │                     │               │  │
│  │  ┌──────┴─────────────────────┴────────────┐  │  │
│  │  │           Connection Manager            │  │  │
│  │  │  (Reconnect, Backoff, Token Refresh)    │  │  │
│  │  └─────────────────────────────────────────┘  │  │
│  └────────────────────────────────────────────────┘  │
│                                                      │
│  ┌────────────────────────────────────────────────┐  │
│  │            Storage Layer                       │  │
│  │  ┌──────────────┐ ┌────────────────────────┐  │  │
│  │  │  SQLite      │ │  File-based Cache      │  │  │
│  │  │  (Offline)   │ │  (Config, Policies)    │  │  │
│  │  └──────────────┘ └────────────────────────┘  │  │
│  └────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────┘
```

## Installation

### Prerequisites
- Windows 10 / 11 or Windows Server 2019 / 2022 / 2025
- .NET Runtime 9.0 (included in installer, if not present)
- Network access to the Sentinela server (port 5001 for SignalR, or configured port)
- Minimum 150 MB disk space, 256 MB RAM

### Silent Installation

```
SentinelaAgent-Installer.exe /S /ServerUrl=https://sentinela.yourcompany.com:5001 /TenantId=your-tenant-id
```

### Installation Parameters

| Parameter       | Required | Description                                |
|----------------|----------|--------------------------------------------|
| `/S`           | Yes      | Silent installation                        |
| `/ServerUrl`   | Yes      | Sentinela server WebSocket endpoint        |
| `/TenantId`    | Yes      | Organization tenant identifier             |
| `/AuthToken`   | No       | Pre-provisioned authentication token       |
| `/ProxyUrl`    | No       | HTTP proxy URL if required                 |
| `/InstallDir`  | No       | Custom installation directory              |
| `/LogLevel`    | No       | Logging level (default: Info)              |

### Interactive Installation

Run the installer without parameters to launch the GUI installer:

```
SentinelaAgent-Installer.exe
```

### Manual Installation

1. Extract the agent package to `C:\Program Files\Sentinela\Agent\`
2. Open PowerShell as Administrator:

```powershell
& "C:\Program Files\Sentinela\Agent\Sentinela.Agent.exe" install --server-url "https://sentinela.yourcompany.com:5001" --tenant-id "your-tenant-id"
```

3. Verify the service is running:

```powershell
Get-Service -Name "SentinelaAgent"
```

### GPO Deployment (Active Directory)

1. Create a network share with the MSI installer
2. Create a GPO: Computer Configuration → Software Settings → Software Installation → Assigned
3. The agent installs silently on next Group Policy refresh
4. Configure agent settings via Group Policy Administrative Templates (import the .admx template provided in the package)

---

## Configuration

### Configuration File

The agent configuration is stored at:

```
C:\ProgramData\Sentinela\Agent\agent.config.json
```

```json
{
  "server": {
    "url": "https://sentinela.yourcompany.com:5001",
    "hubPath": "/hubs/agent",
    "reconnectDelays": [0, 1, 5, 15, 30, 60, 120],
    "heartbeatIntervalSeconds": 30,
    "connectionTimeoutSeconds": 10,
    "useWebSocket": true
  },
  "tenant": {
    "id": "your-tenant-id",
    "machineGroup": "default"
  },
  "authentication": {
    "method": "machine",
    "certificateThumbprint": null
  },
  "collectors": {
    "system": {
      "enabled": true,
      "intervalSeconds": 60
    },
    "process": {
      "enabled": true,
      "intervalSeconds": 300,
      "excludePatterns": ["svchost*", "runtime*", "conhost*"]
    },
    "network": {
      "enabled": true,
      "intervalSeconds": 120
    },
    "usb": {
      "enabled": true,
      "intervalSeconds": 10
    },
    "session": {
      "enabled": true,
      "intervalSeconds": 30
    },
    "security": {
      "enabled": true,
      "intervalSeconds": 60,
      "eventIds": [4624, 4625, 4634, 4647, 4648, 4688, 4698, 4702, 4720, 4722, 4726, 4740, 4776, 4778, 4779]
    },
    "application": {
      "enabled": true,
      "intervalSeconds": 3600
    },
    "windowsUpdate": {
      "enabled": true,
      "intervalSeconds": 3600
    }
  },
  "health": {
    "watchdogEnabled": true,
    "watchdogIntervalSeconds": 60,
    "memoryThresholdMb": 200,
    "cpuThresholdPercent": 80,
    "diskThresholdPercent": 90,
    "autoRestartOnCrash": true
  },
  "offline": {
    "bufferSizeMb": 100,
    "maxRetentionDays": 7,
    "syncOnReconnect": true
  },
  "update": {
    "autoUpdate": true,
    "channel": "stable",
    "checkIntervalHours": 6,
    "allowDowngrade": false
  },
  "logging": {
    "level": "Information",
    "fileMaxSizeMb": 50,
    "fileMaxCount": 5,
    "windowsEventLog": "Sentinela/Agent"
  }
}
```

### Configuration via Environment Variables

Settings can be overridden via environment variables:

| Variable                          | Overrides                    |
|----------------------------------|------------------------------|
| `SENTINELA_SERVER_URL`           | server.url                   |
| `SENTINELA_TENANT_ID`            | tenant.id                    |
| `SENTINELA_LOG_LEVEL`            | logging.level                |
| `SENTINELA_COLLECTOR_INTERVAL`   | collectors.*.intervalSeconds |
| `SENTINELA_HEARTBEAT_INTERVAL`   | server.heartbeatIntervalSeconds |

### Configuration via GPO

When deployed via GPO, the .admx template provides the following policy settings:

- Server URL and Tenant ID
- Collector enable/disable toggles
- Security event IDs to monitor
- Update channel and auto-update behavior
- Logging level
- Proxy configuration

---

## Collected Data

### System Collector

| Data Point            | Description                          | Frequency |
|----------------------|--------------------------------------|-----------|
| OS Version           | Windows edition, build number        | 60s       |
| CPU Usage            | Total CPU utilization percentage     | 60s       |
| Memory Usage         | Used / Total GB                      | 60s       |
| Disk Usage           | Per-drive used / total               | 60s       |
| System Uptime        | System boot time                     | 60s       |
| Computer Name        | NetBIOS and DNS name                 | 60s       |
| Hardware Info        | Manufacturer, model, serial          | 3600s     |

### Process Collector

| Data Point            | Description                          | Frequency |
|----------------------|--------------------------------------|-----------|
| Running Processes    | Name, PID, CPU %, Memory, user       | 300s      |
| Top Consumers        | Top 10 by CPU and memory             | 300s      |
| Process Changes       | New/terminated processes             | 300s      |
| Suspicious Processes  | Known malware names, unsigned        | 300s      |

### Network Collector

| Data Point            | Description                          | Frequency |
|----------------------|--------------------------------------|-----------|
| Network Interfaces   | Name, IP, MAC, DHCP, DNS             | 120s      |
| Active Connections   | Local/remote address, port, state    | 120s      |
| Bandwidth Usage      | Sent/Received bytes per interface    | 120s      |
| Firewall Status      | Windows Firewall profile, rules count| 120s      |

### USB Collector

| Data Point            | Description                          | Frequency |
|----------------------|--------------------------------------|-----------|
| Device Insertion     | Device type, vendor, serial          | 10s       |
| Device Removal       | Device ID, timestamp                 | 10s       |
| Mounted Volumes      | Drive letter, size, label            | 10s       |
| Device Properties    | Manufacturer, product, serial        | 10s       |

### Session Collector

| Data Point            | Description                          | Frequency |
|----------------------|--------------------------------------|-----------|
| Active Sessions      | User name, session type, login time  | 30s       |
| Session Changes      | Logon, logoff, disconnect, reconnect | 30s       |
| Lock/Unlock Events   | Workstation lock/unlock timestamps   | 30s       |
| RDP Connections      | Remote desktop connections (in/out)  | 30s       |

### Security Collector

| Data Point            | Description                          | Frequency |
|----------------------|--------------------------------------|-----------|
| Windows Security Logs | Configured event IDs (see config)    | 60s       |
| Failed Logins        | Account, source IP, count            | 60s       |
| Privilege Usage      | Sensitive privilege use events       | 60s       |
| Service Changes      | Service install/modify/delete        | 60s       |
| Scheduled Tasks      | Task creation, modification          | 60s       |

### Application Collector

| Data Point            | Description                          | Frequency |
|----------------------|--------------------------------------|-----------|
| Installed Apps       | Name, version, publisher, install date | 3600s   |
| Windows Updates      | Installed updates, pending updates   | 3600s     |
| Startup Programs     | Name, path, registry location        | 3600s     |
| Browser Extensions   | Installed extensions (Chrome, Edge)  | 3600s     |

### Windows Update Collector

| Data Point            | Description                          | Frequency |
|----------------------|--------------------------------------|-----------|
| Update Status        | Installed, pending, failed           | 3600s     |
| Missing Updates      | Critical/security updates not installed | 3600s  |
| Update History       | Recent installation events           | 3600s     |

---

## Communication Protocol

### Connection Lifecycle

```
Agent Start
    │
    ├── Load configuration
    ├── Initialize collectors
    ├── Open SQLite database
    │
    └── Connect to server
         │
         ├── SignalR WebSocket (preferred)
         │    ├── Handshake → /hubs/agent?tenant={tenantId}&machine={machineName}
         │    ├── Authenticate → Send machine certificate or token
         │    ├── Register → Server acknowledges and sends configuration
         │    └── Connected → Begin telemetry streaming
         │
         └── HTTP Fallback (if WebSocket unavailable)
              ├── HTTP POST /api/v1/agent/telemetry (batched)
              ├── HTTP POST /api/v1/agent/heartbeat
              └── HTTP GET  /api/v1/agent/commands/pending (polling)
```

### Message Format

All telemetry is sent as JSON over SignalR or HTTP.

```json
{
  "agentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tenantId": "your-tenant-id",
  "timestamp": "2026-07-17T10:15:00Z",
  "sequenceNumber": 1042,
  "payloads": [
    {
      "type": "SystemMetrics",
      "data": {
        "cpuUsage": 23.5,
        "memoryUsedMb": 6144,
        "memoryTotalMb": 16384,
        "diskUsage": [
          { "drive": "C:", "usedGb": 120, "totalGb": 256, "percentUsed": 46.9 },
          { "drive": "D:", "usedGb": 300, "totalGb": 500, "percentUsed": 60.0 }
        ],
        "uptimeSeconds": 1234567
      }
    },
    {
      "type": "ProcessList",
      "data": {
        "processes": [
          { "pid": 1234, "name": "chrome.exe", "cpuPercent": 12.3, "memoryMb": 450, "userName": "CORP\\john.doe" },
          { "pid": 5678, "name": "powershell.exe", "cpuPercent": 0.5, "memoryMb": 120, "userName": "CORP\\john.doe" }
        ]
      }
    }
  ]
}
```

### Heartbeat

Sent every 30 seconds to indicate agent liveness:

```json
{
  "agentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "timestamp": "2026-07-17T10:15:30Z",
  "sequenceNumber": 1043,
  "status": "Running",
  "metrics": {
    "cpuUsage": 18.0,
    "memoryUsageMb": 85,
    "diskUsageMb": 45,
    "uptimeSeconds": 12345
  }
}
```

### Server Commands (Received via SignalR)

| Command                 | Payload                                                | Description                         |
|------------------------|--------------------------------------------------------|-------------------------------------|
| ExecuteCommand         | `{ commandId, command, timeout, runAs }`               | Run a command/script                |
| TerminateProcess       | `{ pid, force }`                                       | Kill a running process              |
| UpdateConfiguration    | `{ configuration }`                                    | Apply new agent config              |
| InitiateUpdate         | `{ version, downloadUrl, mandatory, scheduledTime }`   | Trigger agent update                |
| RestartAgent           | `{ scheduledTime, reason }`                            | Restart the agent service           |
| CollectDiagnostics     | `{ scope, outputPath }`                                | Gather diagnostic information       |
| RemoteAssistance       | `{ sessionId, mode }`                                  | Initiate remote assistance session  |

---

## Offline Behavior

When the agent cannot reach the Sentinela server:

### Detection
- After 3 consecutive failed heartbeat attempts (configurable)
- Connection state transitions to `Offline`

### Operation During Offline
- All collectors continue to run at configured intervals
- Telemetry data is buffered in SQLite database
- Heartbeats are queued with local timestamps
- Commands that require server interaction are queued
- Security events are stored with highest priority

### Storage Limits
| Resource         | Limit                              |
|-----------------|------------------------------------|
| SQLite DB size  | 100 MB (configurable)              |
| Retention       | 7 days (oldest data purged first)  |
| Event priority  | Security events > System > Process |

### Reconnection
- Exponential backoff: 0s, 1s, 5s, 15s, 30s, 60s, 120s (configurable)
- On reconnect, sends all buffered data in order
- Server deduplicates by `sequenceNumber`
- If buffer is large (>10 MB), data is sent in chunks

### Reconnection Logic

```
Disconnected
    │
    ├── Immediate retry (0s delay)
    ├── 1s delay
    ├── 5s delay
    ├── 15s delay
    ├── 30s delay
    │
    └── After 5 attempts: Use exponential backoff
         ├── 60s → 120s → 120s → ...
         └── Reset on any successful connection
```

---

## Update Mechanism

### Update Channel

| Channel  | Frequency      | Risk     |
|---------|---------------|----------|
| stable  | Monthly       | Low      |
| beta     | Bi-weekly    | Medium   |
| nightly  | Daily        | High     |

### Update Flow

1. Server marks a new agent version as available
2. Agent receives `UpdateAvailable` event via SignalR
3. For mandatory updates, agent downloads immediately
4. For optional updates, agent downloads during idle (no user logged on or CPU < 10%)
5. Agent verifies digital signature of downloaded package
6. Agent stops collectors, installs update, restarts service
7. On restart, agent sends version information to server
8. If update fails, agent rolls back to previous version

### Update Verification

- All agent packages are digitally signed with Sentinela code signing certificate
- SHA-256 hash is verified before installation
- If signature is invalid, the update is discarded and an error is logged

### Manual Update

```powershell
& "C:\Program Files\Sentinela\Agent\Sentinela.Agent.exe" update --version "2.2.0"
```

---

## Troubleshooting

### Check Service Status

```powershell
Get-Service -Name "SentinelaAgent"
```

### View Agent Logs

```powershell
# Windows Event Log
Get-WinEvent -LogName "Sentinela/Agent" -MaxEvents 50 | Format-Table TimeCreated, LevelDisplayName, Message -AutoSize

# File log (if enabled)
Get-Content "C:\ProgramData\Sentinela\Agent\logs\agent-*.log" -Tail 100
```

### Test Connectivity

```powershell
& "C:\Program Files\Sentinela\Agent\Sentinela.Agent.exe" test-connection
```

### Collect Diagnostics

```powershell
& "C:\Program Files\Sentinela\Agent\Sentinela.Agent.exe" collect-diagnostics --output "C:\temp\sentinela-diag.zip"
```

### Common Issues

| Symptom                       | Likely Cause                          | Solution                                      |
|------------------------------|---------------------------------------|-----------------------------------------------|
| Agent not starting           | Missing .NET Runtime                  | Install .NET 9 Runtime                        |
| Agent shows Offline          | Network connectivity issue            | Check firewall, proxy, server URL             |
| Agent crashes periodically   | Memory limit exceeded                 | Increase memory threshold in config           |
| Agent not sending telemetry  | Collector disabled in config          | Verify collector enabled: true                |
| Agent fails to authenticate  | Invalid or expired machine token      | Re-install with valid auth token              |
| Update fails                 | Digital signature verification failed | Download latest installer manually            |
| High CPU usage by agent      | Collector interval too frequent       | Increase collector intervals                  |
| SQLite database full         | Offline for extended period           | Increase buffer size or resolve connectivity  |

### Restart Agent

```powershell
Restart-Service -Name "SentinelaAgent"
```

### Uninstall

```powershell
# Silent
& "C:\Program Files\Sentinela\Agent\unins000.exe" /SILENT

# Or via Programs and Features
```

### Factory Reset

```powershell
& "C:\Program Files\Sentinela\Agent\Sentinela.Agent.exe" reset --clear-cache --clear-config
```

---

## Service Recovery

The Windows Service is configured with automatic recovery:

| Failure Attempt | Action               |
|----------------|----------------------|
| 1st            | Restart service       |
| 2nd            | Restart service       |
| 3rd            | Run diagnostics script |
| Subsequent     | Restart service       |

---

## Performance Impact

Under normal operation, the agent uses:

| Resource   | Typical Usage    |
|-----------|-----------------|
| CPU        | 0.5% - 2%      |
| Memory     | 50 MB - 120 MB |
| Disk       | 50 MB - 500 MB  |
| Network    | 1-5 KB/s (upload during telemetry) |

Heavy collection (process details, full network connections) may temporarily increase usage.
