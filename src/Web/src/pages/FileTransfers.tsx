import { useState } from 'react'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { Table, TableHeader, TableBody, TableHead, TableRow, TableCell } from '@/components/ui/table'
import { Button } from '@/components/ui/button'
import { useQuery } from '@tanstack/react-query'
import { api } from '@/lib/api'
import { formatRelative } from '@/lib/utils'
import { useTranslation } from 'react-i18next'
import { Search, Download, RefreshCw } from 'lucide-react'

export function FileTransfers() {
  const { t } = useTranslation()
  const [search, setSearch] = useState('')

  const { data: activity, isLoading, refetch } = useQuery({
    queryKey: ['file-transfers'],
    queryFn: () => api.get<any[]>('/dashboard/activity'),
    refetchInterval: 10000,
  })

  const transfers = (activity || []).filter(
    (e: any) =>
      e.eventType === 'FileCopy' ||
      e.eventType === 'FileTransfer' ||
      e.eventType === 'USBConnected' ||
      e.eventType === 'USBDisconnected' ||
      e.category === 'USB'
  )

  const filtered = search
    ? transfers.filter(
        (t: any) =>
          t.description?.toLowerCase().includes(search.toLowerCase()) ||
          t.username?.toLowerCase().includes(search.toLowerCase()) ||
          t.details?.toLowerCase().includes(search.toLowerCase())
      )
    : transfers

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">{t('fileTransfers.title', 'Transferências de Arquivos')}</h1>
          <p className="text-muted-foreground text-sm">{t('fileTransfers.subtitle', 'Monitoramento de cópia de arquivos via USB')}</p>
        </div>
        <Button variant="outline" size="sm" onClick={() => refetch()}>
          <RefreshCw className="h-4 w-4 mr-1" /> {t('fileTransfers.refresh', 'Atualizar')}
        </Button>
      </div>

      <Card>
        <CardHeader>
          <div className="flex items-center gap-3">
            <div className="relative">
              <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
              <input
                placeholder={t('fileTransfers.search', 'Buscar arquivos...')}
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
                <TableHead>{t('fileTransfers.file', 'Arquivo')}</TableHead>
                <TableHead>{t('fileTransfers.size', 'Tamanho')}</TableHead>
                <TableHead>{t('fileTransfers.user', 'Usuário')}</TableHead>
                <TableHead>{t('fileTransfers.computer', 'Computador')}</TableHead>
                <TableHead>{t('fileTransfers.time', 'Data/Hora')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading ? (
                <TableRow><TableCell colSpan={5} className="text-center py-8 text-muted-foreground">{t('fileTransfers.loading', 'Carregando...')}</TableCell></TableRow>
              ) : filtered.length === 0 ? (
                <TableRow><TableCell colSpan={5} className="text-center py-8 text-muted-foreground">{t('fileTransfers.empty', 'Nenhuma transferência encontrada')}</TableCell></TableRow>
              ) : (
                filtered.map((event: any, i: number) => (
                  <TableRow key={event.id || i}>
                    <TableCell className="font-medium">
                      <div className="flex items-center gap-2">
                        <Download className="h-4 w-4 text-muted-foreground" />
                        {event.description?.replace('File copied: ', '') || event.description || '-'}
                      </div>
                    </TableCell>
                    <TableCell className="text-xs text-muted-foreground">{event.details || '-'}</TableCell>
                    <TableCell>{event.username || '-'}</TableCell>
                    <TableCell className="text-xs text-muted-foreground">{event.computerName || '-'}</TableCell>
                    <TableCell className="text-xs text-muted-foreground">{formatRelative(event.timestamp)}</TableCell>
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
