import { useEffect, useRef, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { AnimatePresence, motion } from 'framer-motion'
import { Ticket, ChevronRight, ChevronLeft, ExternalLink } from 'lucide-react'
import { toast } from 'sonner'
import { api } from '@/lib/api'
import { cn } from '@/lib/utils'
import { useLiveRelativeTime } from '@/hooks/useLiveRelativeTime'

const RAIL_KEY = 'sentinela-hesk-rail'

export interface HeskTicket {
  id: number
  trackId: string
  subject: string
  name: string
  email?: string
  status: number
  statusLabel: string
  priority: number
  priorityLabel: string
  category?: string
  createdAt: string
  updatedAt: string
  event: string
  url?: string
}

interface HeskFeed {
  configured: boolean
  reachable: boolean
  error?: string
  fetchedAt?: string
  openCount: number
  tickets: HeskTicket[]
}

function eventStyle(event: string) {
  switch (event) {
    case 'new':
      return { label: 'Novo', bar: 'bg-emerald-400', text: 'text-emerald-400', bg: 'bg-emerald-400/10' }
    case 'reply':
      return { label: 'Resposta', bar: 'bg-sky-400', text: 'text-sky-400', bg: 'bg-sky-400/10' }
    case 'waiting':
      return { label: 'Aguardando', bar: 'bg-amber-400', text: 'text-amber-400', bg: 'bg-amber-400/10' }
    case 'resolved':
      return { label: 'Resolvido', bar: 'bg-zinc-400', text: 'text-zinc-400', bg: 'bg-zinc-400/10' }
    case 'progress':
      return { label: 'Andamento', bar: 'bg-violet-400', text: 'text-violet-400', bg: 'bg-violet-400/10' }
    case 'hold':
      return { label: 'Espera', bar: 'bg-orange-400', text: 'text-orange-400', bg: 'bg-orange-400/10' }
    default:
      return { label: 'Atualizado', bar: 'bg-primary', text: 'text-primary', bg: 'bg-primary/10' }
  }
}

function initials(name: string) {
  const parts = name.trim().split(/\s+/).filter(Boolean)
  if (parts.length === 0) return '?'
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase()
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase()
}

function TicketCard({ ticket, fresh }: { ticket: HeskTicket; fresh?: boolean }) {
  const age = useLiveRelativeTime(ticket.status === 3 ? ticket.updatedAt : ticket.createdAt)
  const style = eventStyle(ticket.event)
  const clock = (ticket.status === 3 ? ticket.updatedAt : ticket.createdAt)
    ? new Date(ticket.status === 3 ? ticket.updatedAt : ticket.createdAt).toLocaleString('pt-BR', {
        day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit',
      })
    : ''

  const open = () => {
    if (ticket.url) window.open(ticket.url, '_blank', 'noopener,noreferrer')
  }

  return (
    <button
      type="button"
      onClick={open}
      className={cn(
        'group relative w-full overflow-hidden rounded-xl border bg-white/5 text-left backdrop-blur-md transition-colors hover:border-white/20 hover:bg-white/10',
        fresh ? 'border-emerald-400/50 ring-1 ring-emerald-400/30' : 'border-white/10',
      )}
    >
      <span className={cn('absolute inset-y-0 left-0 w-1', style.bar)} />
      <div className="flex gap-2.5 p-2.5 pl-3.5">
        <div className={cn('flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-[11px] font-semibold', style.bg, style.text)}>
          {initials(ticket.name)}
        </div>
        <div className="min-w-0 flex-1">
          <div className="flex items-center justify-between gap-2">
            <span className={cn('text-[10px] font-semibold uppercase tracking-wide', style.text)}>{style.label}</span>
            <span className="tabular-nums text-[10px] text-muted-foreground/80">{clock}</span>
          </div>
          <p className="truncate text-[13px] font-medium leading-tight">{ticket.name || ticket.trackId}</p>
          <p className="truncate text-[11px] text-muted-foreground">{ticket.subject}</p>
          <div className="mt-1 flex items-center justify-between gap-2">
            <span className="truncate font-mono text-[10px] text-muted-foreground/70">{ticket.trackId}</span>
            <span className="shrink-0 text-[10px] text-muted-foreground/70">{age}</span>
          </div>
        </div>
        <ExternalLink className="h-3 w-3 shrink-0 text-muted-foreground/0 transition-colors group-hover:text-muted-foreground/70" />
      </div>
    </button>
  )
}

function pingNewTicket() {
  try {
    const ctx = new (window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext)()
    const osc = ctx.createOscillator()
    const gain = ctx.createGain()
    osc.connect(gain)
    gain.connect(ctx.destination)
    osc.frequency.value = 740
    gain.gain.value = 0.06
    osc.start()
    osc.stop(ctx.currentTime + 0.16)
  } catch {
    /* autoplay */
  }
}

function notifyBrowser(title: string, body: string, url?: string) {
  if (typeof Notification === 'undefined' || Notification.permission !== 'granted') return
  try {
    const n = new Notification(title, { body, tag: `hesk-${url || title}` })
    if (url) n.onclick = () => { window.focus(); window.open(url, '_blank', 'noopener,noreferrer') }
  } catch {
    /* ignore */
  }
}

export function HeskTicketRail() {
  const { t } = useTranslation()
  const [open, setOpen] = useState(() => localStorage.getItem(RAIL_KEY) !== '0')
  const [freshIds, setFreshIds] = useState<Set<string>>(new Set())
  const seenRef = useRef<Set<string> | null>(null)

  const { data } = useQuery({
    queryKey: ['hesk-tickets'],
    queryFn: () => api.get<HeskFeed>('/hesk/tickets'),
    refetchInterval: 8000,
    retry: 1,
  })

  useEffect(() => {
    localStorage.setItem(RAIL_KEY, open ? '1' : '0')
  }, [open])

  const tickets = data?.tickets ?? []
  const openTickets = tickets.filter((ticket) => ticket.status !== 3)
  const resolvedTickets = tickets.filter((ticket) => ticket.status === 3)
  const openCount = data?.openCount ?? openTickets.length

  useEffect(() => {
    if (!data?.reachable) return
    const ids = tickets.map((ticket) => ticket.trackId)
    if (seenRef.current === null) {
      seenRef.current = new Set(ids)
      return
    }
    const fresh = tickets.filter((ticket) => !seenRef.current!.has(ticket.trackId))
    for (const ticket of fresh) seenRef.current.add(ticket.trackId)
    if (fresh.length === 0) return

    setOpen(true)
    setFreshIds((prev) => {
      const next = new Set(prev)
      fresh.forEach((ticket) => next.add(ticket.trackId))
      return next
    })
    window.setTimeout(() => {
      setFreshIds((prev) => {
        const next = new Set(prev)
        fresh.forEach((ticket) => next.delete(ticket.trackId))
        return next
      })
    }, 20000)

    for (const ticket of fresh) {
      const title = t('hesk.newTicket', 'Novo chamado')
      const who = ticket.name || ticket.trackId
      toast.info(`${title} · ${who}`, {
        description: ticket.subject,
        duration: 14000,
        action: ticket.url
          ? {
              label: t('hesk.openTicket', 'Abrir'),
              onClick: () => window.open(ticket.url, '_blank', 'noopener,noreferrer'),
            }
          : undefined,
      })
      notifyBrowser(`${title} · ${who}`, ticket.subject, ticket.url)
    }
    pingNewTicket()
  }, [data?.reachable, data?.fetchedAt, tickets, t])

  if (!open) {
    return (
      <aside className="relative z-20 flex w-12 shrink-0 flex-col items-center border-l border-border/40 bg-card/30 py-3 backdrop-blur-2xl">
        <button
          type="button"
          onClick={() => setOpen(true)}
          title={t('hesk.title', 'Chamados')}
          className="relative flex h-10 w-10 items-center justify-center rounded-lg text-muted-foreground hover:bg-accent hover:text-foreground"
        >
          <Ticket className="h-5 w-5" />
          {openCount > 0 && (
            <span className="absolute -right-0.5 -top-0.5 min-w-[1rem] rounded-full bg-emerald-500 px-1 text-[10px] font-semibold leading-4 text-white">
              {openCount > 99 ? '99+' : openCount}
            </span>
          )}
        </button>
        <button type="button" onClick={() => setOpen(true)} className="mt-2 text-muted-foreground hover:text-foreground">
          <ChevronLeft className="h-4 w-4" />
        </button>
      </aside>
    )
  }

  return (
    <aside className="relative z-20 flex w-80 shrink-0 flex-col border-l border-white/10 bg-card/30 backdrop-blur-2xl">
      <div className="flex h-16 items-center justify-between border-b border-white/10 px-3">
        <div className="min-w-0">
          <div className="flex items-center gap-2">
            <span className="relative flex h-2 w-2">
              <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-emerald-400 opacity-60" />
              <span className="relative inline-flex h-2 w-2 rounded-full bg-emerald-400" />
            </span>
            <h2 className="text-sm font-semibold tracking-wide">{t('hesk.title', 'Chamados')}</h2>
          </div>
          <p className="text-[11px] text-muted-foreground">
            {t('hesk.subtitle', 'Últimos eventos do HESK')}
            {openCount > 0 ? ` · ${openCount} ${t('hesk.open', 'abertos')}` : ''}
          </p>
        </div>
        <button
          type="button"
          onClick={() => setOpen(false)}
          className="rounded-lg p-1.5 text-muted-foreground hover:bg-accent hover:text-foreground"
          title={t('hesk.collapse', 'Recolher')}
        >
          <ChevronRight className="h-4 w-4" />
        </button>
      </div>

      <div className="flex-1 space-y-2 overflow-y-auto p-3">
        {!data?.reachable && (
          <p className="rounded-xl border border-white/10 bg-white/5 px-3 py-4 text-center text-xs text-muted-foreground">
            {data?.error || t('hesk.unavailable', 'Aguardando o feed do HESK.')}
          </p>
        )}
        {data?.reachable && tickets.length === 0 && (
          <p className="px-2 py-8 text-center text-xs text-muted-foreground">
            {t('hesk.empty', 'Nenhum chamado recente.')}
          </p>
        )}
        {openTickets.length > 0 && (
          <p className="px-0.5 pt-1 text-[10px] font-semibold uppercase tracking-wide text-emerald-400/80">
            {t('hesk.openSection', 'Abertos')}
          </p>
        )}
        <AnimatePresence initial={false}>
          {openTickets.map((ticket) => (
            <motion.div
              key={ticket.trackId}
              initial={{ opacity: 0, x: 24 }}
              animate={{ opacity: 1, x: 0 }}
              transition={{ duration: 0.25 }}
            >
              <TicketCard ticket={ticket} fresh={freshIds.has(ticket.trackId)} />
            </motion.div>
          ))}
        </AnimatePresence>
        {resolvedTickets.length > 0 && (
          <p className="px-0.5 pt-3 text-[10px] font-semibold uppercase tracking-wide text-muted-foreground/70">
            {t('hesk.resolvedSection', 'Resolvidos')}
          </p>
        )}
        <AnimatePresence initial={false}>
          {resolvedTickets.map((ticket) => (
            <motion.div
              key={ticket.trackId}
              initial={{ opacity: 0, x: 24 }}
              animate={{ opacity: 1, x: 0 }}
              transition={{ duration: 0.25 }}
            >
              <TicketCard ticket={ticket} fresh={freshIds.has(ticket.trackId)} />
            </motion.div>
          ))}
        </AnimatePresence>
      </div>
    </aside>
  )
}
