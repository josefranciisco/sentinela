import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useQuery } from '@tanstack/react-query'
import { Card, CardContent } from '@/components/ui/card'
import { Table, TableHeader, TableBody, TableHead, TableRow, TableCell } from '@/components/ui/table'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Dialog, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog'
import { api } from '@/lib/api'
import { cn } from '@/lib/utils'
import {
  Search, Monitor, Loader2, LayoutGrid, List, HardDrive, MemoryStick, CircuitBoard,
} from 'lucide-react'

type AgentStatus = 'none' | 'online' | 'offline'
type ViewMode = 'list' | 'cards'
type FleetTab = 'machines' | 'monitors' | 'disks' | 'gpu' | 'memory'
type AgentFilter = 'all' | 'linked' | 'unlinked'

interface FleetMetrics {
  cpuPercent: number
  ramUsedGb: number
  ramTotalGb: number
  ramPercent: number
  diskUsedGb: number
  diskTotalGb: number
  diskFreeGb: number
  diskPercent: number
  uptime?: string
  ip?: string
  user?: string
  topProcess?: string
}

interface FleetMachine {
  hostname: string
  alias?: string | null
  status: string
  lastSeen: number
  healthScore: number
  metrics?: FleetMetrics
  inventory?: any
  sentinelaComputerId?: string | null
  agentStatus: AgentStatus
}

const VIEW_KEY = 'sentinela-computers-view'

function readView(): ViewMode {
  return localStorage.getItem(VIEW_KEY) === 'cards' ? 'cards' : 'list'
}

function Meter({ label, value, muted }: { label: string; value: number; muted?: boolean }) {
  const pct = muted ? 0 : Math.min(100, Math.max(0, value || 0))
  const tone = pct >= 80 ? 'bg-destructive' : pct >= 60 ? 'bg-amber-500' : 'bg-emerald-500'
  return (
    <div className="space-y-1">
      <div className="flex justify-between text-[11px]">
        <span className="text-muted-foreground">{label}</span>
        <span className="tabular-nums text-foreground">{muted ? '—' : `${Math.round(pct)}%`}</span>
      </div>
      <div className="h-1.5 rounded-full bg-muted overflow-hidden">
        <div className={cn('h-full rounded-full transition-all', muted ? 'bg-muted-foreground/20' : tone)} style={{ width: `${pct}%` }} />
      </div>
    </div>
  )
}

function mobiStatus(status: string): { label: string; variant: 'success' | 'warning' | 'destructive' | 'secondary' } {
  switch ((status || '').toLowerCase()) {
    case 'online': return { label: 'Online', variant: 'success' }
    case 'delayed': return { label: 'Atrasada', variant: 'warning' }
    case 'offline': return { label: 'Offline', variant: 'destructive' }
    default: return { label: status || '—', variant: 'secondary' }
  }
}

function agentBadge(status: AgentStatus, t: (k: string, d?: string) => string) {
  if (status === 'online') return { label: t('computers.agentOnline', 'Agent online'), variant: 'success' as const }
  if (status === 'offline') return { label: t('computers.agentOffline', 'Agent offline'), variant: 'warning' as const }
  return { label: t('computers.agentNone', 'Sem Agent'), variant: 'secondary' as const }
}

function lastSeenLabel(ts: number) {
  if (!ts) return '—'
  const sec = Math.max(0, Math.floor(Date.now() / 1000 - ts))
  if (sec < 60) return `há ${sec}s`
  const min = Math.floor(sec / 60)
  if (min < 60) return `há ${min} min`
  const h = Math.floor(min / 60)
  if (h < 48) return `há ${h}h`
  return new Date(ts * 1000).toLocaleString('pt-BR')
}

function offlineLabel(ts: number, status: string) {
  if ((status || '').toLowerCase() !== 'offline' || !ts) return ''
  const hours = Math.floor((Date.now() / 1000 - ts) / 3600)
  if (hours < 1) return ''
  if (hours < 24) return `${hours} hora${hours > 1 ? 's' : ''} offline`
  const days = Math.floor(hours / 24)
  return days === 1 ? '1 dia offline' : `${days} dias offline`
}

