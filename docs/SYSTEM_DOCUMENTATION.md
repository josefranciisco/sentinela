# Sentinela - Documentação do Sistema

**Data:** 05/08/2026  
**Versão:** 1.0.0

---

## 1. Visão Geral da Arquitetura

```
┌─────────────────────────────────────────────────────────────────────┐
│                         NGINX (Proxy)                              │
│                    localhost:3000 (web)                             │
│                    localhost:5002 (api)                             │
└─────────────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
        ▼                     ▼                     ▼
┌───────────────┐    ┌───────────────┐    ┌───────────────┐
│  Web (React)  │    │  API (.NET)   │    │  Identity     │
│   Port: 80    │    │  Port: 8080   │    │  Port: 8081   │
└───────────────┘    └───────────────┘    └───────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
        ▼                     ▼                     ▼
┌───────────────┐    ┌───────────────┐    ┌───────────────┐
│  PostgreSQL   │    │    Redis      │    │   RabbitMQ    │
│  Port: 5432   │    │  Port: 6379   │    │  Port: 5672   │
└───────────────┘    └───────────────┘    └───────────────┘
                              │
                              ▼
                    ┌───────────────┐
                    │  Agent (.NET) │
                    │  (Máquinas)   │
                    └───────────────┘
```

---

## 2. Containers Docker

| Container | Serviço | Porta Exposta | Status |
|-----------|---------|---------------|--------|
| `sentinela-web` | Frontend React + Nginx | 3000 → 80 | ✅ Rodando |
| `sentinela-api` | API ASP.NET Core | 5002 → 8080 | ✅ Rodando |
| `sentinela-identity` | Autenticação JWT | 5003 → 8081 | ✅ Rodando |
| `sentinela-postgres` | Banco PostgreSQL | 5432 | ✅ Rodando |
| `sentinela-redis` | Cache/SignalR | 6379 | ✅ Rodando |
| `sentinela-rabbitmq` | Message Broker | 5672, 15672 | ✅ Rodando |

---

## 3. Configurações de Streaming (Estado Atual)

### 3.1 Agent - RemoteSessionWorker

**Arquivo:** `src/Services/Sentinela.Agent/Workers/RemoteSessionWorker.cs`

| Configuração | Valor Atual | Linha |
|--------------|-------------|-------|
| Frame Interval | `150ms` (~6.7 FPS) | 82 |
| Max Width | `1920px` | 89 |
| JPEG Quality | `60` | 89 |

```csharp
// Linha 82-89
private readonly TimeSpan _frameInterval = TimeSpan.FromMilliseconds(150);

var frameData = await _screenCapture.CaptureForStreamingAsync(
    maxWidth: 1920,
    quality: 60,
    monitorIndex: state.MonitorIndex,
    cancellationToken: ct);
```

### 3.2 Agent - ScreenCaptureService

**Arquivo:** `src/Services/Sentinela.Agent/Core/Monitors/ScreenCaptureService.cs`

| Configuração | Valor Atual | Linha |
|--------------|-------------|-------|
| Quality Clamp | `20 - 80` | 128 |
| Fallback Resolution | `1920x1080` | 200, 203, 216, 240 |

### 3.3 Agent - AgentOptions

**Arquivo:** `src/Services/Sentinela.Agent/Configuration/AgentOptions.cs`

| Configuração | Valor Atual | Linha |
|--------------|-------------|-------|
| EnableScreenCapture | `false` | 10 |
| ScreenCaptureQuality | `50` | 11 |
| ScreenCaptureIntervalMs | `300000` (5 min) | 12 |
| HeartbeatIntervalMs | `10000` (10s) | 5 |
| CollectorIntervalMs | `1000` (1s) | 6 |
| BatchSendIntervalMs | `5000` (5s) | 7 |
| ReconnectDelayMs | `1000` (1s) | 25 |
| MaxReconnectDelayMs | `30000` (30s) | 26 |

### 3.4 RemoteAssistance Options

**Arquivo:** `src/Services/Sentinela.RemoteAssistance/Configuration/RemoteAssistanceOptions.cs`

| Configuração | Valor Atual | Linha | Observação |
|--------------|-------------|-------|------------|
| MaxConcurrentSessions | `50` | 6 | |
| SessionTimeoutMinutes | `60` | 7 | |
| MaxFileTransferSizeMB | `500` | 8 | |
| ScreenFrameQuality | `40` | 13 | ⚠️ NÃO USADO |
| ScreenFps | `10` | 14 | ⚠️ NÃO USADO |

