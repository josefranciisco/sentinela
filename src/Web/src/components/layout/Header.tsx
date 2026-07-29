import { useState, useEffect, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import { cn } from '@/lib/utils'
import { useTranslation } from 'react-i18next'
import { useAuthStore } from '@/stores/auth'
import { Avatar } from '@/components/ui/avatar'
import {
  Search, Bell, Menu, Command, Globe,
  User, Settings as SettingsIcon, LogOut,
} from 'lucide-react'
import i18n from '@/lib/i18n'

interface HeaderProps {
  onMenuClick: () => void
}

export function Header({ onMenuClick }: HeaderProps) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { user, logout } = useAuthStore()
  const [searchOpen, setSearchOpen] = useState(false)
  const [searchQuery, setSearchQuery] = useState('')
  const [userMenuOpen, setUserMenuOpen] = useState(false)
  const [langMenuOpen, setLangMenuOpen] = useState(false)
  const searchRef = useRef<HTMLInputElement>(null)
  const menuRef = useRef<HTMLDivElement>(null)
  const langRef = useRef<HTMLDivElement>(null)

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
    setLangMenuOpen(false)
  }

  return (
    <header className="flex items-center justify-between h-16 px-4 md:px-6 border-b border-border/50 bg-card/30 backdrop-blur-xl">
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
        <button className="relative p-2 rounded-lg hover:bg-accent text-muted-foreground transition-colors">
          <Bell className="h-5 w-5" />
          <span className="absolute top-1.5 right-1.5 h-2 w-2 rounded-full bg-destructive" />
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