export function Computers() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [search, setSearch] = useState('')
  const [tab, setTab] = useState<FleetTab>('machines')
  const [agentFilter, setAgentFilter] = useState<AgentFilter>('all')
  const [view, setView] = useState<ViewMode>(readView)
  const [preview, setPreview] = useState<FleetMachine | null>(null)

  const { data: machines = [], isLoading, isError } = useQuery({
    queryKey: ['monitoramento-machines'],
    queryFn: () => api.get<FleetMachine[]>('/monitoramento/machines'),
    refetchInterval: 15000,
    retry: 1,
  })

  const setViewMode = (mode: ViewMode) => {
    setView(mode)
    localStorage.setItem(VIEW_KEY, mode)
  }

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase()
    return machines.filter((m) => {
      if (agentFilter === 'linked' && m.agentStatus === 'none') return false
      if (agentFilter === 'unlinked' && m.agentStatus !== 'none') return false
      if (!q) return true
      const hay = [m.hostname, m.alias, m.metrics?.ip, m.metrics?.user, m.inventory?.cpu?.model]
        .filter(Boolean)
        .join(' ')
        .toLowerCase()
      return hay.includes(q)
    })
  }, [machines, search, agentFilter])

  const openMachine = (m: FleetMachine) => {
    if (m.sentinelaComputerId) navigate(`/computers/${m.sentinelaComputerId}`)
    else setPreview(m)
  }

  const tabs: { key: FleetTab; label: string }[] = [
    { key: 'machines', label: t('computers.tabMachines', 'Máquinas') },
    { key: 'monitors', label: t('computers.tabMonitors', 'Monitores') },
    { key: 'disks', label: t('computers.tabDisks', 'Discos') },
    { key: 'gpu', label: t('computers.tabGpu', 'GPU') },
    { key: 'memory', label: t('computers.tabMemory', 'Memória RAM') },
  ]

  return (
    <div className="space-y-5">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <h1 className="text-2xl font-bold">{t('computers.title')}</h1>
          <p className="text-muted-foreground text-sm">{t('computers.fleetSubtitle', 'Inventário do monitoramento, integrado ao Agent conforme a instalação.')}</p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <div className="relative">
            <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
            <input
              placeholder={t('computers.search')}
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="h-9 w-52 rounded-lg border border-input bg-background pl-8 pr-3 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            />
          </div>
          <div className="inline-flex h-9 items-center rounded-lg bg-muted p-1 text-muted-foreground">
            {([
              ['all', t('computers.filterAll', 'Todas')],
              ['linked', t('computers.filterLinked', 'Com Agent')],
              ['unlinked', t('computers.filterUnlinked', 'Sem Agent')],
            ] as const).map(([key, label]) => (
              <button
                key={key}
                type="button"
                onClick={() => setAgentFilter(key)}
                className={cn(
                  'rounded-md px-2.5 py-1 text-xs font-medium transition-all',
                  agentFilter === key ? 'bg-background text-foreground shadow-sm' : 'hover:text-foreground',
                )}
              >
                {label}
              </button>
            ))}
          </div>
          <div className="inline-flex h-9 items-center rounded-lg border border-border/70 bg-card p-0.5">
            <button
              type="button"
              title={t('computers.viewList', 'Linhas')}
              onClick={() => setViewMode('list')}
              className={cn(
                'inline-flex h-8 w-8 items-center justify-center rounded-md transition-all',
                view === 'list' ? 'bg-primary text-primary-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground',
              )}
            >
              <List className="h-4 w-4" />
            </button>
            <button
              type="button"
              title={t('computers.viewCards', 'Cards')}
              onClick={() => setViewMode('cards')}
              className={cn(
                'inline-flex h-8 w-8 items-center justify-center rounded-md transition-all',
                view === 'cards' ? 'bg-primary text-primary-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground',
              )}
            >
              <LayoutGrid className="h-4 w-4" />
            </button>
          </div>
        </div>
      </div>

      <div className="inline-flex h-10 items-center rounded-lg bg-muted p-1 text-muted-foreground">
        {tabs.map((item) => (
          <button
            key={item.key}
            type="button"
            onClick={() => setTab(item.key)}
            className={cn(
              'rounded-md px-3 py-1.5 text-sm font-medium transition-all',
              tab === item.key ? 'bg-background text-foreground shadow-sm' : 'hover:text-foreground',
            )}
          >
            {item.label}
          </button>
        ))}
      </div>

      {isLoading ? (
        <p className="text-sm text-muted-foreground py-12 text-center">
          <Loader2 className="h-4 w-4 animate-spin inline mr-1" /> {t('computers.loading')}
        </p>
      ) : isError ? (
        <Card>
          <CardContent className="py-10 text-center text-sm text-muted-foreground">
            {t('computers.mobiUnavailable', 'Não foi possível ler o monitoramento. Confira se o painel Mobi está no ar (:8000).')}
          </CardContent>
        </Card>
      ) : tab === 'machines' ? (
        view === 'list'
          ? <MachineTable machines={filtered} onOpen={openMachine} t={t} />
          : <MachineCards machines={filtered} onOpen={openMachine} t={t} />
      ) : (
        <InventorySlice tab={tab} machines={filtered} view={view} onOpen={openMachine} t={t} />
      )}

      <Dialog open={!!preview} onClose={() => setPreview(null)}>
        <DialogHeader>
          <DialogTitle>{preview?.alias || preview?.hostname}</DialogTitle>
        </DialogHeader>
        {preview && (
          <div className="space-y-3 text-sm">
            <p className="text-muted-foreground">
              {t('computers.noAgentHint', 'Esta máquina ainda não tem o Agent Sentinela. Inventário vem do monitoramento; gravação e USB liberam na instalação.')}
            </p>
            <div className="grid grid-cols-2 gap-2">
              <Fact label="CPU" value={preview.inventory?.cpu?.model} />
              <Fact label="RAM" value={preview.inventory?.memory?.total_gb ? `${preview.inventory.memory.total_gb} GB` : preview.metrics?.ramTotalGb ? `${preview.metrics.ramTotalGb} GB` : '—'} />
              <Fact label="IP" value={preview.metrics?.ip || preview.inventory?.network?.ip} />
              <Fact label="Windows" value={preview.inventory?.system?.edition} />
            </div>
          </div>
        )}
        <DialogFooter>
          <Button variant="outline" onClick={() => setPreview(null)}>{t('computers.cancel')}</Button>
        </DialogFooter>
      </Dialog>
    </div>
  )
}

