# Sentinela — Estado Atual (04/08/2026)

## Assistência Remota / Streaming ao Vivo

### Pipeline
Agente captura tela (`RemoteSessionWorker` a cada ~300ms, JPEG q60) → `AgentHub.SendRemoteScreenFrame` (grupo `session:{SessionId}`) → `RemoteAssistanceHub` → browser via `/hubs/remote?sessionId=...`

### API
- `Models/Dtos.cs`: `RemoteScreenFrameDto` (SessionId, FrameNumber, ImageBase64, Timestamp)
- `Hubs/AgentHub.cs`: `SendRemoteScreenFrame` encaminha ao `IHubContext<RemoteAssistanceHub>`
- `Controllers/v1/RemoteAssistanceController.cs`: cria sessão `Status="Active"` e notifica o agente com `StartRemoteSession`/`StopRemoteSession` (grupo `agent:{computerId}`), payload PascalCase `SessionId`/`SessionType`
- `Configuration/ApiServiceRegistration.cs`: `MaximumReceiveMessageSize = 16MB` (frames JPEG grandes)
- `Models/MappingProfile.cs`: `RemoteSession → RemoteSessionDto`; `RemoteSessionDto.ComputerName` adicionado (ainda vazio no response do request)

### Agente
- `Services/AgentHubClient.cs`: eventos `RemoteSessionStarted`/`RemoteSessionStopped`, handlers `StartRemoteSession`/`StopRemoteSession`, `SendRemoteScreenFrameAsync`
- `Workers/RemoteSessionWorker.cs`: loop de captura `CaptureCompressedAsync(quality: 60)` a cada ~300ms
- `Services/CommunicationService.cs`: `RemoteSessionRequest`, `RemoteSessionStartedEventArgs`, `RemoteSessionStoppedEventArgs`, `RemoteScreenFrameData`
- Registrado em `AgentServiceRegistration.cs`

### Frontend (`RemoteAssistance.tsx`)
- Conecta ao hub `/hubs/remote?sessionId=...` via `useSignalR`
- Trata `ScreenFrameReceived` e exibe `img data:image/jpeg;base64` com badge "Ao vivo #N"
- Chaves i18n `live`/`waitingStream` (pt-BR e en-US)

### Correção de queda de WebSocket (hoje)
- **Causa raiz**: `Cannot create a DbSet for 'ScreenCapture'` — o stub de `Api.Models.MissingTypes.cs` não estava no modelo EF; derrubava a conexão a cada envio periódico de `SendScreenCapture`
- **Fix**: movido para `Sentinela.Persistence/Models/ScreenCapture.cs` (+ enum `CaptureStatus`), `Configurations/ScreenCaptureConfiguration.cs` (`ToTable("ScreenCaptures")`), `DbSet<ScreenCapture>` no contexto; stubs duplicados removidos
- **Resultado**: API compila (0 erros), WebSocket estável (2 conexões em 10min, sem novas quedas), sessão de teste `7169a5e7...` com frames fluindo

### Status dos testes
- `POST /api/v1/remote/request` → 201/200 com sessão Active
- Log agente: `Remote session <id> started (view)`
- WebSocket permanece estável após correção do DbSet (antes caía a cada ~5min)
- Troca de monitor ao vivo testada: `SwitchMonitor(sessionId, index)` → agente loga `switched to monitor N`, frames continuam fluindo

### Seleção e Troca de Monitor (04/08)
- Agente: `ScreenCaptureService.GetMonitors()` (EnumDisplayMonitors) + `CaptureForStreamingAsync(maxWidth, quality, monitorIndex)` — captura monitor específico ou todos
- Agente: heartbeat reporta `MonitorCount`; `HeartbeatWorker` injeta `IScreenCaptureService`
- Agente: `RemoteSessionRequest.MonitorIndex`, evento `RemoteSessionMonitorChangedEventArgs`, handler `SwitchRemoteSessionMonitor` no `AgentHubClient`
- API: `AgentHeartbeatDto.MonitorCount`; `Computer.MonitorCount` (Shared + config + coluna `monitor_count`); `RequestSessionDto.MonitorIndex`; `RemoteSession.MonitorIndex` (coluna `monitor_index`)
- API: `RemoteAssistanceHub.SwitchMonitor(sessionId, monitorIndex)` atualiza a sessão e envia `SwitchRemoteSessionMonitor` ao agente
- Frontend: dropdown de monitor no dialog de solicitação + seletor ao vivo no player (usa `connection.invoke('SwitchMonitor', ...)`)
- **Nota**: tabela `screen_captures` precisou ser recriada — `ScreenCaptures` antiga tinha schema PascalCase; EF usa `UseSnakeCaseNamingConvention` (colunas `id`, `computer_id`, etc.)
- Máquina MOBI-45: 3 monitores detectados

### Pendente
- Popular `ComputerName` no response do `RequestSession` (enriquecer DTO com hostname)
- Confirmar renderização dos frames no browser na sessão selecionada

