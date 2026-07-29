# Sentinela Architecture

## Overview

Sentinela is a modular, enterprise-grade endpoint monitoring, security, and automation platform. It follows **Clean Architecture** with **Domain-Driven Design (DDD)** principles, ensuring maintainability, testability, and adaptability for organizations of any scale.

The platform consists of three primary components:

- **Windows Agent** — runs on each monitored endpoint, collecting telemetry, enforcing policies, and executing remote commands
- **Backend Services** — a suite of .NET microservices handling API gateway, identity, alerting, automation, correlation, and AI assistance
- **Web Frontend** — a React single-page application providing real-time dashboards, management consoles, and NOC views

## High-Level Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────────────────┐
│                              Load Balancer / NGINX                                  │
├──────────────────────┬──────────────────────┬──────────────────────────────────────┤
│                      │                      │                                      │
│   React SPA (Web)    │   API Gateway        │   Identity Service                   │
│   Port 3000          │   Port 5001          │   Port 5002                          │
│                      │                      │                                      │
├──────────────────────┴──────────────────────┴──────────────────────────────────────┤
│                                                                                     │
│   ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌───────────────────┐        │
│   │Alert Engine │  │ Automation  │  │ Correlation │  │  AI Assistant     │        │
│   │(Worker)     │  │(Worker)     │  │(Worker)     │  │  (Service)        │        │
│   └──────┬──────┘  └──────┬──────┘  └──────┬──────┘  └───────────────────┘        │
│          │                │                │                                       │
├──────────┴────────────────┴────────────────┴───────────────────────────────────────┤
│                                                                                     │
│   ┌────────────────┐  ┌────────────────┐  ┌────────────────┐                       │
│   │   RabbitMQ     │  │    Redis       │  │  PostgreSQL    │                       │
│   │ (Message Bus)  │  │  (Cache/Dist)  │  │  (Persist)     │                       │
│   └────────────────┘  └────────────────┘  └────────────────┘                       │
│                                                                                     │
├─────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                     │
│   ┌─────────────────────────────────────────────────────────────────────────────┐  │
│   │                          Windows Agent (Per Endpoint)                        │  │
│   │                                                                             │  │
│   │  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────────────┐  │  │
│   │  │Collectors│ │Monitors  │ │ Health   │ │ IPC      │ │ SignalR Client   │  │  │
│   │  │ Activity │ │ USB      │ │ Watchdog │ │ Commands │ │ (Real-time)      │  │  │
│   │  │ Process  │ │ Security │ │ Self-Diag│ │          │ │                  │  │  │
│   │  │ Session  │ │ Events   │ │          │ │          │ │                  │  │  │
│   │  └──────────┘ └──────────┘ └──────────┘ └──────────┘ └──────────────────┘  │  │
│   └─────────────────────────────────────────────────────────────────────────────┘  │
│                                                                                     │
└─────────────────────────────────────────────────────────────────────────────────────┘
```

## Architecture Principles

### Clean Architecture
Dependencies point **inward**: Domain → Application → Infrastructure. The domain layer has zero external dependencies.

```
┌──────────────────────────────┐
│      Domain (Core)           │  ← Entities, Value Objects, Aggregates, Domain Events
├──────────────────────────────┤
│    Application (Use Cases)   │  ← Commands, Queries, DTOs, Interfaces
├──────────────────────────────┤
│     Infrastructure           │  ← Persistence, Message Bus, Cache, External APIs
├──────────────────────────────┤
│        Presentation          │  ← API Controllers, SignalR Hubs, UI
└──────────────────────────────┘
```

### Domain-Driven Design
- **Rich domain models** with encapsulated behavior and invariants
- **Bounded Contexts** with clear boundaries and ubiquitous language
- **Aggregates** enforcing consistency boundaries
- **Domain Events** for cross-context communication

### CQRS (Command Query Responsibility Segregation)
- **Commands** handle mutations via MediatR pipelines with validation, logging, and transaction management
- **Queries** handle reads through optimized projections and DTOs
- Read models are denormalized for performance; write models enforce domain rules

### Event-Driven Architecture
- **RabbitMQ** serves as the central message bus for async communication between services
- Domain Events are published to RabbitMQ exchanges
- Worker services consume messages from durable queues with dead-letter handling
- Guarantees at-least-once delivery with idempotent consumers

### Modular Monolith with Extractability
Each bounded context is a separate assembly with its own:
- Database schema (via EF Core entity configurations)
- Message handlers
- API endpoints
- Test project

Any bounded context can be extracted to a standalone microservice by:
1. Moving its assembly to a new process
2. Exposing its own HTTP endpoints
3. Sharing infrastructure via RabbitMQ and Redis

### Polyglot Persistence
| Store        | Purpose                               | Justification                     |
|-------------|---------------------------------------|-----------------------------------|
| PostgreSQL  | Relational data, transactions         | ACID compliance, rich queries     |
| Redis       | Cache, distributed locking, sessions  | Sub-millisecond latency           |
| SQLite      | Agent offline storage                 | Embedded, zero-config             |
| RabbitMQ    | Message queuing                       | Durable, routed messaging         |
| Seq         | Structured log aggregation            | Centralized querying              |
| Prometheus  | Metrics time-series                   | Pull-based monitoring             |

## Technology Stack

### Backend
| Technology               | Purpose                          |
|--------------------------|----------------------------------|
| .NET 9 / ASP.NET Core    | Web framework                    |
| Entity Framework Core    | ORM / data access                |
| Npgsql                   | PostgreSQL provider              |
| MediatR                  | CQRS command/query pipeline      |
| SignalR                  | Real-time bidirectional comms    |
| RabbitMQ.Client          | Message bus integration          |
| StackExchange.Redis      | Redis client                     |
| FluentValidation         | Input validation                 |
| Serilog                  | Structured logging               |
| Swagger / OpenAPI        | API documentation                |
| Refit                    | HTTP client generation           |
| Quartz.NET               | Job scheduling                   |

### Frontend
| Technology        | Purpose                      |
|-------------------|------------------------------|
| React 19          | UI framework                 |
| Vite 6            | Build tool / HMR            |
| TypeScript 5      | Type safety                  |
| Tailwind CSS 4    | Utility-first styling        |
| TanStack Query    | Server state / caching       |
| React Router 7    | Client-side routing          |
| Zustand           | Client state management      |
| Recharts          | Charting / data viz          |
| Framer Motion     | Declarative animations       |
| @microsoft/signalr| Real-time client             |

### Agent
| Technology            | Purpose                      |
|-----------------------|------------------------------|
| .NET Worker Service   | Long-running background proc |
| Windows Service       | NT Service integration       |
| Windows API / P/Invoke| Native OS interaction        |
| WMI / ManagementObject| System information queries   |
| SQLite                | Offline data cache           |
| SignalR Client        | Real-time server connection  |

### Infrastructure
| Technology        | Purpose                      |
|-------------------|------------------------------|
| Docker / Compose  | Container orchestration      |
| NGINX             | Reverse proxy / load balancer|
| PostgreSQL 16     | Primary database             |
| Redis 7           | Cache / distributed primitives|
| RabbitMQ 3.13    | Message broker               |
| Prometheus        | Metrics collection           |
| Grafana           | Metrics visualization        |
| Seq               | Log aggregation              |
| Health Checks UI  | Service health monitoring    |

## Project Structure

```
sentinela/
│
├── src/
│   ├── BuildingBlocks/
│   │   └── Sentinela.Shared/           # Shared Kernel — base classes, value objects, domain primitives
│   │
│   ├── Infrastructure/
│   │   ├── Sentinela.Persistence/      # EF Core DbContext, migrations, repositories
│   │   ├── Sentinela.MessageBus/       # RabbitMQ publisher/consumer abstractions
│   │   └── Sentinela.Caching/          # Redis cache service, distributed lock manager
│   │
│   ├── Services/
│   │   ├── Sentinela.Api/              # API Gateway — controllers, middleware, SignalR hubs
│   │   ├── Sentinela.Agent/            # Windows Agent — collectors, monitors, health
│   │   ├── Sentinela.Identity/         # Identity Service — auth, users, roles, 2FA
│   │   ├── Sentinela.AlertEngine/      # Alert Engine — rule evaluation, alert creation
│   │   ├── Sentinela.Automation/       # Automation Service — workflow triggers/actions
│   │   ├── Sentinela.Correlation/      # Correlation Engine — security event correlation
│   │   ├── Sentinela.ScreenCapture/    # Screen Capture Service — on-demand remote capture
│   │   └── Sentinela.RemoteAssistance/ # Remote Assistance — remote desktop/control
│   │
│   └── Web/                            # React SPA — frontend application
│
├── tests/
│   ├── Sentinela.Architecture.Tests/   # Enforces architectural rules (NetArchTest)
│   ├── Sentinela.Api.Tests/
│   ├── Sentinela.Agent.Tests/
│   ├── Sentinela.Identity.Tests/
│   ├── Sentinela.AlertEngine.Tests/
│   ├── Sentinela.Automation.Tests/
│   └── Sentinela.Correlation.Tests/
│
├── docker/
│   ├── api/                            # Dockerfile for API Gateway
│   ├── identity/                       # Dockerfile for Identity Service
│   ├── alert-engine/
│   ├── automation/
│   ├── correlation/
│   ├── web/                            # Dockerfile for React SPA (multi-stage)
│   ├── nginx/                          # nginx.conf
│   ├── postgres/                       # init scripts
│   ├── prometheus/                     # prometheus.yml
│   └── grafana/                        # dashboards provisioning
│
├── scripts/
│   ├── deploy/                         # Deployment automation scripts
│   └── ci/                             # CI pipeline scripts (GitHub Actions)
│
├── docs/
│   ├── ARCHITECTURE.md
│   ├── API.md
│   ├── AGENT.md
│   ├── DEPLOYMENT.md
│   ├── SECURITY.md
│   ├── CONTRIBUTING.md
│   └── USER_GUIDE.md
│
├── docker-compose.yml                  # Development environment
├── docker-compose.prod.yml             # Production overrides
├── .env.example                        # Environment variable template
└── README.md
```

## Security Architecture

### Authentication Flow
1. User submits credentials to `/api/identity/auth/login`
2. Service validates against PostgreSQL user store
3. On success, returns signed JWT (access token) + opaque refresh token
4. Access token (15 min) is sent as `Authorization: Bearer <token>`
5. Refresh token (7 days) is stored in HTTP-only secure cookie and used to rotate tokens silently
6. 2FA (TOTP) can be enforced per user or per role — requires additional `/verify` step

### Authorization Model
- **RBAC** with roles: Administrator, SecurityAnalyst, Operator, Auditor, ReadOnly
- **Granular permissions** per resource (e.g., `computers:read`, `alerts:acknowledge`, `workflows:execute`)
- Permissions are checked via policy-based authorization in ASP.NET Core
- All authorization decisions are audited

### Data Protection
| Layer          | Mechanism                            |
|---------------|--------------------------------------|
| Transport     | TLS 1.3 (HTTPS / WSS)               |
| Secrets       | Encrypted at rest (AES-256-GCM)     |
| Passwords     | Argon2id hashing                     |
| Tokens        | RS256-signed JWTs                    |
| Database      | Transparent Data Encryption (TDE)   |
| Configuration| User secrets (dev) / Azure Key Vault (prod) |

### API Security
- Rate limiting: 100 req/min per IP (configurable per endpoint)
- CORS restricted to known origins
- CSP, X-Frame-Options, X-Content-Type-Options headers
- Request validation via FluentValidation
- SQL injection protection via parameterized EF Core queries
- Anti-forgery tokens on state-changing endpoints

## Domain Model (Bounded Contexts)

### 1. Identity Context
```
User      → { Id, Name, Email, PasswordHash, MfaSecret, Roles }
Role      → { Id, Name, Permissions[] }
Session   → { UserId, RefreshToken, ExpiresAt, DeviceInfo }
AuditLog  → { UserId, Action, Resource, Timestamp, IpAddress }
```

### 2. Monitoring Context
```
Computer      → { Id, Name, Domain, OsVersion, AgentVersion, Status, LastHeartbeat }
Heartbeat     → { ComputerId, Timestamp, CpuUsage, MemoryUsage, DiskUsage, Processes }
TimelineEvent → { ComputerId, Timestamp, Category, EventType, Payload }
Application   → { ComputerId, Name, Version, InstallDate, Publisher }
```

### 3. Security Context
```
Alert          → { Id, ComputerId, RuleId, Severity, Status, CreatedAt, AcknowledgedBy }
SecurityEvent  → { ComputerId, EventId, ProviderName, LogName, Timestamp, Message }
Vulnerability  → { ComputerId, CveId, Severity, InstalledVersion, FixedVersion }
CorrelationRule → { Id, Name, Conditions[], Severity, AutoActions[] }
```

### 4. Automation Context
```
Workflow       → { Id, Name, TriggerType, Conditions[], Actions[], Enabled }
Trigger        → { Type (Event|Schedule|Webhook), Config }
Condition      → { Field, Operator, Value }
Action         → { Type (Command|Script|Notification|ApiCall), Config }
ExecutionLog   → { WorkflowId, TriggeredBy, Status, StartedAt, CompletedAt, Result }
```

### 5. Alerting Context
```
AlertRule      → { Id, Name, ComputerGroupId, Metric, Threshold, Duration, Severity }
Notification   → { AlertId, Channel (Email|Sms|Webhook|Teams), SentAt, Status }
EscalationPolicy → { Stages[], Timeouts[], TargetRoles[] }
```

### 6. Audit Context
```
AuditEntry     → { Id, Timestamp, ActorId, Action, ResourceType, ResourceId, Details, Ip }
ComplianceReport → { Id, Period, Framework, Status, Findings[] }
```

### 7. Analytics Context
```
Report         → { Id, Name, Type, Parameters, GeneratedAt, Data }
Insight        → { Id, ComputerId, Type, Summary, Confidence, GeneratedAt }
AiQuery        → { Id, UserId, Prompt, Response, TokensUsed, Timestamp }
```

## Data Flow

### Telemetry Pipeline
```
Agent (5s intervals)
  ├── Collect → CPU, RAM, Disk, Network, Processes
  ├── Batch → Buffer up to 50 events or 30s window
  ├── Compress → GZip serialized payload
  └── Send → SignalR Hub (WebSocket with reconnect)

