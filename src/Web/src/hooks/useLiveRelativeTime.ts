import { useState, useEffect } from 'react'
import { formatDistanceToNow } from 'date-fns'
import { ptBR } from 'date-fns/locale'

export function useLiveRelativeTime(date: string | Date | null | undefined): string {
  const [, setTick] = useState(0)

  useEffect(() => {
    if (!date) return
    const id = setInterval(() => setTick(t => t + 1), 1000)
    return () => clearInterval(id)
  }, [date])

  if (!date) return '—'
  return formatDistanceToNow(new Date(date), { addSuffix: true, locale: ptBR })
}
