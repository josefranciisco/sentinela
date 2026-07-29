import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { Table, TableHeader, TableBody, TableHead, TableRow, TableCell } from '@/components/ui/table'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { Dialog, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from '@/components/ui/dialog'
import { Avatar } from '@/components/ui/avatar'
import { Plus, Search, Shield, UserCheck, UserX } from 'lucide-react'

const mockUsers = [
  { id: '1', name: 'Admin', email: 'admin@sentinela.com', role: 'Administrator', status: 'Active', twoFactor: true, lastLogin: '2 min ago' },
  { id: '2', name: 'John Doe', email: 'john@sentinela.com', role: 'Analyst', status: 'Active', twoFactor: false, lastLogin: '1h ago' },
  { id: '3', name: 'Jane Smith', email: 'jane@sentinela.com', role: 'Viewer', status: 'Active', twoFactor: true, lastLogin: '3h ago' },
  { id: '4', name: 'Bob Wilson', email: 'bob@sentinela.com', role: 'Admin', status: 'Inactive', twoFactor: false, lastLogin: '5 days ago' },
]

export function Users() {
  const { t } = useTranslation()
  const [search, setSearch] = useState('')
  const [showCreate, setShowCreate] = useState(false)
  const [newUser, setNewUser] = useState({ name: '', email: '', role: 'Viewer' })

  const filtered = mockUsers.filter(u =>
    u.name.toLowerCase().includes(search.toLowerCase()) ||
    u.email.toLowerCase().includes(search.toLowerCase())
  )

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">{t('users.title')}</h1>
          <p className="text-muted-foreground text-sm">{t('users.subtitle')}</p>
        </div>
        <Button onClick={() => setShowCreate(true)}><Plus className="h-4 w-4 mr-1" /> {t('users.addUser')}</Button>
      </div>

      <Card>
        <CardHeader>
          <div className="flex items-center gap-3">
            <div className="relative">
              <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
              <input
                placeholder={t('users.search')}
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                className="h-9 w-64 rounded-lg border border-input bg-background pl-8 pr-3 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              />
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('users.user')}</TableHead>
                <TableHead>{t('users.role')}</TableHead>
                <TableHead>{t('users.status')}</TableHead>
                <TableHead>{t('users.twoFactor')}</TableHead>
                <TableHead>{t('users.lastLogin')}</TableHead>
                <TableHead>{t('users.actions')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {filtered.map((user) => (
                <TableRow key={user.id}>
                  <TableCell>
                    <div className="flex items-center gap-3">
                      <Avatar fallback={user.name.charAt(0)} size="sm" />
                      <div>
                        <p className="font-medium">{user.name}</p>
                        <p className="text-xs text-muted-foreground">{user.email}</p>
                      </div>
                    </div>
                  </TableCell>
                  <TableCell>
                    <Badge variant={user.role === 'Administrator' ? 'default' : user.role === 'Admin' ? 'default' : 'secondary'}>
                      {user.role}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    <Badge variant={user.status === 'Active' ? 'success' : 'destructive'}>{user.status}</Badge>
                  </TableCell>
                  <TableCell>
                    {user.twoFactor ? (
                      <Shield className="h-4 w-4 text-emerald-400" />
                    ) : (
                      <Shield className="h-4 w-4 text-muted-foreground" />
                    )}
                  </TableCell>
                  <TableCell className="text-xs text-muted-foreground">{user.lastLogin}</TableCell>
                  <TableCell>
                    <div className="flex gap-1">
                      <Button variant="ghost" size="sm"><UserCheck className="h-4 w-4" /></Button>
                      <Button variant="ghost" size="sm"><UserX className="h-4 w-4 text-destructive" /></Button>
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Dialog open={showCreate} onClose={() => setShowCreate(false)}>
        <div className="p-6">
          <DialogHeader>
            <DialogTitle>{t('users.createUser')}</DialogTitle>
            <DialogDescription>{t('users.createUserDesc')}</DialogDescription>
          </DialogHeader>
          <div className="space-y-4 mt-4">
            <Input label={t('users.name')} placeholder={t('users.fullName')} value={newUser.name} onChange={(e) => setNewUser({ ...newUser, name: e.target.value })} />
            <Input label={t('users.email')} type="email" placeholder={t('users.emailPlaceholder')} value={newUser.email} onChange={(e) => setNewUser({ ...newUser, email: e.target.value })} />
            <Select label={t('users.roleLabel')} options={[
              { value: 'Administrator', label: t('users.administrator') },
              { value: 'Analyst', label: t('users.analyst') },
              { value: 'Viewer', label: t('users.viewer') },
              { value: 'Auditor', label: t('users.auditor') },
            ]} value={newUser.role} onChange={(e) => setNewUser({ ...newUser, role: e.target.value })} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowCreate(false)}>{t('users.cancel')}</Button>
            <Button>{t('users.createUser')}</Button>
          </DialogFooter>
        </div>
      </Dialog>
    </div>
  )
}
