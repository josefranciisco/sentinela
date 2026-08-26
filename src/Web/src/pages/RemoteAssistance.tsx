import { useState, useRef, useCallback, useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { Dialog, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog'
import { Radio, Monitor, PowerOff, Video, VideoOff, Maximize, Minimize, X, Trash2, ChevronDown, Camera } from 'lucide-react'
import { api } from '@/lib/api'
import { hubUrl } from '@/lib/config'
import { useSignalR } from '@/hooks/useSignalR'

interface RemoteSession {
  id: string
  computerId: string
  computerName: string
  status: string
  mode: string
  requestedAt: string
  monitorIndex?: number
}

interface Computer {
  id: string
  hostname: string
  monitorCount?: number
}

export function RemoteAssistance() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [searchParams, setSearchParams] = useSearchParams()
  const [showRequest, setShowRequest] = useState(false)
  const [selectedSession, setSelectedSession] = useState<string | null>(searchParams.get('session'))
  const [selectedComputer, setSelectedComputer] = useState('')
  const [selectedMonitor, setSelectedMonitor] = useState<number | undefined>(undefined)
  const [liveFrame, setLiveFrame] = useState<string | null>(null)
  const [frameNumber, setFrameNumber] = useState(0)
  const [streamActive, setStreamActive] = useState(false)
  const [isFullscreen, setIsFullscreen] = useState(false)
  const [showAll, setShowAll] = useState(false)
  const [isRecording, setIsRecording] = useState(false)
  const [activeMonitorIndex, setActiveMonitorIndex] = useState<number | undefined>(undefined)

  const canvasRef = useRef<HTMLCanvasElement | null>(null)
  const recorderRef = useRef<MediaRecorder | null>(null)
  const chunksRef = useRef<Blob[]>([])
  const isRecordingRef = useRef(false)

  useEffect(() => { isRecordingRef.current = isRecording }, [isRecording])

  useEffect(() => {
    if (searchParams.get('session')) {
      setSearchParams({}, { replace: true })
    }
  }, [])

  const toggleFullscreen = () => {
    const el = document.querySelector('#remote-viewport')
    if (!el) return
    if (!document.fullscreenElement) {
      el.requestFullscreen?.().then(() => setIsFullscreen(true)).catch(() => {})
    } else {
      document.exitFullscreen?.().then(() => setIsFullscreen(false)).catch(() => {})
    }
  }

  const { data: sessionList } = useQuery({
    queryKey: ['remote-sessions', showAll],
    queryFn: () => api.get<RemoteSession[]>(`/remote/sessions${showAll ? '' : '?status=Active'}`),
  })

  const { data: computers } = useQuery({
    queryKey: ['computers'],
    queryFn: () => api.get<{ items: Computer[] }>('/computers?pageSize=100'),
  })

  const requestMutation = useMutation({
    mutationFn: (computerId: string) =>
      api.post('/remote/request', { computerId, sessionType: 'view', monitorIndex: selectedMonitor }),
    onSuccess: () => {
      setShowRequest(false)
      setSelectedComputer('')
      setSelectedMonitor(undefined)
      queryClient.invalidateQueries({ queryKey: ['remote-sessions'] })
    },
  })

  const terminateMutation = useMutation({
    mutationFn: (sessionId: string) =>
      api.post(`/remote/sessions/${sessionId}/terminate`),
    onSuccess: (_, sessionId) => {
      if (selectedSession === sessionId) {
        setSelectedSession(null)
        setLiveFrame(null)
        setStreamActive(false)
      }
      queryClient.invalidateQueries({ queryKey: ['remote-sessions'] })
    },
  })

  const deleteMutation = useMutation({
    mutationFn: (sessionId: string) =>
      api.delete(`/remote/sessions/${sessionId}`),
    onSuccess: (_, sessionId) => {
      if (selectedSession === sessionId) {
        setSelectedSession(null)
        setLiveFrame(null)
        setStreamActive(false)
      }
      queryClient.invalidateQueries({ queryKey: ['remote-sessions'] })
    },
  })

  const shutdownMutation = useMutation({
    mutationFn: (computerId: string) =>
      api.post(`/remote/${computerId}/command`, { command: 'shutdown', parameters: '' }),
  })

  const selectedComputerData = (computers?.items ?? []).find(c => c.id === selectedComputer)
  const statusLabel: Record<string, string> = {
    Active: t('remoteAssistance.statusActive'),
    Terminated: t('remoteAssistance.statusTerminated'),
    Pending: t('remoteAssistance.statusPending'),
  }

  const session = sessionList?.find(s => s.id === selectedSession) ?? null
  const sessionComputerData = session
    ? (computers?.items ?? []).find(c => c.id === session.computerId)
    : undefined
  const monitorCount = selectedComputerData?.monitorCount ?? sessionComputerData?.monitorCount ?? 0
  const monitorOptions = [
    { value: 'all', label: t('remoteAssistance.allMonitors') },
    ...Array.from({ length: monitorCount }, (_, i) => ({
      value: String(i),
      label: t('remoteAssistance.monitorN', { n: i + 1 }),
    })),
  ]
  const computerOptions = (computers?.items ?? []).map((c) => ({
    value: c.id,
    label: c.hostname,
  }))

  const connection = useSignalR(selectedSession ? hubUrl(`/hubs/remote?sessionId=${selectedSession}`) : '', {
    ScreenFrameReceived: (payload: { sessionId: string; frameData: string; frameNumber: number }) => {
      if (payload?.frameData) {
        const base64 = Array.isArray(payload.frameData)
          ? btoa(String.fromCharCode(...new Uint8Array(payload.frameData)))
          : payload.frameData
        const dataUrl = `data:image/jpeg;base64,${base64}`
        setLiveFrame(dataUrl)
        setFrameNumber(payload.frameNumber ?? 0)
        setStreamActive(true)
        if (isRecordingRef.current) drawFrameToCanvas(dataUrl)
      }
    },
    SessionEnded: () => setStreamActive(false),
  })

  const switchMonitor = (index: number | undefined) => {
    const hub = connection.current
    if (!hub || hub.state !== 'Connected') return
    hub.invoke('SwitchMonitor', selectedSession, index).catch(err => console.error('SwitchMonitor failed:', err))
    setActiveMonitorIndex(index)
  }

  const startRecording = useCallback(() => {
    const canvas = document.createElement('canvas')
    canvas.width = 1920
    canvas.height = 1080
    canvasRef.current = canvas

    const ctx = canvas.getContext('2d')
    if (!ctx) return

    const stream = canvas.captureStream(30)
    const mimeType = MediaRecorder.isTypeSupported('video/webm;codecs=vp9')
      ? 'video/webm;codecs=vp9'
      : 'video/webm'
    const recorder = new MediaRecorder(stream, { mimeType, videoBitsPerSecond: 5_000_000 })

    chunksRef.current = []
    recorder.ondataavailable = (e) => {
      if (e.data.size > 0) chunksRef.current.push(e.data)
    }
    recorder.onstop = () => {
      const blob = new Blob(chunksRef.current, { type: mimeType })
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `gravacao-${new Date().toISOString().slice(0, 19).replace(/:/g, '-')}.webm`
      a.click()
      URL.revokeObjectURL(url)
      chunksRef.current = []
    }

    recorder.start(1000)
    recorderRef.current = recorder
    setIsRecording(true)
  }, [])

  const stopRecording = useCallback(() => {
    recorderRef.current?.stop()
    recorderRef.current = null
    canvasRef.current = null
    setIsRecording(false)
  }, [])

  const drawFrameToCanvas = useCallback((base64: string) => {
    const canvas = canvasRef.current
    if (!canvas) return
    const ctx = canvas.getContext('2d')
    if (!ctx) return
    const img = new Image()
    img.onload = () => {
      ctx.drawImage(img, 0, 0, canvas.width, canvas.height)
    }
    img.src = base64
  }, [])

  const captureScreenshot = useCallback(() => {
    if (!liveFrame) return
    const a = document.createElement('a')
    a.href = liveFrame
    a.download = `captura-${new Date().toISOString().slice(0, 19).replace(/:/g, '-')}.jpg`
    a.click()
  }, [liveFrame])

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">{t('remoteAssistance.title')}</h1>
          <p className="text-muted-foreground text-sm">{t('remoteAssistance.subtitle')}</p>
        </div>
        <Button onClick={() => setShowRequest(true)}><Radio className="h-4 w-4 mr-1" /> {t('remoteAssistance.newSession')}</Button>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <Card className="lg:col-span-1">
          <CardHeader>
            <div className="flex items-center justify-between">
              <CardTitle className="text-base">
                {showAll ? t('remoteAssistance.allSessions') : t('remoteAssistance.activeSessions')}
              </CardTitle>
              <Button
                variant="ghost"
                size="sm"
                onClick={() => setShowAll(!showAll)}
                className="h-7 text-xs"
              >
                {showAll ? t('remoteAssistance.showActiveOnly') : t('remoteAssistance.showAll')}
              </Button>
            </div>
          </CardHeader>
          <CardContent className="space-y-2">
            {!sessionList?.length ? (
              <p className="text-sm text-muted-foreground">{t('remoteAssistance.selectSession')}</p>
            ) : sessionList.map((s) => (
              <div
                key={s.id}
                onClick={() => {
                  setSelectedSession(s.id)
                  setActiveMonitorIndex(s.monitorIndex)
                }}
                className={`p-3 rounded-lg cursor-pointer transition-colors ${
                  selectedSession === s.id ? 'bg-primary/10 border border-primary/30' : 'bg-muted/30 hover:bg-muted/50'
                }`}
              >
                <div className="flex items-center justify-between">
                  <div className="min-w-0 flex-1">
                    <p className="text-sm font-medium truncate">{s.computerName}</p>
                    <p className="text-xs text-muted-foreground">{new Date(s.requestedAt).toLocaleString()}</p>
                  </div>
                  <div className="flex items-center gap-1 shrink-0">
                    <Badge variant={s.status === 'Active' ? 'success' : 'outline'}>
                      {statusLabel[s.status] ?? s.status}
                    </Badge>
                    {s.status === 'Active' ? (
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={(e) => {
                          e.stopPropagation()
                          terminateMutation.mutate(s.id)
                        }}
                        className="h-7 w-7 p-0 text-muted-foreground hover:text-destructive"
                        title={t('remoteAssistance.terminateSession')}
                      >
                        <X className="h-4 w-4" />
                      </Button>
                    ) : (
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={(e) => {
                          e.stopPropagation()
                          deleteMutation.mutate(s.id)
                        }}
                        className="h-7 w-7 p-0 text-muted-foreground hover:text-destructive"
                        title={t('remoteAssistance.deleteSession')}
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    )}
                  </div>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>

        <Card className="lg:col-span-2">
          {session ? (
            <>
              <CardHeader>
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <CardTitle className="text-base">{session.computerName}</CardTitle>
                    {streamActive && (
                      <Badge variant="success" className="whitespace-nowrap"><Video className="h-3 w-3 mr-1" /> {t('remoteAssistance.live')} #{frameNumber}</Badge>
                    )}
                  </div>
                  <div className="flex items-center gap-2">
                    {streamActive ? (
                      <>
                        <div className="relative">
                          <select
                            value={activeMonitorIndex === undefined ? 'all' : String(activeMonitorIndex)}
                            onChange={(e) => {
                              const v = e.target.value
                              switchMonitor(v === 'all' ? undefined : Number(v))
                            }}
                            className="h-8 appearance-none rounded-md border border-input bg-background px-2 pr-7 text-xs ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                          >
                            {monitorOptions.map((opt) => (
                              <option key={opt.value} value={opt.value}>{opt.label}</option>
                            ))}
                          </select>
                          <ChevronDown className="pointer-events-none absolute right-2 top-1/2 -translate-y-1/2 h-3 w-3 text-muted-foreground" />
                        </div>
                      </>
                    ) : (
                      <Badge variant="outline"><VideoOff className="h-3 w-3 mr-1" /> {t('remoteAssistance.waitingStream')}</Badge>
                    )}
                  </div>
                </div>
              </CardHeader>
              <CardContent className="space-y-4">
                <div
                  id="remote-viewport"
                  className="aspect-video rounded-lg bg-black flex items-center justify-center border border-border/50 overflow-hidden relative"
                >
                  {liveFrame ? (
                    <img
                      src={liveFrame}
                      alt="Live screen"
                      className="w-full h-full object-contain"
                    />
                  ) : (
                    <div className="text-center">
                      <Monitor className="h-16 w-16 text-muted-foreground mx-auto mb-2" />
                      <p className="text-sm text-muted-foreground">{t('remoteAssistance.screenPreview')}</p>
                    </div>
                  )}
                  <div className="absolute top-2 right-2 flex gap-1">
                    <Button
                      variant="secondary"
                      size="sm"
                      onClick={isRecording ? stopRecording : startRecording}
                      className={isRecording ? 'bg-red-600 hover:bg-red-700 text-white' : ''}
                      disabled={!streamActive}
                    >
                      {isRecording ? <><span className="h-2 w-2 rounded-full bg-white animate-pulse mr-1" /> {t('remoteAssistance.stopRecording')}</> : <><Video className="h-4 w-4 mr-1" /> {t('remoteAssistance.record')}</>}
                    </Button>
                    <Button
                      variant="secondary"
                      size="sm"
                      onClick={captureScreenshot}
                      disabled={!streamActive}
                    >
                      <Camera className="h-4 w-4 mr-1" /> {t('remoteAssistance.screenshot')}
                    </Button>
                    <Button
                      variant="secondary"
                      size="sm"
                      onClick={toggleFullscreen}
                    >
                      {isFullscreen ? <Minimize className="h-4 w-4 mr-1" /> : <Maximize className="h-4 w-4 mr-1" />}
                      {isFullscreen ? t('remoteAssistance.exitFullscreen') : t('remoteAssistance.fullscreen')}
                    </Button>
                  </div>
                </div>

                <div className="flex flex-wrap gap-2">
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => {
                      if (confirm(t('remoteAssistance.confirmStopStream'))) {
                        terminateMutation.mutate(session.id)
                      }
                    }}
                  >
                    <PowerOff className="h-4 w-4 mr-1" /> {t('remoteAssistance.stopStream')}
                  </Button>
                </div>
              </CardContent>
            </>
          ) : (
            <CardContent className="py-12 text-center text-muted-foreground">
              <Radio className="h-12 w-12 mx-auto mb-3 opacity-50" />
              <p>{t('remoteAssistance.selectSession')}</p>
            </CardContent>
          )}
        </Card>
      </div>

      <Dialog open={showRequest} onClose={() => setShowRequest(false)}>
        <div className="p-6">
          <DialogHeader>
            <DialogTitle>{t('remoteAssistance.requestSession')}</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 mt-4">
            <Select
              label={t('remoteAssistance.computer')}
              value={selectedComputer}
              onChange={(e) => setSelectedComputer(e.target.value)}
              options={computerOptions}
              placeholder={t('remoteAssistance.computer')}
            />
            <Select
              label={t('remoteAssistance.monitor')}
              value={selectedMonitor === undefined ? 'all' : String(selectedMonitor)}
              onChange={(e) => {
                const v = e.target.value
                setSelectedMonitor(v === 'all' ? undefined : Number(v))
              }}
              options={monitorOptions}
            />
            <Input label={t('remoteAssistance.justification')} placeholder={t('remoteAssistance.reasonPlaceholder')} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowRequest(false)}>{t('remoteAssistance.cancel')}</Button>
            <Button onClick={() => requestMutation.mutate(selectedComputer)} disabled={!selectedComputer}>
              {t('remoteAssistance.request')}
            </Button>
          </DialogFooter>
        </div>
      </Dialog>
    </div>
  )
}
