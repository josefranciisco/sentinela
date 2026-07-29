# Sentinela API Reference

## Base URL

| Environment | URL                                |
|------------|------------------------------------|
| Development| `https://localhost:5001/api`       |
| Staging    | `https://staging.sentinela.local/api` |
| Production | `https://sentinela.yourcompany.com/api` |

## API Versioning

The API is versioned via URL prefix:

```
/api/v1/computers
/api/v1/alerts
/api/v1/identity/users
```

When breaking changes are introduced, a new version (`/api/v2/...`) is released while maintaining the previous version for at least one release cycle. Deprecated versions are announced via the `Sunset` and `Deprecation` HTTP headers.

## Authentication

### JWT Bearer Token

All endpoints require authentication unless explicitly marked as public.

```
Authorization: Bearer <access_token>
```

### Obtain Access Token

```
POST /api/v1/identity/auth/login
Content-Type: application/json

{
  "email": "operator@sentinela.local",
  "password": "your-password",
  "rememberMe": false
}
```

**Response (200 OK):**
```json
{
  "accessToken": "eyJhbGciOiJSUzI1NiIs...",
  "expiresIn": 900,
  "tokenType": "Bearer"
}
```

The access token is valid for 15 minutes. A refresh token is set as an HTTP-only secure cookie (`X-Refresh-Token`).

### Refresh Token

```
POST /api/v1/identity/auth/refresh
Cookie: X-Refresh-Token=<refresh_token>
```

**Response (200 OK):**
```json
{
  "accessToken": "eyJhbGciOiJSUzI1NiIs...",
  "expiresIn": 900,
  "tokenType": "Bearer"
}
```

### 2FA Verification

If 2FA is enabled, login returns a 401 with `X-Requires-2FA: true` header. Complete verification:

```
POST /api/v1/identity/auth/verify-2fa
Content-Type: application/json

{
  "email": "operator@sentinela.local",
  "code": "123456"
}
```

### Logout

```
POST /api/v1/identity/auth/logout
Authorization: Bearer <token>
```

Invalidates the refresh token server-side.

---

## Endpoints

### Identity

#### List Users

```
GET /api/v1/identity/users
Authorization: Bearer <token>
```

**Query Parameters:**
| Parameter | Type   | Description                    |
|-----------|--------|--------------------------------|
| page      | int    | Page number (default: 1)       |
| pageSize  | int    | Items per page (default: 20)   |
| search    | string | Search by name or email        |
| role      | string | Filter by role                 |
| active    | bool   | Filter by active/inactive      |

**Response (200 OK):**
```json
{
  "data": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "name": "John Doe",
      "email": "john@sentinela.local",
      "role": "SecurityAnalyst",
      "active": true,
      "lastLoginAt": "2026-07-16T14:30:00Z",
      "mfaEnabled": true
    }
  ],
  "totalCount": 42,
  "page": 1,
  "pageSize": 20,
  "totalPages": 3
}
```

#### Get User

```
GET /api/v1/identity/users/{id}
Authorization: Bearer <token>
```

#### Create User

```
POST /api/v1/identity/users
Authorization: Bearer <token>
Content-Type: application/json

{
  "name": "Jane Smith",
  "email": "jane@sentinela.local",
  "password": "SecureP@ss123",
  "role": "SecurityAnalyst",
  "sendWelcomeEmail": true
}
```

**Response (201 Created):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "Jane Smith",
  "email": "jane@sentinela.local",
  "role": "SecurityAnalyst",
  "active": true
}
```

#### Update User

```
PUT /api/v1/identity/users/{id}
Authorization: Bearer <token>
Content-Type: application/json

{
  "name": "Jane Smith-Updated",
  "role": "Administrator",
  "active": true
}
```

#### Delete User

```
DELETE /api/v1/identity/users/{id}
Authorization: Bearer <token>
```

**Response (204 No Content)**

#### List Roles

```
GET /api/v1/identity/roles
Authorization: Bearer <token>
```

#### Get Role Permissions

```
GET /api/v1/identity/roles/{role}/permissions
Authorization: Bearer <token>
```

#### Update Role Permissions

```
PUT /api/v1/identity/roles/{role}/permissions
Authorization: Bearer <token>
Content-Type: application/json

{
  "permissions": [
    "computers:read",
    "computers:write",
    "alerts:read",
    "alerts:acknowledge",
    "workflows:read"
  ]
}
```

#### Enable/Disable 2FA for User

```
POST /api/v1/identity/users/{id}/mfa
Authorization: Bearer <token>
Content-Type: application/json

