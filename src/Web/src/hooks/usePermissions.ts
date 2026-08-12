import { useAuthStore } from '@/stores/auth'

export function usePermissions() {
  const { user } = useAuthStore()

  const permissions = user?.permissions ?? []

  function hasPermission(permission: string): boolean {
    if (user?.roles?.includes('SuperAdmin')) return true
    return permissions.includes(permission)
  }

  function hasAnyPermission(...perms: string[]): boolean {
    return perms.some(p => hasPermission(p))
  }

  function hasAllPermissions(...perms: string[]): boolean {
    return perms.every(p => hasPermission(p))
  }

  function lacksPermission(permission: string): boolean {
    return !hasPermission(permission)
  }

  return {
    permissions,
    hasPermission,
    hasAnyPermission,
    hasAllPermissions,
    lacksPermission,
  }
}
