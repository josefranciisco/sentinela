import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { Badge } from '@/components/ui/badge'
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs'
import { Dialog, DialogHeader, DialogTitle, DialogContent, DialogFooter } from '@/components/ui/dialog'
import { Plus, ToggleLeft, ToggleRight, Play, History, Workflow } from 'lucide-react'

const triggerOptions = (t: (key: string) => string) => [
  { value: 'usb', label: t('automation.usbDevice') },
  { value: 'login', label: t('automation.userLogin') },
  { value: 'software', label: t('automation.softwareInstall') },
  { value: 'security', label: t('automation.securityEvent') },
  { value: 'schedule', label: t('automation.schedule') },
  { value: 'custom', label: t('automation.customEvent') },
]

const actionOptions = (t: (key: string) => string) => [
  { value: 'alert', label: t('automation.sendAlert') },
  { value: 'script', label: t('automation.executeScript') },
  { value: 'ticket', label: t('automation.openTicket') },
  { value: 'email', label: t('automation.sendEmail') },
  { value: 'teams', label: t('automation.teamsNotification') },
  { value: 'slack', label: t('automation.slackMessage') },
  { value: 'block_usb', label: t('automation.blockUsb') },
  { value: 'powershell', label: t('automation.powerShell') },
  { value: 'webhook', label: t('automation.webhook') },
]

export function Automation() {
  const { t } = useTranslation()
  const [showEditor, setShowEditor] = useState(false)
  const [workflows, setWorkflows] = useState([
    { id: '1', name: 'Block Unknown USB', trigger: 'usb', action: 'block_usb', enabled: true },
    { id: '2', name: 'Alert on Admin Login', trigger: 'login', action: 'alert', enabled: true },
    { id: '3', name: 'Scan on Software Install', trigger: 'software', action: 'script', enabled: false },
  ])
  const [editName, setEditName] = useState('')
  const [editTrigger, setEditTrigger] = useState('usb')
  const [editAction, setEditAction] = useState('alert')

  const toggleWorkflow = (id: string) => {
    setWorkflows(workflows.map(w => w.id === id ? { ...w, enabled: !w.enabled } : w))
  }

  const createWorkflow = () => {
    if (!editName.trim()) return
    setWorkflows([...workflows, { id: Date.now().toString(), name: editName, trigger: editTrigger, action: editAction, enabled: true }])
    setShowEditor(false)
    setEditName('')
  }

  const executionHistory: { id: string; workflow: string; status: string; time: string; computer: string }[] = []

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">{t('automation.automation')}</h1>
          <p className="text-muted-foreground text-sm">{t('automation.workflowAutomationAndOrchestration')}</p>
        </div>
        <Button onClick={() => setShowEditor(true)}><Plus className="h-4 w-4 mr-1" /> {t('automation.newWorkflow')}</Button>
      </div>

      <Tabs defaultValue="workflows">
        <TabsList>
          <TabsTrigger value="workflows"><Workflow className="h-4 w-4 mr-1" /> {t('automation.workflows')}</TabsTrigger>
          <TabsTrigger value="history"><History className="h-4 w-4 mr-1" /> {t('automation.executionHistory')}</TabsTrigger>
        </TabsList>

        <TabsContent value="workflows">
          <div className="space-y-3">
            {workflows.map((wf) => (
              <Card key={wf.id} className="card-hover">
                <CardContent className="p-4 flex items-center justify-between">
                  <div className="flex items-center gap-4">
                    <div className={`p-2 rounded-lg ${wf.enabled ? 'bg-emerald-500/20 text-emerald-400' : 'bg-muted text-muted-foreground'}`}>
                      <Workflow className="h-5 w-5" />
                    </div>
                    <div>
                      <p className="font-medium">{wf.name}</p>
                      <div className="flex items-center gap-2 mt-0.5">
                        <Badge variant="outline" className="text-xs">{triggerOptions(t).find(opt => opt.value === wf.trigger)?.label}</Badge>
                        <span className="text-xs text-muted-foreground">&rarr;</span>
                        <Badge variant="outline" className="text-xs">{actionOptions(t).find(opt => opt.value === wf.action)?.label}</Badge>
                      </div>
                    </div>
                  </div>
                  <div className="flex items-center gap-2">
                    <Button variant="ghost" size="sm"><Play className="h-4 w-4" /></Button>
                    <Button variant="ghost" size="sm" onClick={() => toggleWorkflow(wf.id)}>
                      {wf.enabled ? <ToggleRight className="h-5 w-5 text-emerald-400" /> : <ToggleLeft className="h-5 w-5 text-muted-foreground" />}
                    </Button>
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>
        </TabsContent>

        <TabsContent value="history">
          <Card>
            <CardHeader><CardTitle className="text-base">{t('automation.executionHistory')}</CardTitle></CardHeader>
            <CardContent>
              <div className="space-y-2">
                {executionHistory.map((ex) => (
                  <div key={ex.id} className="flex items-center gap-3 text-sm p-3 rounded-lg bg-muted/30">
                    <Badge variant={ex.status === 'success' ? 'success' : 'destructive'}>{ex.status}</Badge>
                    <div className="flex-1 min-w-0">
                      <p className="font-medium">{ex.workflow}</p>
                      <p className="text-xs text-muted-foreground">{ex.time} &middot; {ex.computer}</p>
                    </div>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      <Dialog open={showEditor} onClose={() => setShowEditor(false)}>
        <div className="p-6">
          <DialogHeader>
            <DialogTitle>{t('automation.createWorkflow')}</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 mt-4">
            <Input label={t('automation.name')} placeholder={t('automation.workflowName')} value={editName} onChange={(e) => setEditName(e.target.value)} />
            <Select label={t('automation.trigger')} options={triggerOptions(t)} value={editTrigger} onChange={(e) => setEditTrigger(e.target.value)} />
            <Select label={t('automation.action')} options={actionOptions(t)} value={editAction} onChange={(e) => setEditAction(e.target.value)} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowEditor(false)}>{t('automation.cancel')}</Button>
            <Button onClick={createWorkflow}>{t('automation.create')}</Button>
          </DialogFooter>
        </div>
      </Dialog>
    </div>
  )
}