{
  "enabled": true
}
```

---

### Computers

#### List Computers

```
GET /api/v1/computers
Authorization: Bearer <token>
```

**Query Parameters:**
| Parameter  | Type   | Description                        |
|-----------|--------|------------------------------------|
| page       | int    | Page number (default: 1)          |
| pageSize   | int    | Items per page (default: 20)      |
| search     | string | Search by name or domain           |
| status     | string | Filter: Online, Offline, Warning   |
| groupId    | guid   | Filter by computer group           |
| os         | string | Filter by OS (Windows 10, Windows 11, Windows Server 2022) |
| sortBy     | string | Field to sort by                   |
| sortOrder  | string | asc or desc                        |

**Response (200 OK):**
```json
{
  "data": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "name": "DESKTOP-ABC123",
      "domain": "CORP",
      "osVersion": "Windows 11 Pro 23H2",
      "agentVersion": "2.1.0",
      "status": "Online",
      "lastHeartbeat": "2026-07-17T10:15:00Z",
      "cpuUsage": 23.5,
      "memoryUsage": 6144,
      "memoryTotal": 16384,
      "diskUsage": 45.2,
      "ipAddress": "192.168.1.100"
    }
  ],
  "totalCount": 150,
  "page": 1,
  "pageSize": 20,
  "totalPages": 8
}
```

#### Get Computer Details

```
GET /api/v1/computers/{id}
Authorization: Bearer <token>
```

**Response (200 OK):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "DESKTOP-ABC123",
  "domain": "CORP",
  "osVersion": "Windows 11 Pro 23H2",
  "osBuild": "22631.2861",
  "manufacturer": "Dell Inc.",
  "model": "Latitude 5540",
  "serialNumber": "ABC123DEF456",
  "processor": "13th Gen Intel Core i7-1370P",
  "totalMemory": 16384,
  "totalDisk": 512000,
  "agentVersion": "2.1.0",
  "agentInstalledAt": "2026-01-15T08:00:00Z",
  "status": "Online",
  "lastHeartbeat": "2026-07-17T10:15:00Z",
  "ipAddress": "192.168.1.100",
  "macAddress": "00-1A-2B-3C-4D-5E",
  "loggedOnUser": "CORP\\john.doe",
  "groupId": null,
  "tags": ["finance", "critical"],
  "createdAt": "2026-01-15T08:00:00Z",
  "updatedAt": "2026-07-17T10:15:00Z"
}
```

#### Get Computer Timeline

```
GET /api/v1/computers/{id}/timeline
Authorization: Bearer <token>
```

**Query Parameters:**
| Parameter | Type   | Description                     |
|-----------|--------|---------------------------------|
| from       | datetime | Start of time range           |
| to         | datetime | End of time range             |
| category   | string   | Filter: System, Security, Application, User |
| page       | int      | Page number                   |
| pageSize   | int      | Items per page                |

#### Get Computer Applications

```
GET /api/v1/computers/{id}/applications
Authorization: Bearer <token>
```

#### Get Computer Heartbeat History

```
GET /api/v1/computers/{id}/heartbeats
Authorization: Bearer <token>
```

**Query Parameters:**
| Parameter | Type   | Description                  |
|-----------|--------|------------------------------|
| from       | datetime | Start of range             |
| to         | datetime | End of range               |
| interval   | string   | Aggregation: 1m, 5m, 15m, 1h |

#### Get Computer Security Events

```
GET /api/v1/computers/{id}/security-events
Authorization: Bearer <token>
```

#### Execute Remote Command

```
POST /api/v1/computers/{id}/execute
Authorization: Bearer <token>
Content-Type: application/json

{
  "command": "ipconfig /all",
  "timeoutSeconds": 30,
  "runAs": "System"
}
```

**Response (202 Accepted):**
```json
{
  "commandId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Pending",
  "estimatedCompletion": "2026-07-17T10:15:10Z"
}
```

#### Get Command Result

```
GET /api/v1/computers/commands/{commandId}
Authorization: Bearer <token>
```