### Ajustes finos sugeridos (próxima sessão)
1. Ajustar FPS/qualidade do streaming (testar 10fps com intervalo 100ms, ou qualidade 70-80)
2. Popular `ComputerName` no response do `RequestSession`
3. Opções de controle no player (restart/shutdown/lock já têm botões; conectar a comandos reais)
4. Chat/PowerShell ao vivo (botões existentes sem handler)
5. Status visual de conexão do stream (latência, fps real)
6. Confirmar renderização final dos frames no browser

---

## Screenshot Capture

### Pipeline
`BoundingRectFromMonitors()` → `CaptureGdi` (PNG lossless) → `FindContentBounds` (auto-crop, threshold 15) → **PNG** (intermediário, `ImageFormat.Png`) → `CompressionService` (resize 3840x2160, JPEG q100)

### Agent (`ScreenCaptureOrchestrator.cs`)
- `MaxWidth = 3840`, `MaxHeight = 2160`
- `MonitorName` = contagem de monitores (ex: `"3 Monitores"`)

### Frontend (`ComputerDetail.tsx`)
| Parâmetro | Antes | Agora |
|-----------|-------|-------|
| Progresso | 5% / 750ms | **10% / 500ms** |
| Timeout 1ª invalidação | 15s | **10s** |
| Timeout 2ª invalidação | +7s | **+3s** |
| `refetchInterval` | 5s | **3s** |

### Qualidade
- PNG intermediário elimina dupla compressão JPEG
- `FindContentBounds` remove bordas pretas entre monitores desalinhados
- JPEG q100 sem compressão adicional

---

## Antivírus / Segurança

### Correções no agente (`ISecurityCollector.cs`)
- **`RealTimeProtectionEnabled`**: agora considera Bitdefender/terceiros (`defender.RTP || thirdParty.Any(p => p.IsEnabled)`)
- **`AntivirusSignatureAgeDays`**: = 0 quando terceiro está ativo e atualizado (evita 65535 do Defender desabilitado)
- **`AntivirusProductName`**: prioridade para terceiro ativo, encurtado via `SimplifyProductName` (ex: "Bitdefender Endpoint Security Tools Anti-malware" → "Bitdefender")
- **Evento "AntivirusDisabled"**: não é mais emitido quando Bitdefender está funcionando

### Frontend (`ComputerDetail.tsx`)
- Badge de segurança mostra `antivirusProductName` (ex: "Bitdefender") em vez de "Defender"
- Verifica `antivirusEnabled` em vez de `defenderEnabled`

---

## Frontend (Geral)

### Rotas (App.tsx)
5 rotas: Dashboard, Computadores, Usuários, Configurações, Login

### Sidebar
4 itens: Dashboard, Computadores, Usuários, Configurações

### ComputerDetail Tabs
Overview | Timeline | Transferências | Captura de Tela | Assist. Remota | Segurança

### Dashboard
- NOC mode com fullscreen toggle, relógio, auto-rotação de views
- Badges de segurança com dados reais do backend

---

## Agent

### Execução
- Iniciado via `Start-Process` (sobrevive ao shell)
- PID atual: flutuante (precisa verificar)
- `dist/agent/` publicada em `C:\Users\ti3\Documents\sentinela\dist\agent\`

### Config
- `AgentOptions.cs`: defaults `http://localhost:5002`
- `appsettings.json`: `ApiUrl: http://localhost:5002`
- `ContentRootPath`: pode estar incorreto quando executado de diretório diferente

### Bloqueio Conhecido (resolvido)
- ~~WebSocket fecha a cada ~5min quando `CollectorWorker` envia `SendScreenCaptureAsync`~~ — **RESOLVIDO**: `ScreenCapture` não estava no modelo EF (stub em `Api.Models.MissingTypes.cs`); movido para `Persistence/Models` com config + `DbSet`. Agente mantém conexão estável.

---

## Docker

| Serviço | Porta Host | Porta Container |
|---------|-----------|----------------|
| sentinela-api | 5002 | 8080 |
| sentinela-identity | 5003 | 8080 |
| sentinela-web | 3000 | 80 |
| nginx | 80 | 80 |
| postgres | 5432 | 5432 |
| redis | 6379 | 6379 |
| rabbitmq | 5672/15672 | 5672/15672 |

### Web Nginx (`docker/web/nginx.conf`)
- `/api/v1/auth/` → `sentinela-identity:8080`
- `/api/` → `sentinela-api:8080`
- `/hubs/` → `sentinela-api:8080` (WebSocket, `proxy_read_timeout 86400`)

### Credenciais Padrão
- Admin: `Admin` / `4517`
- Login endpoint: `POST /api/auth/login`

---

## Próximos Passos Sugeridos
1. Popular `ComputerName` no response do `RequestSession` (hostname real do computador)
2. Testar renderização dos frames no browser (localhost:3000 → Assist. Remota → sessão selecionada)
3. Adicionar suporte a mais AVs no `SimplifyProductName` (Kaspersky, Norton, etc.)
