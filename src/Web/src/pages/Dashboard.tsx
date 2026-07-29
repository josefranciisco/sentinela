import { useState } from 'react'
import { motion } from 'framer-motion'
import { useTranslation } from 'react-i18next'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { useDashboardStats, useDashboardActivity, useDashboardTopApps, useDashboardAvailability, useDashboardHeatmap } from '@/hooks/useDashboard'
import { formatRelative, formatDate } from '@/lib/utils'
import { RefreshCw, Monitor, Users, AlertTriangle, Activity, Wifi, WifiOff } from 'lucide-react'
import { PieChart, Pie, Cell, BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, AreaChart, Area, LineChart, Line } from 'recharts'

const severityColors: Record<string, string> = {
  Critical: '#ef4444', High: '#f97316', Medium: '#eab308', Low: '#3b82f6', Info: '#6b7280',
}

export function Dashboard() {
  const { t } = useTranslation()
  const [autoRefresh, setAutoRefresh] = useState(false)
  const { data: stats, isLoading: statsLoading } = useDashboardStats()
  const { data: activity } = useDashboardActivity()
  const { data: topApps } = useDashboardTopApps()
  const { data: availability } = useDashboardAvailability()
  const { data: heatmap } = useDashboardHeatmap()

  const statCards = [
    { label: t('dashboard.totalComputers'), value: stats?.totalComputers ?? 0, icon: Monitor, color: 'from-primary to-blue-500' },
    { label: t('dashboard.online'), value: stats?.onlineComputers ?? 0, icon: Wifi, color: 'from-emerald-500 to-green-500' },
    { label: t('dashboard.offline'), value: stats?.offlineComputers ?? 0, icon: WifiOff, color: 'from-red-500 to-rose-500' },
    { label: t('dashboard.activeAlerts'), value: stats?.totalAlerts ?? 0, icon: AlertTriangle, color: 'from-amber-500 to-orange-500' },
    { label: t('dashboard.users'), value: stats?.totalUsers ?? 0, icon: Users, color: 'from-violet-500 to-purple-500' },
  ]

  const donutData = [
    { name: t('dashboard.online'), value: stats?.onlineComputers ?? 0, color: '#22c55e' },
    { name: t('dashboard.offline'), value: stats?.offlineComputers ?? 0, color: '#ef4444' },
    { name: t('dashboard.away'), value: (stats?.totalComputers ?? 0) - (stats?.onlineComputers ?? 0) - (stats?.offlineComputers ?? 0), color: '#eab308' },
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
            <input type="checkbox" checked={autoRefresh} onChange={() => setAutoRefresh(!autoRefresh)} className="rounded" />
            {t('dashboard.autoRefresh')}
          </label>
          <Button variant="outline" size="sm"><RefreshCw className="h-4 w-4" /></Button>
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
          <CardHeader><CardTitle className="text-base">{t('dashboard.activityTimeline')}</CardTitle></CardHeader>
          <CardContent>
            <div className="space-y-3">
              {activity?.slice(0, 8).map((event: any) => (
                <div key={event.id} className="flex items-start gap-3 text-sm">
                  <div className="w-2 h-2 mt-1.5 rounded-full shrink-0" style={{ backgroundColor: severityColors[event.severity] || '#6b7280' }} />
                  <div className="flex-1 min-w-0">
                    <p className="truncate">{event.description}</p>
                    <p className="text-xs text-muted-foreground">{event.computerName} &middot; {formatRelative(event.timestamp)}</p>
                  </div>
                </div>
              ))}
              {(!activity || activity.length === 0) && <p className="text-sm text-muted-foreground text-center py-4">{t('dashboard.noRecentActivity')}</p>}
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

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <Card>
          <CardHeader><CardTitle className="text-base">{t('dashboard.topApplications')}</CardTitle></CardHeader>
          <CardContent>
            <ResponsiveContainer width="100%" height={250}>
              <BarChart data={topApps?.slice(0, 8) || []} layout="vertical">
                <XAxis type="number" hide />
                <YAxis type="category" dataKey="name" width={140} tick={{ fontSize: 12 }} />
                <Tooltip />
                <Bar dataKey="count" fill="hsl(var(--primary))" radius={[0, 4, 4, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle className="text-base">{t('dashboard.availability')}</CardTitle></CardHeader>
          <CardContent>
            <ResponsiveContainer width="100%" height={250}>
              <AreaChart data={availability || []}>
                <defs>
                  <linearGradient id="availGrad" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="hsl(var(--primary))" stopOpacity={0.3} />
                    <stop offset="95%" stopColor="hsl(var(--primary))" stopOpacity={0} />
                  </linearGradient>
                </defs>
                <XAxis dataKey="date" tick={{ fontSize: 11 }} />
                <YAxis domain={[90, 100]} tick={{ fontSize: 11 }} />
                <Tooltip />
                <Area type="monotone" dataKey="percentage" stroke="hsl(var(--primary))" fill="url(#availGrad)" strokeWidth={2} />
              </AreaChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader><CardTitle className="text-base">{t('dashboard.activityHeatmap')}</CardTitle></CardHeader>
        <CardContent>
          <ResponsiveContainer width="100%" height={160}>
            <LineChart data={heatmap || []}>
              <XAxis dataKey="hour" tick={{ fontSize: 11 }} />
              <Tooltip />
              <Line type="monotone" dataKey="value" stroke="hsl(var(--primary))" strokeWidth={2} dot={false} />
            </LineChart>
          </ResponsiveContainer>
        </CardContent>
      </Card>
    </div>
  )
}