**Response (200 OK):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "computerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "command": "ipconfig /all",
  "status": "Completed",
  "exitCode": 0,
  "stdout": "Windows IP Configuration\n\nEthernet adapter Ethernet0:\n   ...",
  "stderr": "",
  "requestedAt": "2026-07-17T10:15:00Z",
  "completedAt": "2026-07-17T10:15:05Z"
}
```

#### Get Computer Groups

```
GET /api/v1/computers/groups
Authorization: Bearer <token>
```

#### Create Computer Group

```
POST /api/v1/computers/groups
Authorization: Bearer <token>
Content-Type: application/json

{
  "name": "Finance Department",
  "description": "All finance workstations and laptops",
  "query": "tags contains 'finance'"
}
```

#### Add Computers to Group

```
POST /api/v1/computers/groups/{groupId}/members
Authorization: Bearer <token>
Content-Type: application/json

{
  "computerIds": ["id1", "id2", "id3"]
}
```

---

### Alerts

#### List Alerts

```
GET /api/v1/alerts
Authorization: Bearer <token>
```

**Query Parameters:**
| Parameter    | Type   | Description                                |
|-------------|--------|--------------------------------------------|
| page         | int    | Page number (default: 1)                  |
| pageSize     | int    | Items per page (default: 20)              |
| status       | string | Filter: New, Acknowledged, Resolved, Closed |
| severity     | string | Filter: Critical, High, Medium, Low, Info |
| computerId   | guid   | Filter by computer                         |
| ruleId       | guid   | Filter by alert rule                       |
| from         | datetime | Start of time range                      |
| to           | datetime | End of time range                        |
| sortBy       | string | Field to sort by                           |
| sortOrder    | string | asc or desc                                |

**Response (200 OK):**
```json
{
  "data": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "ruleName": "High CPU Usage",
      "severity": "Critical",
      "status": "New",
      "computerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "computerName": "DESKTOP-ABC123",
      "message": "CPU usage at 95% for over 10 minutes",
      "value": 95.2,
      "threshold": 90,
      "createdAt": "2026-07-17T10:00:00Z",
      "acknowledgedBy": null,
      "acknowledgedAt": null,
      "resolvedBy": null,
      "resolvedAt": null
    }
  ],
  "totalCount": 230,
  "page": 1,
  "pageSize": 20,
  "totalPages": 12,
  "summary": {
    "critical": 3,
    "high": 12,
    "medium": 45,
    "low": 170
  }
}
```

#### Get Alert

```
GET /api/v1/alerts/{id}
Authorization: Bearer <token>
```

#### Acknowledge Alert

```
POST /api/v1/alerts/{id}/acknowledge
Authorization: Bearer <token>
Content-Type: application/json

{
  "comment": "Investigating high CPU on this machine"
}
```

**Response (200 OK):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Acknowledged",
  "acknowledgedBy": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "acknowledgedByName": "John Doe",
  "acknowledgedAt": "2026-07-17T10:05:00Z"
}
```

#### Resolve Alert

```
POST /api/v1/alerts/{id}/resolve
Authorization: Bearer <token>
Content-Type: application/json

{
  "resolution": "Process 'crypto-miner.exe' terminated. User notified.",
  "closeAlert": true
}
```

#### Bulk Acknowledge Alerts

```
POST /api/v1/alerts/bulk/acknowledge
Authorization: Bearer <token>
Content-Type: application/json

{
  "alertIds": ["id1", "id2", "id3"],
  "comment": "Batch acknowledged for investigation"
}
```

#### Get Alert Timeline

```
GET /api/v1/alerts/{id}/timeline
Authorization: Bearer <token>
```

Returns the full history of status changes, comments, and related events.

#### List Alert Rules

```
GET /api/v1/alerts/rules
Authorization: Bearer <token>
```

#### Create Alert Rule

```
POST /api/v1/alerts/rules
Authorization: Bearer <token>
Content-Type: application/json

{
  "name": "High CPU - Finance Group",
  "description": "Triggers when CPU exceeds 90% for 10 minutes on finance computers",
  "enabled": true,
  "severity": "High",
  "metric": "cpu_usage",
  "aggregation": "average",
  "operator": "greater_than",
  "threshold": 90,
  "durationMinutes": 10,
  "computerGroupId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "notificationChannels": ["email", "teams"]
}
```

#### Update Alert Rule

```
PUT /api/v1/alerts/rules/{ruleId}
Authorization: Bearer <token>
```

#### Delete Alert Rule

```
DELETE /api/v1/alerts/rules/{ruleId}
Authorization: Bearer <token>
```

**Response (204 No Content)**

#### Test Alert Rule

