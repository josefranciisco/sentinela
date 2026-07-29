import { useState, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { Dialog, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog'
import { Camera, Eye, Download, Search, X, Loader2, Monitor, Maximize2 } from 'lucide-react'
import { toast } from 'sonner'
import { api } from '@/lib/api'

interface Screenshot {
  id: string
  computerId: string
  requestId: string
  user: string
  monitorName: string
  width: number
  height: number
  mimeType: string
  size: number
  createdAt: string
  createdBy: string
  imageUrl?: string
  thumbnailUrl?: string
}

interface Computer {
  id: string
  hostname: string
}

export function ScreenCapture() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [showRequest, setShowRequest] = useState(false)
  const [viewCapture, setViewCapture] = useState<Screenshot | null>(null)
  const [selectedComputer, setSelectedComputer] = useState('')
  const [reason, setReason] = useState('')
  const [search, setSearch] = useState('')
  const [selectedComputerFilter, setSelectedComputerFilter] = useState('')
  const [zoomed, setZoomed] = useState(false)
  const [capturing, setCapturing] = useState(false)
  const [progress, setProgress] = useState(0)
  const imgRef = useRef<HTMLImageElement>(null)

  const { data: screenshots, isLoading } = useQuery({
    queryKey: ['screenshots', selectedComputerFilter],
    queryFn: () => {
      const params = new URLSearchParams({ pageSize: '50' })
      if (selectedComputerFilter) params.set('computerId', selectedComputerFilter)
      return api.get<{ items: Screenshot[]; total: number }>(`/screencapture?${params}`)
    },
    refetchInterval: 10000,
  })

  const { data: computers } = useQuery({
    queryKey: ['computers'],
    queryFn: () => api.get<{ items: Computer[] }>('/computers?pageSize=100'),
  })

  const requestMutation = useMutation({
    mutationFn: (data: { computerId: string; reason: string }) =>
      api.post('/screencapture/request', data),
    onError: (err: Error) => toast.error(err.message),
    onSuccess: () => {
      setShowRequest(false)
      setSelectedComputer('')
      setReason('')
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
        queryClient.invalidateQueries({ queryKey: ['screenshots'] })
      }, 5000)
    },
  })

  const handleRequest = () => {
    if (!selectedComputer) return
    requestMutation.mutate({ computerId: selectedComputer, reason })
  }

  const computerOptions = (computers?.items ?? []).map((c) => ({
    value: c.id,
    label: c.hostname,
  }))

  const filterOptions = [
    { value: '', label: t('screenCapture.allComputers', 'Todos') },
    ...computerOptions,
  ]

  const formatSize = (bytes: number) => {
    if (bytes < 1024) return `${bytes} B`
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
  }

  const formatDate = (date: string) => {
    try {
      return new Date(date).toLocaleString('pt-BR')
    } catch {
      return date
    }
  }

  const filteredScreenshots = (screenshots?.items ?? []).filter((s) => {
    if (!search) return true
    const q = search.toLowerCase()
    return s.monitorName.toLowerCase().includes(q) || s.user.toLowerCase().includes(q) || s.id.toLowerCase().includes(q)
  })

  return (
    <div className="space-y-6">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold">{t('screenCapture.title')}</h1>
          <p className="text-muted-foreground text-sm">{t('screenCapture.subtitle')}</p>
        </div>
        <Button onClick={() => setShowRequest(true)} disabled={capturing}>
          {capturing ? (
            <><Loader2 className="h-4 w-4 mr-1 animate-spin" /> {t('screenCapture.capturing', 'Capturando...')}</>
          ) : (
            <><Camera className="h-4 w-4 mr-1" /> {t('screenCapture.requestCapture')}</>
          )}
        </Button>
      </div>

      <Card>
        <CardHeader>
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
            <CardTitle className="text-base">{t('screenCapture.gallery', 'Galeria')}</CardTitle>
            <div className="flex items-center gap-2">
              <div className="relative">
                <Search className="h-4 w-4 absolute left-2.5 top-1/2 -translate-y-1/2 text-muted-foreground" />
                <Input
                  placeholder={t('screenCapture.searchPlaceholder', 'Buscar...')}
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  className="pl-8 h-8 w-48 text-sm"
                />
              </div>
              <Select
                value={selectedComputerFilter}
                onChange={(e) => setSelectedComputerFilter(e.target.value)}
                options={filterOptions}
                className="w-40 h-8 text-sm"
              />
            </div>
          </div>
        </CardHeader>
        <CardContent>
          {capturing && (
            <div className="mb-4 p-4 rounded-lg bg-primary/5 border border-primary/20">
              <div className="flex items-center gap-2 mb-2">
                <Loader2 className="h-4 w-4 animate-spin text-primary" />
                <span className="text-sm font-medium">{t('screenCapture.capturingInProgress', 'Captura em andamento...')}</span>
              </div>
              <div className="w-full bg-muted rounded-full h-2">
                <div className="bg-primary h-2 rounded-full transition-all duration-300" style={{ width: `${progress}%` }} />
              </div>
            </div>
          )}

          {isLoading ? (
            <div className="flex items-center justify-center py-16">
              <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
            </div>
          ) : filteredScreenshots.length === 0 ? (
            <div className="text-center py-16">
              <Camera className="h-12 w-12 text-muted-foreground mx-auto mb-3" />
              <p className="text-muted-foreground">{t('screenCapture.noCaptures', 'Nenhuma captura encontrada')}</p>
            </div>
          ) : (
            <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-3">
              {filteredScreenshots.map((s) => (
                <div
                  key={s.id}
                  onClick={() => setViewCapture(s)}
                  className="group relative rounded-lg overflow-hidden border border-border/50 bg-muted/30 hover:border-primary/40 hover:shadow-md transition-all cursor-pointer"
                >
                  <div className="aspect-video bg-muted/50 flex items-center justify-center overflow-hidden">
                    {s.thumbnailUrl ? (
                      <img
                        src={s.thumbnailUrl}
                        alt=""
                        className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300"
                        loading="lazy"
                        onError={(e) => { (e.target as HTMLImageElement).style.display = 'none' }}
                      />
                    ) : (
                      <Camera className="h-8 w-8 text-muted-foreground" />
                    )}
                  </div>
                  <div className="p-2">
                    <p className="text-xs font-medium truncate">{s.monitorName}</p>
                    <p className="text-[10px] text-muted-foreground truncate">{formatDate(s.createdAt)}</p>
                    <div className="flex items-center gap-1 mt-1">
                      <Badge variant="outline" className="text-[9px] px-1 py-0">{s.width}x{s.height}</Badge>
                      <Badge variant="outline" className="text-[9px] px-1 py-0">{formatSize(s.size)}</Badge>
                    </div>
                  </div>
                  <div className="absolute inset-0 bg-black/0 group-hover:bg-black/10 transition-colors" />
                  <div className="absolute top-2 right-2 opacity-0 group-hover:opacity-100 transition-opacity">
                    <div className="bg-background/90 backdrop-blur-sm rounded-full p-1 shadow">
                      <Eye className="h-3.5 w-3.5" />
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      <Dialog open={showRequest} onClose={() => setShowRequest(false)}>
        <div className="p-6">
          <DialogHeader>
            <DialogTitle>{t('screenCapture.requestDialogTitle')}</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 mt-4">
            <Select
              label={t('screenCapture.computer')}
              value={selectedComputer}
              onChange={(e) => setSelectedComputer(e.target.value)}
              options={computerOptions}
              placeholder={t('screenCapture.computer')}
            />
            <Input
              label={t('screenCapture.justification')}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder={t('screenCapture.reasonPlaceholder')}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowRequest(false)}>{t('screenCapture.cancel')}</Button>
            <Button onClick={handleRequest} disabled={!selectedComputer || requestMutation.isPending}>
              {requestMutation.isPending ? <><Loader2 className="h-4 w-4 mr-1 animate-spin" /> {t('common.loading')}</> : t('screenCapture.request')}
            </Button>
          </DialogFooter>
        </div>
      </Dialog>

      {zoomed ? (
        <div className="fixed inset-0 z-[60] bg-black/90 flex flex-col" onClick={() => setZoomed(false)}>
          <div className="flex items-center justify-between p-4 text-white">
            <span className="text-sm text-white/70">{viewCapture?.monitorName} - {viewCapture?.width}x{viewCapture?.height}</span>
            <div className="flex items-center gap-2">
              <Button variant="ghost" size="sm" className="text-white hover:text-white/80" onClick={(e) => { e.stopPropagation(); window.open(viewCapture?.imageUrl, '_blank') }}>
                <Download className="h-4 w-4" />
              </Button>
              <Button variant="ghost" size="sm" className="text-white hover:text-white/80" onClick={(e) => { e.stopPropagation(); setZoomed(false) }}>
                <X className="h-4 w-4" />
              </Button>
            </div>
          </div>
          <div className="flex-1 flex items-center justify-center p-4" onClick={(e) => e.stopPropagation()}>
            <img ref={imgRef} src={viewCapture?.imageUrl} alt="" className="max-w-full max-h-full object-contain" />
          </div>
        </div>
      ) : (
        <Dialog open={!!viewCapture} onClose={() => { setViewCapture(null); setZoomed(false) }}>
          <div className="p-4">
            <DialogHeader>
              <div className="flex items-center justify-between">
                <DialogTitle>{t('screenCapture.previewDialogTitle')}</DialogTitle>
                <div className="flex items-center gap-1">
                  <Button variant="ghost" size="sm" onClick={() => setZoomed(true)} title={t('screenCapture.fullscreen')}>
                    <Maximize2 className="h-4 w-4" />
                  </Button>
                  <Button variant="ghost" size="sm" onClick={() => window.open(viewCapture?.imageUrl, '_blank')} title={t('screenCapture.download')}>
                    <Download className="h-4 w-4" />
                  </Button>
                </div>
              </div>
            </DialogHeader>
            {viewCapture && (
              <div className="mt-4">
                <div className="relative rounded-lg bg-muted/30 border border-border/50 overflow-hidden flex items-center justify-center max-h-[60vh]">
                  <img
                    ref={imgRef}
                    src={viewCapture.imageUrl}
                    alt=""
                    className="max-w-full max-h-full object-contain cursor-zoom-in"
                    onClick={() => setZoomed(true)}
                    onError={(e) => { (e.target as HTMLImageElement).style.display = 'none' }}
                  />
                </div>
                <div className="flex flex-wrap gap-4 mt-3 text-xs text-muted-foreground">
                  <span><Monitor className="h-3 w-3 inline mr-1" />{viewCapture.monitorName}</span>
                  <span>{viewCapture.width}x{viewCapture.height}</span>
                  <span>{formatSize(viewCapture.size)}</span>
                  <span>{viewCapture.mimeType}</span>
                  <span>{t('screenCapture.user')}: {viewCapture.user || '-'}</span>
                  <span>{formatDate(viewCapture.createdAt)}</span>
                </div>
              </div>
            )}
          </div>
        </Dialog>
      )}
    </div>
  )
}
