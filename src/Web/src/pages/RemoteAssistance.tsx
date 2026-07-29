import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { Dialog, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog'
import { Radio, Monitor, Power, PowerOff, Lock, MessageSquare, Terminal, ArrowRight } from 'lucide-react'
import { api } from '@/lib/api'

interface RemoteSession {
  id: string
  computerId: string
  computerName: string
  status: string
  mode: string
  requestedAt: string
}

interface Computer {
  id: string
  hostname: string
}

export function RemoteAssistance() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [showRequest, setShowRequest] = useState(false)
  const [selectedSession, setSelectedSession] = useState<string | null>(null)
  const [selectedComputer, setSelectedComputer] = useState('')

  const { data: sessionList } = useQuery({
    queryKey: ['remote-sessions'],
    queryFn: () => api.get<RemoteSession[]>('/remote/sessions'),
  })

  const { data: computers } = useQuery({
    queryKey: ['computers'],
    queryFn: () => api.get<{ items: Computer[] }>('/computers?pageSize=100'),
  })

  const requestMutation = useMutation({
    mutationFn: (computerId: string) =>
      api.post('/remote/request', { computerId, sessionType: 'view' }),
    onSuccess: () => {
      setShowRequest(false)
      setSelectedComputer('')
      queryClient.invalidateQueries({ queryKey: ['remote-sessions'] })
    },
  })

  const session = sessionList?.find(s => s.id === selectedSession) ?? null
  const computerOptions = (computers?.items ?? []).map((c) => ({
    value: c.id,
    label: c.hostname,
  }))

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">{t('remoteAssistance.remoteAssistance')}</h1>
          <p className="text-muted-foreground text-sm">{t('remoteAssistance.remoteControlAndSupport')}</p>
        </div>
        <Button onClick={() => setShowRequest(true)}><Radio className="h-4 w-4 mr-1" /> {t('remoteAssistance.newSession')}</Button>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <Card className="lg:col-span-1">
          <CardHeader><CardTitle className="text-base">{t('remoteAssistance.activeSessions')}</CardTitle></CardHeader>
          <CardContent className="space-y-2">
            {!sessionList?.length ? (
              <p className="text-sm text-muted-foreground">{t('remoteAssistance.selectSessionToView')}</p>
            ) : sessionList.map((s) => (
              <div
                key={s.id}
                onClick={() => setSelectedSession(s.id)}
                className={`p-3 rounded-lg cursor-pointer transition-colors ${
                  selectedSession === s.id ? 'bg-primary/10 border border-primary/30' : 'bg-muted/30 hover:bg-muted/50'
                }`}
              >
                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-sm font-medium">{s.computerName}</p>
                    <p className="text-xs text-muted-foreground">{new Date(s.requestedAt).toLocaleString()}</p>
                  </div>
                  <Badge variant={s.status === 'Active' ? 'success' : 'outline'}>{s.status}</Badge>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>

        <Card className="lg:col-span-2">
          {session ? (
            <>
              <CardHeader>
                <div className="flex items-center justify-between">
                  <CardTitle className="text-base">{session.computerName}</CardTitle>
                  <Badge variant="success">{t('remoteAssistance.connected')}</Badge>
                </div>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="aspect-video rounded-lg bg-muted/50 flex items-center justify-center border border-border/50">
                  <div className="text-center">
                    <Monitor className="h-16 w-16 text-muted-foreground mx-auto mb-2" />
                    <p className="text-sm text-muted-foreground">{t('remoteAssistance.screenSharePreview')}</p>
                  </div>
                </div>

                <div className="flex flex-wrap gap-2">
                  <Button variant="outline" size="sm"><Power className="h-4 w-4 mr-1" /> {t('remoteAssistance.restart')}</Button>
                  <Button variant="outline" size="sm"><PowerOff className="h-4 w-4 mr-1" /> {t('remoteAssistance.shutdown')}</Button>
                  <Button variant="outline" size="sm"><Lock className="h-4 w-4 mr-1" /> {t('remoteAssistance.lock')}</Button>
                  <Button variant="outline" size="sm"><MessageSquare className="h-4 w-4 mr-1" /> {t('remoteAssistance.message')}</Button>
                  <Button variant="outline" size="sm"><Terminal className="h-4 w-4 mr-1" /> {t('remoteAssistance.powerShell')}</Button>
                </div>

                <div className="border border-border/50 rounded-lg p-3">
                  <div className="flex items-center gap-2 text-sm text-muted-foreground mb-2">
                    <MessageSquare className="h-4 w-4" /> {t('remoteAssistance.chat')}
                  </div>
                  <div className="flex gap-2 mt-3">
                    <Input placeholder={t('remoteAssistance.typeMessage')} className="flex-1" />
                    <Button size="sm"><ArrowRight className="h-4 w-4" /></Button>
                  </div>
                </div>
              </CardContent>
            </>
          ) : (
            <CardContent className="py-12 text-center text-muted-foreground">
              <Radio className="h-12 w-12 mx-auto mb-3 opacity-50" />
              <p>{t('remoteAssistance.selectSessionToView')}</p>
            </CardContent>
          )}
        </Card>
      </div>

      <Dialog open={showRequest} onClose={() => setShowRequest(false)}>
        <div className="p-6">
          <DialogHeader>
            <DialogTitle>{t('remoteAssistance.requestRemoteSession')}</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 mt-4">
            <Select
              label={t('remoteAssistance.computer')}
              value={selectedComputer}
              onChange={(e) => setSelectedComputer(e.target.value)}
              options={computerOptions}
              placeholder={t('remoteAssistance.computer')}
            />
            <Select label={t('remoteAssistance.mode')} options={[
              { value: 'view', label: t('remoteAssistance.viewOnly') },
              { value: 'control', label: t('remoteAssistance.fullControl') },
            ]} />
            <Input label={t('remoteAssistance.justification')} placeholder={t('remoteAssistance.reasonForRemoteAccess')} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowRequest(false)}>{t('remoteAssistance.cancel')}</Button>
            <Button onClick={() => requestMutation.mutate(selectedComputer)} disabled={!selectedComputer}>
              {t('remoteAssistance.request')}
            </Button>
          </DialogFooter>
        </div>
      </Dialog>
    </div>
  )
}
