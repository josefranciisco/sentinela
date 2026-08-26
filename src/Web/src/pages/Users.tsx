import { useState, useEffect } from 'react'
import { api } from '@/lib/api'
import { usePermissions } from '@/hooks/usePermissions'
import { HasPermission } from '@/components/HasPermission'

interface User {
  id: string
  username: string
  email: string
  fullName: string
  department: string
  isActive: boolean
  createdAt: string
  lastLoginAt: string | null
  twoFactorEnabled: boolean
  roles: string[]
}

interface Role {
  id: string
  name: string
  description: string
  isSystemRole: boolean
  isDefault: boolean
  permissions: string[]
}

export function Users() {
  const { hasPermission } = usePermissions()
  const [users, setUsers] = useState<User[]>([])
  const [roles, setRoles] = useState<Role[]>([])
  const [loading, setLoading] = useState(true)
  const [showCreate, setShowCreate] = useState(false)
  const [editingUser, setEditingUser] = useState<User | null>(null)
  const [showRoles, setShowRoles] = useState<User | null>(null)
  const [confirmDelete, setConfirmDelete] = useState<User | null>(null)
  const [deleting, setDeleting] = useState(false)
  const [formData, setFormData] = useState({
    username: '',
    email: '',
    password: '',
    fullName: '',
    department: '',
    roleIds: [] as string[],
  })
  const [search, setSearch] = useState('')

  useEffect(() => {
    loadUsers()
    loadRoles()
  }, [])

  async function loadUsers() {
    try {
      const data = await api.get<User[]>('/users')
      setUsers(data)
    } catch (error) {
      console.error('Failed to load users:', error)
    } finally {
      setLoading(false)
    }
  }

  async function loadRoles() {
    try {
      const data = await api.get<Role[]>('/roles')
      setRoles(data)
    } catch (error) {
      console.error('Failed to load roles:', error)
    }
  }

  function openCreateModal() {
    setEditingUser(null)
    setFormData({ username: '', email: '', password: '', fullName: '', department: '', roleIds: [] })
    setShowCreate(true)
  }

  function openEditModal(user: User) {
    setEditingUser(user)
    setFormData({
      username: user.username,
      email: user.email,
      password: '',
      fullName: user.fullName || '',
      department: user.department || '',
      roleIds: userIdsFor(user)
    })
    setShowCreate(true)
  }

  function userIdsFor(user: User): string[] {
    return user.roles
      .map(roleName => roles.find(r => r.name === roleName)?.id)
      .filter((id): id is string => !!id)
  }

  function toggleRole(roleId: string) {
    setFormData(prev => ({
      ...prev,
      roleIds: prev.roleIds.includes(roleId)
        ? prev.roleIds.filter(r => r !== roleId)
        : [...prev.roleIds, roleId]
    }))
  }

  async function handleSave() {
    try {
      if (editingUser) {
        await api.put(`/users/${editingUser.id}`, {
          fullName: formData.fullName,
          department: formData.department,
          roleIds: formData.roleIds,
        })
      } else {
        await api.post('/users', {
          username: formData.username,
          email: formData.email,
          password: formData.password,
          fullName: formData.fullName,
          department: formData.department,
          roleIds: formData.roleIds,
        })
      }
      setShowCreate(false)
      loadUsers()
    } catch (error) {
      console.error('Failed to save user:', error)
    }
  }

  async function handleToggleActive(user: User) {
    try {
      if (user.isActive) {
        await api.post(`/users/${user.id}/lock`, {})
      } else {
        await api.post(`/users/${user.id}/unlock`, {})
      }
      loadUsers()
    } catch (error) {
      console.error('Failed to toggle user status:', error)
    }
  }

  async function handleSaveRoles() {
    if (!showRoles) return
    try {
      const roleIds = roles
        .filter(r => formData.roleIds.includes(r.id))
        .map(r => r.id)
      await api.post(`/users/${showRoles.id}/roles`, { roleIds })
      setShowRoles(null)
      loadUsers()
    } catch (error) {
      console.error('Failed to save roles:', error)
    }
  }

  async function handleDeleteUser(user: User) {
    if (user.roles.includes('SuperAdmin')) {
      alert('Não é possível excluir uma conta SuperAdmin.')
      return
    }
    setDeleting(true)
    try {
      await api.delete(`/users/${user.id}/permanent`)
      setConfirmDelete(null)
      loadUsers()
    } catch (error) {
      console.error('Failed to delete user:', error)
      alert('Falha ao excluir usuário. Verifique se ele não é a conta atual.')
    } finally {
      setDeleting(false)
    }
  }

  const filteredUsers = users.filter(u =>
    u.username.toLowerCase().includes(search.toLowerCase()) ||
    (u.fullName || '').toLowerCase().includes(search.toLowerCase()) ||
    u.email.toLowerCase().includes(search.toLowerCase())
  )

  const visibleRoles = roles.filter(r => ['administrador', 'operador'].includes(r.name))

  if (loading) {
    return <div className="flex items-center justify-center h-64">Carregando...</div>
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900 dark:text-white">Usuários</h1>
          <p className="text-gray-600 dark:text-gray-400">Gerenciamento de usuários e controle de acesso</p>
        </div>
        <HasPermission permission="users.create">
          <button
            onClick={openCreateModal}
            className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700"
          >
            Adicionar Usuário
          </button>
        </HasPermission>
      </div>

      <div className="relative">
        <input
          type="text"
          placeholder="Buscar usuários..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="w-full px-4 py-2 border rounded-lg dark:bg-gray-800 dark:border-gray-700"
        />
      </div>

      <div className="border rounded-lg overflow-hidden dark:border-gray-700">
        <table className="w-full text-sm">
          <thead className="bg-gray-50 dark:bg-gray-800">
            <tr className="text-left text-gray-600 dark:text-gray-400">
              <th className="px-4 py-3">Usuário</th>
              <th className="px-4 py-3">Funções</th>
              <th className="px-4 py-3">Departamento</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3">Último Login</th>
              <th className="px-4 py-3">Ações</th>
            </tr>
          </thead>
          <tbody className="divide-y dark:divide-gray-700">
            {filteredUsers.map(user => (
              <tr key={user.id} className="hover:bg-gray-50 dark:hover:bg-gray-800">
                <td className="px-4 py-3">
                  <p className="font-medium text-gray-900 dark:text-white">{user.fullName || user.username}</p>
                  <p className="text-xs text-gray-500 dark:text-gray-400">{user.email}</p>
                </td>
                <td className="px-4 py-3">
                  <div className="flex flex-wrap gap-1">
                    {user.roles.length === 0 && <span className="text-xs text-gray-400">Sem funções</span>}
                    {user.roles.map(role => (
                      <span key={role} className="px-2 py-0.5 text-xs rounded-full bg-blue-100 text-blue-700 dark:bg-blue-900/40 dark:text-blue-300">
                        {role}
                      </span>
                    ))}
                  </div>
                </td>
                <td className="px-4 py-3 text-gray-600 dark:text-gray-400">{user.department || '-'}</td>
                <td className="px-4 py-3">
                  <span className={`px-2 py-0.5 text-xs rounded-full ${user.isActive
                    ? 'bg-green-100 text-green-700 dark:bg-green-900/40 dark:text-green-300'
                    : 'bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-300'}`}>
                    {user.isActive ? 'Ativo' : 'Inativo'}
                  </span>
                </td>
                <td className="px-4 py-3 text-xs text-gray-500">
                  {user.lastLoginAt ? new Date(user.lastLoginAt).toLocaleString() : 'Nunca'}
                </td>
                <td className="px-4 py-3">
                  <div className="flex items-center gap-2">
                    <HasPermission permission="users.edit">
                      <button
                        onClick={() => openEditModal(user)}
                        className="px-3 py-1 text-xs border rounded hover:bg-gray-100 dark:hover:bg-gray-700"
                      >
                        Editar
                      </button>
                    </HasPermission>
                    <HasPermission permission="users.edit">
                      <button
                        onClick={() => {
                          setShowRoles(user)
                          setFormData(prev => ({ ...prev, roleIds: userIdsFor(user) }))
                        }}
                        className="px-3 py-1 text-xs border rounded hover:bg-gray-100 dark:hover:bg-gray-700"
                      >
                        Funções
                      </button>
                    </HasPermission>
                    <HasPermission permission="users.edit">
                      <button
                        onClick={() => handleToggleActive(user)}
                        className={`px-3 py-1 text-xs border rounded hover:bg-gray-100 dark:hover:bg-gray-700 ${!user.isActive ? 'text-green-600' : 'text-red-600'}`}
                      >
                        {user.isActive ? 'Bloquear' : 'Ativar'}
                      </button>
                    </HasPermission>
                    <HasPermission permission="users.delete">
                      <button
                        onClick={() => setConfirmDelete(user)}
                        disabled={user.roles.includes('SuperAdmin')}
                        className="px-3 py-1 text-xs border rounded text-red-600 hover:bg-red-50 dark:hover:bg-red-900/20 disabled:opacity-40 disabled:cursor-not-allowed"
                      >
                        Excluir
                      </button>
                    </HasPermission>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {showCreate && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
          <div className="bg-white dark:bg-gray-800 rounded-lg w-full max-w-lg max-h-[90vh] overflow-y-auto p-6">
            <h2 className="text-xl font-bold text-gray-900 dark:text-white mb-4">
              {editingUser ? 'Editar Usuário' : 'Criar Usuário'}
            </h2>
            <div className="space-y-4">
              <div>
                <label className="block text-sm font-medium mb-1">Nome de usuário</label>
                <input
                  type="text"
                  value={formData.username}
                  disabled={!!editingUser}
                  onChange={(e) => setFormData(prev => ({ ...prev, username: e.target.value }))}
                  className="w-full px-3 py-2 border rounded-lg dark:bg-gray-700 dark:border-gray-600 disabled:opacity-50"
                />
              </div>
              <div>
                <label className="block text-sm font-medium mb-1">Email</label>
                <input
                  type="email"
                  value={formData.email}
                  disabled={!!editingUser}
                  onChange={(e) => setFormData(prev => ({ ...prev, email: e.target.value }))}
                  className="w-full px-3 py-2 border rounded-lg dark:bg-gray-700 dark:border-gray-600 disabled:opacity-50"
                />
              </div>
              {!editingUser && (
                <div>
                  <label className="block text-sm font-medium mb-1">Senha</label>
                  <input
                    type="password"
                    value={formData.password}
                    onChange={(e) => setFormData(prev => ({ ...prev, password: e.target.value }))}
                    className="w-full px-3 py-2 border rounded-lg dark:bg-gray-700 dark:border-gray-600"
                  />
                </div>
              )}
              <div>
                <label className="block text-sm font-medium mb-1">Nome completo</label>
                <input
                  type="text"
                  value={formData.fullName}
                  onChange={(e) => setFormData(prev => ({ ...prev, fullName: e.target.value }))}
                  className="w-full px-3 py-2 border rounded-lg dark:bg-gray-700 dark:border-gray-600"
                />
              </div>
              <div>
                <label className="block text-sm font-medium mb-1">Departamento</label>
                <input
                  type="text"
                  value={formData.department}
                  onChange={(e) => setFormData(prev => ({ ...prev, department: e.target.value }))}
                  className="w-full px-3 py-2 border rounded-lg dark:bg-gray-700 dark:border-gray-600"
                />
              </div>
              <div>
                <label className="block text-sm font-medium mb-2">Funções</label>
                <div className="space-y-1">
                  {visibleRoles.map(role => (
                    <label key={role.id} className="flex items-center gap-2">
                      <input
                        type="checkbox"
                        checked={formData.roleIds.includes(role.id)}
                        onChange={() => toggleRole(role.id)}
                        className="w-4 h-4"
                      />
                      <span className="text-sm">{role.name}</span>
                    </label>
                  ))}
                </div>
              </div>
            </div>
            <div className="mt-6 flex justify-end gap-3">
              <button
                onClick={() => setShowCreate(false)}
                className="px-4 py-2 border rounded-lg hover:bg-gray-100 dark:hover:bg-gray-700"
              >
                Cancelar
              </button>
              <button
                onClick={handleSave}
                className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700"
              >
                Salvar
              </button>
            </div>
          </div>
        </div>
      )}

      {showRoles && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
          <div className="bg-white dark:bg-gray-800 rounded-lg w-full max-w-md p-6">
            <h2 className="text-xl font-bold text-gray-900 dark:text-white mb-1">
              Funções de {showRoles.fullName || showRoles.username}
            </h2>
            <p className="text-sm text-gray-600 dark:text-gray-400 mb-4">
              As permissões do usuário são determinadas pelas funções atribuídas
            </p>
            <div className="space-y-1">
              {visibleRoles.map(role => (
                <label key={role.id} className="flex items-center gap-2">
                  <input
                    type="checkbox"
                    checked={formData.roleIds.includes(role.id)}
                    onChange={() => toggleRole(role.id)}
                    className="w-4 h-4"
                  />
                  <span className="text-sm">{role.name}</span>
                </label>
              ))}
            </div>
            <div className="mt-6 flex justify-end gap-3">
              <button
                onClick={() => setShowRoles(null)}
                className="px-4 py-2 border rounded-lg hover:bg-gray-100 dark:hover:bg-gray-700"
              >
                Cancelar
              </button>
              <button
                onClick={handleSaveRoles}
                className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700"
              >
                Salvar Funções
              </button>
            </div>
          </div>
        </div>
      )}
    {confirmDelete && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
          <div className="bg-white dark:bg-gray-800 rounded-lg w-full max-w-md p-6">
            <h2 className="text-xl font-bold text-gray-900 dark:text-white mb-2">Excluir Usuário</h2>
            <p className="text-sm text-gray-600 dark:text-gray-400">
              Tem certeza que deseja excluir permanentemente o usuário{' '}
              <strong className="text-gray-900 dark:text-white">{confirmDelete.fullName || confirmDelete.username}</strong>
              ({confirmDelete.email})? Esta ação remove todos os dados do usuário e não pode ser desfeita.
            </p>
            <div className="mt-6 flex justify-end gap-3">
              <button
                onClick={() => setConfirmDelete(null)}
                disabled={deleting}
                className="px-4 py-2 border rounded-lg hover:bg-gray-100 dark:hover:bg-gray-700 disabled:opacity-50"
              >
                Cancelar
              </button>
              <button
                onClick={() => handleDeleteUser(confirmDelete)}
                disabled={deleting}
                className="px-4 py-2 bg-red-600 text-white rounded-lg hover:bg-red-700 disabled:opacity-50"
              >
                {deleting ? 'Excluindo...' : 'Excluir'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}