import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type { Computer, PaginatedResult } from '@/types'

export function useComputers(params?: Record<string, string>) {
  const query = params ? '?' + new URLSearchParams(params).toString() : ''
  return useQuery<PaginatedResult<Computer>>({
    queryKey: ['computers', params],
    queryFn: () => api.get(`/computers${query}`),
  })
}

export function useComputer(id: string) {
  return useQuery<Computer>({
    queryKey: ['computer', id],
    queryFn: () => api.get(`/computers/${id}`),
    enabled: !!id,
  })
}

export function useComputerTimeline(computerId: string, params?: Record<string, string>) {
  const query = params ? '?' + new URLSearchParams(params).toString() : ''
  return useQuery({
    queryKey: ['computer-timeline', computerId, params],
    queryFn: () => api.get(`/computers/${computerId}/timeline${query}`),
    enabled: !!computerId,
  })
}

export function useComputerApplications(computerId: string) {
  return useQuery({
    queryKey: ['computer-apps', computerId],
    queryFn: () => api.get(`/computers/${computerId}/applications`),
    enabled: !!computerId,
  })
}

export function useComputerAlerts(computerId: string) {
  return useQuery({
    queryKey: ['computer-alerts', computerId],
    queryFn: () => api.get(`/alerts?computerId=${computerId}&pageSize=50`),
    enabled: !!computerId,
  })
}

export function useUpdateComputer() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: Partial<Computer> }) =>
      api.put(`/computers/${id}`, data),
    onSuccess: (_, { id }) => {
      queryClient.invalidateQueries({ queryKey: ['computers'] })
      queryClient.invalidateQueries({ queryKey: ['computer', id] })
    },
  })
}
