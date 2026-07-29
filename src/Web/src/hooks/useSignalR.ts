import { useEffect, useRef } from 'react'
import * as signalR from '@microsoft/signalr'
import { useAuthStore } from '@/stores/auth'

export function useSignalR(hubUrl: string, callbacks: Record<string, (...args: any[]) => void>) {
  const connectionRef = useRef<signalR.HubConnection | null>(null)
  const token = useAuthStore(state => state.accessToken)

  useEffect(() => {
    if (!token) return

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, { accessTokenFactory: () => token })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .build()

    Object.entries(callbacks).forEach(([event, handler]) => {
      connection.on(event, handler)
    })

    connection.start().catch(err => console.error('SignalR connection error:', err))

    connectionRef.current = connection

    return () => {
      connection.stop()
    }
  }, [hubUrl, token])

  return connectionRef
}
