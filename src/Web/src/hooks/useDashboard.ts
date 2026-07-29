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

export function useDashboardTopApps() {
  return useQuery({
    queryKey: ['dashboard-top-apps'],
    queryFn: () => api.get('/dashboard/top-applications'),
  })
}

export function useDashboardAvailability() {
  return useQuery({
    queryKey: ['dashboard-availability'],
    queryFn: () => api.get('/dashboard/availability'),
  })
}

export function useDashboardHeatmap() {
  return useQuery({
    queryKey: ['dashboard-heatmap'],
    queryFn: () => api.get('/dashboard/heatmap'),
  })
}
