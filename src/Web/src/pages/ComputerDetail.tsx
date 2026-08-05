import { useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs'
import { Table, TableHeader, TableBody, TableHead, TableRow, TableCell } from '@/components/ui/table'
import { Dialog, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog'
import { api } from '@/lib/api'
import { useComputer, useComputerTimeline } from '@/hooks/useComputers'
import { formatDate, formatRelative, formatDuration } from '@/lib/utils'
import { ArrowLeft, Monitor, Clock, Shield, Camera, Radio, Download, Search, Eye, Trash2, Loader2, Maximize2, X, AlertTriangle, User } from 'lucide-react'
import { toast } from 'sonner'
import { useAuthStore } from '@/stores/auth'

function FileTransfersTab({ computerId }: { computerId: string }) {
  const { t } = useTranslation()
  const [search, setSearch] = useState('')
  const { data: activity, isLoading } = useQuery({
    queryKey: ['computer-transfers', computerId],
    queryFn: () => api.get<any[]>('/dashboard/activity'),
  })

  const transfers = (activity || []).filter(
    (e: any) =>
      e.computerId === computerId &&
      (e.eventType === 'FileCopy' || e.eventType === 'FileTransfer' || e.eventType === 'USBConnected' || e.eventType === 'USBDisconnected' || e.category === 'USB')
  )

  const filtered = search
    ? transfers.filter((t: any) => t.description?.toLowerCase().includes(search.toLowerCase()) || t.username?.toLowerCase().includes(search.toLowerCase()))
    : transfers

  return (
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
              <TableHead>{t('fileTransfers.time', 'Data/Hora')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading ? (
              <TableRow><TableCell colSpan={4} className="text-center py-8 text-muted-foreground"><Loader2 className="h-4 w-4 animate-spin inline mr-1" /> {t('common.loading')}</TableCell></TableRow>
            ) : filtered.length === 0 ? (
              <TableRow><TableCell colSpan={4} className="text-center py-8 text-muted-foreground">{t('fileTransfers.empty', 'Nenhuma transferência encontrada')}</TableCell></TableRow>
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
                  <TableCell className="text-xs text-muted-foreground">{formatRelative(event.timestamp)}</TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  )
}

function ScreenshotsTab({ computerId }: { computerId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [showRequest, setShowRequest] = useState(false)
  const [reason, setReason] = useState('')
  const [captureAllMonitors, setCaptureAllMonitors] = useState(false)
  const [capturing, setCapturing] = useState(false)
  const [progress, setProgress] = useState(0)
  const [viewCapture, setViewCapture] = useState<any | null>(null)
  const [zoomed, setZoomed] = useState(false)
  const [zoomLevel, setZoomLevel] = useState(1)
  const [confirmDelete, setConfirmDelete] = useState<any | null>(null)

  const { data: screenshots, isLoading } = useQuery({
    queryKey: ['computer-screenshots', computerId],
    queryFn: () => api.get<{ items: any[] }>(`/screencapture?computerId=${computerId}&pageSize=20`),
    refetchInterval: capturing ? 3000 : false,
  })

  const requestMutation = useMutation({
    mutationFn: (data: { computerId: string; reason: string; captureAllMonitors: boolean }) =>
      api.post('/screencapture/request', data),
    onError: (err: Error) => toast.error(err.message),
    onSuccess: () => {
      setShowRequest(false)
      setReason('')
      setCaptureAllMonitors(false)
      setCapturing(true)
      setProgress(0)
      const interval = setInterval(() => {
        setProgress((p) => {
          if (p >= 100) { clearInterval(interval); return 100 }
          return p + 10
        })
      }, 500)
      setTimeout(() => {
        clearInterval(interval)
        setCapturing(false)
        setProgress(100)
        queryClient.invalidateQueries({ queryKey: ['computer-screenshots', computerId] })
        setTimeout(() => queryClient.invalidateQueries({ queryKey: ['computer-screenshots', computerId] }), 3000)
      }, 10000)
    },
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => api.delete(`/screencapture/${id}`),
    onError: (err: Error) => toast.error(err.message),
    onSuccess: () => {
      toast.success(t('screenCapture.deleted', 'Captura excluída'))
      setConfirmDelete(null)
      setViewCapture(null)
      queryClient.invalidateQueries({ queryKey: ['computer-screenshots', computerId] })
    },
  })

  const handleRequest = () => {
    requestMutation.mutate({ computerId, reason, captureAllMonitors })
  }

  const handleDownload = async (screenshot: any) => {
    try {
      const token = useAuthStore.getState().accessToken
      const headers: Record<string, string> = {}
      if (token) headers['Authorization'] = `Bearer ${token}`
      const res = await fetch(screenshot.imageUrl!, { headers })
      if (!res.ok) throw new Error('Download failed')
      const blob = await res.blob()
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `screenshot_${(screenshot.monitorName || 'screen').replace(/\s+/g, '_')}_${screenshot.id?.slice(0, 8) || 'unknown'}.jpg`
      document.body.appendChild(a)
      a.click()
      document.body.removeChild(a)
      URL.revokeObjectURL(url)
    } catch {
      toast.error(t('screenCapture.downloadError', 'Erro ao baixar imagem'))
    }
  }

  const formatSize = (bytes: number) => {
    if (bytes < 1024) return `${bytes} B`
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
  }

  if (isLoading) return <div className="flex items-center justify-center py-12 text-muted-foreground"><Loader2 className="h-6 w-6 animate-spin mr-2" /> {t('common.loading')}</div>

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-muted-foreground">{t('computerDetail.screenshotsDescription', 'Capturas de tela recentes deste computador')}</p>
        <Button variant="outline" size="sm" onClick={() => setShowRequest(true)} disabled={capturing}>
          {capturing ? (
            <><Loader2 className="h-4 w-4 mr-1 animate-spin" /> {t('screenCapture.capturing', 'Capturando...')}</>
          ) : (
            <><Camera className="h-4 w-4 mr-1" /> {t('computerDetail.requestScreenshot', 'Solicitar Captura')}</>
          )}
        </Button>
      </div>

      {capturing && (
        <div className="p-4 rounded-lg bg-primary/5 border border-primary/20">
          <div className="flex items-center gap-2 mb-2">
            <Loader2 className="h-4 w-4 animate-spin text-primary" />
            <span className="text-sm font-medium">{t('screenCapture.capturingInProgress', 'Captura em andamento...')}</span>
          </div>
          <div className="w-full bg-muted rounded-full h-2">
            <div className="bg-primary h-2 rounded-full transition-all duration-300" style={{ width: `${progress}%` }} />
          </div>
        </div>
      )}

      {(!screenshots?.items || screenshots.items.length === 0) ? (
        <Card><CardContent className="py-8 text-center text-muted-foreground">{t('computerDetail.noScreenshots', 'Nenhuma captura de tela encontrada')}</CardContent></Card>
      ) : (
        <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-3">
          {screenshots.items.map((s: any) => (
            <div
              key={s.id}
              onClick={() => setViewCapture(s)}
              className="group relative rounded-lg overflow-hidden border border-border/50 bg-muted/30 hover:border-primary/40 hover:shadow-md transition-all cursor-pointer"
            >
              <div className="aspect-video bg-muted/50 flex items-center justify-center overflow-hidden">
                {s.thumbnailUrl ? (
                  <img src={s.thumbnailUrl} alt="" className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300" loading="lazy" />
                ) : (
                  <Camera className="h-8 w-8 text-muted-foreground" />
                )}
              </div>
              <div className="p-2">
                <p className="text-[10px] text-muted-foreground truncate">{formatDate(s.createdAt)}</p>
                <div className="flex items-center gap-1 mt-0.5 flex-wrap">
                  {s.monitorName && /monitores\b/i.test(s.monitorName) ? (
                    <Badge variant="secondary" className="text-[9px] px-1 py-0">{s.monitorName}</Badge>
                  ) : null}
                  <Badge variant="outline" className="text-[9px] px-1 py-0">{s.width}x{s.height}</Badge>
                  {s.size && <Badge variant="outline" className="text-[9px] px-1 py-0">{formatSize(s.size)}</Badge>}
                </div>
              </div>
              <div className="absolute inset-0 bg-black/0 group-hover:bg-black/10 transition-colors" />
              <div className="absolute top-2 right-2 opacity-0 group-hover:opacity-100 transition-opacity flex gap-1">
                <div className="bg-background/90 backdrop-blur-sm rounded-full p-1 shadow cursor-pointer" onClick={(e) => { e.stopPropagation(); setViewCapture(s) }}>
                  <Eye className="h-3.5 w-3.5" />
                </div>
                <div className="bg-background/90 backdrop-blur-sm rounded-full p-1 shadow cursor-pointer hover:bg-destructive/10" onClick={(e) => { e.stopPropagation(); setConfirmDelete(s) }}>
                  <Trash2 className="h-3.5 w-3.5 text-destructive" />
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {zoomed ? (
        <div className="fixed inset-0 z-[60] bg-black/90 flex flex-col" onClick={() => setZoomed(false)}>
          <div className="flex items-center justify-between p-4 text-white" onClick={(e) => e.stopPropagation()}>
            <span className="text-sm text-white/70">{viewCapture?.monitorName} - {viewCapture?.width}x{viewCapture?.height}</span>
            <div className="flex items-center gap-2">
              <Button variant="ghost" size="sm" className="text-white hover:text-white/80" onClick={() => setZoomLevel((z) => Math.max(0.25, z - 0.25))}>
                <span className="text-lg font-bold">-</span>
              </Button>
              <span className="text-sm text-white/70 min-w-[4rem] text-center">{Math.round(zoomLevel * 100)}%</span>
              <Button variant="ghost" size="sm" className="text-white hover:text-white/80" onClick={() => setZoomLevel((z) => Math.min(4, z + 0.25))}>
                <span className="text-lg font-bold">+</span>
              </Button>
              <Button variant="ghost" size="sm" className="text-white hover:text-white/80 text-xs" onClick={() => setZoomLevel(1)}>1:1</Button>
              <Button variant="ghost" size="sm" className="text-white hover:text-white/80" onClick={() => { viewCapture && handleDownload(viewCapture) }}>
                <Download className="h-4 w-4" />
              </Button>
              <Button variant="ghost" size="sm" className="text-white hover:text-white/80" onClick={() => { setZoomed(false); setZoomLevel(1) }}>
                <X className="h-4 w-4" />
              </Button>
            </div>
          </div>
          <div className="flex-1 flex items-center justify-center p-4 overflow-auto" onClick={(e) => e.stopPropagation()}>
            <img src={viewCapture?.imageUrl} alt="" className="max-w-none max-h-none" style={{ transform: `scale(${zoomLevel})`, transformOrigin: 'center center' }} />
          </div>
        </div>
      ) : (
        <Dialog open={!!viewCapture} onClose={() => { setViewCapture(null); setZoomed(false); setZoomLevel(1) }}>
          <div className="p-4">
            <DialogHeader>
              <div className="flex items-center justify-between">
                <DialogTitle>{t('screenCapture.previewDialogTitle', 'Visualizar Captura')}</DialogTitle>
                <div className="flex items-center gap-1">
                  <Button variant="ghost" size="sm" onClick={() => setZoomed(true)} title={t('screenCapture.fullscreen', 'Tela Cheia')}>
                    <Maximize2 className="h-4 w-4" />
                  </Button>
                  <Button variant="ghost" size="sm" onClick={() => viewCapture && handleDownload(viewCapture)} title={t('screenCapture.download', 'Baixar')}>
                    <Download className="h-4 w-4" />
                  </Button>
                  <Button variant="ghost" size="sm" onClick={() => viewCapture && setConfirmDelete(viewCapture)} title={t('screenCapture.delete', 'Excluir')}>
                    <Trash2 className="h-4 w-4 text-destructive" />
                  </Button>
                </div>
              </div>
            </DialogHeader>
            {viewCapture && (
              <div className="mt-4">
                <div className="relative rounded-lg bg-muted/30 border border-border/50 overflow-hidden flex items-center justify-center max-h-[60vh]">
                  <img
                    src={viewCapture.imageUrl}
                    alt=""
                    className="max-w-full max-h-full object-contain cursor-zoom-in"
                    onClick={() => setZoomed(true)}
                    onError={(e) => { (e.target as HTMLImageElement).style.display = 'none' }}
                  />
                </div>
                <div className="flex flex-wrap gap-4 mt-3 text-xs text-muted-foreground">
                  {viewCapture.monitorName && (
                    <Badge variant="secondary" className="text-xs px-2 py-0.5 gap-1">
                      <Monitor className="h-3 w-3" />{viewCapture.monitorName}
                    </Badge>
                  )}
                  <span className="inline-flex items-center gap-1"><Maximize2 className="h-3 w-3" />{viewCapture.width}x{viewCapture.height}</span>
                  {viewCapture.size && <span className="inline-flex items-center gap-1">{formatSize(viewCapture.size)}</span>}
                  <span className="inline-flex items-center gap-1"><User className="h-3 w-3" />{viewCapture.user || '-'}</span>
                  <span className="inline-flex items-center gap-1">{formatDate(viewCapture.createdAt)}</span>
                </div>
              </div>
            )}
          </div>
        </Dialog>
      )}

      <Dialog open={!!confirmDelete} onClose={() => setConfirmDelete(null)}>
        <div className="p-6">
          <DialogHeader>
            <div className="flex items-center gap-2">
              <AlertTriangle className="h-5 w-5 text-destructive" />
              <DialogTitle>{t('screenCapture.confirmDelete', 'Confirmar exclusão')}</DialogTitle>
            </div>
          </DialogHeader>
          <p className="text-sm text-muted-foreground mt-2">
            {t('screenCapture.confirmDeleteMessage', 'Tem certeza que deseja excluir esta captura? Esta ação não pode ser desfeita.')}
          </p>
          <DialogFooter>
            <Button variant="outline" onClick={() => setConfirmDelete(null)}>{t('screenCapture.cancel', 'Cancelar')}</Button>
            <Button variant="destructive" onClick={() => confirmDelete && deleteMutation.mutate(confirmDelete.id)} disabled={deleteMutation.isPending}>
              {deleteMutation.isPending ? <><Loader2 className="h-4 w-4 mr-1 animate-spin" /> {t('common.loading', 'Carregando...')}</> : t('screenCapture.delete', 'Excluir')}
            </Button>
          </DialogFooter>
        </div>
      </Dialog>

      <Dialog open={showRequest} onClose={() => setShowRequest(false)}>
        <div className="p-2">
          <DialogHeader>
            <DialogTitle>{t('screenCapture.requestDialogTitle', 'Solicitar Captura de Tela')}</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 mt-4">
            <Input
              label={t('screenCapture.justification', 'Justificativa')}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder={t('screenCapture.reasonPlaceholder', 'Motivo da captura...')}
            />
            <label className="flex items-center gap-2 text-sm cursor-pointer">
              <input
                type="checkbox"
                checked={captureAllMonitors}
                onChange={(e) => setCaptureAllMonitors(e.target.checked)}
                className="rounded border-border"
              />
              {t('screenCapture.captureAllMonitors', 'Capturar todos os monitores')}
            </label>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowRequest(false)}>{t('screenCapture.cancel', 'Cancelar')}</Button>
            <Button onClick={handleRequest} disabled={requestMutation.isPending}>
              {requestMutation.isPending ? <><Loader2 className="h-4 w-4 mr-1 animate-spin" /> {t('common.loading', 'Carregando...')}</> : t('screenCapture.request', 'Solicitar')}
            </Button>
          </DialogFooter>
        </div>
      </Dialog>
    </div>
  )
}

export function ComputerDetail() {
  const { t } = useTranslation()
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { data: computer, isLoading } = useComputer(id!)
  const { data: timeline } = useComputerTimeline(id!)

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
            <p className="text-sm text-muted-foreground">{computer.ipAddress}</p>
          </div>
        </div>
      </div>

      <Tabs defaultValue="timeline">
        <TabsList>
          <TabsTrigger value="timeline"><Clock className="h-4 w-4 mr-1" /> {t('computerDetail.timeline')}</TabsTrigger>
          <TabsTrigger value="fileTransfers"><Download className="h-4 w-4 mr-1" /> {t('computerDetail.fileTransfers', 'Transferências')}</TabsTrigger>
          <TabsTrigger value="screenshots"><Camera className="h-4 w-4 mr-1" /> {t('computerDetail.screenshots')}</TabsTrigger>
          <TabsTrigger value="remote"><Radio className="h-4 w-4 mr-1" /> {t('computerDetail.remote')}</TabsTrigger>
          <TabsTrigger value="security"><Shield className="h-4 w-4 mr-1" /> {t('computerDetail.security')}</TabsTrigger>
        </TabsList>

        <TabsContent value="timeline">
          <Card>
            <CardContent className="p-4">
              <div className="flex flex-wrap items-center gap-x-8 gap-y-2 text-sm">
                <span className="text-muted-foreground">{t('computerDetail.currentUser')}: <strong className="text-foreground">{computer.currentUser || '-'}</strong></span>
                <span className="text-muted-foreground">{t('computerDetail.lastHeartbeat')}: <strong className="text-foreground">{formatRelative(computer.lastHeartbeat)}</strong></span>
                <span className="text-muted-foreground shrink-0">|</span>
                {[
                  { label: t('computerDetail.firewall'), ok: computer.firewallEnabled },
                  { label: computer.antivirusProductName || t('computerDetail.antivirus'), ok: computer.antivirusEnabled },
                  { label: t('computerDetail.bitLocker'), ok: computer.bitlockerEnabled },
                  { label: t('computerDetail.rdp'), ok: computer.rdpEnabled },
                ].map((s) => (
                  s.ok !== undefined && (
                    <span key={s.label} className="flex items-center gap-1">
                      <span className="text-muted-foreground">{s.label}</span>
                      <Badge variant={s.ok ? 'success' : 'destructive'} className="text-[10px] px-1.5 py-0">{s.ok ? t('computerDetail.active') : t('computerDetail.inactive')}</Badge>
                    </span>
                  )
                ))}
              </div>
            </CardContent>
          </Card>

          <Card className="mt-6">
            <CardHeader><CardTitle className="text-base">{t('computerDetail.fullTimeline')}</CardTitle></CardHeader>
            <CardContent>
              <div className="space-y-2">
                {timeline?.items?.map((event: any) => (
                  <div key={event.id} className="flex items-start gap-3 text-sm py-2 border-b border-border/50 last:border-0">
                    <div className="w-2 h-2 mt-1.5 rounded-full shrink-0 bg-primary/60" />
                    <div className="flex-1 min-w-0">
                      <p className="font-medium">{t(`computerDetail.eventType.${event.eventType}`, { defaultValue: event.eventType })}</p>
                      <p className="text-muted-foreground">{event.description}</p>
                      <p className="text-xs text-muted-foreground mt-0.5">{formatDate(event.timestamp)} &middot; {event.username || t('computerDetail.system')}</p>
                    </div>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="fileTransfers">
          <FileTransfersTab computerId={id!} />
        </TabsContent>

        <TabsContent value="screenshots">
          <ScreenshotsTab computerId={id!} />
        </TabsContent>

        <TabsContent value="remote">
          <Card>
            <CardHeader>
              <div className="flex items-center justify-between">
                <CardTitle className="text-base">{t('computerDetail.remote')}</CardTitle>
                <Button variant="outline" size="sm" asChild>
                  <a href="/remote-assistance"><Radio className="h-4 w-4 mr-1" /> {t('nav.remoteAssist')}</a>
                </Button>
              </div>
            </CardHeader>
            <CardContent>
              <p className="text-sm text-muted-foreground mb-4">{t('remoteAssistance.subtitle')}</p>
              <Button onClick={() => {
                const token = useAuthStore.getState().accessToken
                fetch('/api/v1/remote/request', {
                  method: 'POST',
                  headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
                  body: JSON.stringify({ computerId: id, sessionType: 'view' })
                })
                .then(res => res.json())
                .then((data: any) => {
                  toast.success(t('remoteAssistance.requestSession'))
                  navigate(`/remote-assistance?session=${data.id}`)
                })
                .catch(() => toast.error(t('common.error')))
              }}>
                <Radio className="h-4 w-4 mr-1" /> {t('remoteAssistance.newSession')}
              </Button>
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
      </Tabs>
    </div>
  )
}
