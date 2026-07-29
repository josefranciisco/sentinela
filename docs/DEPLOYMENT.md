# Sentinela Deployment Guide

## Prerequisites

### Required Software

| Component          | Version      | Purpose                          |
|-------------------|-------------|----------------------------------|
| Docker            | 24+         | Container runtime                |
| Docker Compose    | 2.24+       | Multi-container orchestration    |
| .NET SDK          | 9.0+        | Local builds (development only)  |
| Node.js           | 20 LTS+     | Frontend builds (development only)|
| OpenSSL           | 3.x         | Certificate generation           |
| Git               | 2.40+       | Source control                   |

### Minimum Server Requirements (Production)

| Environment | CPU  | RAM   | Disk   | Network     |
|-------------|------|-------|--------|-------------|
| Small       | 4 vCPU | 16 GB | 100 GB SSD | 1 Gbps |
| Medium      | 8 vCPU | 32 GB | 250 GB SSD | 1 Gbps |
| Large       | 16 vCPU| 64 GB | 500 GB SSD | 10 Gbps |

### Supported Operating Systems

- **Linux**: Ubuntu 22.04 / 24.04 LTS, Debian 12, RHEL 9
- **Windows**: Windows Server 2019 / 2022 / 2025 (for agent only)
- **Docker**: Linux containers (recommended), Windows containers (experimental)

---

## Environment Variables

### Core Variables

| Variable                   | Required | Default                        | Description                          |
|---------------------------|----------|--------------------------------|--------------------------------------|
| `ASPNETCORE_ENVIRONMENT`  | Yes      | Production                     | Runtime environment                  |
| `SENTINELA_DB_CONNECTION` | Yes      | —                              | PostgreSQL connection string         |
| `SENTINELA_REDIS_CONNECTION` | Yes   | —                              | Redis connection string              |
| `SENTINELA_RABBITMQ_CONNECTION`| Yes  | —                              | RabbitMQ connection string           |
| `SENTINELA_JWT_SECRET`    | Yes      | —                              | JWT signing key (base64, 256+ bits)  |
| `SENTINELA_JWT_ISSUER`    | Yes      | Sentinela                      | Token issuer                         |
| `SENTINELA_JWT_AUDIENCE`  | Yes      | sentinela-api                  | Token audience                       |
| `SENTINELA_ENCRYPTION_KEY`| Yes      | —                              | AES-256-GCM key for data at rest     |

### Identity Service

| Variable                           | Required | Default | Description                    |
|-----------------------------------|----------|---------|--------------------------------|
| `SENTINELA_IDENTITY_DB_CONNECTION`| No       | Falls back to SENTINELA_DB_CONNECTION | Identity-specific DB |
| `SENTINELA_MFA_ISSUER`            | No       | Sentinela | TOTP issuer name            |

### Alert Engine

| Variable                          | Required | Default | Description                    |
|----------------------------------|----------|---------|--------------------------------|
| `SENTINELA_ALERT_EVALUATION_INTERVAL` | No | 60 | Rule evaluation interval (seconds) |
| `SENTINELA_ALERT_MAX_RULES_PER_COMPUTER`| No | 20 | Max concurrent rules per computer |

### Automation

| Variable                          | Required | Default | Description                    |
|----------------------------------|----------|---------|--------------------------------|
| `SENTINELA_AUTOMATION_MAX_EXECUTIONS` | No | 100 | Max concurrent workflow executions |

### Frontend

| Variable          | Required | Default | Description              |
|------------------|----------|---------|--------------------------|
| `VITE_API_URL`   | Yes      | —       | API Gateway base URL     |
| `VITE_WS_URL`    | Yes      | —       | SignalR WebSocket URL    |
| `VITE_APP_TITLE` | No       | Sentinela | Browser tab title      |

### Email (SMTP)

| Variable                   | Required | Default | Description                    |
|---------------------------|----------|---------|--------------------------------|
| `SENTINELA_SMTP_HOST`     | Conditional | —   | SMTP server host              |
| `SENTINELA_SMTP_PORT`     | Conditional | 587 | SMTP port                     |
| `SENTINELA_SMTP_USERNAME` | Conditional | —   | SMTP username                 |
| `SENTINELA_SMTP_PASSWORD` | Conditional | —   | SMTP password                 |
| `SENTINELA_SMTP_FROM`     | Conditional | —   | From address                  |
| `SENTINELA_SMTP_USE_TLS`  | No       | true | Enable TLS                   |

