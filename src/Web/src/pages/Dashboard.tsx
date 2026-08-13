import { useState, useEffect } from 'react'
import { motion, AnimatePresence } from 'framer-motion'
import { useTranslation } from 'react-i18next'
import { useQueryClient } from '@tanstack/react-query'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { useDashboardStats, useDashboardActivity, useIncidents } from '@/hooks/useDashboard'
import { useComputers } from '@/hooks/useComputers'
import { useLiveRelativeTime } from '@/hooks/useLiveRelativeTime'
import { formatRelative, formatDate } from '@/lib/utils'
import { RefreshCw, Monitor, AlertTriangle, Wifi, WifiOff, Tv, Search, Shield, Clock, ChevronRight, X, ExternalLink, AlertCircle, Info } from 'lucide-react'
import { PieChart, Pie, Cell, ResponsiveContainer } from 'recharts'

const riskColors: Record<string, string> = {
  'Crítico': '#ef4444',
  'Alto': '#f97316',
  'Médio': '#eab308',
  'Baixo': '#3b82f6',
  'Informativo': '#6b7280',
}

const riskIcons: Record<string, string> = {
  'Crítico': '🔴',
  'Alto': '🟠',
  'Médio': '🟡',
  'Baixo': '🔵',
  'Informativo': '⚪',
}

const eventTypeTranslations: Record<string, string> = {
  'CryptominerDetected': 'Criptominerador Detectado',
  'HighCpuProcess': 'Processo com CPU Alta',
  'MassFileRename': 'Renomeação em Massa de Arquivos',
  'RansomwarePattern': 'Padrão de Ransomware',
  'MalwareDetected': 'Malware Detectado',
  'AntivirusDisabled': 'Antivírus Desativado',
  'AntivirusOutdated': 'Antivírus Desatualizado',
  'FailedLogon': 'Falha de Login',
  'USBConnected': 'USB Conectado',
  'USBDisconnected': 'USB Desconectado',
  'FileCopy': 'Cópia de Arquivo',
  'SoftwareInstalled': 'Software Instalado',
  'SoftwareUninstalled': 'Software Desinstalado',
  'SuspiciousNetworkActivity': 'Atividade de Rede Suspeita',
}

const eventDescriptionTranslations: Record<string, string> = {
  'Mass file rename detected': 'Renomeação em massa detectada',
  'files renamed': 'arquivos renomeados',
  'Suspicious file with ransomware extension': 'Arquivo suspeito com extensão de ransomware',
  'Antivirus disabled on': 'Antivírus desativado em',
  'Antivirus protection disabled': 'Proteção do antivírus desativada',
  'Cryptominer detected': 'Criptominerador detectado',
  'Suspicious high CPU process': 'Processo com CPU alta suspeito',
  'USB device connected': 'Dispositivo USB conectado',
  'USB device disconnected': 'Dispositivo USB desconectado',
  'File copied': 'Arquivo copiado',
  'files': 'arquivos',
  'renamed': 'renomeados',
}

function translateEventType(eventType: string): string {
  return eventTypeTranslations[eventType] || eventType
}

function translateDescription(description: string): string {
  let translated = description
  for (const [en, pt] of Object.entries(eventDescriptionTranslations)) {
    translated = translated.replace(new RegExp(en, 'g'), pt)
  }
  return translated
}

function HeartbeatTime({ date }: { date?: string | null }) {
  return <>{useLiveRelativeTime(date)}</>
}

