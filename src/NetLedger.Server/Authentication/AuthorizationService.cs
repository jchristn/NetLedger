namespace NetLedger.Server.Authentication
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger;
    using NetLedger.Database;
    using NetLedger.Server.Models;
    using SyslogLogging;

    /// <summary>
    /// Authorization service for tenant and account scoped requests.
    /// </summary>
    public class AuthorizationService
    {
        private readonly string _Header = "[AuthorizationService] ";
        private readonly DatabaseDriverBase _Driver;
        private readonly LoggingModule _Logging;

        /// <summary>
        /// Instantiate the authorization service.
        /// </summary>
        /// <param name="driver">Database driver.</param>
        /// <param name="logging">Logging module.</param>
        public AuthorizationService(DatabaseDriverBase driver, LoggingModule logging)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        /// <summary>
        /// Authorize a request.
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <param name="resourceType">Resource type.</param>
        /// <param name="operationType">Operation type.</param>
        /// <param name="resourceId">Resource identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Authorization result.</returns>
        public async Task<AuthorizationDecision> AuthorizeAsync(
            RequestContext req,
            string resourceType,
            string operationType,
            string? resourceId = null,
            CancellationToken token = default)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));
            if (String.IsNullOrEmpty(resourceType)) throw new ArgumentNullException(nameof(resourceType));
            if (String.IsNullOrEmpty(operationType)) throw new ArgumentNullException(nameof(operationType));

            if (req.Auth == null || !req.Auth.IsAuthenticated)
            {
                return await DenyAsync(req, resourceType, operationType, resourceId, "Authentication required", token).ConfigureAwait(false);
            }

            if (req.Auth.Result == AuthResult.NotRequired)
            {
                return AuthorizationDecision.Permit();
            }

            string? tenantId = ResolveTenant(req);
            if (req.Auth.IsAdmin)
            {
                await WriteAuditAsync(req, tenantId, resourceType, operationType, resourceId, "Permit", "System admin bypass", token).ConfigureAwait(false);
                return AuthorizationDecision.Permit();
            }

            if (String.IsNullOrEmpty(tenantId) && !String.Equals(resourceType, "Tenant", StringComparison.OrdinalIgnoreCase))
            {
                return await DenyAsync(req, resourceType, operationType, resourceId, "Tenant context is required", token).ConfigureAwait(false);
            }

            if (String.Equals(resourceType, "Tenant", StringComparison.OrdinalIgnoreCase) &&
                String.Equals(operationType, "Read", StringComparison.OrdinalIgnoreCase) &&
                String.IsNullOrEmpty(resourceId))
            {
                return await DenyAsync(req, resourceType, operationType, resourceId, "Tenant resource identifier is required", token).ConfigureAwait(false);
            }

            if (!String.IsNullOrEmpty(req.Auth.TenantId) && !String.IsNullOrEmpty(tenantId) && !String.Equals(req.Auth.TenantId, tenantId, StringComparison.Ordinal))
            {
                return await DenyAsync(req, resourceType, operationType, resourceId, "Authenticated tenant does not match request tenant", token).ConfigureAwait(false);
            }

            if (req.Auth.IsTenantAdmin)
            {
                await WriteAuditAsync(req, tenantId, resourceType, operationType, resourceId, "Permit", "Tenant admin bypass", token).ConfigureAwait(false);
                return AuthorizationDecision.Permit();
            }

            AuthorizationDecision rbacDecision = await AuthorizeRbacAsync(req, tenantId, resourceType, operationType, resourceId, token).ConfigureAwait(false);
            if (rbacDecision.Permitted)
            {
                return rbacDecision;
            }

            if (String.Equals(rbacDecision.Reason, "Explicit deny", StringComparison.Ordinal))
            {
                return await DenyAsync(req, resourceType, operationType, resourceId, "Explicit deny", token).ConfigureAwait(false);
            }

            if (String.Equals(resourceType, "Tenant", StringComparison.OrdinalIgnoreCase) &&
                String.Equals(operationType, "Read", StringComparison.OrdinalIgnoreCase) &&
                !String.IsNullOrEmpty(req.Auth.TenantId) &&
                String.Equals(resourceId, req.Auth.TenantId, StringComparison.Ordinal))
            {
                return AuthorizationDecision.Permit();
            }

            if (String.Equals(resourceType, "User", StringComparison.OrdinalIgnoreCase) &&
                String.Equals(operationType, "Read", StringComparison.OrdinalIgnoreCase) &&
                !String.IsNullOrEmpty(req.Auth.PrincipalId) &&
                String.Equals(resourceId, req.Auth.PrincipalId, StringComparison.Ordinal))
            {
                return AuthorizationDecision.Permit();
            }

            if (String.Equals(resourceType, "Account", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(resourceType, "Entry", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(resourceType, "Balance", StringComparison.OrdinalIgnoreCase))
            {
                if (String.IsNullOrEmpty(req.Auth.PrincipalId))
                {
                    return await DenyAsync(req, resourceType, operationType, resourceId, "User principal is required", token).ConfigureAwait(false);
                }

                string? accountId = ResolveAccountId(req, resourceType, resourceId);
                if (String.IsNullOrEmpty(accountId) &&
                    String.Equals(resourceType, "Account", StringComparison.OrdinalIgnoreCase) &&
                    String.Equals(operationType, "Read", StringComparison.OrdinalIgnoreCase))
                {
                    return AuthorizationDecision.Permit();
                }

                if (String.IsNullOrEmpty(accountId))
                {
                    return await DenyAsync(req, resourceType, operationType, resourceId, "Account identifier is required", token).ConfigureAwait(false);
                }

                bool mapped = await _Driver.AccountUserMaps.ExistsAsync(tenantId ?? String.Empty, accountId, req.Auth.PrincipalId, token).ConfigureAwait(false);
                if (mapped && IsRegularUserOperationAllowed(operationType))
                {
                    return AuthorizationDecision.Permit();
                }
            }

            return await DenyAsync(req, resourceType, operationType, resourceId, "No matching permission", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get effective permissions for a request principal.
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <returns>Effective permission response.</returns>
        public EffectivePermissionsResponse GetEffectivePermissions(RequestContext req)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));

            EffectivePermissionsResponse response = new EffectivePermissionsResponse();
            response.TenantId = ResolveTenant(req);
            response.PrincipalId = req.Auth?.PrincipalId;
            response.PrincipalType = req.Auth?.PrincipalType;
            response.IsAdmin = req.Auth?.IsAdmin ?? false;
            response.IsTenantAdmin = req.Auth?.IsTenantAdmin ?? false;

            if (req.Auth == null || !req.Auth.IsAuthenticated)
            {
                return response;
            }

            if (req.Auth.IsAdmin || req.Auth.IsTenantAdmin)
            {
                response.Permissions.Add(new EffectivePermission { ResourceType = "All", OperationType = "All" });
            }
            else
            {
                response.Permissions.Add(new EffectivePermission { ResourceType = "User", OperationType = "Read", ResourceScope = "Resource", ResourceId = req.Auth.PrincipalId });
                response.Permissions.Add(new EffectivePermission { ResourceType = "Account", OperationType = "Read" });
                response.Permissions.Add(new EffectivePermission { ResourceType = "Entry", OperationType = "Write" });
                response.Permissions.Add(new EffectivePermission { ResourceType = "Balance", OperationType = "Read" });
            }

            return response;
        }

        private async Task<AuthorizationDecision> AuthorizeRbacAsync(
            RequestContext req,
            string? tenantId,
            string resourceType,
            string operationType,
            string? resourceId,
            CancellationToken token)
        {
            if (_Driver.Rbac == null || String.IsNullOrEmpty(tenantId) || req.Auth == null || String.IsNullOrEmpty(req.Auth.PrincipalId))
            {
                return AuthorizationDecision.Deny("No matching permission");
            }

            List<Permission> permissions = await LoadPrincipalPermissionsAsync(req.Auth, tenantId, resourceId, token).ConfigureAwait(false);
            bool permit = false;

            foreach (Permission permission in permissions)
            {
                if (!permission.Active) continue;
                if (!PermissionMatches(permission, resourceType, operationType)) continue;

                if (String.Equals(permission.PermissionType, "Deny", StringComparison.OrdinalIgnoreCase))
                {
                    return AuthorizationDecision.Deny("Explicit deny");
                }

                if (String.Equals(permission.PermissionType, "Permit", StringComparison.OrdinalIgnoreCase))
                {
                    permit = true;
                }
            }

            return permit ? AuthorizationDecision.Permit() : AuthorizationDecision.Deny("No matching permission");
        }

        private async Task<List<Permission>> LoadPrincipalPermissionsAsync(AuthContext auth, string tenantId, string? resourceId, CancellationToken token)
        {
            List<Permission> permissions = new List<Permission>();

            if (String.Equals(auth.PrincipalType, "User", StringComparison.OrdinalIgnoreCase))
            {
                List<UserRoleAssignment> assignments = await _Driver.Rbac.EnumerateUserRoleAssignmentsAsync(tenantId, auth.PrincipalId ?? String.Empty, token).ConfigureAwait(false);
                foreach (UserRoleAssignment assignment in assignments)
                {
                    if (!ScopeMatches(assignment.ResourceScope, assignment.ResourceId, assignment.InheritsToChildren, resourceId)) continue;
                    await AddRolePermissionsAsync(permissions, tenantId, assignment.RoleId, assignment.RoleName, token).ConfigureAwait(false);
                }
            }
            else if (String.Equals(auth.PrincipalType, "Credential", StringComparison.OrdinalIgnoreCase))
            {
                List<CredentialScopeAssignment> assignments = await _Driver.Rbac.EnumerateCredentialScopeAssignmentsAsync(tenantId, auth.PrincipalId ?? String.Empty, token).ConfigureAwait(false);
                foreach (CredentialScopeAssignment assignment in assignments)
                {
                    if (!ScopeMatches(assignment.ResourceScope, assignment.ResourceId, true, resourceId)) continue;
                    await AddRolePermissionsAsync(permissions, tenantId, assignment.RoleId, assignment.RoleName, token).ConfigureAwait(false);
                    if (assignment.OperationTypes.Count > 0 && assignment.ResourceTypes.Count > 0)
                    {
                        permissions.Add(new Permission
                        {
                            TenantId = tenantId,
                            Name = "Direct credential scope",
                            ResourceTypes = assignment.ResourceTypes,
                            OperationTypes = assignment.OperationTypes,
                            PermissionType = "Permit"
                        });
                    }
                }
            }

            return permissions;
        }

        private async Task AddRolePermissionsAsync(List<Permission> permissions, string tenantId, string? roleId, string? roleName, CancellationToken token)
        {
            UserRole? role = null;
            if (!String.IsNullOrEmpty(roleId))
            {
                role = await _Driver.Rbac.ReadRoleAsync(tenantId, roleId, token).ConfigureAwait(false);
            }

            if (role == null && !String.IsNullOrEmpty(roleName))
            {
                role = await _Driver.Rbac.ReadRoleByNameAsync(tenantId, roleName, token).ConfigureAwait(false);
            }

            if (role == null || !role.Active) return;

            List<RolePermissionMap> maps = await _Driver.Rbac.EnumerateRolePermissionMapsAsync(tenantId, role.Id, token).ConfigureAwait(false);
            foreach (RolePermissionMap map in maps)
            {
                Permission? permission = await _Driver.Rbac.ReadPermissionAsync(tenantId, map.PermissionId, token).ConfigureAwait(false);
                if (permission != null && permission.Active)
                {
                    permissions.Add(permission);
                }
            }
        }

        private bool ScopeMatches(string scope, string? scopeResourceId, bool inheritsToChildren, string? resourceId)
        {
            if (String.Equals(scope, "Tenant", StringComparison.OrdinalIgnoreCase))
            {
                return inheritsToChildren || String.IsNullOrEmpty(resourceId);
            }

            return !String.IsNullOrEmpty(resourceId) && String.Equals(scopeResourceId, resourceId, StringComparison.Ordinal);
        }

        private bool PermissionMatches(Permission permission, string resourceType, string operationType)
        {
            bool resourceMatches = ContainsWildcardOrValue(permission.ResourceTypes, resourceType);
            bool operationMatches = ContainsWildcardOrValue(permission.OperationTypes, operationType);
            if (!operationMatches && String.Equals(operationType, "Update", StringComparison.OrdinalIgnoreCase))
            {
                operationMatches = ContainsWildcardOrValue(permission.OperationTypes, "Write");
            }

            if (!operationMatches && String.Equals(operationType, "Create", StringComparison.OrdinalIgnoreCase))
            {
                operationMatches = ContainsWildcardOrValue(permission.OperationTypes, "Write");
            }

            if (!operationMatches && String.Equals(operationType, "Delete", StringComparison.OrdinalIgnoreCase))
            {
                operationMatches = ContainsWildcardOrValue(permission.OperationTypes, "Write");
            }

            return resourceMatches && operationMatches;
        }

        private bool ContainsWildcardOrValue(List<string> values, string value)
        {
            foreach (string current in values)
            {
                if (String.Equals(current, "All", StringComparison.OrdinalIgnoreCase)) return true;
                if (String.Equals(current, value, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        private bool IsRegularUserOperationAllowed(string operationType)
        {
            return String.Equals(operationType, "Read", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(operationType, "Create", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(operationType, "Write", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(operationType, "Execute", StringComparison.OrdinalIgnoreCase);
        }

        private string? ResolveTenant(RequestContext req)
        {
            return !String.IsNullOrEmpty(req.TenantId) ? req.TenantId : req.Auth?.TenantId;
        }

        private string? ResolveAccountId(RequestContext req, string resourceType, string? resourceId)
        {
            if (!String.IsNullOrEmpty(req.AccountGuid)) return req.AccountGuid;
            if (String.Equals(resourceType, "Account", StringComparison.OrdinalIgnoreCase)) return resourceId;
            return null;
        }

        private async Task<AuthorizationDecision> DenyAsync(
            RequestContext req,
            string resourceType,
            string operationType,
            string? resourceId,
            string reason,
            CancellationToken token)
        {
            _Logging.Warn(_Header + "denied " + operationType + " on " + resourceType + ": " + reason);
            await WriteAuditAsync(req, ResolveTenant(req), resourceType, operationType, resourceId, "Denied", reason, token).ConfigureAwait(false);
            return AuthorizationDecision.Deny(reason);
        }

        private async Task WriteAuditAsync(
            RequestContext req,
            string? tenantId,
            string resourceType,
            string operationType,
            string? resourceId,
            string result,
            string? reason,
            CancellationToken token)
        {
            AuditRecord record = new AuditRecord
            {
                TenantId = tenantId ?? String.Empty,
                PrincipalId = req.Auth?.PrincipalId,
                PrincipalType = req.Auth?.PrincipalType,
                EventType = "Authorization",
                ResourceType = resourceType,
                OperationType = operationType,
                ResourceId = resourceId,
                Result = result,
                Reason = reason,
                RequestId = req.RequestGuid.ToString()
            };

            await _Driver.AuditRecords.CreateAsync(record, token).ConfigureAwait(false);
        }
    }
}