function Fact({ label, value }: { label: string; value?: string | number | null }) {
  return (
    <div className="rounded-lg border border-border/50 bg-muted/30 px-3 py-2">
      <p className="text-[11px] text-muted-foreground">{label}</p>
      <p className="font-medium truncate">{value || '—'}</p>
    </div>
  )
}

function MachineTable({ machines, onOpen, t }: { machines: FleetMachine[]; onOpen: (m: FleetMachine) => void; t: (k: string, d?: string) => string }) {
  if (machines.length === 0) {
    return <p className="text-sm text-muted-foreground py-10 text-center">{t('computers.noComputers')}</p>
  }
  return (
    <Card>
      <CardContent className="p-0">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('computers.hostname')}</TableHead>
              <TableHead>{t('computers.status')}</TableHead>
              <TableHead>{t('computers.agent', 'Agent')}</TableHead>
              <TableHead>CPU</TableHead>
              <TableHead>RAM</TableHead>
              <TableHead>{t('computers.tabDisks', 'Disco')}</TableHead>
              <TableHead>{t('computers.ipAddress')}</TableHead>
              <TableHead>{t('computers.user')}</TableHead>
              <TableHead>{t('computers.lastHeartbeat')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {machines.map((m) => {
              const st = mobiStatus(m.status)
              const ag = agentBadge(m.agentStatus, t)
              const offline = (m.status || '').toLowerCase() === 'offline'
              return (
                <TableRow key={m.hostname} className="cursor-pointer" onClick={() => onOpen(m)}>
                  <TableCell className="font-medium">
                    <div className="flex items-center gap-2">
                      <Monitor className="h-4 w-4 text-muted-foreground shrink-0" />
                      <span className="truncate">{m.alias || m.hostname}</span>
                    </div>
                  </TableCell>
                  <TableCell><Badge variant={st.variant}>{st.label}</Badge></TableCell>
                  <TableCell><Badge variant={ag.variant}>{ag.label}</Badge></TableCell>
                  <TableCell className="tabular-nums text-xs">{offline ? '—' : `${Math.round(m.metrics?.cpuPercent || 0)}%`}</TableCell>
                  <TableCell className="tabular-nums text-xs">{offline ? '—' : `${Math.round(m.metrics?.ramPercent || 0)}%`}</TableCell>
                  <TableCell className="tabular-nums text-xs">{offline ? '—' : `${Math.round(m.metrics?.diskPercent || 0)}%`}</TableCell>
                  <TableCell className="font-mono text-xs text-muted-foreground">{m.metrics?.ip || m.inventory?.network?.ip || '—'}</TableCell>
                  <TableCell>{m.metrics?.user || '—'}</TableCell>
                  <TableCell className="text-xs text-muted-foreground">{lastSeenLabel(m.lastSeen)}</TableCell>
                </TableRow>
              )
            })}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  )
}

