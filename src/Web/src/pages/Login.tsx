import { useState, useEffect } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { motion } from 'framer-motion'
import { useTranslation } from 'react-i18next'
import { useAuthStore } from '@/stores/auth'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { toast } from 'sonner'
import { Eye, EyeOff, Shield, ArrowRight } from 'lucide-react'

export function Login() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const { login, isAuthenticated } = useAuthStore()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [twoFactorCode, setTwoFactorCode] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [requires2FA, setRequires2FA] = useState(false)
  const [loading, setLoading] = useState(false)
  const [remember, setRemember] = useState(false)

  if (isAuthenticated) {
    navigate('/', { replace: true })
    return null
  }

  useEffect(() => {
    const error = searchParams.get('error')
    if (error) {
      toast.error(error)
    }
  }, [searchParams])

  useEffect(() => {
    const onMessage = (event: MessageEvent) => {
      if (event.origin !== window.location.origin) return
      const data = event.data
      if (!data || data.type !== 'SSO_CALLBACK') return

      if (data.ok) {
        const user = data.user as Record<string, unknown>
        localStorage.setItem('accessToken', data.accessToken)
        localStorage.setItem('refreshToken', data.refreshToken)
        localStorage.setItem('user', JSON.stringify(user))
        useAuthStore.setState({
          user: user as never,
          accessToken: data.accessToken,
          refreshToken: data.refreshToken,
          isAuthenticated: true,
          isLoading: false,
        })
        toast.success(t('login.loginSuccess'))
        navigate('/', { replace: true })
      } else {
        toast.error((data.error as string) || t('login.loginFailed'))
      }
    }

    window.addEventListener('message', onMessage)
    return () => window.removeEventListener('message', onMessage)
  }, [navigate, t])

  const handleSsoLogin = (provider: string) => {
    const width = 550
    const height = 700
    const left = window.screenX + (window.outerWidth - width) / 2
    const top = window.screenY + (window.outerHeight - height) / 2
    const popup = window.open(
      `/api/v1/auth/sso/login/${provider}`,
      'sentinelSso',
      `width=${width},height=${height},left=${left},top=${top},popup=1`
    )
    if (!popup) {
      toast.error(t('login.popupBlocked'))
    }
  }

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!username || !password) {
      toast.error(t('login.fillAllFields'))
      return
    }
    setLoading(true)
    try {
      await login(username, password, requires2FA ? twoFactorCode : undefined)
      toast.success(t('login.loginSuccess'))
      navigate('/')
    } catch (err: any) {
      if (err.message?.includes('2FA') || err.message?.includes('TwoFactor')) {
        setRequires2FA(true)
      } else {
        toast.error(err.message || t('login.loginFailed'))
      }
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="min-h-screen flex items-center justify-center relative overflow-hidden bg-background">
      <div className="absolute inset-0 bg-gradient-to-br from-primary/10 via-transparent to-blue-500/10" />
      <div className="absolute top-20 left-20 w-72 h-72 bg-primary/20 rounded-full blur-3xl animate-pulse-slow" />
      <div className="absolute bottom-20 right-20 w-96 h-96 bg-blue-500/20 rounded-full blur-3xl animate-pulse-slow" />

      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.6 }}
        className="relative w-full max-w-md px-4"
      >
        <div className="glass rounded-2xl p-8 shadow-2xl">
          <div className="text-center mb-8">
            <motion.div
              initial={{ scale: 0 }}
              animate={{ scale: 1 }}
              transition={{ delay: 0.2, type: 'spring', stiffness: 200 }}
              className="inline-flex items-center justify-center w-16 h-16 rounded-2xl bg-gradient-to-br from-primary to-blue-500 mb-4"
            >
              <Shield className="h-8 w-8 text-white" />
            </motion.div>
            <h1 className="text-2xl font-bold bg-clip-text text-transparent bg-gradient-to-r from-primary to-blue-500">
              {t('login.title')}
            </h1>
            <p className="text-sm text-muted-foreground mt-1">
              {t('login.subtitle')}
            </p>
          </div>

          <form onSubmit={handleLogin} className="space-y-4">
            <Input
              id="username"
              label={t('login.usernameOrEmail')}
              placeholder={t('login.usernamePlaceholder')}
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              autoComplete="username"
            />

            <div className="relative">
              <Input
                id="password"
                label={t('login.password')}
                type={showPassword ? 'text' : 'password'}
                placeholder={t('login.passwordPlaceholder')}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                autoComplete="current-password"
              />
              <button
                type="button"
                onClick={() => setShowPassword(!showPassword)}
                className="absolute right-3 top-9 text-muted-foreground hover:text-foreground"
              >
                {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
              </button>
            </div>

            {requires2FA && (
              <motion.div initial={{ opacity: 0, height: 0 }} animate={{ opacity: 1, height: 'auto' }}>
                <Input
                  id="2fa"
                  label={t('login.twoFactorCode')}
                  placeholder={t('login.twoFactorPlaceholder')}
                  value={twoFactorCode}
                  onChange={(e) => setTwoFactorCode(e.target.value)}
                  maxLength={6}
                />
              </motion.div>
            )}

            <div className="flex items-center justify-between">
              <label className="flex items-center gap-2 cursor-pointer">
                <input
                  type="checkbox"
                  checked={remember}
                  onChange={(e) => setRemember(e.target.checked)}
                  className="rounded border-input bg-background"
                />
                <span className="text-sm text-muted-foreground">{t('login.rememberMe')}</span>
              </label>
              <button type="button" className="text-sm text-primary hover:underline">
                {t('login.forgotPassword')}
              </button>
            </div>

            <Button type="submit" loading={loading} className="w-full">
              {requires2FA ? t('login.verifyCode') : t('login.signIn')}
              <ArrowRight className="h-4 w-4" />
            </Button>
          </form>

          <div className="grid grid-cols-1 gap-3 mt-6">
            <Button type="button" variant="outline" className="w-full" onClick={() => handleSsoLogin('google')}>
              <svg className="h-4 w-4" viewBox="0 0 48 48" fill="none">
                <path fill="#EA4335" d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5z" />
                <path fill="#4285F4" d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6c4.51-4.18 7.09-10.36 7.09-17.65z" />
                <path fill="#FBBC05" d="M10.53 28.59c-.48-1.45-.76-2.99-.76-4.59s.27-3.14.76-4.59l-7.98-6.19C.92 16.46 0 20.12 0 24c0 3.88.92 7.54 2.56 10.78l7.97-6.19z" />
                <path fill="#34A853" d="M24 48c6.48 0 11.93-2.13 15.89-5.81l-7.73-6c-2.15 1.45-4.92 2.3-8.16 2.3-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48z" />
              </svg>
              {t('login.google')}
            </Button>
            <Button type="button" variant="outline" className="w-full" onClick={() => handleSsoLogin('microsoft')}>
              <svg className="h-4 w-4" viewBox="0 0 23 23" fill="none">
                <path fill="#F25022" d="M1 1h10v10H1z" />
                <path fill="#7FBA00" d="M12 1h10v10H12z" />
                <path fill="#00A4EF" d="M1 12h10v10H1z" />
                <path fill="#FFB900" d="M12 12h10v10H12z" />
              </svg>
              {t('login.microsoft')}
            </Button>
          </div>
        </div>

        <p className="text-center text-xs text-muted-foreground mt-6">
          {t('login.footer')}
        </p>
      </motion.div>
    </div>
  )
}
