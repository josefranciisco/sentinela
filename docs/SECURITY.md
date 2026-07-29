# Sentinela Security Documentation

## Overview

Security is a foundational aspect of the Sentinela platform. This document covers authentication, authorization, encryption, network security, audit logging, regulatory compliance, and incident response procedures.

---

## Authentication Flow

### Standard Login

```
User                      Frontend                  API Gateway               Identity Service            Database
  │                          │                          │                          │                       │
  │  Enter credentials       │                          │                          │                       │
  │─────────────────────────>│                          │                          │                       │
  │                          │  POST /auth/login        │                          │                       │
  │                          │─────────────────────────>│                          │                       │
  │                          │                          │  Authenticate            │                       │
  │                          │                          │─────────────────────────>│                       │
  │                          │                          │                          │  Validate credentials  │
  │                          │                          │                          │──────────────────────>│
  │                          │                          │                          │<──────────────────────│
  │                          │                          │                          │                       │
  │                          │                          │  Generate JWT + Refresh  │                       │
  │                          │                          │  Store refresh token hash│                       │
  │                          │                          │─────────────────────────>│                       │
  │                          │                          │                          │                       │
  │                          │<─────────────────────────│                          │                       │
  │                          │  Set refresh token cookie│                          │                       │
  │                          │  Return access token     │                          │                       │
  │<─────────────────────────│                          │                          │                       │
```

### JWT Token Structure

```json
{
  "header": {
    "alg": "RS256",
    "typ": "JWT",
    "kid": "key-id-2026-01"
  },
  "payload": {
    "sub": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "operator@sentinela.local",
    "name": "John Doe",
    "role": "SecurityAnalyst",
    "permissions": [
      "computers:read",
      "alerts:read",
      "alerts:acknowledge",
      "alerts:resolve",
      "workflows:read"
    ],
    "tenant": "your-tenant-id",
    "iat": 1721234567,
    "exp": 1721235467,
    "iss": "sentinela",
    "aud": "sentinela-api"
  },
  "signature": "RSASHA256(base64url(header).base64url(payload))"
}
```

### Token Lifetime

| Token          | Lifetime  | Storage               | Rotation                 |
|---------------|-----------|-----------------------|--------------------------|
| Access Token  | 15 min    | Memory (frontend)     | Via refresh token        |
| Refresh Token | 7 days    | HTTP-only secure cookie + DB (hashed) | Rotated on each use, old tokens invalidated |

### 2FA (TOTP)

Sentinela supports Time-based One-Time Password (TOTP) authentication per RFC 6238.

**Enabling 2FA:**
1. User navigates to Profile → Security → Enable 2FA
2. Server generates a secret key and returns a QR code (provisioning URI)
3. User scans QR code with authenticator app (Google Authenticator, Microsoft Authenticator, Authy)
4. User enters the current 6-digit code to verify setup
5. Backup codes (8 codes, single-use) are generated and displayed

**Verification flow:**
1. Standard login returns `X-Requires-2FA: true` header + 401
2. Frontend shows 2FA code input
3. User submits `POST /auth/verify-2fa` with email + code
4. Server validates TOTP code against stored secret
5. On success, returns access + refresh tokens

### SSO / External Identity Providers

Sentinela supports integration with:

- **Azure Active Directory** (OIDC / OAuth 2.0)
- **Active Directory Federation Services (ADFS)**
- **LDAP** (OpenLDAP, Active Directory)
- **Okta**
- **Any SAML 2.0 provider**

Configuration is managed via environment variables and database settings.

---

## Authorization Model

### Role-Based Access Control (RBAC)

| Role               | Description                                      |
|--------------------|--------------------------------------------------|
| Administrator      | Full system access, configuration, user management |
| SecurityAnalyst    | Alert management, computer investigation, reports |
| Operator           | Dashboard viewing, basic alert acknowledgment    |
| Auditor            | Read-only access to audit logs and reports       |
| ReadOnly           | View dashboards, no state changes                |

### Granular Permissions

Permissions follow the pattern `resource:action`:

