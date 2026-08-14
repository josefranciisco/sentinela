import { create } from 'zustand'
import { api } from '@/lib/api'

interface User {
  id: string
  username: string
  email: string
  roles: string[]
  twoFactorEnabled: boolean
  tenantId?: string
  permissions?: string[]
}

export function normalizeAuthUser(raw: unknown): User {
  const data = (raw ?? {}) as Record<string, unknown>
  const roles = data.roles ?? data.Roles
  const permissions = data.permissions ?? data.Permissions
  return {
    id: String(data.id ?? data.Id ?? ''),
    username: String(data.username ?? data.Username ?? ''),
    email: String(data.email ?? data.Email ?? ''),
    roles: Array.isArray(roles) ? roles.map(String) : [],
    twoFactorEnabled: Boolean(data.twoFactorEnabled ?? data.TwoFactorEnabled),
    tenantId: data.tenantId != null || data.TenantId != null
      ? String(data.tenantId ?? data.TenantId)
      : undefined,
    permissions: Array.isArray(permissions) ? permissions.map(String) : [],
  }
}

interface AuthState {
  user: User | null
  accessToken: string | null
  refreshToken: string | null
  isAuthenticated: boolean
  isLoading: boolean
  login: (username: string, password: string, twoFactorCode?: string) => Promise<void>
  logout: () => void
  refreshAuth: () => Promise<void>
  initialize: () => void
}

export const useAuthStore = create<AuthState>((set, get) => ({
  user: null,
  accessToken: null,
  refreshToken: null,
  isAuthenticated: false,
  isLoading: true,

  initialize: () => {
    const token = localStorage.getItem('accessToken')
    const refresh = localStorage.getItem('refreshToken')
    const user = localStorage.getItem('user')
    if (token && refresh && user) {
      const parsed = normalizeAuthUser(JSON.parse(user))
      localStorage.setItem('user', JSON.stringify(parsed))
      set({ accessToken: token, refreshToken: refresh, user: parsed, isAuthenticated: true, isLoading: false })
    } else {
      set({ isLoading: false })
    }
  },

  login: async (username, password, twoFactorCode) => {
    const response = await api.post<{
      accessToken: string
      refreshToken: string
      user: User
    }>('/auth/login', { username, password, twoFactorCode })

    localStorage.setItem('accessToken', response.accessToken)
    localStorage.setItem('refreshToken', response.refreshToken)
    localStorage.setItem('user', JSON.stringify(response.user))

    set({
      user: response.user,
      accessToken: response.accessToken,
      refreshToken: response.refreshToken,
      isAuthenticated: true,
    })
  },

  logout: () => {
    localStorage.removeItem('accessToken')
    localStorage.removeItem('refreshToken')
    localStorage.removeItem('user')
    set({ user: null, accessToken: null, refreshToken: null, isAuthenticated: false })
  },

  refreshAuth: async () => {
    try {
      const { refreshToken } = get()
      if (!refreshToken) throw new Error('No refresh token')

      const response = await api.post<{
        accessToken: string
        refreshToken: string
      }>('/auth/refresh', { refreshToken })

      localStorage.setItem('accessToken', response.accessToken)
      localStorage.setItem('refreshToken', response.refreshToken)

      set({ accessToken: response.accessToken, refreshToken: response.refreshToken })
    } catch {
      get().logout()
    }
  },
}))
