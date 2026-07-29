import { useState, useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { Card, CardContent } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Monitor, AlertTriangle, Activity, Wifi, WifiOff, Bell } from 'lucide-react'
import { ResponsiveContainer, PieChart, Pie, Cell } from 'recharts'

const views = (t: (key: string) => string) => [
  { id: 'overview', label: t('noc.overview') },
  { id: 'alerts', label: t('noc.criticalAlerts') },
  { id: 'stats', label: t('noc.statistics') },
]

const mockCriticalAlerts: { title: string; computer: string; severity: string; time: string }[] = []

export function Noc() {
  const { t } = useTranslation()
  const [currentView, setCurrentView] = useState(0)
  const [fullscreen, setFullscreen] = useState(false)
  const [time, setTime] = useState(new Date())

  useEffect(() => {
    const interval = setInterval(() => setTime(new Date()), 1000)
    return () => clearInterval(interval)
  }, [])

  useEffect(() => {
    const viewInterval = setInterval(() => {
      setCurrentView((prev) => (prev + 1) % views(t).length)
    }, 10000)
    return () => clearInterval(viewInterval)
  }, [])

  const toggleFullscreen = async () => {
    if (!document.fullscreenElement) {
      await document.documentElement.requestFullscreen()
      setFullscreen(true)
    } else {
      await document.exitFullscreen()
      setFullscreen(false)
    }
  }

  const donutData = [
    { name: t('noc.online'), value: 1, color: '#22c55e' },
    { name: t('noc.offline'), value: 0, color: '#ef4444' },
    { name: t('noc.away'), value: 0, color: '#eab308' },
  ]

  return (
    <div className="min-h-[calc(100vh-8rem)] -m-6 p-8 bg-background">
      <div className="flex items-center justify-between mb-8">
        <div className="flex items-center gap-4">
          <h1 className="text-3xl font-bold bg-clip-text text-transparent bg-gradient-to-r from-primary to-blue-500">
            {t('noc.nocMode')}
          </h1>
          <div className="flex gap-2">
            {views(t).map((v, i) => (
              <button
                key={v.id}
                onClick={() => setCurrentView(i)}
                className={`px-3 py-1 rounded-lg text-sm transition-colors ${currentView === i ? 'bg-primary text-primary-foreground' : 'bg-muted text-muted-foreground'}`}
              >
                {v.label}
              </button>
            ))}
          </div>
        </div>
        <div className="flex items-center gap-6">
          <span className="text-2xl font-mono font-bold">{time.toLocaleTimeString('pt-BR')}</span>
          <button
            onClick={toggleFullscreen}
            className="px-4 py-2 rounded-lg bg-muted hover:bg-accent text-sm transition-colors"
          >
            {fullscreen ? t('noc.exitFullscreen') : t('noc.fullscreen')}
          </button>
        </div>
      </div>

      {views(t)[currentView].id === 'overview' && (
        <div className="space-y-8">
          <div className="grid grid-cols-2 lg:grid-cols-4 gap-6">
            {[
              { label: t('noc.totalComputers'), value: '1', icon: Monitor, color: 'from-primary to-blue-500' },
              { label: t('noc.online'), value: '1', icon: Wifi, color: 'from-emerald-500 to-green-500' },
              { label: t('noc.offline'), value: '0', icon: WifiOff, color: 'from-red-500 to-rose-500' },
              { label: t('noc.criticalAlerts'), value: '0', icon: AlertTriangle, color: 'from-amber-500 to-orange-500' },
            ].map((stat) => (
              <Card key={stat.label} className="bg-card/80 backdrop-blur border-border/50">
                <CardContent className="p-6 text-center">
                  <div className={`inline-flex p-3 rounded-xl bg-gradient-to-br ${stat.color} text-white mb-3`}>
                    <stat.icon className="h-7 w-7" />
                  </div>
                  <p className="text-4xl font-bold">{stat.value}</p>
                  <p className="text-sm text-muted-foreground uppercase tracking-wider mt-1">{stat.label}</p>
                </CardContent>
              </Card>
            ))}
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
            <Card className="bg-card/80 backdrop-blur border-border/50">
              <CardContent className="p-6">
                <h3 className="text-lg font-semibold mb-4">{t('noc.statusDistribution')}</h3>
                <div className="flex items-center gap-8">
                  <ResponsiveContainer width={200} height={200}>
                    <PieChart>
                      <Pie data={donutData} cx="50%" cy="50%" innerRadius={60} outerRadius={90} dataKey="value">
                        {donutData.map((entry, idx) => (
                          <Cell key={idx} fill={entry.color} />
                        ))}
                      </Pie>
                    </PieChart>
                  </ResponsiveContainer>
                  <div className="space-y-3">
                    {donutData.map((d) => (
                      <div key={d.name} className="flex items-center gap-2">
                        <div className="w-3 h-3 rounded-full" style={{ backgroundColor: d.color }} />
                        <span className="text-sm">{d.name}: <strong>{d.value}</strong></span>
                      </div>
                    ))}
                  </div>
                </div>
              </CardContent>
            </Card>

            <Card className="bg-card/80 backdrop-blur border-border/50">
              <CardContent className="p-6">
                <h3 className="text-lg font-semibold mb-4">{t('noc.recentActivity')}</h3>
                <div className="space-y-3">
                  <div className="flex items-center gap-3 text-sm text-muted-foreground">
                    <Activity className="h-4 w-4" />
                    <span>{t('noc.noRecentActivity', 'Nenhuma atividade recente')}</span>
                  </div>
                </div>
              </CardContent>
            </Card>
          </div>
        </div>
      )}

      {views(t)[currentView].id === 'alerts' && (
        <div className="space-y-4">
          <h2 className="text-xl font-bold flex items-center gap-2">
            <Bell className="h-5 w-5 text-destructive" />
            {t('noc.activeCriticalAlerts')}
          </h2>
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
            {mockCriticalAlerts.map((alert, i) => (
              <Card key={i} className="bg-card/80 backdrop-blur border-destructive/30 animate-pulse-slow">
                <CardContent className="p-5">
                  <div className="flex items-start justify-between">
                    <div>
                      <div className="flex items-center gap-2 mb-1">
                        <Badge variant="destructive">{t('alerts.' + alert.severity.toLowerCase(), { defaultValue: alert.severity })}</Badge>
                        <span className="text-xs text-muted-foreground">{alert.time}</span>
                      </div>
                      <p className="text-lg font-bold">{alert.title}</p>
                      <p className="text-sm text-muted-foreground">{alert.computer}</p>
                    </div>
                    <AlertTriangle className="h-8 w-8 text-destructive" />
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>
        </div>
      )}

      {views(t)[currentView].id === 'stats' && (
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-6">
          {[
            { label: t('noc.avgResponseTime'), value: '1.2s', change: '+0.1s' },
            { label: t('noc.eventsPerMin'), value: '247', change: '+12%' },
            { label: t('noc.activeSessions'), value: '38', change: '-2' },
            { label: t('noc.bandwidthUsage'), value: '2.4 Gbps', change: '+8%' },
            { label: t('noc.cpuUsageAvg'), value: '42%', change: '+3%' },
            { label: t('noc.memoryUsage'), value: '68%', change: '-1%' },
            { label: t('noc.diskIo'), value: '156 MB/s', change: '+5%' },
            { label: t('noc.networkLatency'), value: '4ms', change: '-0.5ms' },
          ].map((stat) => (
            <Card key={stat.label} className="bg-card/80 backdrop-blur border-border/50">
              <CardContent className="p-5 text-center">
                <p className="text-muted-foreground text-sm uppercase tracking-wider mb-1">{stat.label}</p>
                <p className="text-3xl font-bold">{stat.value}</p>
                <p className={`text-sm mt-1 ${stat.change.startsWith('+') ? 'text-emerald-400' : 'text-destructive'}`}>{stat.change}</p>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  )
}
