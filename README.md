# Sentinela

**Enterprise-Grade Endpoint Monitoring, Security, and Automation Platform**

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)](https://github.com/your-org/sentinela/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![Version](https://img.shields.io/badge/version-2.1.0-blue)](https://github.com/your-org/sentinela/releases)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](CONTRIBUTING.md)
[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/download)
[![React 19](https://img.shields.io/badge/React-19-61DAFB)](https://react.dev)

---

Sentinela provides real-time visibility, security monitoring, and automated response for your entire Windows endpoint fleet. From a single pane of glass, monitor computer health, detect security threats, trigger automated workflows, and investigate incidents with AI-powered assistance.

![Sentinela Dashboard](https://via.placeholder.com/800x450/1a1a2e/e94560?text=Sentinela+Dashboard+Screenshot)

---

## Key Features

- **Real-Time Endpoint Monitoring** -- CPU, memory, disk, network, processes, and sessions with live updates via SignalR
- **Security Event Detection** -- Windows security log monitoring, failed login detection, USB device tracking, and suspicious process alerts
- **Intelligent Alerting** -- Configurable alert rules with severity levels, thresholds, duration windows, and escalation policies
- **Automation Workflows** -- Trigger-based workflows that execute remote commands, kill processes, send notifications, or call external APIs
- **Security Correlation Engine** -- Pattern detection across multiple events to identify complex attack sequences
- **AI Assistant** -- Natural language querying of your infrastructure, automated report generation, and suggested remediation actions
- **Remote Command Execution** -- Run PowerShell, CMD, or batch commands on any endpoint with real-time output streaming
- **Remote Assistance** -- Secure screen viewing and remote control sessions with full audit logging
- **Screen Capture** -- On-demand or scheduled screenshots for compliance and investigation
- **Role-Based Access Control** -- Granular permissions with RBAC, 2FA, SSO integration, and full audit trail
- **NOC Mode** -- Full-screen real-time dashboard optimized for wall displays and operations centers
- **LGPD/GDPR Compliance** -- Built-in data retention policies, user data export/deletion, and compliance reporting

---

## Quick Start

### Prerequisites

- [Docker](https://docs.docker.com/get-docker/) (24+)
- [Docker Compose](https://docs.docker.com/compose/install/) (2.24+)

### Run Sentinela in 5 Minutes

```bash
# Clone the repository
git clone https://github.com/your-org/sentinela.git
cd sentinela

# Configure environment
cp .env.example .env
# Edit .env with your settings (or use defaults for local development)

# Start all services
docker compose up -d

# Apply database migrations
docker compose exec api dotnet Sentinela.Api.dll --apply-migrations

# Create admin user
docker compose exec api dotnet Sentinela.Api.dll --seed-admin --email admin@example.com --password "SecureP@ss123"

# Open the web interface
open https://localhost:3000
```

### Install the Windows Agent

Download the latest agent installer from the **Administration** > **Agent Downloads** page, or deploy via GPO:

```powershell
SentinelaAgent-Installer.exe /S /ServerUrl=https://sentinela.yourcompany.com:5001 /TenantId=your-tenant-id
```

---

## Architecture Overview

Sentinela follows **Clean Architecture** with **Domain-Driven Design** principles, ensuring maintainability, testability, and scalability.

```
                    +------------------+
                    |  React SPA (Web) |
                    |    Port 3000     |
                    +--------+---------+
                             |
                    +--------+---------+
                    |  API Gateway     |
                    |  Port 5001       |
                    +--------+---------+
                             |
        +--------------------+--------------------+
        |                    |                    |
+-------+--------+  +--------+-------+  +--------+-------+
| Alert Engine   |  | Automation     |  | Correlation    |
| (Worker)       |  | (Worker)       |  | (Worker)       |
+-------+--------+  +--------+-------+  +--------+-------+
        |                    |                    |
        +--------------------+--------------------+
                             |
              +--------------+--------------+
              |              |              |
        +-----+-----+ +-----+-----+ +-----+-----+
        |  RabbitMQ  | |   Redis   | |PostgreSQL |
        | (Message)  | |  (Cache)  | |   (DB)    |
        +-----------+ +-----------+ +-----------+
                             |
              +--------------+--------------+
              |         Windows Agent       |
              |  (Per Endpoint, .NET 9)     |
              +-----------------------------+
```

### Technology Stack

| Layer          | Technologies |
|---------------|--------------|
| **Backend**   | .NET 9, ASP.NET Core, Entity Framework Core, MediatR, SignalR, FluentValidation, Serilog |
| **Frontend**  | React 19, Vite 6, TypeScript 5, Tailwind CSS 4, TanStack Query, Zustand, Recharts, Framer Motion |
| **Agent**     | .NET Worker Service, Windows Service, WMI, P/Invoke, SQLite |
| **Infrastructure** | PostgreSQL 16, Redis 7, RabbitMQ 3.13, NGINX, Prometheus, Grafana, Seq |

---

## Project Structure

```
sentinela/
├── src/
│   ├── BuildingBlocks/
│   │   └── Sentinela.Shared/         # Shared Kernel (base classes, value objects)
│   ├── Infrastructure/
│   │   ├── Sentinela.Persistence/    # EF Core, PostgreSQL
│   │   ├── Sentinela.MessageBus/     # RabbitMQ
│   │   └── Sentinela.Caching/        # Redis
│   ├── Services/
│   │   ├── Sentinela.Api/            # API Gateway + SignalR Hubs
│   │   ├── Sentinela.Agent/          # Windows Agent
│   │   ├── Sentinela.Identity/       # Authentication & Authorization
│   │   ├── Sentinela.AlertEngine/    # Alert Rule Evaluation
│   │   ├── Sentinela.Automation/     # Workflow Engine
│   │   ├── Sentinela.Correlation/    # Security Correlation
│   │   ├── Sentinela.ScreenCapture/  # Remote Screenshots
│   │   └── Sentinela.RemoteAssistance/ # Remote Control
│   └── Web/                          # React Frontend
├── tests/                            # Unit, Integration, Architecture tests
├── docker/                           # Dockerfiles & configuration
├── scripts/                          # CI & deployment scripts
└── docs/                             # Documentation
```

---

## Development Setup

### Prerequisites

| Tool               | Version   |
|-------------------|-----------|
| Visual Studio 2022| 17.12+    |
| .NET SDK          | 9.0+      |
| Node.js           | 20 LTS+   |
| Docker Desktop    | 4.30+     |
| Git               | 2.40+     |

### Setup Commands

```bash
# Start infrastructure services
docker compose up -d postgres redis rabbitmq seq

# Configure secrets (API)
cd src/Services/Sentinela.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:PostgreSQL" "Host=localhost;Port=5432;Database=sentinela;Username=sentinela;Password=sentinela"
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379"
dotnet user-secrets set "ConnectionStrings:RabbitMQ" "amqp://sentinela:sentinela@localhost:5672"
dotnet user-secrets set "Jwt:Secret" "development-secret-key-256-bits-minimum"

# Apply migrations
dotnet ef database update

# Start API
dotnet run

# Start Frontend (new terminal)
cd src/Web
npm install
npm run dev
```

Open `https://localhost:3000` in your browser.

---

## Deployment

### Production Docker Compose

```bash
# Configure environment
cp .env.example .env.production
# Edit .env.production with production values

# Generate security keys
openssl rand -base64 64 > jwt_secret.txt
openssl rand -base64 32 > encryption_key.txt

# Deploy
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

See [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) for detailed instructions including:
- SSL/TLS configuration with Let's Encrypt
- High availability setup
- Backup and restore procedures
- Monitoring with Prometheus and Grafana
- Agent deployment via GPO

---

## Documentation

| Document | Description |
|----------|-------------|
| [Architecture](docs/ARCHITECTURE.md) | Clean Architecture, DDD, CQRS, technology decisions |
| [API Reference](docs/API.md) | All endpoints, authentication, WebSocket hubs, rate limiting |
| [Agent Guide](docs/AGENT.md) | Installation, configuration, collected data, troubleshooting |
| [Deployment Guide](docs/DEPLOYMENT.md) | Docker setup, production considerations, GPO deployment |
| [Security](docs/SECURITY.md) | Authentication, authorization, encryption, compliance |
| [Contributing](docs/CONTRIBUTING.md) | Development setup, code conventions, testing, PR process |
| [User Guide](docs/USER_GUIDE.md) | Dashboard, alerts, automation, AI Assistant, NOC mode |

---

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

---

## Contact

- **Documentation**: [docs/](docs/)
- **Issues**: [GitHub Issues](https://github.com/your-org/sentinela/issues)
- **Security Reports**: security@sentinela.local
- **Support**: support@sentinela.local

---

<p align="center">Built with .NET 9, React 19, and a commitment to endpoint security.</p>
