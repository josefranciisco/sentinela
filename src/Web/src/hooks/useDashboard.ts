import { useQuery } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type { DashboardStats } from '@/types'

export function useDashboardStats() {
  return useQuery<DashboardStats>({
    queryKey: ['dashboard-stats'],
    queryFn: () => api.get('/dashboard/stats'),
  })
}

export function useDashboardActivity() {
  return useQuery({
    queryKey: ['dashboard-activity'],
    queryFn: () => api.get('/dashboard/activity'),
  })
}

export function useIncidents(limit = 5) {
  return useQuery({
    queryKey: ['security-incidents', limit],
    queryFn: () => api.get(`/security/incidents?limit=${limit}`),
  })
}
