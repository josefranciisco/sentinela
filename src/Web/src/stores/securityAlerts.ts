import { create } from 'zustand'

export interface SecurityAlertItem {
  id: string
  computerId?: string
  computerName?: string
  eventType: string
  category?: string
  description: string
  severity: string
  timestamp: string
  details?: string
  read: boolean
}

interface SecurityAlertsState {
  alerts: SecurityAlertItem[]
  unreadCount: number
  push: (alert: Omit<SecurityAlertItem, 'read'>) => void
  markAllRead: () => void
  markRead: (id: string) => void
  clear: () => void
}

export const useSecurityAlertsStore = create<SecurityAlertsState>((set, get) => ({
  alerts: [],
  unreadCount: 0,

  push: (alert) => {
    const existing = get().alerts
    if (existing.some((a) => a.id === alert.id)) return

    const next = [{ ...alert, read: false }, ...existing].slice(0, 50)
    set({
      alerts: next,
      unreadCount: next.filter((a) => !a.read).length,
    })
  },

  markAllRead: () => {
    set({
      alerts: get().alerts.map((a) => ({ ...a, read: true })),
      unreadCount: 0,
    })
  },

  markRead: (id) => {
    const alerts = get().alerts.map((a) => (a.id === id ? { ...a, read: true } : a))
    set({
      alerts,
      unreadCount: alerts.filter((a) => !a.read).length,
    })
  },

  clear: () => set({ alerts: [], unreadCount: 0 }),
}))
