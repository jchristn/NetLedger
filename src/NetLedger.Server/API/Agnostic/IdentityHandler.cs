namespace NetLedger.Server.API.Agnostic
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger;
    using NetLedger.Database;
    using NetLedger.Server.Authentication;
    using NetLedger.Server.Models.Identity;
    using NetLedger.Server.Models;
    using NetLedger.Server.Settings;
    using SyslogLogging;

    /// <summary>
    /// Identity, tenant, session, mapping, and audit handler.
    /// </summary>
    internal class IdentityHandler
    {
        private readonly string _Header = "[IdentityHandler] ";
        private readonly ServerSettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly DatabaseDriverBase _Driver;
        private readonly AuthService _AuthService;
        private readonly AuthorizationService _AuthorizationService;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="driver">Database driver.</param>
        /// <param name="authService">Authentication service.</param>
        /// <param name="authorizationService">Authorization service.</param>
        internal IdentityHandler(
            ServerSettings settings,
            LoggingModule logging,
            DatabaseDriverBase driver,
            AuthService authService,
            AuthorizationService authorizationService)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _AuthService = authService ?? throw new ArgumentNullException(nameof(authService));
            _AuthorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
            _Logging.Debug(_Header + "initialized");
        }

        /// <summary>
        /// Discover tenants by email.
        /// </summary>
        internal async Task<ResponseContext> DiscoverTenantsAsync(RequestContext req, CancellationToken token = default)
        {
            TenantDiscoveryRequest? discovery = req.DeserializeBody<TenantDiscoveryRequest>();
            if (discovery == null || String.IsNullOrEmpty(discovery.Email))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Email is required");
            }

            List<Tenant> tenants = await _AuthService.DiscoverTenantsByEmailAsync(discovery.Email, token).ConfigureAwait(false);
            return new ResponseContext(req, tenants);
        }

        /// <summary>
        /// Login with tenant, email, and password.
        /// </summary>
        internal async Task<ResponseContext> LoginAsync(RequestContext req, CancellationToken token = default)
        {
            LoginRequest? login = req.DeserializeBody<LoginRequest>();
            if (login == null || String.IsNullOrEmpty(login.TenantId) || String.IsNullOrEmpty(login.Email) || String.IsNullOrEmpty(login.Password))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "TenantId, Email, and Password are required");
            }

            try
            {
                AuthSession session = await _AuthService.LoginAsync(login.TenantId, login.Email, login.Password, token).ConfigureAwait(false);
                User? user = await _Driver.Users.ReadAsync(login.TenantId, session.UserId ?? String.Empty, token).ConfigureAwait(false);
                Tenant? tenant = await _Driver.Tenants.ReadAsync(login.TenantId, token).ConfigureAwait(false);
                return new ResponseContext(req, new LoginResponse { Session = session, User = RedactUser(user), Tenant = tenant });
            }
            catch (UnauthorizedAccessException e)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.Unauthorized, null, e.Message);
            }
        }

        /// <summary>
        /// Logout current session.
        /// </summary>
        internal async Task<ResponseContext> LogoutAsync(RequestContext req, CancellationToken token = default)
        {
            if (req.Auth?.Session == null)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "No session is active");
            }

            await _Driver.AuthSessions.RevokeAsync(req.Auth.Session.TenantId, req.Auth.Session.Id, "Logout", token).ConfigureAwait(false);
            return new ResponseContext(req);
        }

        /// <summary>
        /// Enumerate tenants.
        /// </summary>
        internal async Task<ResponseContext> EnumerateTenantsAsync(RequestContext req, CancellationToken token = default)
        {
            ResponseContext? authz = await AuthorizeAsync(req, "Tenant", "Read", null, token).ConfigureAwait(false);
            if (authz != null) return authz;
            EnumerationResult<Tenant> result = await _Driver.Tenants.EnumerateAsync(ToEnumerationQuery(req), token).ConfigureAwait(false);
            return new ResponseContext(req, result);
        }

        /// <summary>
        /// Create a tenant.
        /// </summary>
        internal async Task<ResponseContext> CreateTenantAsync(RequestContext req, CancellationToken token = default)
        {
            ResponseContext? authz = await AuthorizeAsync(req, "Tenant", "Create", null, token).ConfigureAwait(false);
            if (authz != null) return authz;

            Tenant? tenant = req.DeserializeBody<Tenant>();
            if (tenant == null || String.IsNullOrEmpty(tenant.Name))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Tenant name is required");
            }

            tenant = await _Driver.Tenants.CreateAsync(tenant, token).ConfigureAwait(false);
            ResponseContext resp = new ResponseContext(req, tenant);
            resp.StatusCode = 201;
            return resp;
        }

        /// <summary>
        /// Read a tenant.
        /// </summary>
        internal async Task<ResponseContext> ReadTenantAsync(RequestContext req, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(req.TenantId)) return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Tenant ID is required");
            ResponseContext? authz = await AuthorizeAsync(req, "Tenant", "Read", req.TenantId, token).ConfigureAwait(false);
            if (authz != null) return authz;
            Tenant? tenant = await _Driver.Tenants.ReadAsync(req.TenantId, token).ConfigureAwait(false);
            if (tenant == null) return ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, "Tenant not found");
            return new ResponseContext(req, tenant);
        }

        /// <summary>
        /// Delete a tenant.
        /// </summary>
        internal async Task<ResponseContext> DeleteTenantAsync(RequestContext req, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(req.TenantId)) return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Tenant ID is required");
            ResponseContext? authz = await AuthorizeAsync(req, "Tenant", "Delete", req.TenantId, token).ConfigureAwait(false);
            if (authz != null) return authz;
            bool deleted = await _Driver.Tenants.DeleteAsync(req.TenantId, token).ConfigureAwait(false);
            if (!deleted) return ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, "Tenant not found");
            return new ResponseContext(req);
        }

        /// <summary>
        /// Enumerate users.
        /// </summary>
        internal async Task<ResponseContext> EnumerateUsersAsync(RequestContext req, CancellationToken token = default)
        {
            ResponseContext? authz = await AuthorizeAsync(req, "User", "Read", null, token).ConfigureAwait(false);
            if (authz != null) return authz;
            EnumerationResult<User> result = await _Driver.Users.EnumerateAsync(ToEnumerationQuery(req), token).ConfigureAwait(false);
            result.Objects = result.Objects.ConvertAll(RedactUser);
            return new ResponseContext(req, result);
        }

        /// <summary>
        /// Create a user.
        /// </summary>
        internal async Task<ResponseContext> CreateUserAsync(RequestContext req, CancellationToken token = default)
        {
            ResponseContext? authz = await AuthorizeAsync(req, "User", "Create", null, token).ConfigureAwait(false);
            if (authz != null) return authz;

            CreateUserRequest? create = req.DeserializeBody<CreateUserRequest>();
            if (create == null || String.IsNullOrEmpty(req.TenantId) || String.IsNullOrEmpty(create.Email) || String.IsNullOrEmpty(create.Password))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "TenantId, Email, and Password are required");
            }

            User user = new User
            {
                TenantId = req.TenantId,
                FirstName = create.FirstName,
                LastName = create.LastName,
                Email = create.Email,
                PasswordSha256 = AuthService.HashPasswordSha256(create.Password),
                IsAdmin = create.IsAdmin,
                IsTenantAdmin = create.IsTenantAdmin,
                Active = create.Active
            };

            user = await _Driver.Users.CreateAsync(user, token).ConfigureAwait(false);
            ResponseContext resp = new ResponseContext(req, RedactUser(user));
            resp.StatusCode = 201;
            return resp;
        }

        /// <summary>
        /// Read a user.
        /// </summary>
        internal async Task<ResponseContext> ReadUserAsync(RequestContext req, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(req.TenantId) || String.IsNullOrEmpty(req.UserId))
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Tenant ID and user ID are required");
            ResponseContext? authz = await AuthorizeAsync(req, "User", "Read", req.UserId, token).ConfigureAwait(false);
            if (authz != null) return authz;
            User? user = await _Driver.Users.ReadAsync(req.TenantId, req.UserId, token).ConfigureAwait(false);
            if (user == null) return ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, "User not found");
            return new ResponseContext(req, RedactUser(user));
        }

        /// <summary>
        /// Enumerate account-user maps.
        /// </summary>
        internal async Task<ResponseContext> EnumerateAccountUsersAsync(RequestContext req, CancellationToken token = default)
        {
            ResponseContext? authz = await AuthorizeAsync(req, "Account", "Read", req.AccountId, token).ConfigureAwait(false);
            if (authz != null) return authz;
            EnumerationResult<AccountUserMap> result = await _Driver.AccountUserMaps.EnumerateAsync(ToEnumerationQuery(req), token).ConfigureAwait(false);
            return new ResponseContext(req, result);
        }

        /// <summary>
        /// Map a user to an account.
        /// </summary>
        internal async Task<ResponseContext> MapAccountUserAsync(RequestContext req, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(req.TenantId) || String.IsNullOrEmpty(req.AccountId) || String.IsNullOrEmpty(req.UserId))
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Tenant ID, account ID, and user ID are required");
            ResponseContext? authz = await AuthorizeAsync(req, "Account", "Update", req.AccountId, token).ConfigureAwait(false);
            if (authz != null) return authz;
            AccountUserMap map = await _Driver.AccountUserMaps.CreateAsync(new AccountUserMap { TenantId = req.TenantId, AccountId = req.AccountId, UserId = req.UserId }, token).ConfigureAwait(false);
            return new ResponseContext(req, map);
        }

        /// <summary>
        /// Delete a user account mapping.
        /// </summary>
        internal async Task<ResponseContext> DeleteAccountUserAsync(RequestContext req, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(req.TenantId) || String.IsNullOrEmpty(req.AccountId) || String.IsNullOrEmpty(req.UserId))
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Tenant ID, account ID, and user ID are required");
            ResponseContext? authz = await AuthorizeAsync(req, "Account", "Update", req.AccountId, token).ConfigureAwait(false);
            if (authz != null) return authz;
            await _Driver.AccountUserMaps.DeleteAsync(req.TenantId, req.AccountId, req.UserId, token).ConfigureAwait(false);
            return new ResponseContext(req);
        }

        /// <summary>
        /// Enumerate sessions.
        /// </summary>
        internal async Task<ResponseContext> EnumerateSessionsAsync(RequestContext req, CancellationToken token = default)
        {
            ResponseContext? authz = await AuthorizeAsync(req, "Session", "Read", null, token).ConfigureAwait(false);
            if (authz != null) return authz;
            EnumerationResult<AuthSession> result = await _Driver.AuthSessions.EnumerateAsync(ToEnumerationQuery(req), token).ConfigureAwait(false);
            return new ResponseContext(req, result);
        }

        /// <summary>
        /// Revoke a session.
        /// </summary>
        internal async Task<ResponseContext> RevokeSessionAsync(RequestContext req, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(req.TenantId) || String.IsNullOrEmpty(req.SessionId))
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Tenant ID and session ID are required");
            ResponseContext? authz = await AuthorizeAsync(req, "Session", "Delete", req.SessionId, token).ConfigureAwait(false);
            if (authz != null) return authz;
            await _Driver.AuthSessions.RevokeAsync(req.TenantId, req.SessionId, "Admin revocation", token).ConfigureAwait(false);
            return new ResponseContext(req);
        }

        /// <summary>
        /// Enumerate audit records.
        /// </summary>
        internal async Task<ResponseContext> EnumerateAuditAsync(RequestContext req, CancellationToken token = default)
        {
            ResponseContext? authz = await AuthorizeAsync(req, "Audit", "Read", null, token).ConfigureAwait(false);
            if (authz != null) return authz;
            EnumerationResult<AuditRecord> result = await _Driver.AuditRecords.EnumerateAsync(ToEnumerationQuery(req), token).ConfigureAwait(false);
            return new ResponseContext(req, result);
        }

        /// <summary>
        /// Enumerate roles.
        /// </summary>
        internal async Task<ResponseContext> EnumerateRolesAsync(RequestContext req, CancellationToken token = default)
        {
            ResponseContext? authz = await AuthorizeAsync(req, "Role", "Read", null, token).ConfigureAwait(false);
            if (authz != null) return authz;
            EnumerationResult<UserRole> result = await _Driver.Rbac.EnumerateRolesAsync(ToEnumerationQuery(req), token).ConfigureAwait(false);
            return new ResponseContext(req, result);
        }

        /// <summary>
        /// Create a tenant-scoped custom role.
        /// </summary>
        internal async Task<ResponseContext> CreateRoleAsync(RequestContext req, CancellationToken token = default)
        {
            ResponseContext? authz = await AuthorizeAsync(req, "Role", "Create", null, token).ConfigureAwait(false);
            if (authz != null) return authz;
            UserRole? role = req.DeserializeBody<UserRole>();
            if (role == null || String.IsNullOrEmpty(req.TenantId) || String.IsNullOrEmpty(role.Name))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Tenant ID and role name are required");
            }

            role.TenantId = req.TenantId;
            role.IsBuiltIn = false;
            role.IsProtected = false;
            role = await _Driver.Rbac.CreateRoleAsync(role, token).ConfigureAwait(false);
            ResponseContext resp = new ResponseContext(req, role);
            resp.StatusCode = 201;
            return resp;
        }

        /// <summary>
        /// Enumerate permissions.
        /// </summary>
        internal async Task<ResponseContext> EnumeratePermissionsAsync(RequestContext req, CancellationToken token = default)
        {
            ResponseContext? authz = await AuthorizeAsync(req, "Permission", "Read", null, token).ConfigureAwait(false);
            if (authz != null) return authz;
            EnumerationResult<Permission> result = await _Driver.Rbac.EnumeratePermissionsAsync(ToEnumerationQuery(req), token).ConfigureAwait(false);
            return new ResponseContext(req, result);
        }

        /// <summary>
        /// Create a tenant-scoped custom permission.
        /// </summary>
        internal async Task<ResponseContext> CreatePermissionAsync(RequestContext req, CancellationToken token = default)
        {
            ResponseContext? authz = await AuthorizeAsync(req, "Permission", "Create", null, token).ConfigureAwait(false);
            if (authz != null) return authz;
            Permission? permission = req.DeserializeBody<Permission>();
            if (permission == null || String.IsNullOrEmpty(req.TenantId) || String.IsNullOrEmpty(permission.Name))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Tenant ID and permission name are required");
            }

            permission.TenantId = req.TenantId;
            permission.IsProtected = false;
            permission = await _Driver.Rbac.CreatePermissionAsync(permission, token).ConfigureAwait(false);
            ResponseContext resp = new ResponseContext(req, permission);
            resp.StatusCode = 201;
            return resp;
        }

        /// <summary>
        /// Assign a role to a user.
        /// </summary>
        internal async Task<ResponseContext> AssignUserRoleAsync(RequestContext req, CancellationToken token = default)
        {
            ResponseContext? authz = await AuthorizeAsync(req, "Assignment", "Create", null, token).ConfigureAwait(false);
            if (authz != null) return authz;
            UserRoleAssignment? assignment = req.DeserializeBody<UserRoleAssignment>();
            if (assignment == null || String.IsNullOrEmpty(req.TenantId) || String.IsNullOrEmpty(req.UserId))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Tenant ID and user ID are required");
            }

            assignment.TenantId = req.TenantId;
            assignment.UserId = req.UserId;
            assignment = await _Driver.Rbac.CreateUserRoleAssignmentAsync(assignment, token).ConfigureAwait(false);
            ResponseContext resp = new ResponseContext(req, assignment);
            resp.StatusCode = 201;
            return resp;
        }

        /// <summary>
        /// Map a permission to a role.
        /// </summary>
        internal async Task<ResponseContext> MapRolePermissionAsync(RequestContext req, CancellationToken token = default)
        {
            ResponseContext? authz = await AuthorizeAsync(req, "Assignment", "Create", null, token).ConfigureAwait(false);
            if (authz != null) return authz;
            string? roleId = req.UrlParameters["roleId"];
            string? permissionId = req.UrlParameters["permissionId"];
            if (String.IsNullOrEmpty(req.TenantId) || String.IsNullOrEmpty(roleId) || String.IsNullOrEmpty(permissionId))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Tenant ID, role ID, and permission ID are required");
            }

            RolePermissionMap map = await _Driver.Rbac.CreateRolePermissionMapAsync(new RolePermissionMap
            {
                TenantId = req.TenantId,
                RoleId = roleId,
                PermissionId = permissionId
            }, token).ConfigureAwait(false);
            ResponseContext resp = new ResponseContext(req, map);
            resp.StatusCode = 201;
            return resp;
        }

        /// <summary>
        /// Get current effective permissions.
        /// </summary>
        internal Task<ResponseContext> GetEffectivePermissionsAsync(RequestContext req, CancellationToken token = default)
        {
            return Task.FromResult(new ResponseContext(req, _AuthorizationService.GetEffectivePermissions(req)));
        }

        private async Task<ResponseContext?> AuthorizeAsync(RequestContext req, string resourceType, string operationType, string? resourceId, CancellationToken token)
        {
            AuthorizationDecision decision = await _AuthorizationService.AuthorizeAsync(req, resourceType, operationType, resourceId, token).ConfigureAwait(false);
            if (decision.Permitted) return null;
            ApiErrorEnum error = String.Equals(decision.Reason, "Authentication required", StringComparison.Ordinal)
                ? ApiErrorEnum.Unauthorized
                : ApiErrorEnum.Forbidden;
            return ResponseContext.FromError(req, error, null, decision.Reason);
        }

        private EnumerationQuery ToEnumerationQuery(RequestContext req)
        {
            return new EnumerationQuery
            {
                TenantId = ResolveEnumerationTenant(req),
                MaxResults = req.MaxResults,
                Skip = req.Skip,
                ContinuationToken = req.ContinuationToken,
                SearchTerm = req.SearchTerm,
                Ordering = req.Ordering
            };
        }

        private string? ResolveEnumerationTenant(RequestContext req)
        {
            if (req.Auth?.IsAdmin == true) return req.TenantId;
            if (req.Auth?.IsAuthenticated == true) return req.TenantId ?? req.Auth.TenantId;
            return req.TenantId;
        }

        private User RedactUser(User? user)
        {
            if (user == null) return new User();
            user.PasswordSha256 = String.Empty;
            return user;
        }

    }
}
