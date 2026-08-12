import { useState } from 'react'
import { Sidebar } from './Sidebar'
import { Header } from './Header'
import { RealtimeSecurityAlerts } from '@/components/RealtimeSecurityAlerts'

export function Layout({ children }: { children: React.ReactNode }) {
  const [sidebarOpen, setSidebarOpen] = useState(true)

  return (
    <div className="relative flex h-screen overflow-hidden bg-background">
      {/* Atmosfera — mesmo vocabulário visual do login */}
      <div className="pointer-events-none absolute inset-0 z-0 bg-gradient-to-br from-primary/15 via-transparent to-blue-500/15" />
      <div className="pointer-events-none absolute -top-24 -left-16 z-0 h-80 w-80 rounded-full bg-primary/25 blur-3xl animate-pulse-slow" />
      <div className="pointer-events-none absolute -bottom-32 -right-20 z-0 h-[28rem] w-[28rem] rounded-full bg-blue-500/20 blur-3xl animate-pulse-slow" />
      <div className="pointer-events-none absolute top-1/3 right-1/4 z-0 h-64 w-64 rounded-full bg-violet-500/10 blur-3xl" />

      <RealtimeSecurityAlerts />
      <div className="relative z-10 flex h-full w-full overflow-hidden">
        <Sidebar open={sidebarOpen} onToggle={() => setSidebarOpen(!sidebarOpen)} />
        <div className="flex flex-col flex-1 overflow-hidden min-w-0">
          <Header onMenuClick={() => setSidebarOpen(!sidebarOpen)} />
          <main className="flex-1 overflow-y-auto p-6">
            <div className="animate-in mx-auto max-w-7xl">
              {children}
            </div>
          </main>
        </div>
      </div>
    </div>
  )
}
