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

function PrivateRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated } = useAuthStore()
  return isAuthenticated ? <Layout>{children}</Layout> : <Navigate to="/login" />
}

export default function App() {
  const { initialize } = useAuthStore()

  useEffect(() => { initialize() }, [initialize])

  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route path="/" element={<PrivateRoute><Dashboard /></PrivateRoute>} />
      <Route path="/computers" element={<PrivateRoute><Computers /></PrivateRoute>} />
      <Route path="/computers/:id" element={<PrivateRoute><ComputerDetail /></PrivateRoute>} />
      <Route path="/users" element={<PrivateRoute><Users /></PrivateRoute>} />
      <Route path="/settings" element={<PrivateRoute><Settings /></PrivateRoute>} />
      <Route path="/remote-assistance" element={<PrivateRoute><RemoteAssistance /></PrivateRoute>} />
    </Routes>
  )
}