---

## 4. Configurações SignalR

### 4.1 Servidor (API)

**Arquivo:** `src/Services/Sentinela.Api/Configuration/ApiServiceRegistration.cs`

| Configuração | Valor Atual | Linha |
|--------------|-------------|-------|
| MaximumReceiveMessageSize | `16 MB` | 93 |
| Protocolo | MessagePack | 95 |
| KeepAliveInterval | **NÃO CONFIGURADO** | - |
| ClientTimeoutInterval | **NÃO CONFIGURADO** | - |

### 4.2 Cliente Agent

**Arquivo:** `src/Services/Sentinela.Agent/Services/AgentHubClient.cs`

| Configuração | Valor Atual | Linha |
|--------------|-------------|-------|
| Reconnect Policy | Exponencial (1s, 2s, 4s, 8s, 16s, 30s) | 227-235 |
| MaxReconnectDelayMs | `30000` | 123 |
| MaximumReceiveMessageSize | **NÃO CONFIGURADO** (default 32KB) | - |

### 4.3 Cliente Frontend

**Arquivo:** `src/Web/src/hooks/useSignalR.ts`

| Configuração | Valor Atual | Linha |
|--------------|-------------|-------|
| Reconnect Delays | `[0, 2000, 5000, 10000, 30000]` | 14 |
| Transport | Default (WebSockets preferred) | - |
| serverTimeoutInMilliseconds | **NÃO CONFIGURADO** (default 30s) | - |
| keepAliveIntervalInMilliseconds | **NÃO CONFIGURADO** (default 15s) | - |

---

## 5. Configurações NGINX

**Arquivo:** `docker/nginx/nginx.conf`

### 5.1 HTTP Geral

| Configuração | Valor Atual | Linha |
|--------------|-------------|-------|
| sendfile | `on` | 28 |
| tcp_nodelay | `on` | 30 |
| keepalive_timeout | `65s` | 31 |
| client_max_body_size | `500M` | 33 |
| client_body_buffer_size | `128k` | 34 |
| proxy_connect_timeout | `90s` | 35 |
| proxy_send_timeout | `90s` | 36 |
| proxy_read_timeout | `90s` | 37 |
| proxy_buffers | `32 4k` | 38 |

### 5.2 WebSocket (SignalR)

| Configuração | Valor Atual | Linha |
|--------------|-------------|-------|
| proxy_read_timeout | `86400s` (24h) | 138 |
| proxy_send_timeout | `86400s` (24h) | 139 |
| Upgrade Header | `$http_upgrade` | 132 |
| Connection Header | `upgrade` | 133 |
| proxy_buffering | **NÃO DESABILITADO** | - |

### 5.3 Rate Limiting

| Configuração | Valor Atual | Linha |
|--------------|-------------|-------|
| API Rate Limit | `30r/s` por IP | 56 |
| API Burst | `50 nodelay` | 101 |
| Login Rate Limit | `5r/m` | 57 |
| Login Burst | `3 nodelay` | 80 |

---

## 6. Frontend - Renderização

**Arquivo:** `src/Web/src/pages/RemoteAssistance.tsx`

### 6.1 Exibição ao Vivo

| Configuração | Valor Atual | Linha |
|--------------|-------------|-------|
| Método | `<img src={liveFrame}>` | 348-353 |
| Formato | `data:image/jpeg;base64,...` | 152 |
| Throttling | **NENHUM** | - |
| Frame Skip | **NENHUM** | - |
| requestAnimationFrame | **NÃO USADO** | - |

### 6.2 Gravação

| Configuração | Valor Atual | Linha |
|--------------|-------------|-------|
| Canvas Size | `1920x1080` (hardcoded) | 171-172 |
| Capture Stream FPS | `30` | 178 |
| Codec | `video/webm;codecs=vp9` | 179 |
| Bitrate | `5 Mbps` | 182 |
| Chunk Interval | `1000ms` | 199 |

---

## 7. Banco de Dados

### 7.1 Tabelas Principais

| Tabela | Descrição |
|--------|-----------|
| `computers` | Máquinas monitoradas |
| `heartbeats` | Heartbeats dos agentes |
| `timeline_entries` | Linha do tempo de eventos |
| `security_events` | Eventos de segurança |
| `remote_sessions` | Sessões de assistência remota |
| `file_transfers` | Transferências de arquivos |
| `screen_captures` | Capturas de tela |
| `screenshots` | Screenshots agendados |
| `alert_rules` | Regras de alerta |
| `alerts` | Alertas gerados |
| `audit_trail` | Auditoria de ações |