```
POST /api/v1/alerts/rules/{ruleId}/test
Authorization: Bearer <token>
Content-Type: application/json

{
  "sampleData": {
    "cpu_usage": 95.0,
    "memory_usage": 70.0
  }
}
```

---

### Automation

#### List Workflows

```
GET /api/v1/automation/workflows
Authorization: Bearer <token>
```

#### Get Workflow

```
GET /api/v1/automation/workflows/{id}
Authorization: Bearer <token>
```

#### Create Workflow

```
POST /api/v1/automation/workflows
Authorization: Bearer <token>
Content-Type: application/json

{
  "name": "Auto-kill Crypto Miners",
  "description": "Automatically terminate known crypto mining processes",
  "enabled": true,
  "trigger": {
    "type": "event",
    "config": {
      "eventType": "AlertCreated",
      "condition": "alert.ruleName contains 'Crypto Miner'"
    }
  },
  "conditions": [
    {
      "field": "alert.severity",
      "operator": "greater_than_or_equal",
      "value": "High"
    }
  ],
  "actions": [
    {
      "type": "remote_command",
      "config": {
        "command": "taskkill /f /im crypto-miner.exe",
        "runAs": "System",
        "timeout": 30
      }
    },
    {
      "type": "notification",
      "config": {
        "channel": "teams",
        "template": "crypto-miner-terminated"
      }
    }
  ]
}
```

#### Update Workflow

```
PUT /api/v1/automation/workflows/{id}
Authorization: Bearer <token>
```

#### Toggle Workflow

```
POST /api/v1/automation/workflows/{id}/toggle
Authorization: Bearer <token>

{
  "enabled": false
}
```

#### Delete Workflow

```
DELETE /api/v1/automation/workflows/{id}
Authorization: Bearer <token>
```

**Response (204 No Content)**

#### Get Workflow Execution Logs

```
GET /api/v1/automation/workflows/{id}/executions
Authorization: Bearer <token>
```

#### Test Workflow

```
POST /api/v1/automation/workflows/{id}/test
Authorization: Bearer <token>
Content-Type: application/json

{
  "simulatedEvent": {
    "alert": {
      "id": "test-alert-id",
      "ruleName": "High CPU Usage",
      "severity": "High",
      "computerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    }
  }
}
```

---

### Audit

#### List Audit Entries

```
GET /api/v1/audit
Authorization: Bearer <token>
```

**Query Parameters:**
| Parameter | Type   | Description                  |
|-----------|--------|------------------------------|
| page       | int    | Page number                  |
| pageSize   | int    | Items per page               |
| actorId    | guid   | Filter by user               |
| action     | string | Filter: Login, Logout, AlertAcknowledge, ComputerExecute, etc. |
| resource   | string | Filter by resource type      |
| from       | datetime | Start of time range        |
| to         | datetime | End of time range          |

**Response (200 OK):**
```json
{
  "data": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "timestamp": "2026-07-17T10:05:00Z",
      "actorId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "actorName": "John Doe",
      "action": "AlertAcknowledge",
      "resourceType": "Alert",
      "resourceId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "details": {
        "comment": "Investigating high CPU"
      },
      "ipAddress": "192.168.1.50",
      "userAgent": "Mozilla/5.0..."
    }
  ],
  "totalCount": 1250,
  "page": 1,
  "pageSize": 20,
  "totalPages": 63
}
```

#### Export Audit Log

```
GET /api/v1/audit/export
Authorization: Bearer <token>
```

**Query Parameters:**
| Parameter | Type   | Description               |
|-----------|--------|---------------------------|
| from       | datetime | Start of range          |
| to         | datetime | End of range            |
| format     | string | csv or json (default: csv) |

**Response (200 OK):** File download with `Content-Disposition: attachment`.

---

### Analytics

#### Generate Report

```
POST /api/v1/analytics/reports
Authorization: Bearer <token>
Content-Type: application/json

{
  "name": "Monthly Security Summary",
  "type": "security_summary",
  "parameters": {
    "period": "2026-06-01/2026-06-30",
    "groupBy": "severity",
    "includeComputers": true,
    "includeResolvedAlerts": true
  }
}
```

**Response (202 Accepted):**
```json
{
  "reportId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Generating",
  "estimatedCompletion": "2026-07-17T10:05:30Z"
}
```

#### Get Report

```
GET /api/v1/analytics/reports/{reportId}
Authorization: Bearer <token>
```

#### List Reports

```
GET /api/v1/analytics/reports
Authorization: Bearer <token>
```

