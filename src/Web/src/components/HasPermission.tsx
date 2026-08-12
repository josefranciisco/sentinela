import { usePermissions } from '@/hooks/usePermissions'

interface HasPermissionProps {
  permission: string
  children: React.ReactNode
}

export function HasPermission({ permission, children }: HasPermissionProps) {
  const { hasPermission } = usePermissions()

  if (!hasPermission(permission)) {
    return null
  }

  return <>{children}</>
}
