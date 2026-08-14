import { useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery } from '@tanstack/react-query'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { api } from '@/lib/api'
import { apiUrl } from '@/lib/config'
import { formatBytes, formatDate } from '@/lib/utils'
import { Download, Flag, Loader2, Pause, Play, Video } from 'lucide-react'
import { toast } from 'sonner'
import { useAuthStore } from '@/stores/auth'

interface RecordingMonitor {
  index: number
  name: string
  width: number
  height: number
  isPrimary: boolean
}

interface RecordingSegment {
  monitorIndex: number
  fromUtc: string
  toUtc: string
}

interface RecordingStatus {
  computerId: string
  enabled: boolean
  fromUtc?: string | null
  toUtc?: string | null
  bytes: number
  segmentCount: number
  monitors?: RecordingMonitor[]
  segments?: RecordingSegment[]
  inSchedule?: boolean
  scheduleSummary?: string | null
  maxBytes?: number
}

const MAX_CLIP_MS = 2 * 60 * 60 * 1000
const MIN_CLIP_MS = 5_000

function toLocalInput(ms: number) {
  const d = new Date(ms)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`
}

function fromLocalInput(value: string) {
  const parsed = new Date(value)
  return Number.isNaN(parsed.getTime()) ? null : parsed.getTime()
}

function formatClipLength(ms: number) {
  const totalSec = Math.max(0, Math.round(ms / 1000))
  const h = Math.floor(totalSec / 3600)
  const m = Math.floor((totalSec % 3600) / 60)
  const s = totalSec % 60
  if (h > 0) return `${h}h ${m}min`
  if (m > 0) return s ? `${m} min ${s}s` : `${m} min`
  return `${s}s`
}

function hourTicks(fromMs: number, toMs: number) {
  const ticks: number[] = []
  const d = new Date(fromMs)
  d.setMinutes(0, 0, 0)
  if (d.getTime() < fromMs) d.setHours(d.getHours() + 1)
  for (let t = d.getTime(); t <= toMs; t += 60 * 60 * 1000) ticks.push(t)
  const step = ticks.length > 18 ? 2 : 1
  return ticks.filter((_, i) => i % step === 0)
}

function formatTick(ms: number) {
  return new Date(ms).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })
}

export function RecordingTab({ computerId, online, monitorCount = 1 }: { computerId: string; online: boolean; monitorCount?: number }) {
  const { t } = useTranslation()
  const { data: status, refetch, isLoading } = useQuery({
    queryKey: ['computer-recording', computerId],
    queryFn: () => api.get<RecordingStatus>(`/computers/${computerId}/recording`),
    refetchInterval: 15000,
  })

  const monitors = status?.monitors?.length
    ? status.monitors
    : Array.from({ length: Math.max(1, monitorCount) }, (_, index) => ({
        index,
        name: index === 0 ? t('computerDetail.recordingPrimary') : t('computerDetail.recordingMonitorN', { n: index + 1 }),
        width: 0,
        height: 0,
        isPrimary: index === 0,
      }))

  const [monitorIndex, setMonitorIndex] = useState(0)
  const fromMs = status?.fromUtc ? new Date(status.fromUtc).getTime() : Date.now() - 86_400_000
  const toMs = status?.toUtc ? new Date(status.toUtc).getTime() : Date.now()
  const span = Math.max(60_000, toMs - fromMs)

  const [cursor, setCursor] = useState(toMs)
  const [clipFrom, setClipFrom] = useState<number | null>(null)
  const [clipTo, setClipTo] = useState<number | null>(null)
  const [playing, setPlaying] = useState(false)
  const [frame, setFrame] = useState<string | null>(null)
  const [loadingFrame, setLoadingFrame] = useState(false)
  const [exporting, setExporting] = useState(false)
  const playRef = useRef<number | null>(null)
  const cursorReady = useRef(false)
  const clipReady = useRef(false)

  useEffect(() => {
    if (!monitors.some((m) => m.index === monitorIndex)) {
      const primary = monitors.find((m) => m.isPrimary)
      setMonitorIndex(primary?.index ?? monitors[0]?.index ?? 0)
    }
  }, [monitors, monitorIndex])

  useEffect(() => {
    if (!status?.toUtc) return
    const latest = new Date(status.toUtc).getTime()
    if (!cursorReady.current) {
      setCursor(latest)
      cursorReady.current = true
      void loadFrame(latest, monitorIndex)
    }
    if (!clipReady.current && status.fromUtc) {
      const startBound = new Date(status.fromUtc).getTime()
      setClipTo(latest)
      setClipFrom(Math.max(startBound, latest - 5 * 60 * 1000))
      clipReady.current = true
    }
  }, [status?.fromUtc, status?.toUtc, monitorIndex])

  const loadFrame = async (at: number, monitor = monitorIndex) => {
    setLoadingFrame(true)
    try {
      const data = await api.get<{ imageBase64: string }>(
        `/computers/${computerId}/recording/frame?at=${new Date(at).toISOString()}&monitorIndex=${monitor}`
      )
      if (data?.imageBase64) setFrame(data.imageBase64)
    } catch (err: any) {
      setPlaying(false)
      toast.error(err.message || t('computerDetail.recordingFrameError'))
    } finally {
      setLoadingFrame(false)
    }
  }

  useEffect(() => {
    setFrame(null)
    if (status?.toUtc || status?.fromUtc) void loadFrame(cursor, monitorIndex)
  }, [monitorIndex])

  useEffect(() => {
    if (!playing) {
      if (playRef.current) window.clearInterval(playRef.current)
      return
    }
    playRef.current = window.setInterval(() => {
      setCursor((current) => {
        const next = current + 1000
        if (next >= toMs) {
          setPlaying(false)
          return toMs
        }
        void loadFrame(next, monitorIndex)
        return next
      })
    }, 400)
    return () => {
      if (playRef.current) window.clearInterval(playRef.current)
    }
  }, [playing, toMs, computerId, monitorIndex])

  const clampClip = (start: number, end: number) => {
    let from = Math.min(start, end)
    let to = Math.max(start, end)
    from = Math.min(toMs, Math.max(fromMs, from))
    to = Math.min(toMs, Math.max(fromMs, to))
    if (to - from < MIN_CLIP_MS) to = Math.min(toMs, from + MIN_CLIP_MS)
    if (to - from > MAX_CLIP_MS) {
      to = from + MAX_CLIP_MS
      toast.error(t('computerDetail.recordingClipTooLong'))
    }
    setClipFrom(from)
    setClipTo(to)
  }

  const markStart = () => {
    const end = clipTo ?? cursor
    clampClip(cursor, Math.max(cursor + MIN_CLIP_MS, end))
    toast.success(t('computerDetail.recordingStartMarked'))
  }

  const markEnd = () => {
    const start = clipFrom ?? cursor
    clampClip(Math.min(start, cursor), cursor)
    toast.success(t('computerDetail.recordingEndMarked'))
  }

  const applyPreset = (minutes: number) => {
    const end = Math.min(toMs, Math.max(fromMs, cursor))
    clampClip(Math.max(fromMs, end - minutes * 60 * 1000), end)
  }

  const handleExport = async () => {
    if (clipFrom == null || clipTo == null) {
      toast.error(t('computerDetail.recordingSelectClip'))
      return
    }
    setExporting(true)
    try {
      const started = await api.post<{ exportId: string }>(`/computers/${computerId}/recording/export`, {
        from: new Date(clipFrom).toISOString(),
        to: new Date(clipTo).toISOString(),
        monitorIndex,
      })
      const token = useAuthStore.getState().accessToken
      for (let i = 0; i < 60; i++) {
        await new Promise((r) => setTimeout(r, 2000))
        const st = await api.get<{ status: string }>(`/computers/${computerId}/recording/exports/${started.exportId}`)
        if (st.status === 'failed') throw new Error(t('computerDetail.recordingExportFailed'))
        if (st.status === 'ready') {
          const res = await fetch(apiUrl(`/api/v1/computers/${computerId}/recording/exports/${started.exportId}/download`), {
            headers: token ? { Authorization: `Bearer ${token}` } : undefined,
          })
          if (!res.ok) throw new Error(t('computerDetail.recordingDownloadError'))
          const blob = await res.blob()
          const url = URL.createObjectURL(blob)
          const a = document.createElement('a')
          a.href = url
          a.download = `sentinela-gravacao-${computerId.slice(0, 8)}-m${monitorIndex + 1}.mp4`
          a.click()
          URL.revokeObjectURL(url)
          toast.success(t('computerDetail.recordingDownloaded'))
          return
        }
      }
      toast.error(t('computerDetail.recordingExportTimeout'))
    } catch (err: any) {
      toast.error(err.message || t('computerDetail.recordingDownloadError'))
    } finally {
      setExporting(false)
    }
  }

  const selected = monitors.find((m) => m.index === monitorIndex)
  const monitorOptions = monitors.map((m) => ({
    value: String(m.index),
    label: m.width && m.height
      ? `${m.name} · ${m.width}×${m.height}`
      : m.name,
  }))

  const rangeStart = clipFrom ?? Math.max(fromMs, toMs - 5 * 60 * 1000)
  const rangeEnd = clipTo ?? toMs
  const leftPct = ((rangeStart - fromMs) / span) * 100
  const widthPct = ((rangeEnd - rangeStart) / span) * 100
  const clipLength = rangeEnd - rangeStart
  const cursorPct = ((Math.min(toMs, Math.max(fromMs, cursor)) - fromMs) / span) * 100

  const recordedRanges = useMemo(() => {
    const raw = (status?.segments || []).filter((s) => s.monitorIndex === monitorIndex)
    return raw
      .map((s) => {
        const from = Math.max(fromMs, new Date(s.fromUtc).getTime())
        const to = Math.min(toMs, new Date(s.toUtc).getTime())
        if (!(to > from)) return null
        return {
          left: ((from - fromMs) / span) * 100,
          width: ((to - from) / span) * 100,
        }
      })
      .filter((x): x is { left: number; width: number } => x != null)
  }, [status?.segments, monitorIndex, fromMs, toMs, span])

  const ticks = useMemo(() => hourTicks(fromMs, toMs), [fromMs, toMs])

  return (
    <Card>
      <CardHeader>
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <CardTitle className="text-base flex items-center gap-2">
              <Video className="h-4 w-4" /> {t('computerDetail.recordingTitle')}
            </CardTitle>
            <p className="text-sm text-muted-foreground mt-1">
              {status?.scheduleSummary
                ? t('computerDetail.recordingSubtitleScheduled', {
                    schedule: status.scheduleSummary,
                    cap: status.maxBytes ? formatBytes(status.maxBytes) : '8 GB',
                  })
                : t('computerDetail.recordingSubtitle')}
            </p>
          </div>
          <div className="flex items-center gap-2">
            <Badge variant={status?.enabled && status.inSchedule !== false && online ? 'success' : 'secondary'}>
              {!status?.enabled
                ? t('computerDetail.recordingOff')
                : status.inSchedule === false
                  ? t('computerDetail.recordingOutsideHours')
                  : t('computerDetail.recordingOn')}
            </Badge>
            <Button variant="outline" size="sm" onClick={() => refetch()}>
              {t('common.refresh', 'Atualizar')}
            </Button>
          </div>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {isLoading ? (
          <p className="text-sm text-muted-foreground"><Loader2 className="h-4 w-4 animate-spin inline mr-1" /> {t('common.loading')}</p>
        ) : (
          <p className="text-sm text-muted-foreground">
            {t('computerDetail.recordingWindow')}: {status?.fromUtc ? formatDate(status.fromUtc) : '—'} → {status?.toUtc ? formatDate(status.toUtc) : '—'}
            {' · '}{formatBytes(status?.bytes || 0)}{status?.maxBytes ? ` / ${formatBytes(status.maxBytes)}` : ''}
          </p>
        )}

        <div className="max-w-sm">
          <Select
            label={t('computerDetail.recordingMonitor')}
            value={String(monitorIndex)}
            onChange={(e) => setMonitorIndex(Number(e.target.value))}
            options={monitorOptions}
          />
        </div>

        <div className="rounded-xl border border-border/60 bg-black/80 min-h-[360px] flex items-center justify-center overflow-hidden">
          {frame ? (
            <img alt="" src={`data:image/jpeg;base64,${frame}`} className="max-h-[720px] w-full object-contain" />
          ) : (
            <p className="text-sm text-muted-foreground px-6 text-center">{t('computerDetail.recordingEmpty')}</p>
          )}
        </div>

        <div className="space-y-1.5">
          <div className="flex items-center justify-between text-[11px] text-muted-foreground">
            <span className="inline-flex items-center gap-3">
              <span className="inline-flex items-center gap-1.5">
                <span className="h-2 w-3 rounded-sm bg-cyan-400" />
                {t('computerDetail.recordingHasVideo', 'Com gravação')}
              </span>
              <span className="inline-flex items-center gap-1.5">
                <span className="h-2 w-3 rounded-sm bg-zinc-800 border border-zinc-700" />
                {t('computerDetail.recordingNoVideo', 'Sem gravação')}
              </span>
            </span>
          </div>
          <div className="relative h-9">
            <div className="absolute inset-x-0 top-1 h-7 overflow-hidden rounded-md border border-zinc-700 bg-zinc-950">
              {recordedRanges.map((r, i) => (
                <div
                  key={i}
                  className="absolute top-0.5 bottom-0.5 rounded-[2px] bg-cyan-400/90"
                  style={{ left: `${r.left}%`, width: `${Math.max(0.15, r.width)}%` }}
                />
              ))}
              <div
                className="absolute top-0 bottom-0 bg-amber-400/25 pointer-events-none"
                style={{ left: `${Math.max(0, leftPct)}%`, width: `${Math.max(0.4, widthPct)}%` }}
              />
              <div
                className="absolute top-0 bottom-0 w-0.5 bg-white shadow-[0_0_6px_rgba(255,255,255,0.8)] pointer-events-none"
                style={{ left: `${Math.min(100, Math.max(0, cursorPct))}%` }}
              />
            </div>
            <input
              type="range"
              min={fromMs}
              max={toMs}
              step={1000}
              value={Math.min(toMs, Math.max(fromMs, cursor))}
              onChange={(e) => {
                const value = Number(e.target.value)
                setCursor(value)
                void loadFrame(value, monitorIndex)
              }}
              className="absolute inset-0 z-10 w-full cursor-pointer opacity-0"
            />
          </div>
          <div className="relative h-4">
            {ticks.map((tick) => (
              <span
                key={tick}
                className="absolute -translate-x-1/2 text-[10px] tabular-nums text-muted-foreground"
                style={{ left: `${((tick - fromMs) / span) * 100}%` }}
              >
                {formatTick(tick)}
              </span>
            ))}
          </div>
        </div>
        <p className="text-xs text-muted-foreground">
          {t('computerDetail.recordingPlayhead')}: {formatDate(new Date(cursor).toISOString())}
          {selected ? ` · ${selected.name}` : ''}
        </p>

        <div className="rounded-lg border border-border/60 p-3 space-y-3">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <p className="text-sm font-medium">{t('computerDetail.recordingClip')}</p>
            <p className="text-xs text-muted-foreground">
              {formatDate(new Date(rangeStart).toISOString())} → {formatDate(new Date(rangeEnd).toISOString())}
              {' · '}{formatClipLength(clipLength)}
              {' · '}{t('computerDetail.recordingClipMax')}
            </p>
          </div>
          <div className="grid gap-3 sm:grid-cols-2">
            <Input
              type="datetime-local"
              step={1}
              label={t('computerDetail.recordingClipStart')}
              value={toLocalInput(rangeStart)}
              onChange={(e) => {
                const value = fromLocalInput(e.target.value)
                if (value != null) clampClip(value, rangeEnd)
              }}
            />
            <Input
              type="datetime-local"
              step={1}
              label={t('computerDetail.recordingClipEnd')}
              value={toLocalInput(rangeEnd)}
              onChange={(e) => {
                const value = fromLocalInput(e.target.value)
                if (value != null) clampClip(rangeStart, value)
              }}
            />
          </div>
          <div className="flex flex-wrap gap-2">
            <Button type="button" variant="outline" size="sm" onClick={markStart}>
              <Flag className="h-3.5 w-3.5 mr-1" /> {t('computerDetail.recordingMarkStart')}
            </Button>
            <Button type="button" variant="outline" size="sm" onClick={markEnd}>
              <Flag className="h-3.5 w-3.5 mr-1" /> {t('computerDetail.recordingMarkEnd')}
            </Button>
            {[1, 5, 15].map((minutes) => (
              <Button key={minutes} type="button" variant="ghost" size="sm" onClick={() => applyPreset(minutes)}>
                {t('computerDetail.recordingLastMinutes', { n: minutes })}
              </Button>
            ))}
          </div>
        </div>

        <div className="flex flex-wrap gap-2">
          <Button
            onClick={() => {
              if (!playing) void loadFrame(cursor, monitorIndex)
              setPlaying((p) => !p)
            }}
            disabled={!online || loadingFrame}
          >
            {playing ? <><Pause className="h-4 w-4 mr-1" /> {t('computerDetail.recordingPause')}</> : <><Play className="h-4 w-4 mr-1" /> {t('computerDetail.recordingPlay')}</>}
          </Button>
          <Button variant="outline" onClick={handleExport} disabled={!online || exporting || clipFrom == null}>
            {exporting ? <Loader2 className="h-4 w-4 mr-1 animate-spin" /> : <Download className="h-4 w-4 mr-1" />}
            {t('computerDetail.recordingDownload')} ({formatClipLength(clipLength)})
          </Button>
        </div>
      </CardContent>
    </Card>
  )
}
