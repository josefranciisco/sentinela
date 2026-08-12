import { useQuery } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type { DashboardStats } from '@/types'

const REFRESH_MS = 15_000

export function useDashboardStats(autoRefresh = false) {
  return useQuery<DashboardStats>({
    queryKey: ['dashboard-stats'],
    queryFn: () => api.get('/dashboard/stats'),
    refetchInterval: autoRefresh ? REFRESH_MS : false,
  })
}

export function useDashboardActivity(autoRefresh = false) {
  return useQuery({
    queryKey: ['dashboard-activity'],
    queryFn: () => api.get('/dashboard/activity'),
    refetchInterval: autoRefresh ? REFRESH_MS : false,
  })
}

export function useIncidents(limit = 5, autoRefresh = false) {
  return useQuery({
    queryKey: ['security-incidents', limit],
    queryFn: () => api.get(`/security/incidents?limit=${limit}`),
    refetchInterval: autoRefresh ? REFRESH_MS : false,
  })
}