---

### AI Assistant

#### Send Query

```
POST /api/v1/ai/query
Authorization: Bearer <token>
Content-Type: application/json

{
  "prompt": "Show me all computers with CPU usage above 80% in the last hour",
  "includeContext": true
}
```

**Response (200 OK):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "response": "Found 8 computers with CPU usage above 80% in the last hour:\n\n| Computer | CPU % | Duration |\n|----------|-------|----------|\n| DESKTOP-ABC123 | 95% | 25 min |\n| SERVER-FIN-01 | 92% | 15 min |\n...\n\nWould you like me to take any action on these computers?",
  "suggestedActions": [
    {
      "label": "Generate alert for top 3",
      "endpoint": "POST /api/v1/alerts/bulk/create",
      "payload": { "computerIds": ["..."] }
    },
    {
      "label": "Run diagnostic on all",
      "endpoint": "POST /api/v1/computers/bulk/execute",
      "payload": { "command": "performance diagnostic" }
    }
  ],
  "tokensUsed": 452,
  "processingTimeMs": 2340
}
```

#### Get Query History

```
GET /api/v1/ai/history
Authorization: Bearer <token>
```

---

### Dashboard

#### Get Dashboard Summary

```
GET /api/v1/dashboard/summary
Authorization: Bearer <token>
```

**Response (200 OK):**
```json
{
  "totalComputers": 150,
  "onlineComputers": 142,
  "offlineComputers": 8,
  "totalAlerts": 230,
  "criticalAlerts": 3,
  "highAlerts": 12,
  "acknowledgedAlerts": 45,
  "resolvedAlerts": 160,
  "activeWorkflows": 12,
  "recentExecutions": 45,
  "averageCpuUsage": 32.5,
  "averageMemoryUsage": 55.2,
  "averageDiskUsage": 62.1,
  "lastUpdated": "2026-07-17T10:15:00Z"
}
```

#### Get Alert Timeline (Dashboard)

```
GET /api/v1/dashboard/alert-timeline
Authorization: Bearer <token>
```

**Query Parameters:**
| Parameter | Type   | Description                 |
|-----------|--------|-----------------------------|
| period     | string | Last1h, Last24h, Last7d, Last30d |

**Response (200 OK):**
```json
{
  "timeline": [
    { "timestamp": "2026-07-17T09:00:00Z", "count": 2, "critical": 0, "high": 1, "medium": 1, "low": 0 },
    { "timestamp": "2026-07-17T10:00:00Z", "count": 5, "critical": 1, "high": 2, "medium": 2, "low": 0 }
  ],
  "period": "Last1h"
}
```

#### Get Top Alerts

```
GET /api/v1/dashboard/top-alerts
Authorization: Bearer <token>
```

---

### Health

#### Service Health

```
GET /api/v1/health
```

**Response (200 OK):**
```json
{
  "status": "Healthy",
  "timestamp": "2026-07-17T10:15:00Z",
  "services": {
    "api": { "status": "Healthy", "uptime": "12d 4h 32m" },
    "database": { "status": "Healthy", "responseTimeMs": 3 },
    "redis": { "status": "Healthy", "responseTimeMs": 1 },
    "rabbitmq": { "status": "Healthy", "queues": { "ready": 12, "unacked": 0, "total": 1500 } },
    "identity": { "status": "Healthy", "uptime": "12d 4h 30m" },
    "alert-engine": { "status": "Healthy", "uptime": "12d 4h 28m" },
    "automation": { "status": "Healthy", "uptime": "12d 4h 28m" }
  }
}
```

#### Detailed Health

```
GET /api/v1/health/detailed
Authorization: Bearer <token>
```

Provides detailed diagnostics including database migration status, disk space, memory usage per service.

---

## WebSocket Hubs

### Computer Hub

**Endpoint:** `wss://sentinela.yourcompany.com/hubs/computers`

| Event Name        | Direction     | Payload                                  |
|------------------|---------------|------------------------------------------|
| HeartbeatReceived| Server → Client | `{ computerId, cpuUsage, memoryUsage, timestamp }` |
| ComputerStatusChanged | Server → Client | `{ computerId, oldStatus, newStatus }` |
| CommandCompleted | Server → Client | `{ commandId, computerId, status, exitCode }` |
| AgentConnected   | Server → Client | `{ computerId, agentVersion, ipAddress }` |
| AgentDisconnected| Server → Client | `{ computerId, lastHeartbeat }` |