function MachineCards({ machines, onOpen, t }: { machines: FleetMachine[]; onOpen: (m: FleetMachine) => void; t: (k: string, d?: string) => string }) {
  if (machines.length === 0) {
    return <p className="text-sm text-muted-foreground py-10 text-center">{t('computers.noComputers')}</p>
  }
  return (
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
      {machines.map((m) => {
        const st = mobiStatus(m.status)
        const offline = (m.status || '').toLowerCase() === 'offline'
        const seen = lastSeenLabel(m.lastSeen)
        const downFor = offlineLabel(m.lastSeen, m.status)
        return (
          <button
            key={m.hostname}
            type="button"
            onClick={() => onOpen(m)}
            className="text-left rounded-xl border border-border/50 bg-card/60 backdrop-blur-xl p-4 space-y-3 transition-colors hover:border-primary/40 hover:bg-card"
          >
            <div className="space-y-1.5 min-w-0">
              <p className="font-semibold truncate">{m.alias || m.hostname}</p>
              <div className="flex items-center justify-between gap-2">
                <span className="inline-flex items-center gap-1.5 min-w-0">
                  <span
                    className={cn(
                      'h-1.5 w-1.5 rounded-full shrink-0',
                      st.variant === 'success' && 'bg-emerald-400',
                      st.variant === 'warning' && 'bg-amber-400',
                      st.variant === 'destructive' && 'bg-destructive',
                      st.variant === 'secondary' && 'bg-muted-foreground/50',
                    )}
                  />
                  <span className={cn(
                    'text-[11px] font-medium',
                    st.variant === 'success' && 'text-emerald-400',
                    st.variant === 'warning' && 'text-amber-400',
                    st.variant === 'destructive' && 'text-destructive',
                    st.variant === 'secondary' && 'text-muted-foreground',
                  )}>
                    {st.label}
                  </span>
                </span>
                <span
                  className="shrink-0 text-[10px] tabular-nums tracking-wide text-muted-foreground/70"
                  title={m.lastSeen ? new Date(m.lastSeen * 1000).toLocaleString('pt-BR') : undefined}
                >
                  {downFor || seen}
                </span>
              </div>
            </div>
            <div className="space-y-2">
              <Meter label="CPU" value={m.metrics?.cpuPercent || 0} muted={offline} />
              <Meter label="RAM" value={m.metrics?.ramPercent || 0} muted={offline} />
              <Meter label="Disco" value={m.metrics?.diskPercent || 0} muted={offline} />
            </div>
          </button>
        )
      })}
    </div>
  )
}

