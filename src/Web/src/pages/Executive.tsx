import { useTranslation } from 'react-i18next'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Monitor, Activity, AlertTriangle, Shield, TrendingUp } from 'lucide-react'
import { ResponsiveContainer, LineChart, Line, BarChart, Bar, XAxis, YAxis, Tooltip, AreaChart, Area, RadialBarChart, RadialBar } from 'recharts'

const kpiCards = (t: (key: string) => string) => [
  { label: t('executive.totalComputers'), value: '1,423', change: '+12', icon: Monitor },
  { label: t('executive.availability'), value: '98.7%', change: '+0.5%', icon: Activity },
  { label: t('executive.criticalAlerts'), value: '3', change: '-2', icon: AlertTriangle, negative: true },
  { label: t('executive.securityScore'), value: '87', change: '+4', icon: Shield },
]

const trendData = Array.from({ length: 30 }, (_, i) => ({
  day: `D${i + 1}`,
  availability: 97 + Math.random() * 3,
  alerts: Math.floor(Math.random() * 10),
}))

const deptData = [
  { name: 'IT', value: 320 }, { name: 'Finance', value: 180 }, { name: 'HR', value: 95 },
  { name: 'Sales', value: 210 }, { name: 'Engineering', value: 280 }, { name: 'Legal', value: 60 },
]

const slaData = [{ name: 'SLA', value: 98.7, fill: '#6366f1' }]

export function Executive() {
  const { t } = useTranslation()
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">{t('executive.executiveDashboard')}</h1>
        <p className="text-muted-foreground text-sm">{t('executive.highLevelOverview')}</p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        {kpiCards(t).map((kpi) => (
          <Card key={kpi.label} className="card-hover">
            <CardContent className="p-5">
              <div className="flex items-center justify-between mb-2">
                <p className="text-xs text-muted-foreground uppercase tracking-wider">{kpi.label}</p>
                <kpi.icon className="h-5 w-5 text-muted-foreground" />
              </div>
              <p className="text-3xl font-bold">{kpi.value}</p>
              <p className={`text-sm mt-1 ${kpi.negative ? 'text-destructive' : 'text-emerald-400'}`}>
                {kpi.change} {t('executive.fromLastMonth')}
              </p>
            </CardContent>
          </Card>
        ))}
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between">
              <CardTitle className="text-base">{t('executive.availabilityTrend')}</CardTitle>
              <Badge variant="success">{t('executive.ninetyEightPointSevenAvg')}</Badge>
            </div>
          </CardHeader>
          <CardContent>
            <ResponsiveContainer width="100%" height={300}>
              <AreaChart data={trendData}>
                <defs>
                  <linearGradient id="execAvail" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#6366f1" stopOpacity={0.3} />
                    <stop offset="95%" stopColor="#6366f1" stopOpacity={0} />
                  </linearGradient>
                </defs>
                <XAxis dataKey="day" tick={{ fontSize: 11 }} />
                <YAxis domain={[95, 100]} tick={{ fontSize: 11 }} />
                <Tooltip />
                <Area type="monotone" dataKey="availability" stroke="#6366f1" fill="url(#execAvail)" strokeWidth={3} />
              </AreaChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle className="text-base">{t('executive.departmentBreakdown')}</CardTitle></CardHeader>
          <CardContent>
            <ResponsiveContainer width="100%" height={300}>
              <BarChart data={deptData} layout="vertical">
                <XAxis type="number" />
                <YAxis type="category" dataKey="name" width={100} tick={{ fontSize: 12 }} />
                <Tooltip />
                <Bar dataKey="value" fill="#6366f1" radius={[0, 4, 4, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <Card>
          <CardHeader><CardTitle className="text-base">{t('executive.slaAvailability')}</CardTitle></CardHeader>
          <CardContent className="flex flex-col items-center">
            <ResponsiveContainer width="100%" height={200}>
              <RadialBarChart cx="50%" cy="50%" innerRadius="60%" outerRadius="90%" barSize={20} data={slaData} startAngle={180} endAngle={0}>
                <RadialBar dataKey="value" cornerRadius={10} />
              </RadialBarChart>
            </ResponsiveContainer>
            <p className="text-3xl font-bold mt-2">98.7%</p>
            <p className="text-sm text-muted-foreground">{t('executive.aboveTarget')}</p>
          </CardContent>
        </Card>

        <Card className="lg:col-span-2">
          <CardHeader><CardTitle className="text-base">{t('executive.alertVolume')}</CardTitle></CardHeader>
          <CardContent>
            <ResponsiveContainer width="100%" height={200}>
              <LineChart data={trendData}>
                <XAxis dataKey="day" tick={{ fontSize: 11 }} />
                <Tooltip />
                <Line type="monotone" dataKey="alerts" stroke="#f97316" strokeWidth={2} dot={false} />
              </LineChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>
      </div>
    </div>
  )
}