| Resource      | Actions                                    |
|---------------|--------------------------------------------|
| computers     | read, write, delete, execute, screen_capture |
| alerts        | read, acknowledge, resolve, create, delete  |
| workflows     | read, write, execute, delete, toggle       |
| alert_rules   | read, write, delete, test                  |
| users         | read, write, delete, mfa_manage            |
| roles         | read, write                                |
| audit         | read, export                               |
| reports       | read, write, delete, export                |
| ai            | query, view_history                        |
| settings      | read, write                                |
| groups        | read, write, delete, manage_members        |

### Permission Evaluation

```
Request → Authentication (JWT validated)
  → Authorization Middleware
    → Extract user ID, role, permissions from token
    → Match required permission against user's permissions
    → If resource-specific (e.g., computer ID), check resource-level policy
    → Allow / Deny
```

### Policy-Based Authorization

```csharp
// Example ASP.NET policy
services.AddAuthorization(options =>
{
    options.AddPolicy("ExecuteCommand", policy =>
        policy.RequirePermission("computers:execute"));
    
    options.AddPolicy("AcknowledgeAlert", policy =>
        policy.RequirePermission("alerts:acknowledge"));
});
```

---

## Data Encryption

### Encryption at Rest

| Data Type              | Encryption Method                          |
|-----------------------|--------------------------------------------|
| Database (PostgreSQL) | Transparent Data Encryption (TDE)          |
| Agent cache (SQLite)  | AES-256-CBC (key derived from machine key) |
| Configuration files   | AES-256-GCM (key in secure storage)        |
| Backups               | AES-256-CBC (dedicated backup key)         |
| Agent logs            | No sensitive data (PII stripped)           |

### Encryption in Transit

| Communication Path    | Protocol        | Cipher                      |
|----------------------|-----------------|-----------------------------|
| Browser → Server     | HTTPS / TLS 1.3 | TLS_AES_256_GCM_SHA384     |
| Agent → Server       | WSS / TLS 1.3   | TLS_AES_256_GCM_SHA384     |
| API → PostgreSQL     | TLS 1.3         | TLS_AES_256_GCM_SHA384     |
| API → Redis          | TLS (REDIS)     | AES-256-GCM                 |
| API → RabbitMQ       | AMQPS / TLS 1.3 | AES-256-GCM                 |
| Inter-service (Docker)| Internal network (TLS optional) | |

### Secrets Management

| Secret Type             | Storage Method                           |
|------------------------|------------------------------------------|
| JWT signing key        | Environment variable (prod), User Secrets (dev) |
| Database password      | Docker secrets / Environment variable    |
| Redis password         | Docker secrets / Environment variable    |
| RabbitMQ credentials   | Docker secrets / Environment variable   |
| SMTP password          | Encrypted database column                |
| Agent authentication   | Machine certificate + signed tokens      |
| Encryption keys        | Azure Key Vault / HashiCorp Vault        |

### Password Policies

| Policy                  | Requirement                             |
|------------------------|-----------------------------------------|
| Minimum length         | 12 characters                           |
| Character types        | Uppercase, lowercase, digit, special    |
| Password history       | Last 10 passwords                       |
| Maximum age            | 90 days                                 |
| Lockout threshold      | 5 failed attempts                       |
| Lockout duration       | 15 minutes                              |
| MFA enforcement        | Configurable per role / group           |
| Password hashing       | Argon2id (memory: 64MB, iterations: 3, parallelism: 4) |

---

## Network Security

### Architecture

```
Internet
    │
    ├── Firewall (only ports 80/443 open)
    │
    ├── WAF (Web Application Firewall — ModSecurity / Cloudflare)
    │
    ├── NGINX Reverse Proxy (TLS termination, rate limiting)
    │
    ├── DMZ Network
    │   ├── API Gateway (port 8080 internal)
    │   └── Identity Service (port 8080 internal)
    │
    ├── Application Network
    │   ├── Alert Engine (no public ports)
    │   ├── Automation (no public ports)
    │   └── Correlation (no public ports)
    │
    └── Data Network
        ├── PostgreSQL (port 5432 internal, TLS)
        ├── Redis (port 6379 internal, password)
        └── RabbitMQ (port 5671 internal, TLS)
```

### Docker Network Isolation

```
networks:
  frontend:
    - api
    - web
    - identity
    - nginx
  backend:
    - api
    - identity
    - alert-engine
    - automation
    - correlation
  data:
    - postgres
    - redis
    - rabbitmq

# Services only have access to networks they need.
# Frontend services cannot access the data network directly.
```

