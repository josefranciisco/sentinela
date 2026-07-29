import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery } from '@tanstack/react-query'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { Table, TableHeader, TableBody, TableHead, TableRow, TableCell } from '@/components/ui/table'
import { Badge } from '@/components/ui/badge'
import { api } from '@/lib/api'
import { formatRelative } from '@/lib/utils'
import { Search, Package, RefreshCw } from 'lucide-react'
import { Button } from '@/components/ui/button'

interface SoftwareItem {
  name: string
  version: string
  publisher: string
  installCount: number
  isAuthorized: boolean
  category: string
  firstSeen: string
  lastSeen: string
}

export function Software() {
  const { t } = useTranslation()
  const [search, setSearch] = useState('')

  const { data: softwareList = [], isLoading, refetch } = useQuery({
    queryKey: ['software-inventory', search],
    queryFn: () =>
      api.get<SoftwareItem[]>(
        `/software${search ? `?search=${encodeURIComponent(search)}` : ''}`
      ),
    refetchInterval: 30000,
  })

  const filtered = softwareList

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">{t('software.title')}</h1>
          <p className="text-muted-foreground text-sm">{t('software.subtitle')}</p>
        </div>
        <Button variant="outline" size="sm" onClick={() => refetch()}>
          <RefreshCw className="h-4 w-4 mr-1" /> {t('common.refresh', 'Atualizar')}
        </Button>
      </div>

      <Card>
        <CardHeader>
          <div className="flex items-center gap-3">
            <div className="relative">
              <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
              <input
                placeholder={t('software.search')}
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                className="h-9 w-64 rounded-lg border border-input bg-background pl-8 pr-3 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              />
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('software.software')}</TableHead>
                <TableHead>{t('software.version')}</TableHead>
                <TableHead>{t('software.manufacturer')}</TableHead>
                <TableHead>{t('software.installs')}</TableHead>
                <TableHead>{t('software.lastUsed')}</TableHead>
                <TableHead>{t('software.status')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading ? (
                <TableRow>
                  <TableCell colSpan={6} className="text-center py-8 text-muted-foreground">
                    {t('common.loading', 'Carregando...')}
                  </TableCell>
                </TableRow>
              ) : filtered.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={6} className="text-center py-8 text-muted-foreground">
                    {t('software.empty', 'Nenhum software encontrado no inventário')}
                  </TableCell>
                </TableRow>
              ) : (
                filtered.map((s) => (
                  <TableRow key={`${s.name}-${s.version}`}>
                    <TableCell>
                      <div className="flex items-center gap-2">
                        <Package className="h-4 w-4 text-muted-foreground" />
                        <span className="font-medium">{s.name}</span>
                      </div>
                    </TableCell>
                    <TableCell className="text-xs font-mono text-muted-foreground">{s.version || '-'}</TableCell>
                    <TableCell className="text-sm">{s.publisher || '-'}</TableCell>
                    <TableCell className="text-sm">{s.installCount}</TableCell>
                    <TableCell className="text-xs text-muted-foreground">
                      {s.lastSeen ? formatRelative(s.lastSeen) : '-'}
                    </TableCell>
                    <TableCell>
                      {s.isAuthorized ? (
                        <Badge variant="success">{t('software.authorized')}</Badge>
                      ) : (
                        <Badge variant="destructive">{t('software.unauthorized')}</Badge>
                      )}
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  )
}
