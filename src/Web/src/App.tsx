import { Routes, Route, Navigate } from 'react-router-dom'
import { useAuthStore } from '@/stores/auth'
import { useEffect } from 'react'
import { Layout } from '@/components/layout/Layout'
import { Dashboard } from '@/pages/Dashboard'
import { Login } from '@/pages/Login'
import { Computers } from '@/pages/Computers'
import { ComputerDetail } from '@/pages/ComputerDetail'
import { Alerts } from '@/pages/Alerts'
import { AlertDetail } from '@/pages/AlertDetail'
import { Security } from '@/pages/Security'
import { Automation } from '@/pages/Automation'
import { Audit } from '@/pages/Audit'
import { Executive } from '@/pages/Executive'
import { Noc } from '@/pages/Noc'
import { Settings } from '@/pages/Settings'
import { AiAssistant } from '@/pages/AiAssistant'
import { Software } from '@/pages/Software'
import { RemoteAssistance } from '@/pages/RemoteAssistance'
import { ScreenCapture } from '@/pages/ScreenCapture'
import { Users } from '@/pages/Users'
import { FileTransfers } from '@/pages/FileTransfers'

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
      <Route path="/alerts" element={<PrivateRoute><Alerts /></PrivateRoute>} />
      <Route path="/alerts/:id" element={<PrivateRoute><AlertDetail /></PrivateRoute>} />
      <Route path="/security" element={<PrivateRoute><Security /></PrivateRoute>} />
      <Route path="/automation" element={<PrivateRoute><Automation /></PrivateRoute>} />
      <Route path="/audit" element={<PrivateRoute><Audit /></PrivateRoute>} />
      <Route path="/executive" element={<PrivateRoute><Executive /></PrivateRoute>} />
      <Route path="/noc" element={<PrivateRoute><Noc /></PrivateRoute>} />
      <Route path="/settings" element={<PrivateRoute><Settings /></PrivateRoute>} />
      <Route path="/ai" element={<PrivateRoute><AiAssistant /></PrivateRoute>} />
      <Route path="/software" element={<PrivateRoute><Software /></PrivateRoute>} />
      <Route path="/remote" element={<PrivateRoute><RemoteAssistance /></PrivateRoute>} />
      <Route path="/screenshots" element={<PrivateRoute><ScreenCapture /></PrivateRoute>} />
      <Route path="/users" element={<PrivateRoute><Users /></PrivateRoute>} />
      <Route path="/file-transfers" element={<PrivateRoute><FileTransfers /></PrivateRoute>} />
    </Routes>
  )
}