### Alert Hub

**Endpoint:** `wss://sentinela.yourcompany.com/hubs/alerts`

| Event Name        | Direction     | Payload                                  |
|------------------|---------------|------------------------------------------|
| AlertCreated     | Server → Client | `{ alertId, severity, ruleName, computerName, message }` |
| AlertAcknowledged| Server → Client | `{ alertId, acknowledgedBy }`           |
| AlertResolved    | Server → Client | `{ alertId, resolvedBy, resolution }`   |
| AlertUpdated     | Server → Client | `{ alertId, changes }`                  |

### Notification Hub

**Endpoint:** `wss://sentinela.yourcompany.com/hubs/notifications`

| Event Name        | Direction     | Payload                                  |
|------------------|---------------|------------------------------------------|
| Notification     | Server → Client | `{ id, type, title, message, actionUrl, timestamp }` |

### Agent Hub

**Endpoint:** `wss://sentinela.yourcompany.com/hubs/agent`

| Event Name        | Direction     | Payload                                  |
|------------------|---------------|------------------------------------------|
| CommandIssued    | Server → Client | `{ commandId, command, timeout }`       |
| UpdateAvailable  | Server → Client | `{ version, downloadUrl, mandatory, scheduledTime }` |
| PolicyUpdated    | Server → Client | `{ policyId, configuration }`           |

---

## Rate Limiting

Rate limits are applied per IP address and per authenticated user.

| Endpoint Group       | Rate Limit              |
|---------------------|------------------------|
| `/api/v1/identity/auth/*` | 10 req/min            |
| `/api/v1/ai/*`      | 30 req/min              |
| `/api/v1/*`         | 100 req/min             |
| `/api/v1/health`    | 60 req/min              |
| WebSocket connections | 10 concurrent per IP   |

Rate limit headers are included in every response:

```
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 87
X-RateLimit-Reset: 1626516000
```

When exceeded, a **429 Too Many Requests** response is returned with a `Retry-After` header.

---

## Error Handling

The API follows [RFC 9457 — Problem Details](https://www.rfc-editor.org/rfc/rfc9457) for error responses.

### Validation Error (400)

```json
{
  "type": "https://sentinela.local/errors/validation",
  "title": "Validation Error",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "instance": "/api/v1/computers",
  "errors": {
    "command": ["'command' must not be empty."],
    "timeoutSeconds": ["'timeoutSeconds' must be between 1 and 300."]
  }
}
```

### Unauthorized (401)

```json
{
  "type": "https://sentinela.local/errors/unauthorized",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Invalid or expired access token."
}
```

### Forbidden (403)

```json
{
  "type": "https://sentinela.local/errors/forbidden",
  "title": "Forbidden",
  "status": 403,
  "detail": "You do not have permission to execute remote commands on this computer."
}
```

### Not Found (404)

```json
{
  "type": "https://sentinela.local/errors/not-found",
  "title": "Resource Not Found",
  "status": 404,
  "detail": "Computer with ID '3fa85f64-5717-4562-b3fc-2c963f66afa6' was not found."
}
```

### Conflict (409)

```json
{
  "type": "https://sentinela.local/errors/conflict",
  "title": "Conflict",
  "status": 409,
  "detail": "A computer with the name 'DESKTOP-ABC123' already exists."
}
```

### Rate Limited (429)

```json
{
  "type": "https://sentinela.local/errors/rate-limited",
  "title": "Too Many Requests",
  "status": 429,
  "detail": "Rate limit exceeded. Please retry after 30 seconds.",
  "retryAfter": 30
}
```

### Internal Server Error (500)

```json
{
  "type": "https://sentinela.local/errors/internal",
  "title": "Internal Server Error",
  "status": 500,
  "detail": "An unexpected error occurred. Please contact support with reference ID: ERR-ABC123."
}
```

In production, internal error details are never exposed to the client. A correlation ID is provided for support traceability.

---

## Standard Headers

| Header             | Description                                  |
|-------------------|----------------------------------------------|
| `X-Request-Id`    | Unique request identifier for tracing        |
| `X-Correlation-Id`| End-to-end correlation across services       |
| `X-Trace-Id`      | Distributed tracing span ID                  |
| `X-RateLimit-*`   | Rate limit information                       |
| `Sunset`          | Deprecation date for API version             |

Clients should send `X-Request-Id` and `X-Correlation-Id` on all requests for traceability.
