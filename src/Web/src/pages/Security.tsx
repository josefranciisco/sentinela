import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { useTranslation } from 'react-i18next'
import { useQuery } from '@tanstack/react-query'
import { Shield, AlertTriangle, Monitor, Activity } from 'lucide-react'
import { ResponsiveContainer, BarChart, Bar, XAxis, YAxis, Tooltip, Cell } from 'recharts'
import { api } from '@/lib/api'
import { formatRelative } from '@/lib/utils'

const COLORS = ['#ef4444', '#f97316', '#eab308', '#3b82f6', '#8b5cf6', '#6b7280']

interface SecuritySummary {
  eventsLast24h: number
  eventsLast7d: number
  criticalEvents: number
  highEvents: number
  openIncidents: number
  computersAtRisk: number
  topThreatCategories: { category: string; count: number }[]
  compliance: { name: string; value: number }[]
}

interface SecurityEventsResponse {
  items: {
    id: string
    severity: string
    description: string
    timestamp: string
    computerName?: string
    category: string
    eventType: string
  }[]
}

export function Security() {
  const { t } = useTranslation()

  const { data: summary } = useQuery({
    queryKey: ['security-summary'],
    queryFn: () => api.get<SecuritySummary>('/security/summary'),
    refetchInterval: 15000,
  })

  const { data: eventsPage } = useQuery({
    queryKey: ['security-events'],
    queryFn: () => api.get<SecurityEventsResponse>('/security/events?pageSize=10'),
    refetchInterval: 15000,
  })

  const summaryCards = [
    {
      label: t('security.eventsToday', 'Events Today'),
      value: String(summary?.eventsLast24h ?? 0),
      icon: Activity,
      color: 'from-blue-500 to-cyan-500',
    },
    {
      label: t('security.criticalAlerts', 'Critical Alerts'),
      value: String(summary?.criticalEvents ?? 0),
      icon: AlertTriangle,
      color: 'from-red-500 to-rose-500',
    },
    {
      label: t('security.openIncidents', 'Open Incidents'),
      value: String(summary?.openIncidents ?? 0),
      icon: Shield,
      color: 'from-amber-500 to-orange-500',
    },
    {
      label: t('security.computersAtRisk', 'Computers at Risk'),
      value: String(summary?.computersAtRisk ?? 0),
      icon: Monitor,
      color: 'from-violet-500 to-purple-500',
    },
  ]

  const categoryData = (summary?.topThreatCategories || []).map((c) => ({
    name: c.category || 'Other',
    count: c.count,
  }))

  const complianceData = summary?.compliance?.length
    ? summary.compliance
    : [
        { name: 'Firewall', value: 0 },
        { name: 'Defender', value: 0 },
        { name: 'BitLocker', value: 0 },
        { name: 'RDP Hardened', value: 0 },
      ]

  const recentEvents = eventsPage?.items || []

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">{t('security.title')}</h1>
        <p className="text-muted-foreground text-sm">{t('security.subtitle')}</p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        {summaryCards.map((card) => (
          <Card key={card.label} className="card-hover">
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
        ))}
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <Card>
          <CardHeader><CardTitle className="text-base">{t('security.securityCategories')}</CardTitle></CardHeader>
          <CardContent>
            {categoryData.length === 0 ? (
              <p className="text-sm text-muted-foreground text-center py-16">
                {t('security.noCategories', 'Nenhuma categoria de ameaça ainda')}
              </p>
            ) : (
              <ResponsiveContainer width="100%" height={300}>
                <BarChart data={categoryData}>
                  <XAxis dataKey="name" tick={{ fontSize: 11 }} />
                  <YAxis />
                  <Tooltip />
                  <Bar dataKey="count" radius={[4, 4, 0, 0]}>
                    {categoryData.map((_, idx) => (
                      <Cell key={idx} fill={COLORS[idx % COLORS.length]} />
                    ))}
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle className="text-base">{t('security.securityCompliance')}</CardTitle></CardHeader>
          <CardContent>
            <div className="space-y-4">
              {complianceData.map((item) => (
                <div key={item.name}>
                  <div className="flex items-center justify-between text-sm mb-1">
                    <span className="text-muted-foreground">{item.name}</span>
                    <span className="font-medium">{item.value}%</span>
                  </div>
                  <div className="h-2 rounded-full bg-muted overflow-hidden">
                    <div
                      className="h-full rounded-full transition-all duration-500"
                      style={{
                        width: `${item.value}%`,
                        backgroundColor: item.value >= 80 ? '#22c55e' : item.value >= 60 ? '#eab308' : '#ef4444',
                      }}
                    />
                  </div>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader><CardTitle className="text-base">{t('security.recentEvents')}</CardTitle></CardHeader>
        <CardContent>
          <div className="space-y-3">
            {recentEvents.length === 0 ? (
              <p className="text-sm text-muted-foreground text-center py-4">
                {t('security.noEvents', 'Nenhum evento de segurança recente')}
              </p>
            ) : (
              recentEvents.map((event) => (
                <div key={event.id} className="flex items-center gap-3 text-sm p-3 rounded-lg bg-muted/30">
                  <Badge
                    variant={
                      event.severity === 'Critical'
                        ? 'destructive'
                        : event.severity === 'High'
                          ? 'warning'
                          : 'info'
                    }
                  >
                    {t('alerts.' + event.severity.toLowerCase(), { defaultValue: event.severity })}
                  </Badge>
                  <div className="flex-1 min-w-0">
                    <p className="truncate">{event.description}</p>
                    <p className="text-xs text-muted-foreground">
                      {formatRelative(event.timestamp)} &middot; {event.computerName || event.category}
                    </p>
                  </div>
                </div>
              ))
            )}
          </div>
        </CardContent>
      </Card>
    </div>
  )
}
