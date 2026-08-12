import { usePermissions } from '@/hooks/usePermissions'
import { Navigate } from 'react-router-dom'

interface PermissionGuardProps {
  permission: string
  children: React.ReactNode
  fallback?: React.ReactNode
}

export function PermissionGuard({ permission, children, fallback }: PermissionGuardProps) {
  const { hasPermission } = usePermissions()

  if (!hasPermission(permission)) {
    if (fallback) return <>{fallback}</>
    return <Navigate to="/access-denied" replace />
  }

  return <>{children}</>
}
