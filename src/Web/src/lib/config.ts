const origin = String(import.meta.env.VITE_API_URL || '').replace(/\/$/, '')

/** Absolute API origin in Vercel; empty in Docker/local (same-origin via nginx/proxy). */
export function apiUrl(path: string) {
  const normalized = path.startsWith('/') ? path : `/${path}`
  return `${origin}${normalized}`
}

export function hubUrl(path: string) {
  return apiUrl(path)
}
