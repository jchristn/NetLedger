namespace NetLedger.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Role-based access control data access methods.
    /// </summary>
    public interface IRbacMethods
    {
        /// <summary>
        /// Create a role.
        /// </summary>
        Task<UserRole> CreateRoleAsync(UserRole role, CancellationToken token = default);

        /// <summary>
        /// Read a role by identifier.
        /// </summary>
        Task<UserRole?> ReadRoleAsync(string? tenantId, string roleId, CancellationToken token = default);

        /// <summary>
        /// Read a role by name, using tenant role first and built-in role fallback.
        /// </summary>
        Task<UserRole?> ReadRoleByNameAsync(string? tenantId, string name, CancellationToken token = default);

        /// <summary>
        /// Enumerate roles.
        /// </summary>
        Task<EnumerationResult<UserRole>> EnumerateRolesAsync(EnumerationQuery query, CancellationToken token = default);

        /// <summary>
        /// Create a permission.
        /// </summary>
        Task<Permission> CreatePermissionAsync(Permission permission, CancellationToken token = default);

        /// <summary>
        /// Read a permission by identifier.
        /// </summary>
        Task<Permission?> ReadPermissionAsync(string? tenantId, string permissionId, CancellationToken token = default);

        /// <summary>
        /// Enumerate permissions.
        /// </summary>
        Task<EnumerationResult<Permission>> EnumeratePermissionsAsync(EnumerationQuery query, CancellationToken token = default);

        /// <summary>
        /// Create a role-permission mapping.
        /// </summary>
        Task<RolePermissionMap> CreateRolePermissionMapAsync(RolePermissionMap map, CancellationToken token = default);

        /// <summary>
        /// Enumerate role-permission mappings for a role.
        /// </summary>
        Task<List<RolePermissionMap>> EnumerateRolePermissionMapsAsync(string? tenantId, string roleId, CancellationToken token = default);

        /// <summary>
        /// Create a user role assignment.
        /// </summary>
        Task<UserRoleAssignment> CreateUserRoleAssignmentAsync(UserRoleAssignment assignment, CancellationToken token = default);

        /// <summary>
        /// Enumerate assignments for a user.
        /// </summary>
        Task<List<UserRoleAssignment>> EnumerateUserRoleAssignmentsAsync(string tenantId, string userId, CancellationToken token = default);

        /// <summary>
        /// Create a credential scope assignment.
        /// </summary>
        Task<CredentialScopeAssignment> CreateCredentialScopeAssignmentAsync(CredentialScopeAssignment assignment, CancellationToken token = default);

        /// <summary>
        /// Enumerate assignments for a credential.
        /// </summary>
        Task<List<CredentialScopeAssignment>> EnumerateCredentialScopeAssignmentsAsync(string tenantId, string credentialId, CancellationToken token = default);

        /// <summary>
        /// Seed built-in roles and permissions.
        /// </summary>
        Task SeedBuiltInsAsync(CancellationToken token = default);
    }
}
