# Sentinela User Guide

## Dashboard Overview

The Sentinela dashboard is the central hub for monitoring your entire infrastructure. It provides real-time visibility into computer status, alert activity, and system health.

### KPI Cards

| Card              | Description                                 |
|-------------------|---------------------------------------------|
| Total Computers   | All registered endpoints                    |
| Online            | Computers with heartbeat in last 5 minutes  |
| Offline           | Computers with no heartbeat in last 5 minutes |
| Alerts            | Total open alerts (New + Acknowledged)      |
| Active Workflows  | Enabled automation workflows                |

### Navigation

The sidebar provides access to all main sections:

- **Dashboard** -- Home screen with summary widgets
- **Computers** -- Endpoint inventory and management
- **Alerts** -- Alert center with filtering and management
- **Automation** -- Workflow designer and execution logs
- **AI Assistant** -- Natural language query interface
- **Reports** -- Generated reports and analytics
- **Audit** -- Audit trail viewer and export
- **Administration** -- Users, roles, settings, integrations

---

## Computer Management

### Computers List

The Computers page displays all monitored endpoints in a sortable, filterable table.

**Columns:**
| Column            | Description                              |
|-------------------|------------------------------------------|
| Name              | Computer NetBIOS name                    |
| Status            | Online (green), Offline (gray), Warning (yellow) |
| OS                | Operating system version                 |
| Agent             | Agent version number                     |
| CPU               | Current CPU utilization                  |
| Memory            | Used / Total GB                          |
| Disk              | Used / Total GB (system drive)           |
| Last Heartbeat    | Time of last communication               |
| Logged On User    | Currently logged-in user                 |

**Actions:**
- Click row to view computer details
- Multi-select for bulk operations
- Search by name, domain, or IP
- Filter by status, OS, group, tags
- Export list to CSV

### Computer Details

The detail view provides comprehensive information about a single endpoint.

**Tabs:**
| Tab              | Content                                       |
|-----------------|-----------------------------------------------|
| Overview        | Hardware info, OS version, agent status       |
| Timeline        | Chronological event log (security, system, user) |
| Applications    | Installed software inventory                  |
| Processes       | Running processes with CPU/memory usage       |
| Performance     | CPU, memory, disk charts (1h, 24h, 7d)        |
| Security        | Security events from Windows Event Log        |
| Network         | Active connections, bandwidth usage           |

**Overview Tab includes:**
- Hardware: manufacturer, model, serial number, BIOS version
- System: OS name, build number, install date, domain
- Agent: version, install date, last check-in, config status
- User: currently logged-on user, session type, login time

### Remote Commands

Execute commands on remote endpoints directly from the web UI:

1. Navigate to Computer Details
2. Click **Execute Command**
3. Enter the command (PowerShell, CMD, or batch)
4. Set timeout (max 300 seconds)
5. Choose run context (System or logged-on user)
6. Click **Execute**

Results appear in real-time via SignalR. Command history is preserved.

### Remote Assistance

Initiate a remote assistance session:

1. Navigate to Computer Details
2. Click **Remote Assistance**
3. Choose mode:
   - **View Only** -- Observe the user's screen
   - **Full Control** -- Take control with user permission
4. The agent launches a secure remote session
5. Session is fully encrypted and logged

### Screen Capture

Take on-demand screenshots of remote endpoints:

1. Navigate to Computer Details
2. Click **Screen Capture**
3. Choose capture mode:
   - **Single Capture** -- One screenshot
   - **Timelapse** -- Multiple captures over a period (e.g., 10 captures at 30s intervals)
4. Screenshots are encrypted in transit and at rest
5. Captures are stored for 7 days, then automatically deleted

### Computer Groups

Organize computers into groups for targeted alert rules and bulk actions:

- Create dynamic groups using filters (e.g., `tags contains 'finance'`)
- Create static groups by manually selecting computers
- Apply alert rules and automation workflows at group level
- View group-wide metrics and alert summaries

---

## Alert Management

### Alerts List

The Alerts page provides a centralized view of all security and system alerts.

**Columns:**
| Column        | Description                               |
|---------------|-------------------------------------------|
| Severity      | Critical (red), High (orange), Medium (yellow), Low (blue), Info (gray) |
| Status        | New, Acknowledged, Resolved, Closed       |
| Rule          | Name of the alert rule that triggered     |
| Computer      | Affected endpoint                         |
| Message       | Alert description and context             |
| Value         | Current metric value (e.g., CPU 95%)      |
| Threshold     | Rule threshold that was exceeded          |
| Created       | When the alert was generated              |
| Age           | Time since alert creation                 |

