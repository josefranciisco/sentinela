import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type { Alert, PaginatedResult } from '@/types'

export function useAlerts(params?: Record<string, string>) {
  const query = params ? '?' + new URLSearchParams(params).toString() : ''
  return useQuery<PaginatedResult<Alert>>({
    queryKey: ['alerts', params],
    queryFn: () => api.get(`/alerts${query}`),
  })
}

export function useAlert(id: string) {
  return useQuery<Alert>({
    queryKey: ['alert', id],
    queryFn: () => api.get(`/alerts/${id}`),
    enabled: !!id,
  })
}

export function useUpdateAlertStatus() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, status, assignedTo }: { id: string; status: string; assignedTo?: string }) =>
      api.put(`/alerts/${id}/status`, { status, assignedTo }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['alerts'] })
    },
  })
}

export function useBulkUpdateAlerts() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ ids, status, assignedTo }: { ids: string[]; status: string; assignedTo?: string }) =>
      api.put('/alerts/bulk', { ids, status, assignedTo }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['alerts'] })
    },
  })
}
