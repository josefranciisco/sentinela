import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { Table, TableHeader, TableBody, TableHead, TableRow, TableCell } from '@/components/ui/table'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { Badge } from '@/components/ui/badge'
import { FileSearch, Search, Clock, User, Monitor } from 'lucide-react'
import { useTranslation } from 'react-i18next'

const auditLogs: { action: string; user: string; target: string; details: string; timestamp: string; severity: string }[] = []

export function Audit() {
  const { t } = useTranslation()
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">{t('audit.auditLog')}</h1>
        <p className="text-muted-foreground text-sm">{t('audit.comprehensiveAuditTrail')}</p>
      </div>

      <Card>
        <CardHeader>
          <div className="flex flex-col md:flex-row gap-3">
            <div className="relative">
              <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
              <input
                placeholder={t('audit.searchAuditLog')}
                className="h-9 w-64 rounded-lg border border-input bg-background pl-8 pr-3 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              />
            </div>
            <Select options={[
              { value: '', label: t('audit.allActions') },
              { value: 'login', label: 'Login' },
              { value: 'alert', label: 'Alert' },
              { value: 'policy', label: 'Policy' },
              { value: 'user', label: 'User' },
            ]} />
            <Select options={[
              { value: '', label: t('audit.allSeverities') },
              { value: 'high', label: 'High' },
              { value: 'medium', label: 'Medium' },
              { value: 'low', label: 'Low' },
              { value: 'info', label: 'Info' },
            ]} />
          </div>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('audit.action')}</TableHead>
                <TableHead>{t('audit.user')}</TableHead>
                <TableHead>{t('audit.target')}</TableHead>
                <TableHead>{t('audit.details')}</TableHead>
                <TableHead>{t('audit.severity')}</TableHead>
                <TableHead>{t('audit.timestamp')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {auditLogs.map((entry, i) => (
                <TableRow key={i}>
                  <TableCell className="font-medium">{entry.action}</TableCell>
                  <TableCell>
                    <div className="flex items-center gap-1.5">
                      <User className="h-3.5 w-3.5 text-muted-foreground" />
                      {entry.user}
                    </div>
                  </TableCell>
                  <TableCell className="text-muted-foreground">{entry.target}</TableCell>
                  <TableCell className="text-xs text-muted-foreground max-w-[250px] truncate">{entry.details}</TableCell>
                  <TableCell>
                    <Badge variant={entry.severity === 'High' ? 'destructive' : entry.severity === 'Medium' ? 'warning' : 'info'}>
                      {t('alerts.' + entry.severity.toLowerCase(), { defaultValue: entry.severity })}
                    </Badge>
                  </TableCell>
                  <TableCell className="text-xs text-muted-foreground whitespace-nowrap">{entry.timestamp}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  )
}
