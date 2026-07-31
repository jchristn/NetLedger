namespace NetLedger.Archive.Server.Authentication
{
    using System;
    using System.Collections.Generic;
    using NetLedger;

    /// <summary>
    /// Archive Server authenticated request context.
    /// </summary>
    internal sealed class ArchiveAuthContext
    {
        /// <summary>
        /// Whether the request authenticated successfully.
        /// </summary>
        public bool IsAuthenticated { get; set; } = false;

        /// <summary>
        /// Whether authentication was intentionally disabled.
        /// </summary>
        public bool IsNotRequired { get; set; } = false;

        /// <summary>
        /// Tenant identifier resolved by NetLedger Server.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Principal identifier.
        /// </summary>
        public string? PrincipalId { get; set; } = null;

        /// <summary>
        /// Principal type.
        /// </summary>
        public string? PrincipalType { get; set; } = null;

        /// <summary>
        /// Whether the principal has system administrator privileges.
        /// </summary>
        public bool IsAdmin { get; set; } = false;

        /// <summary>
        /// Whether the principal has tenant administrator privileges.
        /// </summary>
        public bool IsTenantAdmin { get; set; } = false;

        /// <summary>
        /// Effective permissions returned by NetLedger Server.
        /// </summary>
        public List<EffectivePermission> Permissions { get; set; } = new List<EffectivePermission>();

        /// <summary>
        /// Account identifiers mapped to this principal.
        /// </summary>
        public List<string> MappedAccountIds { get; set; } = new List<string>();

        /// <summary>
        /// Failure reason when authentication fails.
        /// </summary>
        public string? ErrorMessage { get; set; } = null;

        /// <summary>
        /// Create a context for disabled authentication.
        /// </summary>
        /// <returns>Authentication context.</returns>
        public static ArchiveAuthContext NotRequired()
        {
            return new ArchiveAuthContext
            {
                IsAuthenticated = true,
                IsNotRequired = true
            };
        }

        /// <summary>
        /// Create a failed authentication context.
        /// </summary>
        /// <param name="message">Failure message.</param>
        /// <returns>Authentication context.</returns>
        public static ArchiveAuthContext Failed(string message)
        {
            return new ArchiveAuthContext
            {
                IsAuthenticated = false,
                ErrorMessage = message
            };
        }

        /// <summary>
        /// Determine whether this principal can use a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <returns>True if permitted.</returns>
        public bool CanUseTenant(string? tenantId)
        {
            if (IsNotRequired || IsAdmin) return true;
            if (String.IsNullOrWhiteSpace(tenantId)) return false;
            if (String.IsNullOrWhiteSpace(TenantId)) return false;
            return String.Equals(TenantId, tenantId, StringComparison.Ordinal);
        }

        /// <summary>
        /// Determine whether the context has a permission tuple.
        /// </summary>
        /// <param name="resourceType">Resource type.</param>
        /// <param name="operationType">Operation type.</param>
        /// <returns>True if permitted.</returns>
        public bool HasPermission(string resourceType, string operationType)
        {
            if (IsNotRequired || IsAdmin || IsTenantAdmin) return true;

            bool permit = false;
            foreach (EffectivePermission permission in Permissions)
            {
                bool resourceMatches = Matches(permission.ResourceType, resourceType) || Matches(permission.ResourceType, "All");
                bool operationMatches = Matches(permission.OperationType, operationType) ||
                    Matches(permission.OperationType, "All") ||
                    (String.Equals(permission.OperationType, "Write", StringComparison.OrdinalIgnoreCase) &&
                    (String.Equals(operationType, "Create", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(operationType, "Update", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(operationType, "Delete", StringComparison.OrdinalIgnoreCase)));

                if (!resourceMatches || !operationMatches) continue;

                if (String.Equals(permission.PermissionType, "Deny", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (String.Equals(permission.PermissionType, "Permit", StringComparison.OrdinalIgnoreCase))
                {
                    permit = true;
                }
            }

            return permit;
        }

        /// <summary>
        /// Determine whether this context needs account-map enforcement.
        /// </summary>
        /// <returns>True when the authenticated principal is a regular user.</returns>
        public bool RequiresMappedAccountScope()
        {
            if (IsNotRequired || IsAdmin || IsTenantAdmin) return false;
            return String.Equals(PrincipalType, "User", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determine whether this principal can use an account.
        /// </summary>
        /// <param name="accountId">Account identifier.</param>
        /// <returns>True if permitted.</returns>
        public bool CanUseAccount(string? accountId)
        {
            if (!RequiresMappedAccountScope()) return true;
            if (String.IsNullOrWhiteSpace(accountId)) return false;
            foreach (string mappedAccountId in MappedAccountIds)
            {
                if (String.Equals(mappedAccountId, accountId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Matches(string? value, string expected)
        {
            return String.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}
