import { useTranslation } from 'react-i18next'
import { useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { Table, TableHeader, TableBody, TableHead, TableRow, TableCell } from '@/components/ui/table'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { useComputers, useUpdateComputer } from '@/hooks/useComputers'
import { useLiveRelativeTime } from '@/hooks/useLiveRelativeTime'
import { Search, ChevronLeft, ChevronRight, Monitor, Pencil } from 'lucide-react'
import { toast } from 'sonner'

function HeartbeatTime({ date }: { date: string | null }) {
  return <>{useLiveRelativeTime(date)}</>
}

const statusColors: Record<string, 'success' | 'destructive' | 'warning'> = {
  Online: 'success', Offline: 'destructive', Away: 'warning',
}

function EditableCell({
  value,
  onSave,
  className,
}: {
  value: string
  onSave: (next: string) => void
  className?: string
}) {
  const [editing, setEditing] = useState(false)
  const [draft, setDraft] = useState('')

  const startEdit = () => {
    setDraft(value)
    setEditing(true)
  }

  const commit = () => {
    setEditing(false)
    const next = draft.trim()
    if (next && next !== value) onSave(next)
  }

  if (editing) {
    return (
      <Input
        autoFocus
        value={draft}
        onChange={(e) => setDraft(e.target.value)}
        onBlur={commit}
        onKeyDown={(e) => {
          if (e.key === 'Enter') commit()
          if (e.key === 'Escape') setEditing(false)
        }}
        onClick={(e) => e.stopPropagation()}
        onFocus={(e) => e.target.select()}
        className="h-7 w-full min-w-28 text-sm"
      />
    )
  }

  return (
    <button
      type="button"
      className={`group inline-flex items-center gap-1.5 text-left hover:text-foreground ${className ?? ''}`}
      onClick={(e) => {
        e.stopPropagation()
        startEdit()
      }}
      title="Clique para editar"
    >
      <span className="truncate">{value || '-'}</span>
      <Pencil className="h-3.5 w-3.5 opacity-0 transition-opacity group-hover:opacity-60" />
    </button>
  )
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
  const updateComputer = useUpdateComputer()
  const { t } = useTranslation()

  const handleSave = (id: string, data: Partial<{ hostname: string; department: string }>) => {
    updateComputer.mutate(
      { id, data },
      {
        onSuccess: () => toast.success(t('computers.updated', 'Computador atualizado')),
        onError: () => toast.error(t('computers.updateFailed', 'Falha ao atualizar')),
      }
    )
  }

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
                options={[{ value: '', label: t('computers.allStatus') }, { value: 'Online', label: 'Online' }, { value: 'Offline', label: 'Offline' }, { value: 'Away', label: 'Away' }]}
                value={status} onChange={(e) => { setStatus(e.target.value); setTimeout(updateParams, 0) }}
              />
              <Select
                options={[{ value: '', label: t('computers.allDepartments') }, { value: 'IT', label: 'IT' }, { value: 'HR', label: 'HR' }, { value: 'Finance', label: 'Finance' }]}
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
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading ? (
                <TableRow><TableCell colSpan={6} className="text-center py-8 text-muted-foreground">{t('computers.loading')}</TableCell></TableRow>
              ) : data?.items.length === 0 ? (
                <TableRow><TableCell colSpan={6} className="text-center py-8 text-muted-foreground">{t('computers.noComputers')}</TableCell></TableRow>
              ) : (
                data?.items.map((computer) => (
                  <TableRow key={computer.id} className="cursor-pointer" onClick={() => navigate(`/computers/${computer.id}`)}>
                    <TableCell className="font-medium">
                      <div className="flex items-center gap-2">
                        <Monitor className="h-4 w-4 text-muted-foreground shrink-0" />
                        <EditableCell value={computer.hostname} onSave={(v) => handleSave(computer.id, { hostname: v })} />
                      </div>
                    </TableCell>
                    <TableCell className="text-muted-foreground font-mono text-xs">{computer.ipAddress}</TableCell>
                    <TableCell>{computer.currentUser || '-'}</TableCell>
                    <TableCell>
                      <EditableCell value={computer.department || ''} onSave={(v) => handleSave(computer.id, { department: v })} />
                    </TableCell>
                    <TableCell>
                      <Badge variant={statusColors[computer.status]}>{computer.status}</Badge>
                    </TableCell>
                    <TableCell className="text-muted-foreground text-xs"><HeartbeatTime date={computer.lastHeartbeat} /></TableCell>
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
    </div>
  )
}
