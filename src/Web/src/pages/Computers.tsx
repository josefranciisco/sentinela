import { useTranslation } from 'react-i18next'
import { useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { Table, TableHeader, TableBody, TableHead, TableRow, TableCell } from '@/components/ui/table'
import { Select } from '@/components/ui/select'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Dialog, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog'
import { HasPermission } from '@/components/HasPermission'
import { useComputers, useDepartments, useDeleteComputer } from '@/hooks/useComputers'
import { useLiveRelativeTime } from '@/hooks/useLiveRelativeTime'
import { Search, ChevronLeft, ChevronRight, Monitor, Trash2, Loader2 } from 'lucide-react'
import { toast } from 'sonner'

function HeartbeatTime({ date }: { date: string | null }) {
  return <>{useLiveRelativeTime(date)}</>
}

const statusColors: Record<string, 'success' | 'destructive' | 'warning' | 'secondary'> = {
  Online: 'success', Offline: 'destructive', Away: 'warning', Disabled: 'secondary',
}

export function Computers() {
  const navigate = useNavigate()
  const [searchParams, setSearchParams] = useSearchParams()
  const [search, setSearch] = useState(searchParams.get('search') || '')
  const [status, setStatus] = useState(searchParams.get('status') || '')
  const [department, setDepartment] = useState(searchParams.get('department') || '')
  const page = parseInt(searchParams.get('page') || '1')

  const params: Record<string, string> = { page: page.toString(), pageSize: '20' }
  if (search) params.search = search
  if (status) params.status = status
  if (department) params.department = department

  const { data, isLoading } = useComputers(params)
  const { data: departments = [] } = useDepartments()
  const deleteComputer = useDeleteComputer()
  const [confirmDelete, setConfirmDelete] = useState<any | null>(null)
  const { t } = useTranslation()

  const statusOptions = [
    { value: '', label: t('computers.allStatus') },
    { value: 'Online', label: t('computers.statusOnline') },
    { value: 'Offline', label: t('computers.statusOffline') },
    { value: 'Away', label: t('computers.statusAway') },
    { value: 'Disabled', label: t('computers.statusDisabled') },
  ]

  const departmentOptions = [
    { value: '', label: t('computers.allDepartments') },
    ...departments.map((d) => ({ value: d, label: d })),
  ]

  const statusLabel = (status: string) =>
    t(`computers.status${status}`, status)

  const updateParams = () => {
    const sp = new URLSearchParams()
    if (search) sp.set('search', search)
    if (status) sp.set('status', status)
    if (department) sp.set('department', department)
    sp.set('page', '1')
    setSearchParams(sp)
  }

  const handlePageChange = (newPage: number) => {
    const sp = new URLSearchParams(searchParams)
    sp.set('page', newPage.toString())
    setSearchParams(sp)
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">{t('computers.title')}</h1>
        <p className="text-muted-foreground text-sm">{t('computers.subtitle')}</p>
      </div>

      <Card>
        <CardHeader>
          <div className="flex flex-col md:flex-row gap-3 items-start md:items-center justify-between">
            <CardTitle className="text-base">{t('computers.allComputers')}</CardTitle>
            <div className="flex flex-wrap gap-2">
              <div className="relative">
                <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                <input
                  placeholder={t('computers.search')}
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  onKeyDown={(e) => e.key === 'Enter' && updateParams()}
                  className="h-9 w-48 rounded-lg border border-input bg-background pl-8 pr-3 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                />
              </div>
              <Select
                options={statusOptions}
                value={status} onChange={(e) => { setStatus(e.target.value); setTimeout(updateParams, 0) }}
              />
              <Select
                options={departmentOptions}
                value={department} onChange={(e) => { setDepartment(e.target.value); setTimeout(updateParams, 0) }}
              />
              <Button variant="secondary" size="sm" onClick={updateParams}>{t('computers.filter')}</Button>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('computers.hostname')}</TableHead>
                <TableHead>{t('computers.ipAddress')}</TableHead>
                <TableHead>{t('computers.user')}</TableHead>
                <TableHead>{t('computers.department')}</TableHead>
                <TableHead>{t('computers.status')}</TableHead>
                <TableHead>{t('computers.lastHeartbeat')}</TableHead>
                <TableHead></TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading ? (
                <TableRow><TableCell colSpan={7} className="text-center py-8 text-muted-foreground">{t('computers.loading')}</TableCell></TableRow>
              ) : data?.items.length === 0 ? (
                <TableRow><TableCell colSpan={7} className="text-center py-8 text-muted-foreground">{t('computers.noComputers')}</TableCell></TableRow>
              ) : (
                data?.items.map((computer) => (
                  <TableRow key={computer.id} className="cursor-pointer" onClick={() => navigate(`/computers/${computer.id}`)}>
                    <TableCell className="font-medium">
                      <div className="flex items-center gap-2">
                        <Monitor className="h-4 w-4 text-muted-foreground shrink-0" />
                        <span className="truncate">{computer.hostname || '-'}</span>
                      </div>
                    </TableCell>
                    <TableCell className="text-muted-foreground font-mono text-xs">{computer.ipAddress}</TableCell>
                    <TableCell>{computer.currentUser || '-'}</TableCell>
                    <TableCell>{computer.department || '-'}</TableCell>
                    <TableCell>
                      <Badge variant={statusColors[computer.status]}>{statusLabel(computer.status)}</Badge>
                    </TableCell>
                    <TableCell className="text-muted-foreground text-xs"><HeartbeatTime date={computer.lastHeartbeat} /></TableCell>
                    <TableCell className="text-right">
                      <HasPermission permission="machines.delete">
                        <button
                          type="button"
                          onClick={(e) => { e.stopPropagation(); setConfirmDelete(computer) }}
                          className="inline-flex h-8 w-8 items-center justify-center rounded-md text-muted-foreground transition-colors hover:bg-destructive/10 hover:text-destructive"
                          title={t('computers.delete')}
                        >
                          <Trash2 className="h-4 w-4" />
                        </button>
                      </HasPermission>
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>

          {data && data.totalPages > 1 && (
            <div className="flex items-center justify-between mt-4">
              <p className="text-sm text-muted-foreground">{t('computers.pageOf', { page: data.page, totalPages: data.totalPages, total: data.total })}</p>
              <div className="flex gap-2">
                <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => handlePageChange(page - 1)}>
                  <ChevronLeft className="h-4 w-4" />
                </Button>
                <Button variant="outline" size="sm" disabled={page >= data.totalPages} onClick={() => handlePageChange(page + 1)}>
                  <ChevronRight className="h-4 w-4" />
                </Button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      <Dialog open={!!confirmDelete} onClose={() => setConfirmDelete(null)}>
        <DialogHeader>
          <DialogTitle>{t('computers.confirmDeleteTitle')}</DialogTitle>
        </DialogHeader>
        <p className="text-sm text-muted-foreground">
          {t('computers.confirmDeleteMessage')}
        </p>
        <p className="text-sm font-medium">{confirmDelete?.hostname}</p>
        <DialogFooter>
          <Button variant="outline" onClick={() => setConfirmDelete(null)}>{t('computers.cancel')}</Button>
          <Button
            variant="destructive"
            disabled={deleteComputer.isPending}
            onClick={() => {
              if (!confirmDelete) return
              deleteComputer.mutate(confirmDelete.id, {
                onSuccess: () => {
                  toast.success(t('computers.deleted'))
                  setConfirmDelete(null)
                },
                onError: () => {
                  toast.error(t('computers.deleteFailed'))
                  setConfirmDelete(null)
                },
              })
            }}
          >
            {deleteComputer.isPending ? <><Loader2 className="h-4 w-4 mr-1 animate-spin" /> {t('computers.delete')}</> : t('computers.delete')}
          </Button>
        </DialogFooter>
      </Dialog>
    </div>
  )
}
