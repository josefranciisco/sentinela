import { useEffect, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import * as signalR from '@microsoft/signalr'
import { toast } from 'sonner'
import { useQueryClient } from '@tanstack/react-query'
import { useAuthStore } from '@/stores/auth'
import { hubUrl } from '@/lib/config'
import { useSecurityAlertsStore } from '@/stores/securityAlerts'

const EVENT_LABELS: Record<string, string> = {
  USBConnected: 'USB conectado',
  USBDisconnected: 'USB desconectado',
  FileCopy: 'Cópia de arquivo via USB',
  MalwareDetected: 'Malware detectado',
  AntivirusDisabled: 'Antivírus desativado',
  AntivirusOutdated: 'Antivírus desatualizado',
  CryptominerDetected: 'Criptominerador detectado',
  RansomwarePattern: 'Padrão de ransomware',
  MassFileRename: 'Renomeação em massa',
  FailedLogon: 'Falha de login',
  SuspiciousNetworkActivity: 'Atividade de rede suspeita',
  HighCpuProcess: 'Processo com CPU alta',
  SoftwareInstalled: 'Software instalado',
  SoftwareUninstalled: 'Software desinstalado',
}

/** Eventos que sempre geram toast em tempo real (como pendrive). */
const ALWAYS_ALERT_TYPES = new Set([
  'USBConnected',
  'FileCopy',
  'SoftwareInstalled',
  'SoftwareUninstalled',
  'MalwareDetected',
  'AntivirusDisabled',
  'AntivirusOutdated',
  'CryptominerDetected',
  'RansomwarePattern',
  'MassFileRename',
  'SuspiciousNetworkActivity',
])

function shouldNotify(eventType: string, severity: string) {
  const sev = (severity || '').toLowerCase()
  if (ALWAYS_ALERT_TYPES.has(eventType)) return true
  return sev === 'high' || sev === 'critical'
}

function normalizePayload(raw: any) {
  const eventType = raw?.eventType || raw?.EventType || raw?.title || raw?.Title || 'Security'
  const severity = String(raw?.severity || raw?.Severity || 'Medium')
  const id = String(raw?.id || raw?.Id || `${eventType}-${Date.now()}`)
  return {
    id,
    computerId: raw?.computerId || raw?.ComputerId,
    computerName: raw?.computerName || raw?.ComputerName,
    eventType,
    category: raw?.category || raw?.Category,
    description: raw?.description || raw?.Description || EVENT_LABELS[eventType] || eventType,
    severity,
    timestamp: raw?.timestamp || raw?.Timestamp || raw?.createdAt || raw?.CreatedAt || new Date().toISOString(),
    details: raw?.details || raw?.Details,
  }
}

type AlertPayload = ReturnType<typeof normalizePayload>

function showToast(alert: AlertPayload, navigate: (path: string) => void) {
  const label = EVENT_LABELS[alert.eventType] || alert.eventType
  const where = alert.computerName ? ` em ${alert.computerName}` : ''
  const title = `${label}${where}`
  const message = alert.description || label
  const sev = alert.severity.toLowerCase()

  const opts = {
    description: message,
    duration: sev === 'critical' || sev === 'high' || alert.eventType === 'FileCopy' ? 15000 : 8000,
    action: alert.computerId
      ? {
          label: 'Ver',
          onClick: () => {
            navigate(`/computers/${alert.computerId}?tab=security`)
          },
        }
      : {
          label: 'Ver incidentes',
          onClick: () => navigate('/'),
        },
  }

  if (sev === 'critical' || sev === 'high' || alert.eventType === 'FileCopy') toast.error(title, opts)
  else if (
    alert.eventType === 'USBConnected' ||
    alert.eventType === 'SoftwareInstalled' ||
    alert.eventType === 'SoftwareUninstalled'
  )
    toast.warning(title, opts)
  else toast.warning(title, opts)

  try {
    const ctx = new (window.AudioContext || (window as any).webkitAudioContext)()
    const osc = ctx.createOscillator()
    const gain = ctx.createGain()
    osc.connect(gain)
    gain.connect(ctx.destination)
    osc.frequency.value =
      sev === 'critical' || alert.eventType === 'FileCopy'
        ? 880
        : alert.eventType.startsWith('USB') || alert.eventType === 'SoftwareInstalled'
          ? 660
          : 520
    gain.gain.value = 0.05
    osc.start()
    osc.stop(ctx.currentTime + (alert.eventType === 'FileCopy' ? 0.25 : 0.15))
  } catch {
    /* ignore autoplay restrictions */
  }
}

/**
 * Mantém conexão SignalR com /hubs/monitoring e /hubs/alerts
 * e dispara toast + badge do sino para eventos críticos/USB.
 */
export function RealtimeSecurityAlerts() {
  const token = useAuthStore((s) => s.accessToken)
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated)
  const push = useSecurityAlertsStore((s) => s.push)
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const handledIds = useRef(new Set<string>())
  const navigateRef = useRef(navigate)
  navigateRef.current = navigate

  useEffect(() => {
    if (!token || !isAuthenticated) return

    const handleEvent = (raw: any) => {
      const alert = normalizePayload(raw)
      if (handledIds.current.has(alert.id)) return
      handledIds.current.add(alert.id)
      if (handledIds.current.size > 200) {
        const first = handledIds.current.values().next().value
        if (first) handledIds.current.delete(first)
      }

      if (!shouldNotify(alert.eventType, alert.severity)) return

      push(alert)
      showToast(alert, (path) => navigateRef.current(path))

      queryClient.invalidateQueries({ queryKey: ['incidents'] })
      queryClient.invalidateQueries({ queryKey: ['security-events'] })
      queryClient.invalidateQueries({ queryKey: ['security-summary'] })
      queryClient.invalidateQueries({ queryKey: ['dashboard-activity'] })
      queryClient.invalidateQueries({ queryKey: ['file-transfers'] })
      queryClient.invalidateQueries({ queryKey: ['dashboard-stats'] })
      if (alert.eventType === 'SoftwareInstalled' || alert.eventType === 'SoftwareUninstalled') {
        queryClient.invalidateQueries({ queryKey: ['computer-software'] })
        queryClient.invalidateQueries({ queryKey: ['computer-security-events'] })
      }
    }

    const build = (url: string) =>
      new signalR.HubConnectionBuilder()
        .withUrl(url, { accessTokenFactory: () => useAuthStore.getState().accessToken || token })
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .build()

    const monitoring = build(hubUrl('/hubs/monitoring'))
    const alerts = build(hubUrl('/hubs/alerts'))

    monitoring.on('SecurityEvent', handleEvent)
    monitoring.on('ScreenshotReady', () => {
      queryClient.invalidateQueries({ queryKey: ['computer-screenshots'] })
      queryClient.invalidateQueries({ queryKey: ['screenshots'] })
    })
    alerts.on('SecurityEvent', handleEvent)
    alerts.on('AlertCreated', handleEvent)

    monitoring.start().catch((err) => console.error('MonitoringHub error:', err))
    alerts.start().catch((err) => console.error('AlertHub error:', err))

    return () => {
      monitoring.stop()
      alerts.stop()
    }
  }, [token, isAuthenticated, push, queryClient])

  return null
}
