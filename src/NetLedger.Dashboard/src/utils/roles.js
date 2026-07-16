export function valueOf(obj, pascalKey, camelKey = null) {
  if (!obj) return undefined
  return obj[pascalKey] ?? obj[camelKey || pascalKey.charAt(0).toLowerCase() + pascalKey.slice(1)]
}

export function getPrincipalId(currentUser, effectivePermissions) {
  return valueOf(currentUser, 'Id') || valueOf(effectivePermissions, 'PrincipalId')
}

export function getTenantId(currentUser, effectivePermissions, fallbackTenantId = '') {
  return valueOf(currentUser, 'TenantId') || valueOf(effectivePermissions, 'TenantId') || fallbackTenantId
}

export function getRoleFlags(currentUser, effectivePermissions) {
  const permissions = valueOf(effectivePermissions, 'Permissions') || []
  const hasGlobalAllPermission = permissions.some(permission => {
    const resourceType = valueOf(permission, 'ResourceType')
    const operationType = valueOf(permission, 'OperationType')
    return resourceType === 'All' && operationType === 'All'
  })

  const isSystemAdmin = Boolean(valueOf(currentUser, 'IsAdmin') ?? valueOf(effectivePermissions, 'IsAdmin') ?? false)
  const isTenantAdmin = Boolean(
    valueOf(currentUser, 'IsTenantAdmin') ??
    valueOf(effectivePermissions, 'IsTenantAdmin') ??
    (!isSystemAdmin && hasGlobalAllPermission) ??
    false
  )

  return {
    isSystemAdmin,
    isTenantAdmin,
    isAdmin: isSystemAdmin || isTenantAdmin,
    isRegularUser: !isSystemAdmin && !isTenantAdmin
  }
}