### Firewall Rules

| Source    | Destination | Port  | Protocol | Purpose              |
|-----------|-------------|-------|----------|----------------------|
| Internet  | NGINX       | 443   | TCP      | HTTPS                |
| Internet  | NGINX       | 80    | TCP      | HTTP → HTTPS redirect|
| Agents    | NGINX       | 443   | TCP      | Agent SignalR WSS    |
| NGINX     | API         | 8080  | TCP      | Reverse proxy        |
| NGINX     | Web         | 80    | TCP      | Static files         |
| API       | PostgreSQL  | 5432  | TCP      | Database (TLS)       |
| API       | Redis       | 6379  | TCP      | Cache (password)     |
| API       | RabbitMQ    | 5671  | TCP      | Message bus (TLS)    |
| All       | Seq         | 5341  | TCP      | Log aggregator (internal) |
| Admin     | Grafana     | 3001  | TCP      | Metrics dashboard    |

---

## Audit Logging

### Audit Events

All the following operations are logged with actor, action, resource, timestamp, and IP address:

| Category        | Events Logged                              |
|----------------|--------------------------------------------|
| Authentication | Login success/failure, logout, 2FA verify, 2FA enable/disable, token refresh, password change |
| User Management| Create/update/delete user, role assignment, permission change |
| Computer       | Remote command execution, screen capture, remote assistance start/stop |
| Alerts         | Acknowledge, resolve, escalate, comment, bulk operations |
| Automation     | Workflow create/update/delete/toggle, workflow execution, workflow test |
| Alert Rules    | Create/update/delete, enable/disable, test |
| Configuration  | System settings change, agent policy change, integration settings |
| Reports        | Report generation, export |
| AI             | Query submission (content logged), feedback |
| Admin          | System backup, restore, service restart, maintenance mode |

### Audit Log Format

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "timestamp": "2026-07-17T10:05:00.123Z",
  "actor": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "type": "user",
    "name": "John Doe",
    "email": "john@sentinela.local",
    "role": "SecurityAnalyst",
    "ipAddress": "192.168.1.50",
    "userAgent": "Mozilla/5.0..."
  },
  "action": "AlertAcknowledge",
  "resource": {
    "type": "Alert",
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "name": "High CPU - DESKTOP-ABC123"
  },
  "changes": {
    "previous": { "status": "New" },
    "current": { "status": "Acknowledged" }
  },
  "context": {
    "comment": "Investigating high CPU",
    "correlationId": "abc-123-def-456",
    "source": "web-ui"
  },
  "outcome": "success",
  "severity": "info"
}
```

### Audit Log Retention

| Environment | Retention | Storage       |
|-------------|-----------|---------------|
| Development | 30 days   | Database      |
| Staging     | 90 days   | Database      |
| Production  | 1 year    | Database + S3 cold storage |
| Compliance  | 5 years   | S3 (immutable, encrypted) |

### Audit Log Integrity

- Audit logs are **append-only** (no deletes, no updates)
- Each entry contains a hash of the previous entry (blockchain-style chain)
- Logs are signed with a server-side key
- Tamper detection: periodic verification of hash chain integrity

---

## Compliance (LGPD / GDPR)

### Data Classification

| Classification | Examples                              | Protection                      |
|---------------|---------------------------------------|---------------------------------|
| Public        | Platform version, system uptime       | No restrictions                 |
| Internal      | Computer names, IP addresses          | RBAC                            |
| Confidential  | Usernames, alert details, commands    | RBAC + encryption at rest      |
| Restricted    | Passwords, MFA secrets, personal data | Encryption + access logging + minimal retention |

### Personal Data Handling

| Data Point               | Collected | Purpose                          | Retention     |
|-------------------------|-----------|----------------------------------|---------------|
| User email              | Yes       | Authentication, notifications    | Until account deletion |
| User name               | Yes       | Display, audit trail             | Until account deletion |
| IP address (users)      | Yes       | Audit, security                  | 90 days       |
| Computer hostname       | Yes       | Monitoring, identification       | Until decommission |
| Logged-on user          | Yes       | Session tracking                 | 30 days       |
| Running process details | Yes       | Security monitoring              | 7 days        |
| Keystrokes/screen content | No      | Never collected                  | N/A           |

### User Rights

| Right                    | Implementation                      |
|-------------------------|--------------------------------------|
| Access                  | User can export all personal data    |
| Rectification           | User can update profile information  |
| Erasure                 | Account deletion removes personal data within 30 days |
| Restrict processing     | Suspend data collection on request   |
| Data portability        | Export in JSON format                |
| Object                  | Opt out of non-essential processing  |

### Data Processing Agreement (DPA)

Sentinela provides a standard DPA for enterprise customers covering:
- Data processing scope and purpose
- Sub-processors (hosting providers, AI model providers)
- Data transfer mechanisms (SCCs for cross-border)
- Security measures
- Breach notification procedures
- Return/deletion of data upon termination

---

## Incident Response

### Incident Severity Levels

| Level  | Name          | Description                                    | Response Time |
|--------|---------------|------------------------------------------------|---------------|
| P1     | Critical      | Active breach, data exposure, service down     | 15 min        |
| P2     | High          | Suspicious activity pattern, malware detected  | 1 hour        |
| P3     | Medium        | Policy violation, failed login spike           | 4 hours       |
| P4     | Low           | Informational, single failed login             | 24 hours      |

### Response Process

```
Detection
    │
    ├── Automated (Sentinela alerts, SIEM integration)
    ├── Manual (user report, admin observation)
    │
    ├──→ Triage & Classification
    │      ├── Assess severity (P1-P4)
    │      ├── Assign responder
    │      └── Create incident record
    │
    ├──→ Containment
    │      ├── Isolate affected computers (automated action)
    │      ├── Revoke compromised credentials
    │      ├── Block IPs / domains
    │      └── Preserve evidence (forensic snapshot)
    │
    ├──→ Eradication
    │      ├── Remove malware / unauthorized access
    │      ├── Patch vulnerabilities
    │      ├── Reset affected credentials
    │      └── Verify clean state
    │
    ├──→ Recovery
    │      ├── Restore from clean backup
    │      ├── Re-enable isolated systems
    │      ├── Monitor for recurrence
    │      └── Notify stakeholders
    │
    └──→ Post-Incident
           ├── Root cause analysis
           ├── Update detection rules
           ├── Improve response procedures
           └── Document lessons learned