**Filters:**
- Status: New, Acknowledged, Resolved, Closed
- Severity: Critical, High, Medium, Low, Info
- Date range: predefined (Last 1h, 24h, 7d, 30d) or custom
- Computer: filter by specific endpoint
- Rule: filter by alert rule
- Search: free text in message, computer name, rule name

**Bulk Operations:**
- Select alerts via checkboxes
- Acknowledge selected (add optional comment)
- Resolve selected (add resolution note)
- Export selected to CSV

### Alert Details

Click any alert to view its details:

- **Alert Information**: Full message, rule details, metric values
- **Computer Context**: Current computer status, recent activity
- **Timeline**: Full history of status changes, acknowledgments, comments
- **Related Alerts**: Other alerts from the same computer or rule
- **Suggested Actions**: Remediation steps (preconfigured or AI-generated)

### Alert Lifecycle

```
New
  │
  ├── Acknowledge  (assign to self, add comment)
  │     │
  │     ├── Resolve  (add resolution note, optionally close)
  │     │     │
  │     │     └── Closed (auto-close after X days if resolved)
  │     │
  │     └── Escalate (increase severity, notify higher tier)
  │
  └── Auto-Resolve (if metric returns to normal within threshold period)
```

### Alert Rules

Configure alert rules to define when alerts are generated:

| Setting              | Description                                  |
|---------------------|----------------------------------------------|
| Name                | Rule identifier                              |
| Description         | Detailed explanation of the rule's purpose   |
| Enabled             | Toggle rule on/off without deleting          |
| Severity            | Default severity for triggered alerts        |
| Metric              | What to monitor (CPU, memory, disk, process, event, etc.) |
| Aggregation         | How to aggregate: average, max, min, sum     |
| Operator            | Comparison: greater_than, less_than, equal, not_equal |
| Threshold           | The value that triggers the alert            |
| Duration            | How long the condition must persist          |
| Computer Group      | Target group (or all computers)              |
| Notification Channels | Email, SMS, Teams, Webhook, Slack         |

**Pre-built rule templates:**
- High CPU Usage (>90% for 10 min)
- High Memory Usage (>90% for 10 min)
- Low Disk Space (<10% free)
- Suspicious Process Detected
- Multiple Failed Logins (>5 in 5 min)
- USB Device Connected
- Unknown Process Elevation
- Service Crash Detected

---

## Automation Workflows

### Workflow Designer

Automation workflows allow you to define automated responses to events and alerts.

**Workflow components:**

```
Trigger ──> Conditions ──> Actions
   │            │              │
   └── What     └── When      └── What to do
       starts       should        when triggered
       this         this run?
       workflow?
```

**Triggers:**
| Type        | Description                                  |
|-------------|----------------------------------------------|
| Event       | Fires when an alert is created, acknowledged, resolved |
| Schedule    | Cron-like scheduling (every day at 2 AM, every hour) |
| Webhook     | External system calls a webhook URL          |
| Computer    | Computer goes online/offline, agent connects/disconnects |

**Conditions:**
| Type        | Example                                      |
|-------------|----------------------------------------------|
| Comparison  | `alert.severity >= High`                     |
| Contains    | `alert.ruleName contains 'Crypto'`           |
| In List     | `computer.group in ['Finance', 'Executive']` |
| Time Window | `between 08:00 and 18:00`                    |
| Composite   | `AND`, `OR`, `NOT` of multiple conditions    |

**Actions:**
| Type              | Description                                  |
|-------------------|----------------------------------------------|
| Remote Command    | Execute a PowerShell or CMD command          |
| Kill Process      | Terminate a process by name or PID           |
| Send Notification | Email, SMS, Teams, Slack, Webhook            |
| API Call          | Call an external REST API                    |
| Create Alert      | Generate a new alert                         |
| Isolate Computer  | Block network access (via Windows Firewall or integration) |
| Run Script        | Execute a predefined script                  |
| Update Config     | Change agent configuration remotely          |

### Creating a Workflow

1. Navigate to **Automation** > **Workflows**
2. Click **Create Workflow**
3. Enter a name and description
4. Select the **trigger** type and configure
5. Add **conditions** (optional -- leave empty to run on all triggers)
6. Add one or more **actions** in sequence
7. Click **Save** and toggle the workflow **Enabled**
8. Use **Test** to simulate a trigger and verify behavior

### Workflow Templates

Pre-built workflow templates:

| Template                | Description                                  |
|------------------------|----------------------------------------------|
| Auto-kill Crypto Miners| Terminates known crypto mining processes     |
| Alert Escalation       | Escalates unacknowledged alerts after 15 min |
| USB Device Alert       | Notifies security team on new USB devices    |
| Offline Computer Alert | Alerts if critical server goes offline       |
| Patch Tuesday Reminder | Sends notification on second Tuesday of month |
| Security Incident Response | Isolates computer, notifies team, creates ticket |

