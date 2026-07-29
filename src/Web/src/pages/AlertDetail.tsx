import { useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Card, CardHeader, CardTitle, CardContent, CardFooter } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Select } from '@/components/ui/select'
import { Input } from '@/components/ui/input'
import { useAlert, useUpdateAlertStatus } from '@/hooks/useAlerts'
import { formatDate, formatRelative } from '@/lib/utils'
import { ArrowLeft, AlertTriangle, Brain, Send, Clock, Monitor, User } from 'lucide-react'
import { toast } from 'sonner'

const severityColors: Record<string, 'destructive' | 'warning' | 'info' | 'secondary'> = {
  Critical: 'destructive', High: 'warning', Medium: 'warning', Low: 'info', Info: 'secondary',
}

const statusOptions = [
  { value: 'Open', label: 'Open' },
  { value: 'Acknowledged', label: 'Acknowledged' },
  { value: 'InProgress', label: 'In Progress' },
  { value: 'Resolved', label: 'Resolved' },
  { value: 'FalsePositive', label: 'False Positive' },
]

export function AlertDetail() {
  const { t } = useTranslation()
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { data: alert, isLoading } = useAlert(id!)
  const updateStatus = useUpdateAlertStatus()
  const [comment, setComment] = useState('')
  const [comments, setComments] = useState<{ text: string; timestamp: Date }[]>([])

  if (isLoading) return <div className="flex items-center justify-center h-64 text-muted-foreground">{t('alertDetail.loading')}</div>
  if (!alert) return <div className="flex items-center justify-center h-64 text-muted-foreground">{t('alertDetail.alertNotFound')}</div>

  const handleStatusChange = async (newStatus: string) => {
    try {
      await updateStatus.mutateAsync({ id: alert.id, status: newStatus })
      toast.success(t('alertDetail.statusUpdated'))
    } catch (err: any) {
      toast.error(err.message)
    }
  }

  const handleAddComment = () => {
    if (!comment.trim()) return
    setComments([...comments, { text: comment.trim(), timestamp: new Date() }])
    setComment('')
  }

  return (
    <div className="space-y-6 max-w-4xl">
      <Button variant="ghost" size="sm" onClick={() => navigate('/alerts')}>
        <ArrowLeft className="h-4 w-4 mr-1" /> {t('alertDetail.backToAlerts')}
      </Button>

      <div className="flex items-start justify-between">
        <div className="flex items-center gap-4">
          <div className={`p-3 rounded-xl ${alert.severity === 'Critical' ? 'bg-red-500/20 text-red-400' : alert.severity === 'High' ? 'bg-orange-500/20 text-orange-400' : 'bg-primary/20 text-primary'}`}>
            <AlertTriangle className="h-6 w-6" />
          </div>
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-2xl font-bold">{alert.title}</h1>
              <Badge variant={severityColors[alert.severity]}>{t('alerts.' + alert.severity.toLowerCase(), { defaultValue: alert.severity })}</Badge>
            </div>
            <p className="text-sm text-muted-foreground">{alert.category} &middot; {formatDate(alert.timestamp)}</p>
          </div>
        </div>
        <Select
          options={statusOptions}
          value={alert.status}
          onChange={(e) => handleStatusChange(e.target.value)}
          className="w-40"
        />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <Card className="lg:col-span-2">
          <CardHeader><CardTitle className="text-base">{t('alertDetail.description')}</CardTitle></CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground">{alert.description}</p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle className="text-base">{t('alertDetail.details')}</CardTitle></CardHeader>
          <CardContent className="space-y-3 text-sm">
            <div className="flex items-center gap-2 text-muted-foreground">
              <Monitor className="h-4 w-4" /> {alert.computerName || t('alertDetail.unknown')}
            </div>
            <div className="flex items-center gap-2 text-muted-foreground">
              <User className="h-4 w-4" /> {alert.username || t('alertDetail.system')}
            </div>
            <div className="flex items-center gap-2 text-muted-foreground">
              <Clock className="h-4 w-4" /> {formatRelative(alert.timestamp)}
            </div>
            <div>
              <span className="text-muted-foreground">{t('alertDetail.correlationScore')}</span>
              <span className="ml-2 font-medium">{alert.correlationScore?.toFixed(2) || 'N/A'}</span>
            </div>
            <div>
              <span className="text-muted-foreground">{t('alertDetail.status')}</span>
              <Badge className="ml-2">{alert.status}</Badge>
            </div>
            <div>
              <span className="text-muted-foreground">{t('alertDetail.assigned')}</span>
              <span className="ml-2">{alert.assignedTo || t('alertDetail.unassigned')}</span>
            </div>
            <div>
              <span className="text-muted-foreground">{t('alertDetail.source')}</span>
              <span className="ml-2">{alert.source || 'N/A'}</span>
            </div>
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle className="text-base">{t('alertDetail.aiAnalysis')}</CardTitle>
            <Button variant="outline" size="sm"><Brain className="h-4 w-4 mr-1" /> {t('alertDetail.explain')}</Button>
          </div>
        </CardHeader>
        <CardContent>
          <div className="flex items-center justify-center h-20 border border-dashed border-border rounded-lg">
            <p className="text-sm text-muted-foreground">{t('alertDetail.clickExplain')}</p>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle className="text-base">{t('alertDetail.comments')}</CardTitle></CardHeader>
        <CardContent className="space-y-3">
          {comments.length === 0 && <p className="text-sm text-muted-foreground text-center py-4">{t('alertDetail.noCommentsYet')}</p>}
          {comments.map((c, i) => (
            <div key={i} className="flex gap-2 text-sm p-3 rounded-lg bg-muted/50">
              <div className="w-6 h-6 rounded-full bg-primary/20 flex items-center justify-center text-xs font-medium shrink-0">U</div>
              <div>
                <p>{c.text}</p>
                <p className="text-xs text-muted-foreground mt-0.5">{formatRelative(c.timestamp.toISOString())}</p>
              </div>
            </div>
          ))}
        </CardContent>
        <CardFooter>
          <div className="flex w-full gap-2">
            <Input placeholder={t('alertDetail.addComment')} value={comment} onChange={(e) => setComment(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && handleAddComment()} />
            <Button size="sm" onClick={handleAddComment}><Send className="h-4 w-4" /></Button>
          </div>
        </CardFooter>
      </Card>
    </div>
  )
}