```

### Automated Response Actions

| Trigger                            | Action                                          |
|------------------------------------|-------------------------------------------------|
| Malware detected on endpoint       | Isolate computer from network, notify admin     |
| Brute force detection (10+ failures)| Block source IP for 30 min, notify security    |
| Privilege escalation               | Disable account, trigger investigation workflow |
| Data exfiltration pattern           | Block outbound traffic, capture network session |
| Unknown device connection           | Alert admin, block USB port                     |

### Contact Information

| Role              | Contact Method     | Coverage     |
|------------------|--------------------|--------------|
| Security Team    | security@sentinela.local | 24/7    |
| Incident Response| +1-555-SENTINELA        | 24/7    |
| Compliance Officer| compliance@sentinela.local | Business hours |

---

## Security Checklist

### Pre-Deployment

- [ ] JWT signing key generated with sufficient entropy (256+ bits)
- [ ] Encryption key generated for data at rest
- [ ] Database password meets complexity requirements
- [ ] TLS certificates provisioned and configured
- [ ] Firewall rules configured (only 80/443 open)
- [ ] Docker network isolation configured
- [ ] Secrets not hardcoded in any file
- [ ] Default credentials changed
- [ ] Rate limiting configured
- [ ] CORS restricted to known origins

### Regular Operations

- [ ] Review audit logs weekly
- [ ] Rotate JWT signing key every 90 days
- [ ] Rotate database password every 90 days
- [ ] Update TLS certificates before expiry (30-day warning)
- [ ] Run vulnerability scan monthly
- [ ] Review user accounts and permissions quarterly
- [ ] Test backup restoration quarterly
- [ ] Conduct penetration test annually
- [ ] Review security event alert rules quarterly

### Compliance

- [ ] Data Processing Agreement signed with sub-processors
- [ ] Privacy policy published and accessible
- [ ] Consent mechanism implemented for data collection
- [ ] Data retention schedules applied
- [ ] User data export mechanism tested
- [ ] User data deletion mechanism tested
- [ ] Breach notification procedure documented
- [ ] DPO contact information published
