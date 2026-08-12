import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { Select } from '@/components/ui/select'
import { Button } from '@/components/ui/button'
import { Globe, Moon, Sun, Users, Shield } from 'lucide-react'
import { toast } from 'sonner'
import i18n from '@/lib/i18n'

const THEME_KEY = 'sentinela-theme'
const LANG_KEY = 'sentinela-lang'

function applyTheme(theme: 'light' | 'dark') {
  document.documentElement.classList.toggle('dark', theme === 'dark')
  localStorage.setItem(THEME_KEY, theme)
}

export function Settings() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [language, setLanguage] = useState(i18n.language || 'pt-BR')
  const [theme, setTheme] = useState<'light' | 'dark'>(() => {
    const saved = localStorage.getItem(THEME_KEY) as 'light' | 'dark' | null
    if (saved === 'light' || saved === 'dark') return saved
    return document.documentElement.classList.contains('dark') ? 'dark' : 'light'
  })

  useEffect(() => {
    applyTheme(theme)
  }, [theme])

  const handleLanguageChange = (value: string) => {
    setLanguage(value)
    i18n.changeLanguage(value)
    localStorage.setItem(LANG_KEY, value)
    toast.success(t('settings.languageUpdated', 'Idioma atualizado'))
  }

  return (
    <div className="space-y-6 max-w-xl">
      <div>
        <h1 className="text-2xl font-bold">{t('settings.title')}</h1>
        <p className="text-muted-foreground text-sm">
          {t('settings.subtitle', 'Preferências da sua sessão')}
        </p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base flex items-center gap-2">
            <Globe className="h-4 w-4" />
            {t('settings.language')}
          </CardTitle>
        </CardHeader>
        <CardContent>
          <Select
            value={language.startsWith('en') ? 'en-US' : 'pt-BR'}
            onChange={(e) => handleLanguageChange(e.target.value)}
            options={[
              { value: 'pt-BR', label: 'Português (Brasil)' },
              { value: 'en-US', label: 'English (US)' },
            ]}
          />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base flex items-center gap-2">
            {theme === 'dark' ? <Moon className="h-4 w-4" /> : <Sun className="h-4 w-4" />}
            {t('settings.appearance', 'Aparência')}
          </CardTitle>
        </CardHeader>
        <CardContent className="flex gap-2">
          <Button
            variant={theme === 'light' ? 'default' : 'outline'}
            size="sm"
            onClick={() => setTheme('light')}
          >
            <Sun className="h-4 w-4 mr-1" />
            {t('settings.themeLight', 'Claro')}
          </Button>
          <Button
            variant={theme === 'dark' ? 'default' : 'outline'}
            size="sm"
            onClick={() => setTheme('dark')}
          >
            <Moon className="h-4 w-4 mr-1" />
            {t('settings.themeDark', 'Escuro')}
          </Button>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">
            {t('settings.adminShortcuts', 'Administração')}
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          <p className="text-sm text-muted-foreground mb-3">
            {t(
              'settings.adminHint',
              'Usuários e permissões ficam nas telas próprias — não aqui.'
            )}
          </p>
          <div className="flex flex-wrap gap-2">
            <Button variant="outline" size="sm" onClick={() => navigate('/users')}>
              <Users className="h-4 w-4 mr-1" />
              {t('nav.users')}
            </Button>
            <Button variant="outline" size="sm" onClick={() => navigate('/roles')}>
              <Shield className="h-4 w-4 mr-1" />
              {t('nav.roles')}
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>
  )
}