function NocOverlay({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const [time, setTime] = useState(new Date())
  const { data: stats } = useDashboardStats(true)
  const { data: incidents } = useIncidents(12, true)
  const { data: activity } = useDashboardActivity(true)
  const { data: computersPage } = useComputers({ page: '1', pageSize: '200' })

  const computers = computersPage?.items ?? []
  const offline = computers.filter((c) => c.status === 'Offline')
  const online = computers.filter((c) => c.status === 'Online')
  const recentEvents = (Array.isArray(activity) ? activity : []).slice(0, 12)
  const criticalIncidents = (incidents ?? []).filter((i: any) =>
    ['Crítico', 'Alto', 'Critical', 'High'].includes(i.riskLevel)
  )

  const donutData = [
    { name: t('noc.online'), value: stats?.onlineComputers ?? online.length, color: '#22c55e' },
    { name: t('noc.offline'), value: stats?.offlineComputers ?? offline.length, color: '#ef4444' },
    { name: t('noc.away'), value: stats?.awayComputers ?? 0, color: '#eab308' },
    { name: t('noc.disabled'), value: stats?.disabledComputers ?? 0, color: '#64748b' },
  ]

  useEffect(() => {
    const tick = setInterval(() => setTime(new Date()), 1000)
    return () => clearInterval(tick)
  }, [])

  useEffect(() => {
    const enter = async () => {
      try {
        if (!document.fullscreenElement) await document.documentElement.requestFullscreen()
      } catch {
        /* fullscreen pode ser bloqueado pelo browser */
      }
    }
    enter()

    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose()
    }
    window.addEventListener('keydown', onKey)
    return () => {
      window.removeEventListener('keydown', onKey)
      if (document.fullscreenElement) document.exitFullscreen().catch(() => {})
    }
  }, [onClose])

  const handleClose = async () => {
    try {
      if (document.fullscreenElement) await document.exitFullscreen()
    } catch {
      /* ignore */
    }
    onClose()
  }

  return (
    <div className="fixed inset-0 z-[70] overflow-y-auto bg-background">
      <div className="pointer-events-none absolute inset-0 bg-gradient-to-br from-primary/15 via-transparent to-blue-500/15" />
      <div className="relative min-h-screen p-6 md:p-8">
        <div className="flex items-center justify-between gap-4 mb-8 flex-wrap">
          <div>
            <div className="flex items-center gap-3">
              <Shield className="h-7 w-7 text-primary" />
              <h1 className="text-3xl font-bold bg-clip-text text-transparent bg-gradient-to-r from-primary to-blue-500">
                {t('noc.wallboard', 'Central de Segurança — Monitor')}
              </h1>
              <Badge variant="success" className="animate-pulse">LIVE</Badge>
            </div>
            <p className="text-sm text-muted-foreground mt-1">
              {t('noc.wallboardHint', 'Atualização automática a cada 15s · Esc para sair')}
            </p>
          </div>
          <div className="flex items-center gap-4">
            <span className="text-3xl font-mono font-bold tabular-nums">
              {time.toLocaleTimeString('pt-BR')}
            </span>
            <Button variant="outline" onClick={handleClose}>
              {t('noc.exitFullscreen')}
            </Button>
          </div>
        </div>

        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
          {[
            {
              label: t('noc.totalComputers'),
              value: String(stats?.totalComputers ?? computers.length),
              icon: Monitor,
              color: 'from-primary to-blue-500',
            },
            {
              label: t('noc.online'),
              value: String(stats?.onlineComputers ?? online.length),
              icon: Wifi,
              color: 'from-emerald-500 to-green-500',
            },
            {
              label: t('noc.offline'),
              value: String(stats?.offlineComputers ?? offline.length),
              icon: WifiOff,
              color: 'from-red-500 to-rose-500',
            },
            {
              label: t('noc.criticalAlerts'),
              value: String(stats?.criticalAlerts ?? criticalIncidents.length),
              icon: AlertTriangle,
              color: 'from-amber-500 to-orange-500',
            },
          ].map((stat) => (
            <Card key={stat.label} className="bg-card/70 backdrop-blur-xl border-border/50">
              <CardContent className="p-5 text-center">
                <div className={`inline-flex p-3 rounded-xl bg-gradient-to-br ${stat.color} text-white mb-3`}>
                  <stat.icon className="h-6 w-6" />
                </div>
                <p className="text-4xl font-bold tabular-nums">{stat.value}</p>
                <p className="text-xs text-muted-foreground uppercase tracking-wider mt-1">{stat.label}</p>
              </CardContent>
            </Card>
          ))}
        </div>

        <div className="grid grid-cols-1 xl:grid-cols-3 gap-6">
          <Card className="bg-card/70 backdrop-blur-xl border-border/50">
            <CardHeader>
              <CardTitle className="text-base">{t('noc.statusDistribution')}</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="flex items-center gap-6">
                <ResponsiveContainer width={180} height={180}>
                  <PieChart>
                    <Pie data={donutData} cx="50%" cy="50%" innerRadius={55} outerRadius={85} dataKey="value">
                      {donutData.map((entry, idx) => (
                        <Cell key={idx} fill={entry.color} />
                      ))}
                    </Pie>
                  </PieChart>
                </ResponsiveContainer>
                <div className="space-y-3">
                  {donutData.map((d) => (
                    <div key={d.name} className="flex items-center gap-2 text-sm">
                      <div className="w-3 h-3 rounded-full" style={{ backgroundColor: d.color }} />
                      <span>
                        {d.name}: <strong>{d.value}</strong>
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            </CardContent>
          </Card>

          <Card className="bg-card/70 backdrop-blur-xl border-border/50 xl:col-span-2">
            <CardHeader>
              <CardTitle className="text-base flex items-center gap-2">
                <AlertTriangle className="h-4 w-4 text-destructive" />
                {t('noc.activeCriticalAlerts')}
              </CardTitle>
            </CardHeader>
            <CardContent>
              {criticalIncidents.length === 0 && (incidents ?? []).length === 0 ? (
                <p className="text-sm text-muted-foreground py-6 text-center">
                  {t('noc.noCriticalAlerts', 'Nenhum incidente ativo no momento')}
                </p>
              ) : (
                <div className="space-y-2 max-h-[280px] overflow-y-auto">
                  {(criticalIncidents.length > 0 ? criticalIncidents : incidents ?? []).map((incident: any) => (
                    <div
                      key={incident.id}
                      className="flex items-start gap-3 rounded-lg border border-border/50 px-3 py-2.5"
                    >
                      <span className="text-lg">{riskIcons[incident.riskLevel] || '⚪'}</span>
                      <div className="min-w-0 flex-1">
                        <p className="text-sm font-medium truncate">
                          {translateEventType(incident.title || incident.eventType || 'Incidente')}
                        </p>
                        <p className="text-xs text-muted-foreground truncate">
                          {incident.computerName || incident.hostname || '—'}
                          {incident.timestamp ? ` · ${formatRelative(incident.timestamp)}` : ''}
                        </p>
                      </div>
                      <Badge
                        variant={
                          incident.riskLevel === 'Crítico' || incident.riskLevel === 'Critical'
                            ? 'destructive'
                            : 'warning'
                        }
                        className="text-[10px] shrink-0"
                      >
                        {incident.riskLevel}
                      </Badge>
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>

          <Card className="bg-card/70 backdrop-blur-xl border-border/50">
            <CardHeader>
              <CardTitle className="text-base flex items-center gap-2">
                <WifiOff className="h-4 w-4 text-destructive" />
                {t('noc.offlineMachines', 'Máquinas offline')}
              </CardTitle>
            </CardHeader>
            <CardContent>
              {offline.length === 0 ? (
                <p className="text-sm text-muted-foreground py-4 text-center">
                  {t('noc.allOnline', 'Todas as máquinas estão online')}
                </p>
              ) : (
                <div className="space-y-2 max-h-[320px] overflow-y-auto">
                  {offline
                    .slice()
                    .sort((a, b) => a.hostname.localeCompare(b.hostname, undefined, { numeric: true }))
                    .map((c) => (
                      <div
                        key={c.id}
                        className="flex items-center justify-between rounded-lg border border-destructive/20 bg-destructive/5 px-3 py-2"
                      >
                        <div className="flex items-center gap-2 min-w-0">
                          <Monitor className="h-4 w-4 text-destructive shrink-0" />
                          <span className="text-sm font-medium truncate">{c.hostname}</span>
                        </div>
                        <span className="text-[10px] text-muted-foreground shrink-0">
                          <HeartbeatTime date={c.lastHeartbeat} />
                        </span>
                      </div>
                    ))}
                </div>
              )}
            </CardContent>
          </Card>

          <Card className="bg-card/70 backdrop-blur-xl border-border/50 xl:col-span-2">
            <CardHeader>
              <CardTitle className="text-base flex items-center gap-2">
                <Clock className="h-4 w-4" />
                {t('noc.recentActivity')}
              </CardTitle>
            </CardHeader>
            <CardContent>
              {recentEvents.length === 0 ? (
                <p className="text-sm text-muted-foreground py-4 text-center">
                  {t('noc.noRecentActivity', 'Nenhuma atividade recente')}
                </p>
              ) : (
                <div className="space-y-2 max-h-[320px] overflow-y-auto">
                  {recentEvents.map((event: any, idx: number) => (
                    <div
                      key={event.id || idx}
                      className="flex items-start gap-3 text-sm py-2 border-b border-border/40 last:border-0"
                    >
                      <div className="w-2 h-2 mt-1.5 rounded-full bg-primary/70 shrink-0" />
                      <div className="min-w-0 flex-1">
                        <p className="font-medium truncate">
                          {translateEventType(event.eventType)}
                          {event.computerName || event.hostname
                            ? ` · ${event.computerName || event.hostname}`
                            : ''}
                        </p>
                        <p className="text-xs text-muted-foreground truncate">
                          {translateDescription(event.description || '')}
                        </p>
                      </div>
                      <span className="text-[10px] text-muted-foreground shrink-0">
                        {event.timestamp ? formatRelative(event.timestamp) : ''}
                      </span>
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  )
}

function IncidentCard({ incident, onInvestigate }: { incident: any; onInvestigate: (incident: any) => void }) {
  const { t } = useTranslation()
  const [expanded, setExpanded] = useState(false)

  return (
    <motion.div
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      className="border rounded-lg overflow-hidden"
      style={{ borderColor: riskColors[incident.riskLevel] || '#6b7280' }}
    >
      <div 
        className="p-4 cursor-pointer hover:bg-muted/50 transition-colors"
        onClick={() => setExpanded(!expanded)}
      >
        <div className="flex items-start justify-between gap-3">
          <div className="flex items-start gap-3 flex-1 min-w-0">
            <span className="text-xl mt-0.5">{riskIcons[incident.riskLevel]}</span>
            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-2 mb-1">
                <Shield className="h-4 w-4 shrink-0" style={{ color: riskColors[incident.riskLevel] }} />
                <span className="font-semibold text-sm">{incident.title}</span>
              </div>
              <div className="flex items-center gap-2 text-xs text-muted-foreground">
                <Monitor className="h-3 w-3" />
                <span className="font-medium">{incident.computerName}</span>
                <span>•</span>
                <Clock className="h-3 w-3" />
                <span>{formatRelative(incident.timestamp)}</span>
                <span>•</span>
                <span>{incident.eventCount} evento(s)</span>
              </div>
            </div>
          </div>
          <div className="flex items-center gap-2 shrink-0">
            <span 
              className="px-2 py-0.5 rounded-full text-xs font-medium text-white"
              style={{ backgroundColor: riskColors[incident.riskLevel] }}
            >
              {incident.riskLevel}
            </span>
            <ChevronRight className={`h-4 w-4 transition-transform ${expanded ? 'rotate-90' : ''}`} />
          </div>
        </div>
      </div>
      
      {expanded && (
        <motion.div
          initial={{ height: 0, opacity: 0 }}
          animate={{ height: 'auto', opacity: 1 }}
          className="border-t bg-muted/30"
        >
          <div className="p-4 space-y-3">
            <div>
              <p className="text-xs font-medium text-muted-foreground mb-2">Evidências:</p>
              <div className="space-y-1.5">
                {incident.events?.slice(0, 5).map((event: any, idx: number) => (
                  <div key={idx} className="flex items-start gap-2 text-sm">
                    <div 
                      className="w-1.5 h-1.5 rounded-full mt-1.5 shrink-0"
                      style={{ backgroundColor: riskColors[event.severity] || '#6b7280' }}
                    />
                    <div>
                      <span className="font-medium">{translateEventType(event.eventType)}</span>
                      {event.description && (
                        <span className="text-muted-foreground ml-1">- {translateDescription(event.description)}</span>
                      )}
                    </div>
                  </div>
                ))}
                {incident.events?.length > 5 && (
                  <p className="text-xs text-muted-foreground">+{incident.events.length - 5} mais eventos</p>
                )}
              </div>
            </div>
            <div className="flex justify-end">
              <Button size="sm" variant="outline" onClick={() => onInvestigate(incident)}>
                <Search className="h-3.5 w-3.5 mr-1" />
                Investigar
              </Button>
            </div>
          </div>
        </motion.div>
      )}
    </motion.div>
  )
}

function InvestigationModal({ incident, onClose }: { incident: any; onClose: () => void }) {
  const { t } = useTranslation()
  
  if (!incident) return null

  const getRecommendations = (riskLevel: string, eventTypes: string[]) => {
    const recommendations = []
    
    if (riskLevel === 'Crítico') {
      recommendations.push({
        priority: 'Alta',
        action: 'Isolar máquina da rede imediatamente',
        description: 'Desconecte o cabo de rede e/ou desative o Wi-Fi para prevenir propagação.'
      })
      recommendations.push({
        priority: 'Alta',
        action: 'Verificar backup dos dados',
        description: 'Confirme que backups estão íntegros e não foram comprometidos.'
      })
    }
    
    if (eventTypes.includes('MassFileRename') || eventTypes.includes('RansomwarePattern')) {
      recommendations.push({
        priority: 'Alta',
        action: 'Não pagar resgate',
        description: 'Pagamento não garante recuperação e financia criminosos.'
      })
      recommendations.push({
        priority: 'Média',
        action: 'Verificar fonte de infecção',
        description: 'Analisar e-mails, downloads ou dispositivos USB recentes.'
      })
    }
    
    if (eventTypes.includes('AntivirusDisabled')) {
      recommendations.push({
        priority: 'Alta',
        action: 'Reativar proteção antivírus',
        description: 'Verificar se foi desativado manualmente ou por malware.'
      })
      recommendations.push({
        priority: 'Média',
        action: 'Executar scan completo',
        description: 'Iniciar verificação completa do sistema.'
      })
    }
    
    if (eventTypes.includes('CryptominerDetected') || eventTypes.includes('HighCpuProcess')) {
      recommendations.push({
        priority: 'Alta',
        action: 'Identificar e encerrar processo',
        description: 'Localizar o processo suspeito no Gerenciador de Tarefas.'
      })
      recommendations.push({
        priority: 'Média',
        action: 'Verificar programas instalados recentemente',
        description: 'Desinstalar software suspeito ou desconhecido.'
      })
    }
    
    if (eventTypes.includes('FileCopy')) {
      recommendations.push({
        priority: 'Crítica',
        action: 'Investigar exfiltração via USB imediatamente',
        description: 'Identificar quais arquivos foram copiados, quem estava logado e recolher o dispositivo se possível.'
      })
      recommendations.push({
        priority: 'Alta',
        action: 'Notificar segurança da informação',
        description: 'Registrar o incidente e avaliar impacto dos dados transferidos.'
      })
    } else if (eventTypes.includes('USBConnected')) {
      recommendations.push({
        priority: 'Média',
        action: 'Verificar dispositivo USB',
        description: 'Analisar conteúdo e origem do dispositivo conectado.'
      })
    }
    
    if (recommendations.length === 0) {
      recommendations.push({
        priority: 'Média',
        action: 'Revisar logs do sistema',
        description: 'Analisar eventos detalhados para identificar a causa raiz.'
      })
    }
    
    return recommendations
  }

  const eventTypes = [...new Set(incident.events?.map((e: any) => e.eventType) || [])]
  const recommendations = getRecommendations(incident.riskLevel, eventTypes)

  return (
    <AnimatePresence>
      <motion.div
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        exit={{ opacity: 0 }}
        className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4"
        onClick={onClose}
      >
        <motion.div
          initial={{ scale: 0.95, opacity: 0 }}
          animate={{ scale: 1, opacity: 1 }}
          exit={{ scale: 0.95, opacity: 0 }}
          className="bg-background rounded-xl shadow-xl w-full max-w-2xl max-h-[85vh] overflow-hidden"
          onClick={(e) => e.stopPropagation()}
        >
          <div className="flex items-center justify-between p-4 border-b">
            <div className="flex items-center gap-3">
              <span className="text-2xl">{riskIcons[incident.riskLevel]}</span>
              <div>
                <h2 className="font-semibold">{incident.title}</h2>
                <p className="text-sm text-muted-foreground">{incident.computerName}</p>
              </div>
            </div>
            <Button variant="ghost" size="sm" onClick={onClose}>
              <X className="h-4 w-4" />
            </Button>
          </div>
          
          <div className="p-4 overflow-y-auto max-h-[calc(85vh-140px)] space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="p-3 rounded-lg bg-muted/50">
                <p className="text-xs text-muted-foreground mb-1">Nível de Risco</p>
                <div className="flex items-center gap-2">
                  <span 
                    className="px-2 py-0.5 rounded-full text-xs font-medium text-white"
                    style={{ backgroundColor: riskColors[incident.riskLevel] }}
                  >
                    {incident.riskLevel}
                  </span>
                </div>
              </div>
              <div className="p-3 rounded-lg bg-muted/50">
                <p className="text-xs text-muted-foreground mb-1">Horário</p>
                <p className="text-sm font-medium">{formatDate(incident.timestamp)}</p>
              </div>
              <div className="p-3 rounded-lg bg-muted/50">
                <p className="text-xs text-muted-foreground mb-1">Total de Eventos</p>
                <p className="text-sm font-medium">{incident.eventCount}</p>
              </div>
              <div className="p-3 rounded-lg bg-muted/50">
                <p className="text-xs text-muted-foreground mb-1">Tipos de Evento</p>
                <p className="text-sm font-medium">{eventTypes.length} tipo(s)</p>
              </div>
            </div>

            <div>
              <h3 className="font-medium mb-2 flex items-center gap-2">
                <AlertCircle className="h-4 w-4" />
                Eventos Detectados
              </h3>
              <div className="space-y-2 max-h-48 overflow-y-auto">
                {incident.events?.map((event: any, idx: number) => (
                  <div key={idx} className="flex items-start gap-3 p-2 rounded-lg bg-muted/30 text-sm">
                    <div 
                      className="w-2 h-2 rounded-full mt-1.5 shrink-0"
                      style={{ backgroundColor: riskColors[event.severity] || '#6b7280' }}
                    />
                    <div className="flex-1">
                      <div className="flex items-center justify-between">
                        <span className="font-medium">{translateEventType(event.eventType)}</span>
                        <span className="text-xs text-muted-foreground">{formatRelative(event.timestamp)}</span>
                      </div>
                      {event.description && (
                        <p className="text-xs text-muted-foreground mt-0.5">{translateDescription(event.description)}</p>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            </div>

            <div>
              <h3 className="font-medium mb-2 flex items-center gap-2">
                <Info className="h-4 w-4" />
                Recomendações
              </h3>
              <div className="space-y-2">
                {recommendations.map((rec, idx) => (
                  <div key={idx} className="p-3 rounded-lg border bg-background">
                    <div className="flex items-center gap-2 mb-1">
                      <span className={`text-xs font-medium px-1.5 py-0.5 rounded ${
                        rec.priority === 'Alta' ? 'bg-red-100 text-red-700' :
                        rec.priority === 'Média' ? 'bg-yellow-100 text-yellow-700' :
                        'bg-blue-100 text-blue-700'
                      }`}>
                        {rec.priority}
                      </span>
                      <span className="font-medium text-sm">{rec.action}</span>
                    </div>
                    <p className="text-xs text-muted-foreground">{rec.description}</p>
                  </div>
                ))}
              </div>
            </div>
          </div>
          
          <div className="p-4 border-t flex justify-end gap-2">
            <Button variant="outline" size="sm" onClick={onClose}>
              Fechar
            </Button>
            <Button size="sm">
              <ExternalLink className="h-3.5 w-3.5 mr-1" />
              Abrir Detalhes da Máquina
            </Button>
          </div>
        </motion.div>
      </motion.div>
    </AnimatePresence>
  )
}

export function Dashboard() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [autoRefresh, setAutoRefresh] = useState(false)
  const [nocMode, setNocMode] = useState(false)
  const [investigatingIncident, setInvestigatingIncident] = useState<any>(null)
  const [refreshing, setRefreshing] = useState(false)
  const { data: stats, isFetching: statsFetching } = useDashboardStats(autoRefresh)
  const { data: incidents, isFetching: incidentsFetching } = useIncidents(5, autoRefresh)

  const handleRefresh = async () => {
    setRefreshing(true)
    try {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['dashboard-stats'] }),
        queryClient.invalidateQueries({ queryKey: ['security-incidents'] }),
        queryClient.invalidateQueries({ queryKey: ['dashboard-activity'] }),
      ])
    } finally {
      setRefreshing(false)
    }
  }

  if (nocMode) return <NocOverlay onClose={() => setNocMode(false)} />

  const isRefreshing = refreshing || statsFetching || incidentsFetching

  const statCards = [
    { label: t('dashboard.totalComputers'), value: stats?.totalComputers ?? 0, icon: Monitor, color: 'from-primary to-blue-500' },
    { label: t('dashboard.online'), value: stats?.onlineComputers ?? 0, icon: Wifi, color: 'from-emerald-500 to-green-500' },
    { label: t('dashboard.offline'), value: stats?.offlineComputers ?? 0, icon: WifiOff, color: 'from-red-500 to-rose-500' },
    { label: t('dashboard.activeAlerts'), value: stats?.totalAlerts ?? 0, icon: AlertTriangle, color: 'from-amber-500 to-orange-500' },
  ]

  const donutData = [
    { name: t('dashboard.online'), value: stats?.onlineComputers ?? 0, color: '#22c55e' },
    { name: t('dashboard.offline'), value: stats?.offlineComputers ?? 0, color: '#ef4444' },
    { name: t('dashboard.away'), value: stats?.awayComputers ?? 0, color: '#eab308' },
    { name: t('dashboard.disabled'), value: stats?.disabledComputers ?? 0, color: '#64748b' },
  ]

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">{t('dashboard.title')}</h1>
          <p className="text-muted-foreground text-sm">{t('dashboard.subtitle')}</p>
        </div>
        <div className="flex items-center gap-2">
          <label className="flex items-center gap-2 text-sm text-muted-foreground cursor-pointer">
            <input
              type="checkbox"
              checked={autoRefresh}
              onChange={() => setAutoRefresh(!autoRefresh)}
              className="rounded"
            />
            {t('dashboard.autoRefresh')}
            {autoRefresh && (
              <span className="text-[10px] text-primary">{t('dashboard.every15s', '15s')}</span>
            )}
          </label>
          <Button variant="outline" size="sm" onClick={() => setNocMode(true)}>
            <Tv className="h-4 w-4 mr-1" /> {t('dashboard.nocMode', 'Monitor')}
          </Button>
          <Button
            variant="outline"
            size="sm"
            onClick={handleRefresh}
            disabled={isRefreshing}
            title={t('dashboard.refreshNow', 'Atualizar agora')}
          >
            <RefreshCw className={`h-4 w-4 ${isRefreshing ? 'animate-spin' : ''}`} />
          </Button>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-4">
        {statCards.map((card, i) => (
          <motion.div key={card.label} initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: i * 0.05 }}>
            <Card className="card-hover">
              <CardContent className="p-4">
                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-xs text-muted-foreground">{card.label}</p>
                    <p className="text-2xl font-bold mt-1">{card.value}</p>
                  </div>
                  <div className={`p-2.5 rounded-xl bg-gradient-to-br ${card.color} text-white`}>
                    <card.icon className="h-5 w-5" />
                  </div>
                </div>
              </CardContent>
            </Card>
          </motion.div>
        ))}
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle className="text-base flex items-center gap-2">
              <Shield className="h-4 w-4" />
              Centro de Incidentes
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-3">
              {incidents && incidents.length > 0 ? (
                incidents.map((incident: any) => (
                  <IncidentCard 
                    key={incident.id} 
                    incident={incident} 
                    onInvestigate={setInvestigatingIncident}
                  />
                ))
              ) : (
                <div className="text-center py-8">
                  <Shield className="h-12 w-12 mx-auto text-muted-foreground/50 mb-3" />
                  <p className="text-sm text-muted-foreground">Nenhum incidente ativo</p>
                  <p className="text-xs text-muted-foreground/70 mt-1">Todos os sistemas estão seguros</p>
                </div>
              )}
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle className="text-base">{t('dashboard.computerStatus')}</CardTitle></CardHeader>
          <CardContent className="flex flex-col items-center">
            <ResponsiveContainer width="100%" height={180}>
              <PieChart>
                <Pie data={donutData} cx="50%" cy="50%" innerRadius={55} outerRadius={80} dataKey="value" startAngle={90} endAngle={-270}>
                  {donutData.map((entry, idx) => (
                    <Cell key={idx} fill={entry.color} />
                  ))}
                </Pie>
              </PieChart>
            </ResponsiveContainer>
            <div className="flex gap-4 mt-2">
              {donutData.map((d) => (
                <div key={d.name} className="flex items-center gap-1.5 text-xs">
                  <div className="w-2.5 h-2.5 rounded-full" style={{ backgroundColor: d.color }} />
                  <span className="text-muted-foreground">{d.name}: {d.value}</span>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      </div>
      
      {investigatingIncident && (
        <InvestigationModal 
          incident={investigatingIncident} 
          onClose={() => setInvestigatingIncident(null)} 
        />
      )}
    </div>
  )
}