API Gateway
  ├── Receive → Deserialize, validate, enrich with agent identity
  ├── Publish → RabbitMQ exchange `sentinela.telemetry`
  └── Acknowledge → SignalR callback to agent

Alert Engine (Worker)
  ├── Consume → Queue `sentinela.alerts.evaluate`
  ├── Evaluate → Run configured alert rules against telemetry
  ├── Create → Alert entity in PostgreSQL
  └── Publish → `sentinela.alerts.created` exchange

Correlation Engine (Worker)
  ├── Consume → `sentinela.alerts.created`
  ├── Analyze → Pattern detection, time-window correlation
  └── Create → Correlated alert (severity escalation)

Automation Engine (Worker)
  ├── Consume → `sentinela.alerts.*`, `sentinela.events.*`
  ├── Match → Evaluate workflow triggers and conditions
  └── Execute → Run action chain (scripts, notifications, API calls)

Frontend
  └── Subscribe → SignalR hub for real-time dashboard updates
```

### Request Flow (User Action)
```
User clicks "Acknowledge Alert"
  → React (TanStack Query mutation)
  → API Gateway (POST /api/alerts/{id}/acknowledge)
  → MediatR CommandHandler
    → Validate (FluentValidation)
    → Authorize (Policy check)
    → Execute (Update alert status, create audit entry)
    → Publish (DomainEvent: AlertAcknowledged)
  → Response (200 OK + updated alert DTO)
  → Frontend invalidates query cache → UI updates
