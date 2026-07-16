namespace NetLedger.Sdk.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Identity and security administration methods.
    /// </summary>
    public interface IIdentityMethods
    {
        /// <summary>
        /// Get effective permissions for the current principal.
        /// </summary>
        Task<EffectivePermissionsInfo> GetEffectivePermissionsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Enumerate tenants.
        /// </summary>
        Task<EnumerationResult<TenantInfo>> EnumerateTenantsAsync(int maxResults = 100, CancellationToken cancellationToken = default);

        /// <summary>
        /// Create a tenant.
        /// </summary>
        Task<TenantInfo> CreateTenantAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Enumerate tenant users.
        /// </summary>
        Task<EnumerationResult<UserInfo>> EnumerateUsersAsync(string tenantId, int maxResults = 100, CancellationToken cancellationToken = default);

        /// <summary>
        /// Create a tenant user.
        /// </summary>
        Task<UserInfo> CreateUserAsync(string tenantId, CreateUserInfo user, CancellationToken cancellationToken = default);

        /// <summary>
        /// Enumerate roles.
        /// </summary>
        Task<EnumerationResult<RoleInfo>> EnumerateRolesAsync(string tenantId, int maxResults = 100, CancellationToken cancellationToken = default);

        /// <summary>
        /// Create a custom role.
        /// </summary>
        Task<RoleInfo> CreateRoleAsync(string tenantId, string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Enumerate permissions.
        /// </summary>
        Task<EnumerationResult<PermissionInfo>> EnumeratePermissionsAsync(string tenantId, int maxResults = 100, CancellationToken cancellationToken = default);

        /// <summary>
        /// Create a custom permission.
        /// </summary>
        Task<PermissionInfo> CreatePermissionAsync(string tenantId, PermissionInfo permission, CancellationToken cancellationToken = default);

        /// <summary>
        /// Assign a role to a user.
        /// </summary>
        Task<UserRoleAssignmentInfo> AssignUserRoleAsync(string tenantId, string userId, UserRoleAssignmentInfo assignment, CancellationToken cancellationToken = default);
    }
}
