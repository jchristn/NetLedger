namespace NetLedger.Sdk.Methods
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Sdk.Interfaces;

    /// <summary>
    /// Identity and security administration methods.
    /// </summary>
    internal class IdentityMethods : IIdentityMethods
    {
        private readonly NetLedgerClient _Client;

        /// <summary>
        /// Instantiate identity methods.
        /// </summary>
        /// <param name="client">NetLedger client.</param>
        internal IdentityMethods(NetLedgerClient client)
        {
            _Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        /// <inheritdoc />
        public async Task<EffectivePermissionsInfo> GetEffectivePermissionsAsync(CancellationToken cancellationToken = default)
        {
            ApiResponse<EffectivePermissionsInfo> response = await _Client.SendAsync<EffectivePermissionsInfo>(HttpMethod.Get, "/v1/me/permissions", null, cancellationToken).ConfigureAwait(false);
            return response.Data ?? new EffectivePermissionsInfo();
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<TenantInfo>> EnumerateTenantsAsync(int maxResults = 100, CancellationToken cancellationToken = default)
        {
            ApiResponse<EnumerationResult<TenantInfo>> response = await _Client.SendAsync<EnumerationResult<TenantInfo>>(HttpMethod.Get, "/v1/tenants?maxResults=" + maxResults, null, cancellationToken).ConfigureAwait(false);
            return response.Data ?? new EnumerationResult<TenantInfo>();
        }

        /// <inheritdoc />
        public async Task<TenantInfo> CreateTenantAsync(string name, CancellationToken cancellationToken = default)
        {
            ApiResponse<TenantInfo> response = await _Client.SendAsync<TenantInfo>(HttpMethod.Put, "/v1/tenants", new { Name = name }, cancellationToken).ConfigureAwait(false);
            return response.Data ?? throw new NetLedgerApiException(response.StatusCode, "No data returned from server.");
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<UserInfo>> EnumerateUsersAsync(string tenantId, int maxResults = 100, CancellationToken cancellationToken = default)
        {
            ApiResponse<EnumerationResult<UserInfo>> response = await _Client.SendAsync<EnumerationResult<UserInfo>>(HttpMethod.Get, "/v1/tenants/" + tenantId + "/users?maxResults=" + maxResults, null, cancellationToken).ConfigureAwait(false);
            return response.Data ?? new EnumerationResult<UserInfo>();
        }

        /// <inheritdoc />
        public async Task<UserInfo> CreateUserAsync(string tenantId, CreateUserInfo user, CancellationToken cancellationToken = default)
        {
            ApiResponse<UserInfo> response = await _Client.SendAsync<UserInfo>(HttpMethod.Put, "/v1/tenants/" + tenantId + "/users", user, cancellationToken).ConfigureAwait(false);
            return response.Data ?? throw new NetLedgerApiException(response.StatusCode, "No data returned from server.");
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<RoleInfo>> EnumerateRolesAsync(string tenantId, int maxResults = 100, CancellationToken cancellationToken = default)
        {
            ApiResponse<EnumerationResult<RoleInfo>> response = await _Client.SendAsync<EnumerationResult<RoleInfo>>(HttpMethod.Get, "/v1/tenants/" + tenantId + "/roles?maxResults=" + maxResults, null, cancellationToken).ConfigureAwait(false);
            return response.Data ?? new EnumerationResult<RoleInfo>();
        }

        /// <inheritdoc />
        public async Task<RoleInfo> CreateRoleAsync(string tenantId, string name, CancellationToken cancellationToken = default)
        {
            ApiResponse<RoleInfo> response = await _Client.SendAsync<RoleInfo>(HttpMethod.Put, "/v1/tenants/" + tenantId + "/roles", new { Name = name }, cancellationToken).ConfigureAwait(false);
            return response.Data ?? throw new NetLedgerApiException(response.StatusCode, "No data returned from server.");
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<PermissionInfo>> EnumeratePermissionsAsync(string tenantId, int maxResults = 100, CancellationToken cancellationToken = default)
        {
            ApiResponse<EnumerationResult<PermissionInfo>> response = await _Client.SendAsync<EnumerationResult<PermissionInfo>>(HttpMethod.Get, "/v1/tenants/" + tenantId + "/permissions?maxResults=" + maxResults, null, cancellationToken).ConfigureAwait(false);
            return response.Data ?? new EnumerationResult<PermissionInfo>();
        }

        /// <inheritdoc />
        public async Task<PermissionInfo> CreatePermissionAsync(string tenantId, PermissionInfo permission, CancellationToken cancellationToken = default)
        {
            ApiResponse<PermissionInfo> response = await _Client.SendAsync<PermissionInfo>(HttpMethod.Put, "/v1/tenants/" + tenantId + "/permissions", permission, cancellationToken).ConfigureAwait(false);
            return response.Data ?? throw new NetLedgerApiException(response.StatusCode, "No data returned from server.");
        }

        /// <inheritdoc />
        public async Task<UserRoleAssignmentInfo> AssignUserRoleAsync(string tenantId, string userId, UserRoleAssignmentInfo assignment, CancellationToken cancellationToken = default)
        {
            ApiResponse<UserRoleAssignmentInfo> response = await _Client.SendAsync<UserRoleAssignmentInfo>(HttpMethod.Put, "/v1/tenants/" + tenantId + "/users/" + userId + "/roles", assignment, cancellationToken).ConfigureAwait(false);
            return response.Data ?? throw new NetLedgerApiException(response.StatusCode, "No data returned from server.");
        }
    }
}
