import { useEffect } from 'react'
import { useNavigate, useSearchParams, Navigate } from 'react-router-dom'
import { useAuthStore, normalizeAuthUser } from '@/stores/auth'
import { Loader2, AlertTriangle } from 'lucide-react'
import { toast } from 'sonner'

function applySession(accessToken: string, refreshToken: string, user: unknown) {
  const normalized = normalizeAuthUser(user)
  localStorage.setItem('accessToken', accessToken)
  localStorage.setItem('refreshToken', refreshToken)
  localStorage.setItem('user', JSON.stringify(normalized))
  useAuthStore.setState({
    user: normalized,
    accessToken,
    refreshToken,
    isAuthenticated: true,
    isLoading: false,
  })
}

export function SsoCallback() {
  const [searchParams] = useSearchParams()
  const navigate = useNavigate()
  const { isAuthenticated } = useAuthStore()

  useEffect(() => {
    const error = searchParams.get('error')
    const accessToken = searchParams.get('accessToken')
    const refreshToken = searchParams.get('refreshToken')
    const userParam = searchParams.get('user')

    let payload: Record<string, unknown>
    if (error) {
      payload = { ok: false, error }
    } else if (!accessToken || !refreshToken || !userParam) {
      payload = { ok: false, error: 'Login externo incompleto. Tente novamente.' }
    } else {
      try {
        const user = normalizeAuthUser(JSON.parse(decodeURIComponent(userParam)))
        payload = { ok: true, accessToken, refreshToken, user }
      } catch {
        payload = { ok: false, error: 'Resposta de login inválida. Tente novamente.' }
      }
    }

    // Rota em janela do popup OAuth: devolve para a página de login e fecha a janela.
    if (window.opener && !window.opener.closed) {
      window.opener.postMessage({ type: 'SSO_CALLBACK', ...payload }, window.location.origin)
      window.close()
      return
    }

    // Navegação direta (fallback sem popup).
    if (payload.ok) {
      applySession(payload.accessToken as string, payload.refreshToken as string, payload.user)
      toast.success('Login realizado com sucesso')
      navigate('/', { replace: true })
    } else {
      toast.error((payload.error as string) || 'Falha na autenticação')
      navigate('/login', { replace: true })
    }
  }, [searchParams, navigate])

  if (isAuthenticated && !window.opener) return <Navigate to="/" replace />

  return (
    <div className="min-h-screen flex items-center justify-center bg-background">
      <div className="flex flex-col items-center gap-3 text-muted-foreground">
        {searchParams.get('error') ? (
          <AlertTriangle className="h-8 w-8 text-destructive" />
        ) : (
          <Loader2 className="h-8 w-8 animate-spin text-primary" />
        )}
        <p className="text-sm">{searchParams.get('error') || 'Finalizando login...'}</p>
      </div>
    </div>
  )
}