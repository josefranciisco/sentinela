import { NavLink } from 'react-router-dom'
import { cn } from '@/lib/utils'
import { useAuthStore } from '@/stores/auth'
import { useTranslation } from 'react-i18next'
import { Avatar } from '@/components/ui/avatar'
import {
  LayoutDashboard, Monitor, Bell, Shield, Workflow, Package,
  FileSearch, Camera, Radio, Brain, BarChart3, Tv, Settings, Users,
  ChevronLeft, LogOut, Download,
} from 'lucide-react'

const navItems = [
  { icon: LayoutDashboard, labelKey: 'nav.dashboard', path: '/' },
  { icon: Monitor, labelKey: 'nav.computers', path: '/computers' },
  { icon: Bell, labelKey: 'nav.alerts', path: '/alerts' },
  { icon: Download, labelKey: 'nav.fileTransfers', path: '/file-transfers' },
  { icon: Shield, labelKey: 'nav.security', path: '/security' },
  { icon: Workflow, labelKey: 'nav.automation', path: '/automation' },
  { icon: Package, labelKey: 'nav.software', path: '/software' },
  { icon: FileSearch, labelKey: 'nav.audit', path: '/audit' },
  { icon: Camera, labelKey: 'nav.screenCaptures', path: '/screenshots' },
  { icon: Radio, labelKey: 'nav.remoteAssist', path: '/remote' },
  { icon: Brain, labelKey: 'nav.aiAssistant', path: '/ai' },
  { icon: BarChart3, labelKey: 'nav.executive', path: '/executive' },
  { icon: Tv, labelKey: 'nav.nocMode', path: '/noc' },
  { icon: Users, labelKey: 'nav.users', path: '/users' },
  { icon: Settings, labelKey: 'nav.settings', path: '/settings' },
]

interface SidebarProps {
  open: boolean
  onToggle: () => void
}

export function Sidebar({ open, onToggle }: SidebarProps) {
  const { t } = useTranslation()
  const { user, logout } = useAuthStore()

  return (
    <aside
      className={cn(
        'flex flex-col border-r border-border/50 bg-card/50 backdrop-blur-xl transition-all duration-300 z-30',
        open ? 'w-64' : 'w-16',
      )}
    >
      <div className="flex items-center justify-between h-16 px-4 border-b border-border/50">
        {open && (
          <span className="text-xl font-bold bg-clip-text text-transparent bg-gradient-to-r from-primary to-blue-500">
            Sentinela
          </span>
        )}
        <button
          onClick={onToggle}
          className={cn(
            'p-1.5 rounded-lg hover:bg-accent transition-colors',
            !open && 'mx-auto',
          )}
        >
          <ChevronLeft className={cn('h-5 w-5 transition-transform', !open && 'rotate-180')} />
        </button>
      </div>

      <nav className="flex-1 overflow-y-auto py-2 px-2 space-y-0.5">
        {navItems.map((item) => (
          <NavLink
            key={item.path}
            to={item.path}
            className={({ isActive }) =>
              cn(
                'flex items-center gap-3 px-3 py-2.5 rounded-lg transition-all duration-200',
                isActive
                  ? 'bg-primary/10 text-primary font-medium'
                  : 'text-muted-foreground hover:text-foreground hover:bg-accent/50',
                !open && 'justify-center px-2',
              )
            }
            title={!open ? t(item.labelKey) : undefined}
          >
            <item.icon className="h-5 w-5 shrink-0" />
            {open && <span className="text-sm">{t(item.labelKey)}</span>}
          </NavLink>
        ))}
      </nav>

      <div className={cn('border-t border-border/50 p-3', !open && 'flex justify-center')}>
        {open ? (
          <div className="flex items-center gap-3">
            <Avatar fallback={user?.username?.charAt(0) || 'U'} size="sm" />
            <div className="flex-1 min-w-0">
              <p className="text-sm font-medium truncate">{user?.username || t('nav.user')}</p>
              <p className="text-xs text-muted-foreground truncate">{user?.email || ''}</p>
            </div>
            <button onClick={logout} className="p-1.5 rounded-lg hover:bg-accent text-muted-foreground hover:text-destructive transition-colors">
              <LogOut className="h-4 w-4" />
            </button>
          </div>
        ) : (
          <DropdownLogout onLogout={logout} user={user} />
        )}
      </div>
    </aside>
  )
}

function DropdownLogout({ onLogout, user }: { onLogout: () => void; user: { username?: string } | null }) {
  const { t } = useTranslation()
  return (
    <div className="relative group">
      <Avatar fallback={user?.username?.charAt(0) || 'U'} size="sm" className="cursor-pointer" />
      <div className="absolute bottom-full left-1/2 -translate-x-1/2 mb-2 hidden group-hover:block">
        <div className="bg-popover border rounded-lg p-2 shadow-lg whitespace-nowrap">
          <button onClick={onLogout} className="flex items-center gap-2 text-sm text-destructive hover:bg-accent rounded px-2 py-1 w-full">
            <LogOut className="h-4 w-4" /> {t('nav.logout')}
          </button>
        </div>
      </div>
    </div>
  )
}