### Execution Logs

Each workflow execution is logged with:

- **Workflow**: Name and version
- **Trigger**: What initiated the execution
- **Conditions**: Which conditions were evaluated and their results
- **Actions**: Each action's status (Pending, Running, Success, Failed)
- **Output**: Command results (stdout, stderr, exit code)
- **Timing**: Start time, duration per action, total duration
- **Errors**: Detailed error messages for failed actions

---

## AI Assistant

### Overview

The AI Assistant provides a natural language interface for querying your infrastructure, generating reports, and performing actions. It uses a combination of structured data retrieval and AI-powered analysis.

### Example Queries

| Query                                      | Result                                      |
|--------------------------------------------|---------------------------------------------|
| "Show me all computers with CPU > 80%"     | Table of matching computers with metrics    |
| "What alerts fired in the last hour?"      | Summary and list of recent alerts           |
| "Compare CPU usage between Finance and Engineering groups" | Chart and comparison table |
| "Generate a weekly security report"        | Creates and opens a formatted report        |
| "Why was alert ABC123 triggered?"          | Detailed explanation with rule context      |
| "Schedule a workflow to restart SERVER-DB every Sunday at 3 AM" | Creates workflow automatically |
| "Show trend of critical alerts over last 30 days" | Timeline chart with severity breakdown |

### Suggested Actions

After each response, the AI Assistant can suggest follow-up actions:

- **Create Alert**: Generate an alert based on the findings
- **Execute Command**: Run a remediation command on affected computers
- **Generate Report**: Create a formal report from the data
- **Create Workflow**: Turn the query into a recurring automation workflow
- **Export Data**: Download the results as CSV or JSON

### Query History

All queries are logged with:
- The prompt and response
- Execution time and tokens used
- User feedback (thumbs up/down)

Use the **History** tab to review past queries and reuse successful prompts.

### Feedback

After each query, rate the response:
- **Thumbs Up**: Helpful response, accurate data
- **Thumbs Down**: Incorrect or unhelpful -- provide optional correction

Feedback improves the AI Assistant over time.

---

## Reports

### Available Reports

| Report                  | Description                                  |
|------------------------|----------------------------------------------|
| Security Summary       | Overview of security events, alerts, trends  |
| Computer Inventory     | Full hardware/software inventory across all endpoints |
| Alert Analysis         | Alert volume, severity distribution, MTTR    |
| Compliance Report      | LGPD/GDPR compliance status and findings     |
| Automation Audit       | Workflow execution history and success rates |
| Agent Status           | Agent versions, update compliance, offline list |
| User Activity          | User logins, command executions, changes     |
| Custom Report          | Build your own with selected metrics and filters |

### Generating Reports

1. Navigate to **Reports**
2. Click **Generate Report**
3. Select report **type**
4. Configure **parameters** (date range, computers, groups)
5. Choose **format** (PDF, CSV, JSON, HTML)
6. Click **Generate**

Reports are generated asynchronously. You will be notified when complete.

### Scheduled Reports

Reports can be scheduled for automatic generation:

- **Frequency**: Daily, weekly, monthly, quarterly
- **Distribution**: Email to specified recipients, save to shared folder, post to webhook
- **Retention**: Automatically delete after X days or keep indefinitely

---

## Audit

### Audit Trail

The audit trail records all sensitive operations for compliance and investigation purposes.

**Logged events:**
- Authentication events (logins, logouts, 2FA)
- User management (create, update, delete)
- Computer access (command execution, screen capture, remote assistance)
- Alert operations (acknowledge, resolve, escalate)
- Configuration changes (settings, roles, permissions)
- Report generation and export

**Viewing the Audit Trail:**

1. Navigate to **Administration** > **Audit**
2. Filter by:
   - Date range
   - User
   - Action type
   - Resource type
   - Outcome (success / failure)
3. Click any entry to view full details including request/response data

### Exporting Audit Logs

1. Set date range
2. Choose format (CSV or JSON)
3. Click **Export**
4. File is generated and downloaded

---

## Administration

### User Management

Create, edit, and manage user accounts.

| Field            | Description                              |
|-----------------|------------------------------------------|
| Name             | User's display name                      |
| Email            | Login identifier and notification address |
| Role             | RBAC role (Administrator, SecurityAnalyst, Operator, Auditor, ReadOnly) |
| Active           | Enable/disable account access            |
| MFA Enabled      | Two-factor authentication status         |
| Last Login       | Timestamp of last successful login       |
| Created At       | Account creation date                    |

**Bulk user operations:**
- Import users from CSV
- Assign roles to multiple users
- Send welcome emails
- Enable/disable MFA in bulk

### Role Management

Roles define a set of permissions that grant access to features.

Pre-defined roles cannot be deleted but can be customized:

