export interface Computer {
  id: string
  hostname: string
  ipAddress: string
  macAddress: string
  domain: string
  osVersion: string
  lastHeartbeat: string
  status: 'Online' | 'Offline' | 'Away'
  currentUser: string
  department: string
  agentVersion: string
  uptime: number
  tags: string[]
  firewallEnabled?: boolean
  defenderEnabled?: boolean
  antivirusEnabled?: boolean
  realTimeProtectionEnabled?: boolean
  bitlockerEnabled?: boolean
  rdpEnabled?: boolean
  antivirusProductName?: string
  securityCollectedAt?: string
}

export interface Alert {
  id: string
  title: string
  description: string
  severity: 'Info' | 'Low' | 'Medium' | 'High' | 'Critical'
  category: string
  source: string
  computerId: string
  computerName: string
  username: string
  status: 'Open' | 'Acknowledged' | 'InProgress' | 'Resolved' | 'FalsePositive'
  timestamp: string
  assignedTo: string
  tags: string[]
  correlationScore: number
}

export interface TimelineEntry {
  id: string
  timestamp: string
  eventType: string
  category: string
  description: string
  computerId: string
  username: string
  severity: string
}

export interface DashboardStats {
  totalComputers: number
  onlineComputers: number
  offlineComputers: number
  totalUsers: number
  totalDepartments: number
  totalAlerts: number
  criticalAlerts: number
}

export interface PaginatedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}
