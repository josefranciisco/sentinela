import { useParams, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs'
import { Table, TableHeader, TableBody, TableHead, TableRow, TableCell } from '@/components/ui/table'
import { useComputer, useComputerTimeline, useComputerApplications, useComputerAlerts } from '@/hooks/useComputers'
import { formatDate, formatRelative, formatDuration } from '@/lib/utils'
import { ArrowLeft, Monitor, Clock, HardDrive, Shield, Activity, Package, Bell, Camera, Radio } from 'lucide-react'

const severityColors: Record<string, string> = {
  Critical: 'destructive', High: 'warning', Medium: 'warning', Low: 'info', Info: 'secondary',
}

export function ComputerDetail() {
  const { t } = useTranslation()
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { data: computer, isLoading } = useComputer(id!)
  const { data: timeline } = useComputerTimeline(id!)
  const { data: apps } = useComputerApplications(id!)
  const { data: alerts } = useComputerAlerts(id!)

  if (isLoading) return <div className="flex items-center justify-center h-64 text-muted-foreground">{t('computerDetail.loading')}</div>
  if (!computer) return <div className="flex items-center justify-center h-64 text-muted-foreground">{t('computerDetail.computerNotFound')}</div>

  return (
    <div className="space-y-6">
      <Button variant="ghost" size="sm" onClick={() => navigate('/computers')} className="mb-2">
        <ArrowLeft className="h-4 w-4 mr-1" /> {t('computerDetail.back')}
      </Button>

      <div className="flex items-start justify-between">
        <div className="flex items-center gap-4">
          <div className="p-3 rounded-xl bg-gradient-to-br from-primary to-blue-500 text-white">
            <Monitor className="h-6 w-6" />
          </div>
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-2xl font-bold">{computer.hostname}</h1>
              <Badge variant={computer.status === 'Online' ? 'success' : computer.status === 'Offline' ? 'destructive' : 'warning'}>{computer.status}</Badge>
            </div>
            <p className="text-sm text-muted-foreground">{computer.ipAddress} &middot; {computer.domain || t('computerDetail.noDomain')}</p>
          </div>
        </div>
      </div>

      <Tabs defaultValue="overview">
        <TabsList>
          <TabsTrigger value="overview"><Activity className="h-4 w-4 mr-1" /> {t('computerDetail.overview')}</TabsTrigger>
          <TabsTrigger value="timeline"><Clock className="h-4 w-4 mr-1" /> {t('computerDetail.timeline')}</TabsTrigger>
          <TabsTrigger value="applications"><Package className="h-4 w-4 mr-1" /> {t('computerDetail.applications')}</TabsTrigger>
          <TabsTrigger value="security"><Shield className="h-4 w-4 mr-1" /> {t('computerDetail.security')}</TabsTrigger>
          <TabsTrigger value="alerts"><Bell className="h-4 w-4 mr-1" /> {t('computerDetail.alerts')}</TabsTrigger>
          <TabsTrigger value="screenshots"><Camera className="h-4 w-4 mr-1" /> {t('computerDetail.screenshots')}</TabsTrigger>
          <TabsTrigger value="remote"><Radio className="h-4 w-4 mr-1" /> {t('computerDetail.remote')}</TabsTrigger>
        </TabsList>

        <TabsContent value="overview">
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            <Card className="lg:col-span-2">
              <CardHeader><CardTitle className="text-base">{t('computerDetail.systemInformation')}</CardTitle></CardHeader>
              <CardContent>
                <dl className="grid grid-cols-2 gap-4 text-sm">
                  <div><dt className="text-muted-foreground">{t('computerDetail.operatingSystem')}</dt><dd className="font-medium">{computer.osVersion}</dd></div>
                  <div><dt className="text-muted-foreground">{t('computerDetail.agentVersion')}</dt><dd className="font-medium">{computer.agentVersion}</dd></div>
                  <div><dt className="text-muted-foreground">{t('computerDetail.department')}</dt><dd className="font-medium">{computer.department || '-'}</dd></div>
                  <div><dt className="text-muted-foreground">{t('computerDetail.currentUser')}</dt><dd className="font-medium">{computer.currentUser || '-'}</dd></div>
                  <div><dt className="text-muted-foreground">{t('computerDetail.macAddress')}</dt><dd className="font-medium font-mono text-xs">{computer.macAddress}</dd></div>
                  <div><dt className="text-muted-foreground">{t('computerDetail.lastHeartbeat')}</dt><dd className="font-medium">{formatRelative(computer.lastHeartbeat)}</dd></div>
                  <div><dt className="text-muted-foreground">{t('computerDetail.uptime')}</dt><dd className="font-medium">{formatDuration(computer.uptime)}</dd></div>
                  <div><dt className="text-muted-foreground">{t('computerDetail.domain')}</dt><dd className="font-medium">{computer.domain || '-'}</dd></div>
                </dl>
              </CardContent>
            </Card>

            <Card>
              <CardHeader><CardTitle className="text-base">{t('computerDetail.securityStatus')}</CardTitle></CardHeader>
              <CardContent className="space-y-3">
                {[{ label: t('computerDetail.firewall'), ok: true }, { label: t('computerDetail.defender'), ok: true }, { label: t('computerDetail.bitLocker'), ok: false }, { label: t('computerDetail.rdp'), ok: true }].map((s) => (
                  <div key={s.label} className="flex items-center justify-between text-sm">
                    <span className="text-muted-foreground">{s.label}</span>
                    <Badge variant={s.ok ? 'success' : 'destructive'}>{s.ok ? t('computerDetail.active') : t('computerDetail.inactive')}</Badge>
                  </div>
                ))}
              </CardContent>
            </Card>
          </div>

          <Card className="mt-6">
            <CardHeader><CardTitle className="text-base">{t('computerDetail.recentActivity')}</CardTitle></CardHeader>
            <CardContent>
              <div className="space-y-2">
                {timeline?.slice(0, 10).map((event: any) => (
                  <div key={event.id} className="flex items-start gap-3 text-sm py-1.5">
                    <div className="w-2 h-2 mt-1.5 rounded-full shrink-0 bg-primary/60" />
                    <div className="flex-1 min-w-0">
                      <p className="truncate">{event.description}</p>
                      <p className="text-xs text-muted-foreground">{formatRelative(event.timestamp)}</p>
                    </div>
                  </div>
                ))}
                {(!timeline || timeline.length === 0) && <p className="text-sm text-muted-foreground text-center py-4">{t('computerDetail.noRecentActivity')}</p>}
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="timeline">
          <Card>
            <CardHeader><CardTitle className="text-base">{t('computerDetail.fullTimeline')}</CardTitle></CardHeader>
            <CardContent>
              <div className="space-y-2">
                {timeline?.map((event: any) => (
                  <div key={event.id} className="flex items-start gap-3 text-sm py-2 border-b border-border/50 last:border-0">
                    <div className="w-2 h-2 mt-1.5 rounded-full shrink-0 bg-primary/60" />
                    <div className="flex-1 min-w-0">
                      <p className="font-medium">{event.eventType}</p>
                      <p className="text-muted-foreground">{event.description}</p>
                      <p className="text-xs text-muted-foreground mt-0.5">{formatDate(event.timestamp)} &middot; {event.username || t('computerDetail.system')}</p>
                    </div>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="applications">
          <Card>
            <CardHeader><CardTitle className="text-base">{t('computerDetail.installedApplications')}</CardTitle></CardHeader>
            <CardContent>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t('computerDetail.name')}</TableHead>
                    <TableHead>{t('computerDetail.executable')}</TableHead>
                    <TableHead>{t('computerDetail.totalTime')}</TableHead>
                    <TableHead>{t('computerDetail.executions')}</TableHead>
                    <TableHead>{t('computerDetail.firstSeen')}</TableHead>
                    <TableHead>{t('computerDetail.lastSeen')}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {apps?.map((app: any) => (
                    <TableRow key={app.id}>
                      <TableCell className="font-medium">{app.name}</TableCell>
                      <TableCell className="text-xs font-mono text-muted-foreground">{app.executable}</TableCell>
                      <TableCell>{formatDuration(app.totalTime)}</TableCell>
                      <TableCell>{app.executions}</TableCell>
                      <TableCell className="text-xs">{formatDate(app.firstSeen)}</TableCell>
                      <TableCell className="text-xs">{formatDate(app.lastSeen)}</TableCell>
                    </TableRow>
                  ))}
                  {(!apps || apps.length === 0) && (
                    <TableRow><TableCell colSpan={6} className="text-center py-4 text-muted-foreground">{t('computerDetail.noApplicationsTracked')}</TableCell></TableRow>
                  )}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="security">
          <Card>
            <CardHeader><CardTitle className="text-base">{t('computerDetail.securityEvents')}</CardTitle></CardHeader>
            <CardContent>
              <p className="text-sm text-muted-foreground">{t('computerDetail.securityMonitoringPlaceholder')}</p>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="alerts">
          <Card>
            <CardHeader><CardTitle className="text-base">{t('computerDetail.computerAlerts')}</CardTitle></CardHeader>
            <CardContent>
              {alerts?.map((alert: any) => (
                <div key={alert.id} className="flex items-center justify-between py-2 border-b border-border/50 last:border-0">
                  <div className="flex items-center gap-3">
                    <Badge variant={severityColors[alert.severity]}>{t('alerts.' + alert.severity.toLowerCase(), { defaultValue: alert.severity })}</Badge>
                    <div>
                      <p className="text-sm font-medium">{alert.title}</p>
                      <p className="text-xs text-muted-foreground">{formatRelative(alert.timestamp)}</p>
                    </div>
                  </div>
                  <Badge variant="outline">{alert.status}</Badge>
                </div>
              ))}
              {(!alerts || alerts.length === 0) && <p className="text-sm text-muted-foreground text-center py-4">{t('computerDetail.noAlerts')}</p>}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="screenshots">
          <Card><CardContent className="py-8 text-center text-muted-foreground">{t('computerDetail.screenshotGalleryComingSoon')}</CardContent></Card>
        </TabsContent>

        <TabsContent value="remote">
          <Card><CardContent className="py-8 text-center text-muted-foreground">{t('computerDetail.remoteAssistanceComingSoon')}</CardContent></Card>
        </TabsContent>
      </Tabs>
    </div>
  )
}