```

## Scaling

### Horizontal Scale
| Component           | Strategy                                |
|--------------------|-----------------------------------------|
| API Gateway        | Stateless → add instances behind NGINX |
| Alert Engine       | Multiple workers consuming same queue  |
| Automation         | Partitioned queues per workflow type   |
| Correlation        | Single writer + read replicas          |
| Identity           | Stateless → add instances              |
| Frontend           | CDN + multiple replicas                |

### Message Broker Partitioning
- High-volume topics (telemetry) use partitioned exchanges with multiple consumers
- Dead-letter queues for failed messages with retry policies (3 retries + DLQ)
- Priority queues for alert acknowledgment vs telemetry

### Redis Usage
- **Cache**: Alert rules, user sessions, permission sets, configuration
- **Distributed Locking**: RedLock for critical sections (escalation timing)
- **SignalR Backplane**: WebSocket scale-out across multiple API instances
- **Rate Limiting**: Sliding window counters per IP/user

### Database Scaling
- **Read Replicas**: Reporting queries, analytics, dashboard queries
- **Connection Pooling**: Npgsql connection pool per service
- **Index Strategy**: Covering indexes for frequent query patterns
- **Archival**: Partitioned tables by month for telemetry data
- **CQRS Read Models**: Materialized views for dashboard queries

## Decision Records

### ADR-001: Modular Monolith over Microservices
**Context**: Need for rapid development with future scalability.
**Decision**: Start as modular monolith with explicit bounded contexts; extract to microservices when scaling demands.
**Rationale**: Faster iteration, simpler deployment, lower operational overhead. Clean boundaries make extraction cost low.

### ADR-002: SignalR over Polling
**Context**: Real-time agent communication and UI updates.
**Decision**: SignalR with WebSocket transport, falling back to Server-Sent Events.
**Rationale**: Persistent connections reduce latency, bandwidth, and server load compared to polling.

### ADR-003: RabbitMQ over Kafka
**Context**: Message bus for service communication.
**Decision**: RabbitMQ with topic exchanges and quorum queues.
**Rationale**: Better fit for command/event routing with complex topologies; lower operational complexity; sufficient throughput for our scale.

### ADR-004: PostgreSQL over SQL Server
**Context**: Primary relational database.
**Decision**: PostgreSQL 16.
**Rationale**: Lower licensing cost, rich feature set (JSON, full-text search, partitioning), strong community, Docker support.

### ADR-005: MediatR for CQRS
**Context**: Separating command and query responsibilities.
**Decision**: MediatR library with behavior pipelines for cross-cutting concerns.
**Rationale**: Lightweight, composable pipeline with validation, logging, transaction scoping, and audit behaviors.

### ADR-006: Clean Architecture over Vertical Slices
**Context**: Long-term maintainability.
**Decision**: Clean Architecture with DDD tactical patterns.
**Rationale**: Strong separation of concerns, testability, alignment with team's expertise, well-understood patterns.
