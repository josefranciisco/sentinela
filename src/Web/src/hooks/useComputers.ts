import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type { Computer, PaginatedResult } from '@/types'

export function useComputers(params?: Record<string, string>) {
  const query = params ? '?' + new URLSearchParams(params).toString() : ''
  return useQuery<PaginatedResult<Computer>>({
    queryKey: ['computers', params],
    queryFn: () => api.get(`/computers${query}`),
    refetchInterval: 10_000,
  })
}

export function useComputer(id: string) {
  return useQuery<Computer>({
    queryKey: ['computer', id],
    queryFn: () => api.get(`/computers/${id}`),
    enabled: !!id,
    refetchInterval: 10_000,
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

export function useDeleteComputer() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete(`/computers/${id}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['computers'] })
      queryClient.invalidateQueries({ queryKey: ['computer-departments'] })
      queryClient.invalidateQueries({ queryKey: ['dashboard-stats'] })
    },
  })
}

export function useDepartments() {
  return useQuery<string[]>({
    queryKey: ['computer-departments'],
    queryFn: () => api.get('/computers/departments'),
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
      queryClient.invalidateQueries({ queryKey: ['computer-departments'] })
    },
  })
}
