import { useState, useEffect } from 'react'
import { api } from '@/lib/api'
import { usePermissions } from '@/hooks/usePermissions'
import { HasPermission } from '@/components/HasPermission'

interface Role {
  id: string
  name: string
  description: string
  isSystemRole: boolean
  isDefault: boolean
  permissions: string[]
  userCount: number
}

interface PermissionGroup {
  category: string
  permissions: { code: string; name: string; checked: boolean }[]
}

export function Roles() {
  const { hasPermission } = usePermissions()
  const [roles, setRoles] = useState<Role[]>([])
  const [loading, setLoading] = useState(true)
  const [showModal, setShowModal] = useState(false)
  const [editingRole, setEditingRole] = useState<Role | null>(null)
  const [formData, setFormData] = useState({ name: '', description: '', permissions: [] as string[] })
  const [permissionGroups, setPermissionGroups] = useState<PermissionGroup[]>([])
  const [search, setSearch] = useState('')

  useEffect(() => {
    loadRoles()
    loadPermissions()
  }, [])

  async function loadRoles() {
    try {
      const data = await api.get<Role[]>('/roles')
      setRoles(data)
    } catch (error) {
      console.error('Failed to load roles:', error)
    } finally {
      setLoading(false)
    }
  }

  async function loadPermissions() {
    try {
      const grouped = await api.get<Record<string, { code: string; name: string }[]>>('/permissions/grouped')
      const groups = Object.entries(grouped).map(([category, perms]) => ({
        category,
        permissions: perms.map(p => ({ ...p, checked: false }))
      }))
      setPermissionGroups(groups)
    } catch (error) {
      console.error('Failed to load permissions:', error)
    }
  }

  function openCreateModal() {
    setEditingRole(null)
    setFormData({ name: '', description: '', permissions: [] })
    setPermissionGroups(prev => prev.map(g => ({
      ...g,
      permissions: g.permissions.map(p => ({ ...p, checked: false }))
    })))
    setShowModal(true)
  }

  function openEditModal(role: Role) {
    setEditingRole(role)
    setFormData({ name: role.name, description: role.description || '', permissions: role.permissions })
    setPermissionGroups(prev => prev.map(g => ({
      ...g,
      permissions: g.permissions.map(p => ({ ...p, checked: role.permissions.includes(p.code) }))
    })))
    setShowModal(true)
  }

  function togglePermission(code: string) {
    setFormData(prev => ({
      ...prev,
      permissions: prev.permissions.includes(code)
        ? prev.permissions.filter(p => p !== code)
        : [...prev.permissions, code]
    }))
    setPermissionGroups(prev => prev.map(g => ({
      ...g,
      permissions: g.permissions.map(p => p.code === code ? { ...p, checked: !p.checked } : p)
    })))
  }

  function toggleCategory(category: string, checked: boolean) {
    const categoryPermissions = permissionGroups
      .find(g => g.category === category)
      ?.permissions.map(p => p.code) ?? []

    setFormData(prev => ({
      ...prev,
      permissions: checked
        ? [...new Set([...prev.permissions, ...categoryPermissions])]
        : prev.permissions.filter(p => !categoryPermissions.includes(p))
    }))
    setPermissionGroups(prev => prev.map(g => g.category === category ? {
      ...g,
      permissions: g.permissions.map(p => ({ ...p, checked }))
    } : g))
  }

  async function handleSave() {
    try {
      if (editingRole) {
        await api.put(`/roles/${editingRole.id}`, formData)
      } else {
        await api.post('/roles', formData)
      }
      setShowModal(false)
      loadRoles()
    } catch (error) {
      console.error('Failed to save role:', error)
    }
  }

  async function handleDelete(id: string) {
    if (!confirm('Tem certeza que deseja excluir este perfil?')) return
    try {
      await api.delete(`/roles/${id}`)
      loadRoles()
    } catch (error) {
      console.error('Failed to delete role:', error)
    }
  }

  async function handleDuplicate(id: string) {
    try {
      await api.post(`/roles/${id}/duplicate`, {})
      loadRoles()
    } catch (error) {
      console.error('Failed to duplicate role:', error)
    }
  }

  const allowedRoles = ['administrador', 'operador']
  const filteredRoles = roles.filter(r =>
    allowedRoles.includes(r.name.toLowerCase()) &&
    r.name.toLowerCase().includes(search.toLowerCase())
  )

  if (loading) {
    return <div className="flex items-center justify-center h-64">Carregando...</div>
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900 dark:text-white">Perfis e Permissões</h1>
          <p className="text-gray-600 dark:text-gray-400">Gerencie os perfis de acesso do sistema</p>
        </div>
        <HasPermission permission="roles.create">
          <button
            onClick={openCreateModal}
            className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700"
          >
            Criar Perfil
          </button>
        </HasPermission>
      </div>

      <div className="relative">
        <input
          type="text"
          placeholder="Pesquisar perfil..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="w-full px-4 py-2 border rounded-lg dark:bg-gray-800 dark:border-gray-700"
        />
      </div>

      <div className="grid gap-4">
        {filteredRoles.map(role => (
          <div key={role.id} className="border rounded-lg p-4 dark:border-gray-700 dark:bg-gray-800">
            <div className="flex items-center justify-between">
              <div>
                <h3 className="font-semibold text-gray-900 dark:text-white">{role.name}</h3>
                {role.description && (
                  <p className="text-sm text-gray-600 dark:text-gray-400">{role.description}</p>
                )}
                <div className="mt-2 flex items-center gap-4 text-sm text-gray-500">
                  <span>{role.permissions.length} permissões</span>
                  <span>{role.userCount} usuários</span>
                  {role.isSystemRole && <span className="text-blue-600">Sistema</span>}
                  {role.isDefault && <span className="text-green-600">Padrão</span>}
                </div>
              </div>
              <div className="flex items-center gap-2">
                <HasPermission permission="roles.edit">
                  <button
                    onClick={() => openEditModal(role)}
                    className="px-3 py-1 text-sm border rounded hover:bg-gray-100 dark:hover:bg-gray-700"
                  >
                    Editar
                  </button>
                </HasPermission>
                <HasPermission permission="roles.create">
                  <button
                    onClick={() => handleDuplicate(role.id)}
                    className="px-3 py-1 text-sm border rounded hover:bg-gray-100 dark:hover:bg-gray-700"
                  >
                    Duplicar
                  </button>
                </HasPermission>
                <HasPermission permission="roles.delete">
                  {!role.isSystemRole && (
                    <button
                      onClick={() => handleDelete(role.id)}
                      className="px-3 py-1 text-sm border border-red-300 text-red-600 rounded hover:bg-red-50 dark:hover:bg-red-900/20"
                    >
                      Excluir
                    </button>
                  )}
                </HasPermission>
              </div>
            </div>
          </div>
        ))}
      </div>

      {showModal && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
          <div className="bg-white dark:bg-gray-800 rounded-lg w-full max-w-2xl max-h-[90vh] overflow-hidden flex flex-col">
            <div className="p-6 border-b dark:border-gray-700">
              <h2 className="text-xl font-bold text-gray-900 dark:text-white">
                {editingRole ? 'Editar Perfil' : 'Criar Perfil'}
              </h2>
            </div>
            <div className="p-6 overflow-y-auto flex-1 space-y-6">
              <div>
                <label className="block text-sm font-medium mb-1">Nome</label>
                <input
                  type="text"
                  value={formData.name}
                  onChange={(e) => setFormData(prev => ({ ...prev, name: e.target.value }))}
                  className="w-full px-3 py-2 border rounded-lg dark:bg-gray-700 dark:border-gray-600"
                />
              </div>
              <div>
                <label className="block text-sm font-medium mb-1">Descrição</label>
                <input
                  type="text"
                  value={formData.description}
                  onChange={(e) => setFormData(prev => ({ ...prev, description: e.target.value }))}
                  className="w-full px-3 py-2 border rounded-lg dark:bg-gray-700 dark:border-gray-600"
                />
              </div>
              <div>
                <label className="block text-sm font-medium mb-3">Permissões</label>
                <div className="space-y-4">
                  {permissionGroups.map(group => {
                    const allChecked = group.permissions.every(p => p.checked)
                    const someChecked = group.permissions.some(p => p.checked)
                    return (
                      <div key={group.category} className="border rounded-lg p-4 dark:border-gray-700">
                        <div className="flex items-center gap-2 mb-3">
                          <input
                            type="checkbox"
                            checked={allChecked}
                            ref={el => { if (el) el.indeterminate = someChecked && !allChecked }}
                            onChange={(e) => toggleCategory(group.category, e.target.checked)}
                            className="w-4 h-4"
                          />
                          <span className="font-medium text-gray-900 dark:text-white">{group.category}</span>
                        </div>
                        <div className="grid grid-cols-2 gap-2 ml-6">
                          {group.permissions.map(perm => (
                            <label key={perm.code} className="flex items-center gap-2">
                              <input
                                type="checkbox"
                                checked={perm.checked}
                                onChange={() => togglePermission(perm.code)}
                                className="w-4 h-4"
                              />
                              <span className="text-sm">{perm.name}</span>
                            </label>
                          ))}
                        </div>
                      </div>
                    )
                  })}
                </div>
              </div>
            </div>
            <div className="p-6 border-t dark:border-gray-700 flex justify-end gap-3">
              <button
                onClick={() => setShowModal(false)}
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
    </div>
  )
}
