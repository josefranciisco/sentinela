import { useAuthStore } from '@/stores/auth'

const BASE_URL = '/api/v1'

let isRefreshing = false
let failedQueue: Array<{ resolve: (token: string) => void; reject: (err: unknown) => void }> = []

function processQueue(error: unknown, token: string | null) {
  failedQueue.forEach(({ resolve, reject }) => {
    error ? reject(error) : resolve(token!)
  })
  failedQueue = []
}

async function request<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
  const { accessToken, user } = useAuthStore.getState()

  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(options.headers as Record<string, string>),
  }

  if (accessToken) headers['Authorization'] = `Bearer ${accessToken}`
  if (user?.tenantId) headers['X-Tenant-Id'] = user.tenantId

  let response = await fetch(`${BASE_URL}${endpoint}`, { ...options, headers, cache: 'no-store' })

  if (response.status === 401 && !endpoint.includes('/auth/')) {
    const { refreshToken, refreshAuth } = useAuthStore.getState()

    if (isRefreshing) {
      return new Promise<string>((resolve, reject) => {
        failedQueue.push({ resolve, reject })
      }).then((newToken) => {
        headers['Authorization'] = `Bearer ${newToken}`
        return fetch(`${BASE_URL}${endpoint}`, { ...options, headers }).then(r => r.json())
      }) as Promise<T>
    }

    isRefreshing = true

    try {
      if (!refreshToken) throw new Error('No refresh token')

      const refreshResponse = await fetch(`${BASE_URL}/auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      })

      if (!refreshResponse.ok) throw new Error('Refresh failed')

      const data = await refreshResponse.json()
      localStorage.setItem('accessToken', data.accessToken)
      localStorage.setItem('refreshToken', data.refreshToken)
      useAuthStore.setState({ accessToken: data.accessToken, refreshToken: data.refreshToken })

      processQueue(null, data.accessToken)

      headers['Authorization'] = `Bearer ${data.accessToken}`
      response = await fetch(`${BASE_URL}${endpoint}`, { ...options, headers })
    } catch (err) {
      processQueue(err, null)
      useAuthStore.getState().logout()
      window.location.href = '/login'
      throw err
    } finally {
      isRefreshing = false
    }
  }

  if (response.status === 401) {
    useAuthStore.getState().logout()
    window.location.href = '/login'
    throw new Error('Unauthorized')
  }

  if (response.status === 204) return undefined as T

  if (!response.ok) {
    const error = await response.json().catch(() => ({ message: 'Request failed' }))
    throw new Error(error.message || error.title || 'Request failed')
  }

  return response.json()
}

export const api = {
  get: <T>(url: string) => request<T>(url),
  post: <T>(url: string, data?: unknown) => request<T>(url, { method: 'POST', body: JSON.stringify(data) }),
  put: <T>(url: string, data?: unknown) => request<T>(url, { method: 'PUT', body: JSON.stringify(data) }),
  delete: <T>(url: string) => request<T>(url, { method: 'DELETE' }),
}
