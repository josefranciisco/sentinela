import { Routes, Route, Navigate } from 'react-router-dom'
import { useAuthStore } from '@/stores/auth'
import { useEffect } from 'react'
import { Layout } from '@/components/layout/Layout'
import { Dashboard } from '@/pages/Dashboard'
import { Login } from '@/pages/Login'
import { Computers } from '@/pages/Computers'
import { ComputerDetail } from '@/pages/ComputerDetail'
import { Settings } from '@/pages/Settings'
import { Users } from '@/pages/Users'
import { RemoteAssistance } from '@/pages/RemoteAssistance'
import { Roles } from '@/pages/Roles'
import { AccessDenied } from '@/pages/AccessDenied'
import { PermissionGuard } from '@/components/PermissionGuard'

function PrivateRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, isLoading } = useAuthStore()
  if (isLoading) {
    return (
      <div className="flex h-screen items-center justify-center bg-background text-muted-foreground text-sm">
        Carregando...
      </div>
    )
  }
  return isAuthenticated ? <Layout>{children}</Layout> : <Navigate to="/login" replace />
}

export default function App() {
  const { initialize } = useAuthStore()

  useEffect(() => { initialize() }, [initialize])

  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route path="/access-denied" element={<PrivateRoute><AccessDenied /></PrivateRoute>} />
      <Route path="/" element={<PrivateRoute><Dashboard /></PrivateRoute>} />
      <Route path="/computers" element={<PrivateRoute><Computers /></PrivateRoute>} />
      <Route path="/computers/:id" element={<PrivateRoute><ComputerDetail /></PrivateRoute>} />
      <Route path="/users" element={<PrivateRoute><PermissionGuard permission="users.view"><Users /></PermissionGuard></PrivateRoute>} />
      <Route path="/roles" element={<PrivateRoute><PermissionGuard permission="roles.view"><Roles /></PermissionGuard></PrivateRoute>} />
      <Route path="/settings" element={<PrivateRoute><Settings /></PrivateRoute>} />
      <Route path="/remote-assistance" element={<PrivateRoute><PermissionGuard permission="remote.view"><RemoteAssistance /></PermissionGuard></PrivateRoute>} />
    </Routes>
  )
}
