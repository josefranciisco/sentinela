import { useState, useEffect, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import { cn, formatRelative, formatSeverity } from '@/lib/utils'
import { useTranslation } from 'react-i18next'
import { useAuthStore } from '@/stores/auth'
import { useSecurityAlertsStore } from '@/stores/securityAlerts'
import { Avatar } from '@/components/ui/avatar'
import {
  Search, Bell, Menu, Command, Globe, Sun, Moon,
  Settings as SettingsIcon, LogOut, ShieldAlert,
} from 'lucide-react'
import i18n from '@/lib/i18n'
import { applyTheme, readTheme, type Theme } from '@/lib/theme'

interface HeaderProps {
  onMenuClick: () => void
}

const EVENT_LABELS: Record<string, string> = {
  USBConnected: 'USB conectado',
  USBDisconnected: 'USB desconectado',
  FileCopy: 'Cópia USB',
  SoftwareInstalled: 'Software instalado',
  SoftwareUninstalled: 'Software desinstalado',
  MalwareDetected: 'Malware',
  AntivirusDisabled: 'AV desativado',
  AntivirusOutdated: 'AV desatualizado',
  CryptominerDetected: 'Criptominerador',
  RansomwarePattern: 'Ransomware',
  MassFileRename: 'Rename em massa',
  FailedLogon: 'Falha de login',
}

export function Header({ onMenuClick }: HeaderProps) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { user, logout } = useAuthStore()
  const { alerts, unreadCount, markAllRead, markRead } = useSecurityAlertsStore()
  const [searchOpen, setSearchOpen] = useState(false)
  const [searchQuery, setSearchQuery] = useState('')
  const [userMenuOpen, setUserMenuOpen] = useState(false)
  const [langMenuOpen, setLangMenuOpen] = useState(false)
  const [bellOpen, setBellOpen] = useState(false)
  const [theme, setTheme] = useState<Theme>(() => readTheme())
  const searchRef = useRef<HTMLInputElement>(null)
  const menuRef = useRef<HTMLDivElement>(null)
  const langRef = useRef<HTMLDivElement>(null)
  const bellRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
        e.preventDefault()
        setSearchOpen(true)
        searchRef.current?.focus()
      }
    }
    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [])

  useEffect(() => {
    const handleClick = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setUserMenuOpen(false)
      }
      if (langRef.current && !langRef.current.contains(e.target as Node)) {
        setLangMenuOpen(false)
      }
      if (bellRef.current && !bellRef.current.contains(e.target as Node)) {
        setBellOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClick)
    return () => document.removeEventListener('mousedown', handleClick)
  }, [])

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault()
    if (searchQuery.trim()) {
      navigate(`/computers?search=${encodeURIComponent(searchQuery.trim())}`)
      setSearchOpen(false)
      setSearchQuery('')
    }
  }

  const changeLanguage = (lng: string) => {
    i18n.changeLanguage(lng)
    localStorage.setItem('sentinela-lang', lng)
    setLangMenuOpen(false)
  }

  const openBell = () => {
    setBellOpen(!bellOpen)
    if (!bellOpen) markAllRead()
  }

  return (
    <header className="relative z-40 flex h-16 shrink-0 items-center justify-between border-b border-border/50 bg-card/25 px-4 backdrop-blur-xl md:px-6">
      <div className="flex items-center gap-4">
        <button onClick={onMenuClick} className="p-2 rounded-lg hover:bg-accent text-muted-foreground lg:hidden">
          <Menu className="h-5 w-5" />
        </button>

        {searchOpen ? (
          <form onSubmit={handleSearch} className="flex items-center">
            <input
              ref={searchRef}
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder={t('header.searchPlaceholder')}
              className="w-80 h-9 rounded-lg border border-input bg-background px-3 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              onBlur={() => { if (!searchQuery) setSearchOpen(false) }}
              autoFocus
            />
          </form>
        ) : (
          <button
            onClick={() => setSearchOpen(true)}
            className="flex items-center gap-2 px-3 py-1.5 rounded-lg border border-input bg-background/50 text-muted-foreground hover:text-foreground transition-colors text-sm"
          >
            <Search className="h-4 w-4" />
            <span className="hidden md:inline">{t('header.search')}</span>
            <kbd className="hidden md:inline-flex items-center gap-1 rounded border bg-muted px-1.5 py-0.5 text-xs font-mono">
              <Command className="h-3 w-3" />K
            </kbd>
          </button>
        )}
      </div>

      <div className="flex items-center gap-3">
        <div ref={bellRef} className="relative">
          <button
            onClick={openBell}
            className="relative p-2 rounded-lg hover:bg-accent text-muted-foreground transition-colors"
            aria-label="Alertas em tempo real"
          >
            <Bell className="h-5 w-5" />
            {unreadCount > 0 && (
              <span className="absolute top-1 right-1 min-w-[0.9rem] h-3.5 px-1 rounded-full bg-destructive text-[10px] leading-3.5 text-white font-semibold flex items-center justify-center">
                {unreadCount > 9 ? '9+' : unreadCount}
              </span>
            )}
          </button>

          {bellOpen && (
            <div className="absolute right-0 top-full mt-1 z-[100] w-96 max-h-[28rem] overflow-hidden rounded-lg border bg-popover shadow-lg animate-in">
              <div className="flex items-center justify-between px-3 py-2 border-b border-border">
                <p className="text-sm font-medium flex items-center gap-1.5">
                  <ShieldAlert className="h-4 w-4 text-destructive" />
                  Alertas em tempo real
                </p>
                <span className="text-xs text-muted-foreground">{alerts.length} recentes</span>
              </div>
              <div className="overflow-y-auto max-h-80">
                {alerts.length === 0 ? (
                  <p className="text-sm text-muted-foreground text-center py-8 px-4">
                    Nenhum alerta recente. Quando USB, malware ou AV crítico forem detectados, aparecem aqui.
                  </p>
                ) : (
                  alerts.map((a) => (
                    <button
                      key={a.id}
                      onClick={() => {
                        markRead(a.id)
                        if (a.computerId) navigate(`/computers/${a.computerId}?tab=security`)
                        setBellOpen(false)
                      }}
                      className={cn(
                        'w-full text-left px-3 py-2.5 border-b border-border/50 hover:bg-accent/50 transition-colors',
                        !a.read && 'bg-destructive/5'
                      )}
                    >
                      <div className="flex items-start justify-between gap-2">
                        <p className="text-sm font-medium truncate">
                          {EVENT_LABELS[a.eventType] || a.eventType}
                          {a.computerName ? ` · ${a.computerName}` : ''}
                        </p>
                        <span className={cn(
                          'text-[10px] font-semibold shrink-0',
                          a.severity.toLowerCase() === 'critical' || a.severity.toLowerCase() === 'high'
                            ? 'text-destructive'
                            : 'text-amber-500'
                        )}>
                          {formatSeverity(a.severity)}
                        </span>
                      </div>
                      <p className="text-xs text-muted-foreground truncate mt-0.5">{a.description}</p>
                      <p className="text-[10px] text-muted-foreground mt-1">{formatRelative(a.timestamp)}</p>
                    </button>
                  ))
                )}
              </div>
            </div>
          )}
        </div>

        <button
          type="button"
          onClick={() => {
            const next = theme === 'dark' ? 'light' : 'dark'
            setTheme(next)
            applyTheme(next)
          }}
          className="p-2 rounded-lg text-muted-foreground/80 hover:bg-accent hover:text-foreground transition-colors"
          title={theme === 'dark' ? t('settings.themeLight', 'Claro') : t('settings.themeDark', 'Escuro')}
          aria-label={theme === 'dark' ? t('settings.themeLight', 'Claro') : t('settings.themeDark', 'Escuro')}
        >
          {theme === 'dark' ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
        </button>

        <div ref={langRef} className="relative">
          <button onClick={() => setLangMenuOpen(!langMenuOpen)} className="flex items-center gap-1 p-2 rounded-lg hover:bg-accent text-muted-foreground transition-colors text-sm">
            <Globe className="h-4 w-4" />
          </button>
          {langMenuOpen && (
            <div className="absolute right-0 top-full mt-1 min-w-[10rem] rounded-lg border bg-popover p-1 shadow-lg animate-in">
              <button onClick={() => changeLanguage('pt-BR')} className="flex items-center gap-2 w-full px-2 py-1.5 text-sm rounded-md hover:bg-accent">
                🇧🇷 Português (Brasil)
              </button>
              <button onClick={() => changeLanguage('en-US')} className="flex items-center gap-2 w-full px-2 py-1.5 text-sm rounded-md hover:bg-accent">
                🇺🇸 English (US)
              </button>
            </div>
          )}
        </div>

        <div ref={menuRef} className="relative">
          <button onClick={() => setUserMenuOpen(!userMenuOpen)} className="flex items-center gap-2 p-1 rounded-lg hover:bg-accent transition-colors">
            <Avatar fallback={user?.username?.charAt(0) || 'U'} size="sm" />
            <span className="hidden md:inline text-sm font-medium">{user?.username || t('header.user')}</span>
          </button>

          {userMenuOpen && (
            <div className="absolute right-0 top-full mt-1 min-w-[14rem] rounded-lg border bg-popover p-1 shadow-lg animate-in">
              <div className="px-2 py-2 border-b border-border">
                <p className="text-sm font-medium">{user?.username}</p>
                <p className="text-xs text-muted-foreground">{user?.email}</p>
              </div>
              <button onClick={() => { navigate('/settings'); setUserMenuOpen(false) }} className="flex items-center gap-2 w-full px-2 py-1.5 text-sm rounded-md hover:bg-accent mt-1">
                <SettingsIcon className="h-4 w-4" /> {t('header.settings')}
              </button>
              <button onClick={() => { logout(); setUserMenuOpen(false) }} className="flex items-center gap-2 w-full px-2 py-1.5 text-sm rounded-md hover:bg-accent text-destructive">
                <LogOut className="h-4 w-4" /> {t('header.logout')}
              </button>
            </div>
          )}
        </div>
      </div>
    </header>
  )
}
