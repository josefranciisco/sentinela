import { create } from 'zustand'
import { api } from '@/lib/api'

interface User {
  id: string
  username: string
  email: string
  roles: string[]
  twoFactorEnabled: boolean
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
      set({ accessToken: token, refreshToken: refresh, user: JSON.parse(user), isAuthenticated: true, isLoading: false })
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