---

## Docker Compose Deployment

### Quick Start (Development)

1. Clone the repository:

```bash
git clone https://github.com/your-org/sentinela.git
cd sentinela
```

2. Create environment file:

```bash
cp .env.example .env
```

Edit `.env` with required values (see Environment Variables section).

3. Start all services:

```bash
docker compose up -d
```

4. Apply database migrations:

```bash
docker compose exec api dotnet Sentinela.Api.dll --apply-migrations
```

5. Create initial admin user:

```bash
docker compose exec api dotnet Sentinela.Api.dll --seed-admin --email admin@sentinela.local --password "SecureP@ss123"
```

6. Access the platform:

| Service    | URL                          |
|-----------|------------------------------|
| Frontend  | https://localhost:3000       |
| API       | https://localhost:5001       |
| Seq       | https://localhost:5341       |
| RabbitMQ  | https://localhost:15672      |
| Grafana   | https://localhost:3001       |

---

### Production Deployment

1. Prepare environment:

```bash
cp .env.example .env.production
nano .env.production   # Configure all required variables
```

2. Generate JWT signing key:

```bash
openssl rand -base64 64 > jwt_secret.txt
```

3. Generate encryption key:

```bash
openssl rand -base64 32 > encryption_key.txt
```

4. Deploy with production compose file:

```bash
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

5. Configure health checks:

```bash
curl https://sentinela.yourcompany.com/api/v1/health
# Should return {"status":"Healthy",...}
```

---

### docker-compose.yml

```yaml
version: "3.8"

