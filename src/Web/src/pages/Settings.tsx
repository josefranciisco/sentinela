import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { Select } from '@/components/ui/select'
import { Table, TableHeader, TableBody, TableHead, TableRow, TableCell } from '@/components/ui/table'
import { Badge } from '@/components/ui/badge'
import { Save, Key, Bell, Shield, Database, Wrench } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import i18n from '@/lib/i18n'

export function Settings() {
  const { t } = useTranslation()
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">{t('settings.title')}</h1>
        <p className="text-muted-foreground text-sm">{t('settings.subtitle')}</p>
      </div>

      <Tabs defaultValue="general">
        <TabsList>
          <TabsTrigger value="general"><Wrench className="h-4 w-4 mr-1" /> {t('settings.general')}</TabsTrigger>
          <TabsTrigger value="notifications"><Bell className="h-4 w-4 mr-1" /> {t('settings.notifications')}</TabsTrigger>
          <TabsTrigger value="alerts"><Bell className="h-4 w-4 mr-1" /> {t('settings.alertRules')}</TabsTrigger>
          <TabsTrigger value="capture"><Database className="h-4 w-4 mr-1" /> {t('settings.capturePolicies')}</TabsTrigger>
          <TabsTrigger value="api"><Key className="h-4 w-4 mr-1" /> {t('settings.apiKeys')}</TabsTrigger>
          <TabsTrigger value="audit"><Shield className="h-4 w-4 mr-1" /> {t('settings.auditLog')}</TabsTrigger>
        </TabsList>

        <TabsContent value="general">
          <Card>
            <CardHeader><CardTitle className="text-base">{t('settings.generalSettings')}</CardTitle></CardHeader>
            <CardContent className="space-y-4 max-w-lg">
              <Input label={t('settings.companyName')} defaultValue="My Company" id="company" />
              <Select label={t('settings.timezone')} options={[
                { value: 'America/Sao_Paulo', label: 'America/Sao_Paulo (UTC-3)' },
                { value: 'America/New_York', label: 'America/New_York (UTC-5)' },
                { value: 'UTC', label: 'UTC' },
              ]} />
              <Select label={t('settings.language')} options={[
                { value: 'pt-BR', label: 'Português (Brasil)' },
                { value: 'en-US', label: 'English (US)' },
              ]}
                onChange={(e) => i18n.changeLanguage(e.target.value)} />
              <Button><Save className="h-4 w-4 mr-1" /> {t('settings.saveChanges')}</Button>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="notifications">
          <Card>
            <CardHeader><CardTitle className="text-base">{t('settings.notificationChannels')}</CardTitle></CardHeader>
            <CardContent className="space-y-4 max-w-lg">
              <Input label={t('settings.emailSmtp')} placeholder="smtp.company.com" id="smtp" />
              <Input label={t('settings.teamsWebhook')} placeholder="https://..." id="teams" />
              <Input label={t('settings.slackWebhook')} placeholder="https://..." id="slack" />
              <Button><Save className="h-4 w-4 mr-1" /> {t('settings.save')}</Button>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="alerts">
          <Card>
            <CardHeader><CardTitle className="text-base">{t('settings.alertRulesTitle')}</CardTitle></CardHeader>
            <CardContent>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t('settings.rule')}</TableHead>
                    <TableHead>{t('settings.severity')}</TableHead>
                    <TableHead>{t('settings.enabled')}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {[
                    { name: t('settings.malwareDetection'), severity: 'Critical' },
                    { name: t('settings.multipleFailedLogins'), severity: 'High' },
                    { name: t('settings.usbDeviceConnected'), severity: 'Medium' },
                    { name: t('settings.softwareInstallation'), severity: 'Low' },
                  ].map((rule) => (
                    <TableRow key={rule.name}>
                      <TableCell className="font-medium">{rule.name}</TableCell>
                      <TableCell><Badge>{t('alerts.' + rule.severity.toLowerCase(), { defaultValue: rule.severity })}</Badge></TableCell>
                      <TableCell><input type="checkbox" defaultChecked className="rounded" /></TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="capture">
          <Card>
            <CardHeader><CardTitle className="text-base">{t('settings.screenCapturePolicies')}</CardTitle></CardHeader>
            <CardContent className="space-y-4 max-w-lg">
              <Select label={t('settings.captureInterval')} options={[
                { value: '30', label: t('settings.every30s') },
                { value: '60', label: t('settings.every1m') },
                { value: '300', label: t('settings.every5m') },
                { value: '600', label: t('settings.every10m') },
              ]} />
              <Select label={t('settings.captureQuality')} options={[
                { value: 'high', label: t('settings.high') },
                { value: 'medium', label: t('settings.medium') },
                { value: 'low', label: t('settings.low') },
              ]} />
              <label className="flex items-center gap-2 text-sm">
                <input type="checkbox" defaultChecked className="rounded" /> {t('settings.captureOnLock')}
              </label>
              <label className="flex items-center gap-2 text-sm">
                <input type="checkbox" defaultChecked className="rounded" /> {t('settings.captureOnSwitch')}
              </label>
              <Button><Save className="h-4 w-4 mr-1" /> {t('settings.savePolicies')}</Button>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="api">
          <Card>
            <CardHeader><CardTitle className="text-base">{t('settings.apiKeysTitle')}</CardTitle></CardHeader>
            <CardContent>
              <div className="space-y-3">
                {[{ name: t('settings.productionKey'), key: 'sk-...a3f8', lastUsed: '2 hours ago' }, { name: t('settings.devKey'), key: 'sk-...b2c1', lastUsed: '3 days ago' }].map((apiKey) => (
                  <div key={apiKey.name} className="flex items-center justify-between p-3 rounded-lg bg-muted/50">
                    <div>
                      <p className="text-sm font-medium">{apiKey.name}</p>
                      <p className="text-xs font-mono text-muted-foreground">{apiKey.key}</p>
                    </div>
                    <div className="flex items-center gap-2">
                      <span className="text-xs text-muted-foreground">{apiKey.lastUsed}</span>
                      <Button variant="ghost" size="sm">{t('settings.revoke')}</Button>
                    </div>
                  </div>
                ))}
                <Button variant="outline"><Key className="h-4 w-4 mr-1" /> {t('settings.generateNewKey')}</Button>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="audit">
          <Card>
            <CardHeader><CardTitle className="text-base">{t('settings.auditLogTitle')}</CardTitle></CardHeader>
            <CardContent>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t('settings.action')}</TableHead>
                    <TableHead>{t('settings.user')}</TableHead>
                    <TableHead>{t('settings.target')}</TableHead>
                    <TableHead>{t('settings.timestamp')}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {[
                    { action: 'Login', user: 'admin', target: 'System', time: '2 min ago' },
                    { action: 'Alert Resolved', user: 'admin', target: 'Alert #1234', time: '15 min ago' },
                    { action: 'Policy Updated', user: 'admin', target: 'Capture Policy', time: '1h ago' },
                    { action: 'User Created', user: 'admin', target: 'john.doe', time: '3h ago' },
                  ].map((entry, i) => (
                    <TableRow key={i}>
                      <TableCell className="font-medium">{entry.action}</TableCell>
                      <TableCell>{entry.user}</TableCell>
                      <TableCell className="text-muted-foreground">{entry.target}</TableCell>
                      <TableCell className="text-xs text-muted-foreground">{entry.time}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  )
}