| Role               | Default Permissions                          |
|--------------------|---------------------------------------------|
| Administrator      | Full access to all features                 |
| SecurityAnalyst    | Alerts, computers, reports, AI              |
| Operator           | Dashboard, alert acknowledgment             |
| Auditor            | Read-only audit log and reports             |
| ReadOnly           | Dashboard and reports view only             |

Custom roles can be created with any combination of granular permissions.

### System Settings

| Setting                | Description                              |
|-----------------------|------------------------------------------|
| General               | Platform name, timezone, date format     |
| Security              | Password policy, session timeout, MFA enforcement |
| Notifications         | SMTP configuration, notification channels |
| Agent                 | Default agent configuration, update channel |
| Integration           | Azure AD, LDAP, webhook endpoints        |
| Retention             | Data retention periods for each category |
| Rate Limiting         | API rate limit configuration             |

### Audit Log

See the **Audit** section above.

---

## NOC Mode

NOC (Network Operations Center) Mode provides a full-screen, real-time view of your infrastructure optimized for wall displays and monitoring stations.

### Activating NOC Mode

1. Click the **NOC Mode** button in the top navigation bar
2. The interface switches to full-screen mode
3. Press `Esc` or click **Exit NOC Mode** to return

### NOC Display Panels

| Panel             | Content                                      |
|-------------------|----------------------------------------------|
| Global Status     | Total / Online / Offline / Alert counts      |
| Alert Feed        | Real-time scrolling list of new alerts       |
| Heat Map          | Geographic or group-based alert density      |
| Top CPU           | Computers sorted by CPU usage                |
| Top Memory        | Computers sorted by memory usage             |
| Alert Timeline    | Alert volume chart (last 24 hours)           |
| Recent Activity   | Live feed of agent connections, commands     |

### NOC Configuration

Customize NOC Mode display:

- **Layout**: Choose panel arrangement (2x2, 3x2, 4x3)
- **Refresh Rate**: 5s, 15s, 30s, 60s
- **Alert Filter**: Show all alerts or only Critical/High
- **Sound Alerts**: Enable/disable audible alert on new critical alerts
- **Theme**: Light, Dark, or High-Contrast

---

## Keyboard Shortcuts

| Shortcut              | Action                     |
|----------------------|----------------------------|
| `g` then `d`         | Go to Dashboard            |
| `g` then `c`         | Go to Computers            |
| `g` then `a`         | Go to Alerts               |
| `g` then `w`         | Go to Workflows            |
| `g` then `i`         | Go to AI Assistant         |
| `g` then `r`         | Go to Reports              |
| `g` then `s`         | Go to Settings             |
| `/`                   | Focus search               |
| `Shift+?`            | Show keyboard shortcuts    |
| `Esc`                | Close modal / cancel       |
| `n` (on Alerts page) | Show next alert            |
| `p` (on Alerts page) | Show previous alert        |
| `a` (on Alert detail)| Acknowledge alert          |
| `r` (on Alert detail)| Resolve alert              |
| `.`                  | Toggle NOC Mode            |

---

## Notifications

### Notification Channels

| Channel    | Description                                  |
|-----------|----------------------------------------------|
| In-App    | Bell icon in the top navigation bar          |
| Email     | Configured SMTP server                       |
| SMS       | Twilio or similar provider                   |
| Microsoft Teams | Incoming webhook to Teams channel      |
| Slack     | Incoming webhook to Slack channel            |
| Webhook   | Custom HTTP endpoint                         |
| Push      | Browser push notifications (desktop)         |

### Notification Settings

Configure per user in **Profile** > **Notifications**:

- **Alert Severity**: Which severity levels trigger notifications
- **Alert Rules**: Subscribe to specific rule notifications
- **Computer Status**: Online/offline notifications for specific computers
- **Workflow Status**: Execution success/failure notifications
- **Digest**: Daily or weekly email digest of all activity

---

## Troubleshooting

### Common Issues

| Issue                         | Solution                                    |
|-------------------------------|---------------------------------------------|
| Cannot log in                 | Check credentials, reset password, contact admin |
| Dashboard not loading         | Check internet connection, clear browser cache |
| Computers showing Offline     | Verify agent is running on the endpoint     |
| Alerts not triggering         | Check alert rule is enabled and configured  |
| AI Assistant not responding   | Check AI service status in System Settings  |
| Report generation stuck       | Cancel and regenerate the report            |
| Notifications not sending     | Verify SMTP/webhook configuration           |

### Getting Help

- **Documentation**: Refer to the docs section
- **Status Page**: Check `https://sentinela.yourcompany.com/status` for service status
- **Support**: Email `support@sentinela.local` or click **Help** > **Contact Support**
- **Feature Requests**: Submit via **Help** > **Feature Request**