### 7.2 Credenciais

| Serviço | Usuário | Senha | Database |
|---------|---------|-------|----------|
| PostgreSQL | `sentinela` | `sentinela` | `sentinela` |
| Redis | default | - | - |
| RabbitMQ | `guest` | `guest` | - |

### 7.3 Login API

| Usuário | Senha |
|---------|-------|
| `Admin` | `4517` |

---

## 8. Funcionalidades Implementadas

### 8.1 Streaming Remoto
- ✅ Captura de tela em tempo real via SignalR
- ✅ Seleção/troca de monitor ao vivo
- ✅ Gravação de sessão (WebM/VP9)
- ✅ Fullscreen
- ✅ Heartbeat com MonitorCount

### 8.2 Detecção de Ameaças
- ✅ ProcessCollector com CPU real
- ✅ CryptominerDetector (40+ processos conhecidos)
- ✅ RansomwareDetector (FileSystemWatcher + 35+ extensões)
- ✅ Cooldown de 5 minutos

### 8.3 Centro de Incidentes
- ✅ Agrupamento por computador+severidade
- ✅ Filtro de eventos comuns (noise events)
- ✅ Títulos inteligentes por tipo
- ✅ Modal de investigação com recomendações

### 8.4 Segurança
- ✅ Refresh token automático
- ✅ JWT com expiração de 15 minutos
- ✅ Filtro de eventos Bitdefender

### 8.5 Transferência de Arquivos
- ✅ Persistência em PostgreSQL (antes era in-memory)
- ✅ Validação de extensões bloqueadas
- ✅ Checksum SHA256

---

## 9. Problemas Conhecidos

1. **Streaming pouco fluido** - FPS baixo (~6.7), frontend sem otimização
2. **RemoteAssistanceOptions não utilizado** - ScreenFps e ScreenFrameQuality ignorados
3. **NGINX com buffering ativado** para WebSocket
4. **SignalR sem keepalive configurado** no servidor
5. **Frontend usando `<img>` em vez de `<canvas>`** para renderização

---

## 10. Plano de Otimização (Próximo Passo)

### 10.1 Agent
- Frame interval: `150ms` → `100ms` (10 FPS)
- JPEG quality: `60` → `75`
- Usar `RemoteAssistanceOptions` em vez de hardcoded

### 10.2 Frontend
- Trocar `<img>` por `<canvas>` com `requestAnimationFrame`
- Adicionar skip de frames antigos
- Evitar re-renders do React por frame

### 10.3 NGINX
- Adicionar `proxy_buffering off;` no path `/hubs/`

### 10.4 SignalR
- Configurar `KeepAliveInterval` no servidor

---

## 11. Endpoints Principais

### API (localhost:5002)

| Método | Endpoint | Descrição |
|--------|----------|-----------|
| POST | `/api/auth/login` | Login e obtenção de JWT |
| GET | `/api/v1/computers` | Listar computadores |
| GET | `/api/v1/computers/{id}` | Detalhes do computador |
| GET | `/api/v1/computers/{id}/timeline` | Timeline do computador |
| GET | `/api/v1/security/incidents` | Centro de incidentes |
| GET | `/api/v1/dashboard/overview` | Visão geral do dashboard |
| GET | `/api/v1/remote/sessions` | Listar sessões remotas |
| POST | `/api/v1/remote/request` | Solicitar sessão remota |
| POST | `/api/v1/remote/sessions/{id}/terminate` | Encerrar sessão |
| GET | `/api/v1/screencapture` | Listar capturas de tela |
| POST | `/api/v1/screencapture/request` | Solicitar captura |

### Hubs (SignalR)

| Hub | Rota | Uso |
|-----|------|-----|
| AgentHub | `/hubs/agent` | Comunicação agente-servidor |
| RemoteAssistanceHub | `/hubs/remote` | Streaming ao vivo |
| MonitoringHub | `/hubs/monitoring` | Monitoramento em tempo real |
| AlertHub | `/hubs/alerts` | Alertas em tempo real |

---

## 12. URLs de Acesso

| Serviço | URL |
|---------|-----|
| Dashboard | http://localhost:3000 |
| API | http://localhost:5002 |
| Identity | http://localhost:5003 |
| RabbitMQ Management | http://localhost:15672 |