function InventorySlice({
  tab, machines, view, onOpen, t,
}: {
  tab: Exclude<FleetTab, 'machines'>
  machines: FleetMachine[]
  view: ViewMode
  onOpen: (m: FleetMachine) => void
  t: (k: string, d?: string) => string
}) {
  const rows = useMemo(() => {
    const list: { key: string; hostname: string; machine: FleetMachine; title: string; lines: string[] }[] = []
    for (const m of machines) {
      const inv = m.inventory
      if (tab === 'monitors') {
        (inv?.monitors || []).forEach((mon: any, i: number) => {
          list.push({
            key: `${m.hostname}-mon-${i}`,
            hostname: m.hostname,
            machine: m,
            title: `${mon.primary ? '★ ' : ''}${mon.model || 'Monitor'}`,
            lines: [mon.resolution, mon.primary ? t('computers.primaryMonitor', 'Principal') : ''].filter(Boolean),
          })
        })
      }
      if (tab === 'disks') {
        (inv?.storage?.disks || []).forEach((d: any, i: number) => {
          list.push({
            key: `${m.hostname}-disk-${i}`,
            hostname: m.hostname,
            machine: m,
            title: d.model || t('computers.tabDisks', 'Disco'),
            lines: [d.type, d.capacity_gb ? `${d.capacity_gb} GB` : ''].filter(Boolean),
          })
        })
      }
      if (tab === 'gpu') {
        (inv?.gpu || []).forEach((g: any, i: number) => {
          list.push({
            key: `${m.hostname}-gpu-${i}`,
            hostname: m.hostname,
            machine: m,
            title: g.model || 'GPU',
            lines: [g.vram_gb ? `${g.vram_gb} GB VRAM` : ''].filter(Boolean),
          })
        })
      }
      if (tab === 'memory') {
        (inv?.memory?.modules || []).forEach((mod: any, i: number) => {
          list.push({
            key: `${m.hostname}-ram-${i}`,
            hostname: m.hostname,
            machine: m,
            title: `${mod.slot || 'Slot'} · ${mod.size_gb ?? '—'} GB`,
            lines: [mod.type, mod.speed_mhz ? `${mod.speed_mhz} MHz` : '', mod.manufacturer].filter(Boolean),
          })
        })
      }
    }
    return list
  }, [machines, tab, t])

  const icon = tab === 'monitors' ? Monitor : tab === 'disks' ? HardDrive : tab === 'gpu' ? CircuitBoard : MemoryStick

  if (rows.length === 0) {
    return <p className="text-sm text-muted-foreground py-10 text-center">{t('computers.noComputers')}</p>
  }

  if (view === 'list') {
    return (
      <Card>
        <CardContent className="p-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('computers.hostname')}</TableHead>
                <TableHead>{t('computers.detail', 'Detalhe')}</TableHead>
                <TableHead>{t('computers.info', 'Info')}</TableHead>
                <TableHead>{t('computers.agent', 'Agent')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((row) => {
                const ag = agentBadge(row.machine.agentStatus, t)
                return (
                  <TableRow key={row.key} className="cursor-pointer" onClick={() => onOpen(row.machine)}>
                    <TableCell className="font-medium">{row.hostname}</TableCell>
                    <TableCell>{row.title}</TableCell>
                    <TableCell className="text-muted-foreground text-xs">{row.lines.join(' · ')}</TableCell>
                    <TableCell><Badge variant={ag.variant}>{ag.label}</Badge></TableCell>
                  </TableRow>
                )
              })}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    )
  }

  const Icon = icon
  return (
    <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
      {rows.map((row) => {
        const ag = agentBadge(row.machine.agentStatus, t)
        return (
          <button
            key={row.key}
            type="button"
            onClick={() => onOpen(row.machine)}
            className="text-left rounded-xl border border-border/50 bg-card/60 p-4 space-y-2 hover:border-primary/40 transition-colors"
          >
            <div className="flex items-start justify-between gap-2">
              <div className="flex items-center gap-2 min-w-0">
                <Icon className="h-4 w-4 text-muted-foreground shrink-0" />
                <p className="font-medium truncate">{row.title}</p>
              </div>
              <Badge variant={ag.variant}>{ag.label}</Badge>
            </div>
            <p className="text-xs text-muted-foreground">{row.hostname}</p>
            <p className="text-xs text-muted-foreground">{row.lines.join(' · ')}</p>
          </button>
        )
      })}
    </div>
  )
}