services:
  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: sentinela
      POSTGRES_USER: sentinela
      POSTGRES_PASSWORD: ${SENTINELA_DB_PASSWORD}
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./docker/postgres/init:/docker-entrypoint-initdb.d
    ports:
      - "5432:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U sentinela"]
      interval: 10s
      timeout: 5s
      retries: 5

  redis:
    image: redis:7-alpine
    command: redis-server --requirepass ${SENTINELA_REDIS_PASSWORD}
    volumes:
      - redis_data:/data
    ports:
      - "6379:6379"
    healthcheck:
      test: ["CMD", "redis-cli", "--raw", "incr", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5

  rabbitmq:
    image: rabbitmq:3.13-management-alpine
    environment:
      RABBITMQ_DEFAULT_USER: ${SENTINELA_RABBITMQ_USER}
      RABBITMQ_DEFAULT_PASS: ${SENTINELA_RABBITMQ_PASSWORD}
    volumes:
      - rabbitmq_data:/var/lib/rabbitmq
    ports:
      - "5672:5672"
      - "15672:15672"
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "check_port_connectivity"]
      interval: 15s
      timeout: 5s
      retries: 5

  api:
    build:
      context: .
      dockerfile: docker/api/Dockerfile
    environment:
      ASPNETCORE_ENVIRONMENT: ${ASPNETCORE_ENVIRONMENT:-Production}
      SENTINELA_DB_CONNECTION: "Host=postgres;Database=sentinela;Username=sentinela;Password=${SENTINELA_DB_PASSWORD}"
      SENTINELA_REDIS_CONNECTION: "redis:6379,password=${SENTINELA_REDIS_PASSWORD}"
      SENTINELA_RABBITMQ_CONNECTION: "amqp://${SENTINELA_RABBITMQ_USER}:${SENTINELA_RABBITMQ_PASSWORD}@rabbitmq:5672"
      SENTINELA_JWT_SECRET: ${SENTINELA_JWT_SECRET}
      SENTINELA_JWT_ISSUER: ${SENTINELA_JWT_ISSUER:-Sentinela}
      SENTINELA_JWT_AUDIENCE: ${SENTINELA_JWT_AUDIENCE:-sentinela-api}
      SENTINELA_ENCRYPTION_KEY: ${SENTINELA_ENCRYPTION_KEY}
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
      rabbitmq:
        condition: service_healthy
    ports:
      - "5001:8080"
    volumes:
      - api_logs:/app/logs
    restart: unless-stopped

  identity:
    build:
      context: .
      dockerfile: docker/identity/Dockerfile
    environment:
      ASPNETCORE_ENVIRONMENT: ${ASPNETCORE_ENVIRONMENT:-Production}
      SENTINELA_DB_CONNECTION: "Host=postgres;Database=sentinela;Username=sentinela;Password=${SENTINELA_DB_PASSWORD}"
      SENTINELA_REDIS_CONNECTION: "redis:6379,password=${SENTINELA_REDIS_PASSWORD}"
      SENTINELA_JWT_SECRET: ${SENTINELA_JWT_SECRET}
      SENTINELA_JWT_ISSUER: ${SENTINELA_JWT_ISSUER:-Sentinela}
      SENTINELA_JWT_AUDIENCE: ${SENTINELA_JWT_AUDIENCE:-sentinela-api}
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
    ports:
      - "5002:8080"
    restart: unless-stopped

  alert-engine:
    build:
      context: .
      dockerfile: docker/alert-engine/Dockerfile
    environment:
      SENTINELA_DB_CONNECTION: "Host=postgres;Database=sentinela;Username=sentinela;Password=${SENTINELA_DB_PASSWORD}"
      SENTINELA_RABBITMQ_CONNECTION: "amqp://${SENTINELA_RABBITMQ_USER}:${SENTINELA_RABBITMQ_PASSWORD}@rabbitmq:5672"
    depends_on:
      postgres:
        condition: service_healthy
      rabbitmq:
        condition: service_healthy
    restart: unless-stopped

  automation:
    build:
      context: .
      dockerfile: docker/automation/Dockerfile
    environment:
      SENTINELA_DB_CONNECTION: "Host=postgres;Database=sentinela;Username=sentinela;Password=${SENTINELA_DB_PASSWORD}"
      SENTINELA_RABBITMQ_CONNECTION: "amqp://${SENTINELA_RABBITMQ_USER}:${SENTINELA_RABBITMQ_PASSWORD}@rabbitmq:5672"
    depends_on:
      postgres:
        condition: service_healthy
      rabbitmq:
        condition: service_healthy
    restart: unless-stopped

  correlation:
    build:
      context: .
      dockerfile: docker/correlation/Dockerfile
    environment:
      SENTINELA_DB_CONNECTION: "Host=postgres;Database=sentinela;Username=sentinela;Password=${SENTINELA_DB_PASSWORD}"
      SENTINELA_RABBITMQ_CONNECTION: "amqp://${SENTINELA_RABBITMQ_USER}:${SENTINELA_RABBITMQ_PASSWORD}@rabbitmq:5672"
      SENTINELA_REDIS_CONNECTION: "redis:6379,password=${SENTINELA_REDIS_PASSWORD}"
    depends_on:
      postgres:
        condition: service_healthy
      rabbitmq:
        condition: service_healthy
      redis:
        condition: service_healthy
    restart: unless-stopped

  web:
    build:
      context: .
      dockerfile: docker/web/Dockerfile
      args:
        VITE_API_URL: ${VITE_API_URL:-https://sentinela.yourcompany.com/api}
        VITE_WS_URL: ${VITE_WS_URL:-wss://sentinela.yourcompany.com/hubs}
    ports:
      - "3000:80"
    depends_on:
      - api
    restart: unless-stopped

  nginx:
    image: nginx:alpine
    volumes:
      - ./docker/nginx/nginx.conf:/etc/nginx/nginx.conf:ro
      - ./docker/nginx/ssl:/etc/nginx/ssl:ro
    ports:
      - "80:80"
      - "443:443"
    depends_on:
      - api
      - web
      - identity
    restart: unless-stopped

  seq:
    image: datalust/seq:latest
    environment:
      ACCEPT_EULA: Y
    volumes:
      - seq_data:/data
    ports:
      - "5341:80"
    restart: unless-stopped

  prometheus:
    image: prom/prometheus:latest
    volumes:
      - ./docker/prometheus/prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus_data:/prometheus
    ports:
      - "9090:9090"
    restart: unless-stopped

  grafana:
    image: grafana/grafana:latest
    environment:
      GF_SECURITY_ADMIN_PASSWORD: ${GRAFANA_PASSWORD:-admin}
    volumes:
      - grafana_data:/var/lib/grafana
      - ./docker/grafana/dashboards:/etc/grafana/provisioning/dashboards
      - ./docker/grafana/datasources:/etc/grafana/provisioning/datasources
    ports:
      - "3001:3000"
    restart: unless-stopped

volumes:
  postgres_data:
  redis_data:
  rabbitmq_data:
  api_logs:
  seq_data:
  prometheus_data:
  grafana_data:
```

---

## Production Considerations

### High Availability Setup

For production, deploy multiple instances of each service:

```
┌──────────────┐
│   NGINX      │  ← Load balancer (round-robin)
│  (Active)    │
└──────┬───────┘
       │
       ├── API Instance 1 (port 5001)
       ├── API Instance 2 (port 5002)
       ├── API Instance 3 (port 5003)
       │
       ├── Identity 1, Identity 2
       │
       ├── PostgreSQL Primary → Streaming Replica
       ├── Redis Sentinel (HA)
       └── RabbitMQ Cluster (3 nodes)
```

### Database Connection Pooling

```env
SENTINELA_DB_CONNECTION=Host=postgres;Database=sentinela;Username=sentinela;Password=***;Maximum Pool Size=100;Min Pool Size=10;Connection Idle Lifetime=300;Connection Pruning Interval=60
```

### Security Hardening

1. **Never expose PostgreSQL, Redis, or RabbitMQ ports publicly**
2. Use secrets management (Docker secrets, HashiCorp Vault, or Azure Key Vault)
3. Enable TLS for all external endpoints
4. Configure IP allowlisting for admin endpoints
5. Enable audit logging for all configuration changes

---

## SSL / TLS Configuration

### Self-Signed Certificate (Development)

```bash
# Generate CA
openssl genrsa -out ca.key 4096
openssl req -new -x509 -days 365 -key ca.key -out ca.crt -subj "/CN=SentinelaDevCA"

# Generate server certificate
openssl genrsa -out sentinela.key 2048
openssl req -new -key sentinela.key -out sentinela.csr -subj "/CN=sentinela.local"
openssl x509 -req -days 365 -in sentinela.csr -CA ca.crt -CAkey ca.key -CAcreateserial -out sentinela.crt
```

### Let's Encrypt (Production)

```bash
docker compose -f docker-compose.yml -f docker-compose.prod.yml run --rm certbot certonly --webroot -w /var/www/certbot -d sentinela.yourcompany.com
```

### NGINX Configuration

```nginx
server {
    listen 443 ssl http2;
    server_name sentinela.yourcompany.com;

    ssl_certificate     /etc/nginx/ssl/sentinela.crt;
    ssl_certificate_key /etc/nginx/ssl/sentinela.key;
    ssl_protocols       TLSv1.2 TLSv1.3;
    ssl_ciphers         HIGH:!aNULL:!MD5;
    ssl_prefer_server_ciphers on;
    ssl_session_cache   shared:SSL:10m;
    ssl_session_timeout 10m;

    # HSTS
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;

    # Security headers
    add_header X-Frame-Options "SAMEORIGIN" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-XSS-Protection "1; mode=block" always;
    add_header Content-Security-Policy "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline';" always;

    # Frontend
    location / {
        proxy_pass http://web:80;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # API Gateway
    location /api/ {
        rewrite ^/api/(.*) /api/$1 break;
        proxy_pass http://api:8080;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # SignalR Hubs
    location /hubs/ {
        proxy_pass http://api:8080;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_read_timeout 86400s;
    }

    # Identity Service
    location /identity/ {
        rewrite ^/identity/(.*) /api/$1 break;
        proxy_pass http://identity:8080;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # Health endpoint (no auth needed)
    location /health {
        proxy_pass http://api:8080/health;
        proxy_set_header Host $host;
    }

    access_log /var/log/nginx/sentinela-access.log;
    error_log  /var/log/nginx/sentinela-error.log;
}

server {
    listen 80;
    server_name sentinela.yourcompany.com;
    return 301 https://$server_name$request_uri;
}
```

---

## Backup Strategy

### What to Backup

| Data              | Frequency | Retention     | Method                          |
|------------------|-----------|---------------|---------------------------------|
| PostgreSQL       | Daily     | 30 days       | pg_dump with encryption         |
| Redis            | Snapshot | N/A (cache)   | RDB snapshot                    |
| Configuration    | Per change | 90 days      | Git repository                  |
| Agent certificates| Per change | 90 days     | File system backup              |

### Backup Script

```bash
#!/bin/bash
# PostgreSQL backup
BACKUP_DIR="/backups/sentinela"
DATE=$(date +%Y%m%d_%H%M%S)
DB_PASSWORD=$(cat /run/secrets/db_password)

docker compose exec -T postgres pg_dump -U sentinela -d sentinela \
  --format=custom \
  --compress=9 \
  --file=/tmp/sentinela_${DATE}.dump

# Encrypt backup
openssl enc -aes-256-cbc -salt -in /tmp/sentinela_${DATE}.dump \
  -out ${BACKUP_DIR}/sentinela_${DATE}.dump.enc \
  -pass file:/run/secrets/backup_key

# Sync to remote storage
aws s3 cp ${BACKUP_DIR}/sentinela_${DATE}.dump.enc s3://sentinela-backups/

# Clean old backups (keep 30 days)
find ${BACKUP_DIR} -name "*.dump.enc" -mtime +30 -delete

# Restore (if needed)
# openssl enc -d -aes-256-cbc -in backup.dump.enc -out backup.dump -pass file:/run/secrets/backup_key
# docker compose exec -T postgres pg_restore -U sentinela -d sentinela --clean < backup.dump
```

---

## Monitoring Setup

### Prometheus Configuration

```yaml
# docker/prometheus/prometheus.yml
global:
  scrape_interval: 15s
  scrape_timeout: 10s

scrape_configs:
  - job_name: 'sentinela-api'
    static_configs:
      - targets: ['api:8080']
    metrics_path: '/metrics'

  - job_name: 'sentinela-identity'
    static_configs:
      - targets: ['identity:8080']
    metrics_path: '/metrics'

  - job_name: 'postgres'
    static_configs:
      - targets: ['postgres-exporter:9187']

  - job_name: 'redis'
    static_configs:
      - targets: ['redis-exporter:9121']

  - job_name: 'rabbitmq'
    static_configs:
      - targets: ['rabbitmq-exporter:9419']

  - job_name: 'node'
    static_configs:
      - targets: ['node-exporter:9100']
```

### Key Metrics to Monitor

| Metric                          | Alert Threshold                  |
|--------------------------------|----------------------------------|
| API response time p99          | > 1000 ms                        |
| Database connection pool usage | > 80%                            |
| RabbitMQ queue depth           | > 1000 unprocessed               |
| Redis memory usage             | > 80% of maxmemory               |
| Service health status          | Unhealthy for > 30 seconds       |
| Agent connectivity loss        | > 5% of agents offline           |
| Alert evaluation latency       | > 30 seconds                     |
| Disk space on host             | > 85%                            |

### Grafana Dashboards

Pre-configured dashboards are provisioned in `docker/grafana/dashboards/`:

- **Sentinela Overview**: High-level system health and performance
- **Agent Status**: Connected agents, versions, offline alerts
- **Alert Pipeline**: Alert volume, evaluation times, severity distribution
- **Infrastructure**: CPU, memory, disk, network per service

---

## Agent Deployment via GPO

### Prerequisites

1. Prepare the Sentinela Agent MSI package
2. Create a network share readable by all domain computers: `\\fileserver\software\sentinela\`
3. Prepare the `.admx` administrative template

### Steps

1. **Copy the MSI** to the network share
2. **Import Administrative Template:**
   - Copy `SentinelaAgent.admx` to `\\domain\sysvol\domain\Policies\PolicyDefinitions\`
   - Copy `SentinelaAgent.adml` to `\\domain\sysvol\domain\Policies\PolicyDefinitions\en-US\`

3. **Create GPO:**
   - Open Group Policy Management Console
   - Create new GPO: `Sentinela Agent Deployment`
   - Edit → Computer Configuration → Policies → Software Settings → Software Installation
   - Right-click → New → Package → Select `\\fileserver\software\sentinela\SentinelaAgent.msi`
   - Choose **Assigned** deployment method

4. **Configure Agent Settings:**
   - Computer Configuration → Administrative Templates → Sentinela → Agent
   - Set Server URL, Tenant ID, authentication settings
   - Configure collector intervals and enabled collectors

5. **Link GPO** to the target Organizational Unit

6. **Deployment:**
   - Agents install on next Group Policy refresh (default: 90-120 minutes)
   - Force immediate update: `gpupdate /force` on target computers

### GPO Deployment Script (Alternative)

If MSI deployment is not possible, use a startup script:

```powershell
# startup.ps1 — place in GPO startup scripts
$agentPath = "\\fileserver\software\sentinela\SentinelaAgent-Installer.exe"
$serverUrl = "https://sentinela.yourcompany.com:5001"
$tenantId = "your-tenant-id"

if (-not (Get-Service "SentinelaAgent" -ErrorAction SilentlyContinue)) {
    Start-Process -FilePath $agentPath -ArgumentList "/S", "/ServerUrl=$serverUrl", "/TenantId=$tenantId" -Wait -NoNewWindow
}
```
