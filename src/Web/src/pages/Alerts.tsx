import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { Table, TableHeader, TableBody, TableHead, TableRow, TableCell } from '@/components/ui/table'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Select } from '@/components/ui/select'
import { Input } from '@/components/ui/input'
import { useAlerts, useBulkUpdateAlerts } from '@/hooks/useAlerts'
import { formatRelative } from '@/lib/utils'
import { Search, Bell, CheckCircle, UserCheck } from 'lucide-react'
import { toast } from 'sonner'

const severityColors: Record<string, 'destructive' | 'warning' | 'info' | 'secondary'> = {
  Critical: 'destructive', High: 'warning', Medium: 'warning', Low: 'info', Info: 'secondary',
}

const statusColors: Record<string, 'destructive' | 'warning' | 'success' | 'secondary' | 'outline'> = {
  Open: 'destructive', Acknowledged: 'warning', InProgress: 'info', Resolved: 'success', FalsePositive: 'outline',
}

export function Alerts() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState('')
  const [severityFilter, setSeverityFilter] = useState('')
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [page, setPage] = useState(1)

  const params: Record<string, string> = { page: page.toString(), pageSize: '25' }
  if (search) params.search = search
  if (statusFilter) params.status = statusFilter
  if (severityFilter) params.severity = severityFilter

  const { data, isLoading } = useAlerts(params)
  const bulkUpdate = useBulkUpdateAlerts()

  const toggleSelect = (id: string) => {
    setSelected((prev) => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id); else next.add(id)
      return next
    })
  }

  const toggleAll = () => {
    if (!data?.items) return
    if (selected.size === data.items.length) setSelected(new Set())
    else setSelected(new Set(data.items.map((a) => a.id)))
  }

  const handleBulkAction = async (action: string) => {
    const ids = Array.from(selected)
    if (ids.length === 0) return
    try {
      await bulkUpdate.mutateAsync({ ids, status: action })
      toast.success(t('alerts.updated', { count: ids.length }))
      setSelected(new Set())
    } catch (err: any) {
      toast.error(err.message)
    }
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">{t('alerts.title')}</h1>
        <p className="text-muted-foreground text-sm">{t('alerts.subtitle')}</p>
      </div>

      <Card>
        <CardHeader>
          <div className="flex flex-col gap-3">
            <div className="flex items-center justify-between">
              <CardTitle className="text-base">{t('alerts.allAlerts')}</CardTitle>
              {selected.size > 0 && (
                <div className="flex items-center gap-2">
                  <span className="text-sm text-muted-foreground">{t('alerts.selected', { count: selected.size })}</span>
                  <Button variant="outline" size="sm" onClick={() => handleBulkAction('Acknowledged')}>
                    <CheckCircle className="h-4 w-4 mr-1" /> {t('alerts.acknowledge')}
                  </Button>
                  <Button variant="outline" size="sm" onClick={() => handleBulkAction('Resolved')}>
                    {t('alerts.resolve')}
                  </Button>
                  <Button variant="outline" size="sm">
                    <UserCheck className="h-4 w-4 mr-1" /> {t('alerts.assign')}
                  </Button>
                </div>
              )}
            </div>
            <div className="flex flex-wrap gap-2">
              <div className="relative">
                <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                <input
                  placeholder={t('alerts.search')}
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  onKeyDown={(e) => e.key === 'Enter' && setPage(1)}
                  className="h-9 w-56 rounded-lg border border-input bg-background pl-8 pr-3 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                />
              </div>
              <Select
                options={[{ value: '', label: t('alerts.allSeverities') }, { value: 'Critical', label: t('alerts.critical') }, { value: 'High', label: t('alerts.high') }, { value: 'Medium', label: t('alerts.medium') }, { value: 'Low', label: t('alerts.low') }, { value: 'Info', label: t('alerts.info') }]}
                value={severityFilter} onChange={(e) => { setSeverityFilter(e.target.value); setPage(1) }}
              />
              <Select
                options={[{ value: '', label: t('alerts.allStatus') }, { value: 'Open', label: t('alerts.open') }, { value: 'Acknowledged', label: t('alerts.acknowledged') }, { value: 'InProgress', label: t('alerts.inProgress') }, { value: 'Resolved', label: t('alerts.resolved') }, { value: 'FalsePositive', label: t('alerts.falsePositive') }]}
                value={statusFilter} onChange={(e) => { setStatusFilter(e.target.value); setPage(1) }}
              />
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-8">
                  <input type="checkbox" onChange={toggleAll} checked={data?.items ? selected.size === data.items.length : false} className="rounded" />
                </TableHead>
                <TableHead>{t('alerts.severity')}</TableHead>
                <TableHead>{t('alerts.title_col')}</TableHead>
                <TableHead>{t('alerts.category')}</TableHead>
                <TableHead>{t('alerts.computer')}</TableHead>
                <TableHead>{t('alerts.user')}</TableHead>
                <TableHead>{t('alerts.timestamp')}</TableHead>
                <TableHead>{t('alerts.status')}</TableHead>
                <TableHead>{t('alerts.assignedTo')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading ? (
                <TableRow><TableCell colSpan={9} className="text-center py-8 text-muted-foreground">{t('alerts.loading')}</TableCell></TableRow>
              ) : data?.items.length === 0 ? (
                <TableRow><TableCell colSpan={9} className="text-center py-8 text-muted-foreground">{t('alerts.noAlerts')}</TableCell></TableRow>
              ) : (
                data?.items.map((alert) => (
                  <TableRow key={alert.id} className="cursor-pointer" onClick={() => navigate(`/alerts/${alert.id}`)}>
                    <TableCell onClick={(e) => e.stopPropagation()}>
                      <input type="checkbox" checked={selected.has(alert.id)} onChange={() => toggleSelect(alert.id)} className="rounded" />
                    </TableCell>
                    <TableCell><Badge variant={severityColors[alert.severity]}>{t('alerts.' + alert.severity.toLowerCase(), { defaultValue: alert.severity })}</Badge></TableCell>
                    <TableCell className="font-medium max-w-[200px] truncate">{alert.title}</TableCell>
                    <TableCell className="text-muted-foreground text-xs">{alert.category}</TableCell>
                    <TableCell className="text-sm">{alert.computerName}</TableCell>
                    <TableCell className="text-sm">{alert.username || '-'}</TableCell>
                    <TableCell className="text-xs text-muted-foreground">{formatRelative(alert.timestamp)}</TableCell>
                    <TableCell><Badge variant={statusColors[alert.status]}>{alert.status}</Badge></TableCell>
                    <TableCell className="text-xs text-muted-foreground">{alert.assignedTo || '-'}</TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>

          {data && data.totalPages > 1 && (
            <div className="flex items-center justify-between mt-4">
              <p className="text-sm text-muted-foreground">{t('alerts.pageOf', { page: data.page, totalPages: data.totalPages, total: data.total })}</p>
              <div className="flex gap-2">
                <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>{t('alerts.previous')}</Button>
                <Button variant="outline" size="sm" disabled={page >= data.totalPages} onClick={() => setPage(page + 1)}>{t('alerts.next')}</Button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
