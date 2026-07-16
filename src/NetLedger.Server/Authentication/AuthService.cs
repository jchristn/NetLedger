namespace NetLedger.Server.Authentication
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger;
    using NetLedger.Database;
    using NetLedger.Server.Settings;
    using SyslogLogging;
    using WatsonWebserver.Core;

    /// <summary>
    /// Authentication service for validating API keys.
    /// </summary>
    public class AuthService : IDisposable
    {
        #region Private-Members

        private readonly string _Header = "[AuthService] ";
        private readonly ServerSettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly DatabaseDriverBase _Driver;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the authentication service.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="driver">Database driver.</param>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
        public AuthService(ServerSettings settings, LoggingModule logging, DatabaseDriverBase driver)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));

            InitializeFactoryDefaultsAsync().Wait();

            _Logging.Debug(_Header + "initialized");
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Authenticate a request using the Authorization header.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Authentication context.</returns>
        public async Task<AuthContext> AuthenticateAsync(HttpContextBase ctx, CancellationToken token = default)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            // If authentication is disabled, allow all requests
            if (!_Settings.Authentication.Enabled)
            {
                return AuthContext.NotRequired();
            }

            string? authHeader = ctx.Request.Headers.Get(Constants.AuthorizationHeader);
            string? xToken = ctx.Request.Headers.Get("x-token");
            string? accessKey = ctx.Request.Headers.Get("x-access-key");
            string? secretKey = ctx.Request.Headers.Get("x-secret-key");

            if (string.IsNullOrEmpty(authHeader))
            {
                if (!String.IsNullOrEmpty(xToken))
                {
                    return await AuthenticateTokenAsync(xToken, ctx, token).ConfigureAwait(false);
                }

                if (!String.IsNullOrEmpty(accessKey))
                {
                    return await AuthenticateAccessKeyAsync(accessKey, secretKey, ctx, token).ConfigureAwait(false);
                }

                return AuthContext.Failed(AuthResult.NoCredentials, "No credentials provided");
            }

            if (!authHeader.StartsWith(Constants.BearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return AuthContext.Failed(AuthResult.InvalidApiKey, "Invalid Authorization header format. Expected: Bearer <token>");
            }

            string tokenValue = authHeader.Substring(Constants.BearerPrefix.Length).Trim();
            if (string.IsNullOrEmpty(tokenValue))
            {
                return AuthContext.Failed(AuthResult.InvalidApiKey, "Empty bearer token");
            }

            if (!String.IsNullOrEmpty(xToken) && !String.Equals(xToken, tokenValue, StringComparison.Ordinal))
            {
                return AuthContext.Failed(AuthResult.InvalidApiKey, "Authorization bearer token and x-token disagree");
            }

            AuthContext sessionAuth = await AuthenticateTokenAsync(tokenValue, ctx, token).ConfigureAwait(false);
            if (sessionAuth.IsAuthenticated)
            {
                return sessionAuth;
            }

            return await AuthenticateAccessKeyAsync(tokenValue, null, ctx, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Discover tenants for an email address.
        /// </summary>
        /// <param name="email">Email address.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Tenants with a matching active user.</returns>
        public async Task<List<Tenant>> DiscoverTenantsByEmailAsync(string email, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(email)) throw new ArgumentNullException(nameof(email));

            EnumerationResult<User> users = await _Driver.Users.EnumerateAsync(
                new EnumerationQuery
                {
                    MaxResults = 1000,
                    SearchTerm = email.Trim().ToLowerInvariant()
                },
                token).ConfigureAwait(false);

            List<Tenant> tenants = new List<Tenant>();
            foreach (User user in users.Objects)
            {
                if (!user.Active) continue;
                if (!String.Equals(user.Email, email.Trim().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)) continue;

                Tenant? tenant = await _Driver.Tenants.ReadAsync(user.TenantId, token).ConfigureAwait(false);
                if (tenant != null && tenant.Active && !tenants.Any(t => t.Id == tenant.Id))
                {
                    tenants.Add(tenant);
                }
            }

            return tenants;
        }

        /// <summary>
        /// Authenticate a user with email and password into a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="email">Email address.</param>
        /// <param name="password">Plaintext password.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created authentication session and user.</returns>
        public async Task<AuthSession> LoginAsync(string tenantId, string email, string password, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(email)) throw new ArgumentNullException(nameof(email));
            if (String.IsNullOrEmpty(password)) throw new ArgumentNullException(nameof(password));

            Tenant? tenant = await _Driver.Tenants.ReadAsync(tenantId, token).ConfigureAwait(false);
            if (tenant == null || !tenant.Active) throw new UnauthorizedAccessException("Tenant not found or inactive.");

            User? user = await _Driver.Users.ReadByEmailAsync(tenantId, email, token).ConfigureAwait(false);
            if (user == null || !user.Active) throw new UnauthorizedAccessException("Invalid email, password, or tenant.");

            string expectedHash = user.PasswordSha256;
            string actualHash = HashPasswordSha256(password);
            if (!ConstantTimeEquals(expectedHash, actualHash))
            {
                await WriteAuditAsync(tenantId, user.Id, "User", "Login", null, null, null, "Denied", "Invalid password", null, token).ConfigureAwait(false);
                throw new UnauthorizedAccessException("Invalid email, password, or tenant.");
            }

            AuthSession session = new AuthSession
            {
                TenantId = tenantId,
                UserId = user.Id,
                ExpiresUtc = DateTime.UtcNow.AddHours(12)
            };

            session = await _Driver.AuthSessions.CreateAsync(session, token).ConfigureAwait(false);
            await WriteAuditAsync(tenantId, user.Id, "User", "Login", "Session", "Create", session.Id, "Permit", null, null, token).ConfigureAwait(false);
            return session;
        }

        /// <summary>
        /// Hash a password using SHA-256.
        /// </summary>
        /// <param name="password">Password.</param>
        /// <returns>Hex-encoded hash.</returns>
        public static string HashPasswordSha256(string password)
        {
            if (String.IsNullOrEmpty(password)) throw new ArgumentNullException(nameof(password));
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        /// <summary>
        /// Compare two hex hashes in constant time.
        /// </summary>
        /// <param name="expected">Expected hash.</param>
        /// <param name="actual">Actual hash.</param>
        /// <returns>True if equal.</returns>
        public static bool ConstantTimeEquals(string expected, string actual)
        {
            if (String.IsNullOrEmpty(expected) || String.IsNullOrEmpty(actual)) return false;
            byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
            byte[] actualBytes = Encoding.UTF8.GetBytes(actual);
            return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
        }

        private async Task<AuthContext> AuthenticateTokenAsync(string tokenValue, HttpContextBase ctx, CancellationToken token)
        {
            AuthSession? session = await _Driver.AuthSessions.ReadByTokenAsync(tokenValue, token).ConfigureAwait(false);
            if (session == null)
            {
                return AuthContext.Failed(AuthResult.InvalidApiKey, "Invalid bearer token");
            }

            if (!session.Active || session.ExpiresUtc <= DateTime.UtcNow)
            {
                return AuthContext.Failed(AuthResult.InactiveSession, "Session is inactive or expired");
            }

            if (String.IsNullOrEmpty(session.UserId))
            {
                return AuthContext.Failed(AuthResult.InvalidApiKey, "Session does not contain a user principal");
            }

            User? user = await _Driver.Users.ReadAsync(session.TenantId, session.UserId, token).ConfigureAwait(false);
            if (user == null || !user.Active)
            {
                return AuthContext.Failed(AuthResult.InvalidApiKey, "Session user is not active");
            }

            string? tenantHint = GetTenantHint(ctx);
            if (!user.IsAdmin && !String.IsNullOrEmpty(tenantHint) && !String.Equals(tenantHint, session.TenantId, StringComparison.Ordinal))
            {
                return AuthContext.Failed(AuthResult.InvalidApiKey, "Session tenant and request tenant disagree");
            }

            return AuthContext.Success(user, session);
        }

        private async Task<AuthContext> AuthenticateAccessKeyAsync(string accessKey, string? secretKey, HttpContextBase ctx, CancellationToken token)
        {
            if (String.IsNullOrEmpty(accessKey)) return AuthContext.Failed(AuthResult.NoCredentials, "Access key is required");

            ApiKey? apiKey = await _Driver.ApiKeys.ReadByKeyAsync(accessKey, token).ConfigureAwait(false);
            if (apiKey == null)
            {
                _Logging.Warn(_Header + "invalid API key attempt from " + ctx.Request.Source.IpAddress);
                return AuthContext.Failed(AuthResult.InvalidApiKey, "Invalid API key");
            }

            if (!apiKey.Active)
            {
                _Logging.Warn(_Header + "inactive API key attempt from " + ctx.Request.Source.IpAddress);
                return AuthContext.Failed(AuthResult.InactiveApiKey, "API key is inactive");
            }

            if (!String.IsNullOrEmpty(secretKey) && !String.IsNullOrEmpty(apiKey.SecretKeySha256))
            {
                if (!ConstantTimeEquals(apiKey.SecretKeySha256, Credential.HashSecret(secretKey)))
                {
                    return AuthContext.Failed(AuthResult.InvalidApiKey, "Invalid credential secret");
                }
            }

            User? user = null;
            if (!String.IsNullOrEmpty(apiKey.TenantId) && !String.IsNullOrEmpty(apiKey.UserId))
            {
                user = await _Driver.Users.ReadAsync(apiKey.TenantId, apiKey.UserId, token).ConfigureAwait(false);
                if (user == null || !user.Active)
                {
                    return AuthContext.Failed(AuthResult.InvalidApiKey, "Credential user is not active");
                }
            }

            string? tenantHint = GetTenantHint(ctx);
            bool credentialIsSystemAdmin = apiKey.IsAdmin || (user?.IsAdmin ?? false);
            if (!credentialIsSystemAdmin && !String.IsNullOrEmpty(tenantHint) && !String.IsNullOrEmpty(apiKey.TenantId) && !String.Equals(tenantHint, apiKey.TenantId, StringComparison.Ordinal))
            {
                return AuthContext.Failed(AuthResult.InvalidApiKey, "Credential tenant and request tenant disagree");
            }

            return AuthContext.Success(apiKey, user);
        }

        /// <summary>
        /// Create a new API key.
        /// </summary>
        /// <param name="name">Display name for the key.</param>
        /// <param name="isAdmin">Whether this is an admin key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created API key.</returns>
        public async Task<ApiKey> CreateApiKeyAsync(
            string name,
            bool isAdmin = false,
            string? tenantId = null,
            string? userId = null,
            CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

            ApiKey apiKey = new ApiKey(name, false)
            {
                TenantId = tenantId ?? String.Empty,
                UserId = userId ?? String.Empty
            };
            string secretKey = NetLedgerId.Generate("key_");
            apiKey.SecretKeySha256 = Credential.HashSecret(secretKey);
            apiKey.SecretKeyLast4 = secretKey.Substring(secretKey.Length - 4);
            apiKey.RawSecretKey = secretKey;
            apiKey = await _Driver.ApiKeys.CreateAsync(apiKey, token).ConfigureAwait(false);
            apiKey.RawSecretKey = secretKey;

            _Logging.Info(_Header + "created credential: " + apiKey.Id + " (" + name + ")");
            return apiKey;
        }

        /// <summary>
        /// Get an API key by its identifier.
        /// </summary>
        /// <param name="id">API key identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>API key or null if not found.</returns>
        public async Task<ApiKey?> GetApiKeyByIdAsync(string id, CancellationToken token = default)
        {
            return await _Driver.ApiKeys.ReadByIdAsync(id, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get an API key by its value.
        /// </summary>
        /// <param name="key">API key value.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>API key or null if not found.</returns>
        public async Task<ApiKey?> GetApiKeyByValueAsync(string key, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(key)) return null;
            return await _Driver.ApiKeys.ReadByKeyAsync(key, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get all API keys.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of API keys.</returns>
        public async Task<List<ApiKey>> GetAllApiKeysAsync(CancellationToken token = default)
        {
            return await _Driver.ApiKeys.ReadAllAsync(token).ConfigureAwait(false);
        }

        /// <summary>
        /// Enumerate API keys with pagination.
        /// </summary>
        /// <param name="query">Enumeration query containing pagination parameters and filters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result containing the page of API keys and metadata for continuing the enumeration.</returns>
        /// <exception cref="ArgumentNullException">Thrown when query is null.</exception>
        /// <exception cref="ArgumentException">Thrown when skip and continuation token are both specified.</exception>
        public async Task<EnumerationResult<ApiKey>> EnumerateApiKeysAsync(
            Models.ApiKeyEnumerationQuery query,
            CancellationToken token = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (!String.IsNullOrEmpty(query.ContinuationToken) && query.Skip > 0)
                throw new ArgumentException("Skip count and enumeration tokens cannot be used in the same enumeration request.");

            // Convert to the core EnumerationQuery
            EnumerationQuery coreQuery = new EnumerationQuery
            {
                MaxResults = query.MaxResults,
                Skip = query.Skip,
                ContinuationToken = query.ContinuationToken,
                Ordering = query.Ordering,
                SearchTerm = query.SearchTerm,
                TenantId = query.TenantId,
                UserId = query.UserId,
                CreatedAfterUtc = query.CreatedAfterUtc,
                CreatedBeforeUtc = query.CreatedBeforeUtc
            };

            EnumerationResult<ApiKey> result = await _Driver.ApiKeys.EnumerateAsync(coreQuery, token).ConfigureAwait(false);

            // Redact keys in the result
            if (result.Objects != null)
            {
                result.Objects = result.Objects.Select(k => k.Redact()).ToList();
            }

            return result;
        }

        /// <summary>
        /// Revoke (delete) an API key.
        /// </summary>
        /// <param name="id">API key identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if deleted, false if not found.</returns>
        public async Task<bool> RevokeApiKeyAsync(string id, CancellationToken token = default)
        {
            ApiKey? apiKey = await _Driver.ApiKeys.ReadByIdAsync(id, token).ConfigureAwait(false);
            if (apiKey == null) return false;

            await _Driver.ApiKeys.DeleteByIdAsync(id, token).ConfigureAwait(false);
            _Logging.Info(_Header + "revoked API key: " + id);
            return true;
        }

        /// <summary>
        /// Dispose resources.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            _Logging.Debug(_Header + "disposed");
        }

        #endregion

        #region Private-Methods

        private string? GetTenantHint(HttpContextBase ctx)
        {
            string? tenantHeader = ctx.Request.Headers.Get("x-tenant-id");
            string? tenantRoute = null;

            if (ctx.Request.Url.Parameters != null)
            {
                tenantRoute = ctx.Request.Url.Parameters["tenantId"];
            }

            string? first = !String.IsNullOrEmpty(tenantRoute) ? tenantRoute : tenantHeader;

            if (!String.IsNullOrEmpty(first) && !String.IsNullOrEmpty(tenantHeader) && !String.Equals(first, tenantHeader, StringComparison.Ordinal))
            {
                return "__conflict__";
            }

            return first;
        }

        private async Task WriteAuditAsync(
            string tenantId,
            string? principalId,
            string? principalType,
            string eventType,
            string? resourceType,
            string? operationType,
            string? resourceId,
            string result,
            string? reason,
            string? requestId,
            CancellationToken token)
        {
            AuditRecord record = new AuditRecord
            {
                TenantId = tenantId,
                PrincipalId = principalId,
                PrincipalType = principalType,
                EventType = eventType,
                ResourceType = resourceType,
                OperationType = operationType,
                ResourceId = resourceId,
                Result = result,
                Reason = reason,
                RequestId = requestId
            };

            await _Driver.AuditRecords.CreateAsync(record, token).ConfigureAwait(false);
        }

        private async Task InitializeDefaultApiKeyAsync()
        {
            // Check if any API keys exist
            List<ApiKey> existingKeys = await _Driver.ApiKeys.ReadAllAsync().ConfigureAwait(false);

            if (existingKeys.Count == 0)
            {
                // Create default admin API key
                string keyValue = _Settings.Authentication.DefaultAdminKey ?? ApiKey.GenerateApiKey();
                ApiKey defaultKey = new ApiKey("Default Admin", true)
                {
                Key = keyValue
            };
            string secretKey = NetLedgerId.Generate("key_");
            defaultKey.SecretKeySha256 = Credential.HashSecret(secretKey);
            defaultKey.SecretKeyLast4 = secretKey.Substring(secretKey.Length - 4);

            await _Driver.ApiKeys.CreateAsync(defaultKey).ConfigureAwait(false);

                _Logging.Alert(_Header + "created default admin API key: " + keyValue);
                _Logging.Alert(_Header + "IMPORTANT: save this API key, it will not be shown again!");
            }
        }

        private async Task InitializeFactoryDefaultsAsync()
        {
            const string defaultTenantId = "default";
            const string defaultAdminEmail = "admin@netledger";
            const string defaultAdminPassword = "password";
            const string defaultAdminUserId = "usr_default_admin";
            const string defaultCredentialId = "cred_default";
            const string defaultAccessKey = "default";
            const string defaultSecretKey = "default";
            const string defaultAccountId = "acct_default";

            EnumerationResult<Tenant> existingTenants = await _Driver.Tenants.EnumerateAsync(new EnumerationQuery { MaxResults = 1 }).ConfigureAwait(false);
            bool seedFullFactoryDefaults = existingTenants.TotalRecords == 0;

            if (seedFullFactoryDefaults)
            {
                Tenant tenant = new Tenant
                {
                    Id = defaultTenantId,
                    Name = "Default",
                    Active = true,
                    IsProtected = true
                };

                await _Driver.Tenants.CreateAsync(tenant).ConfigureAwait(false);
                _Logging.Alert(_Header + "created default tenant: " + defaultTenantId);
            }

            User? admin = await _Driver.Users.ReadByEmailAsync(defaultTenantId, defaultAdminEmail).ConfigureAwait(false);
            if (seedFullFactoryDefaults && admin == null)
            {
                admin = new User
                {
                    Id = defaultAdminUserId,
                    TenantId = defaultTenantId,
                    FirstName = "Default",
                    LastName = "Admin",
                    Email = defaultAdminEmail,
                    PasswordSha256 = HashPasswordSha256(defaultAdminPassword),
                    IsAdmin = true,
                    IsTenantAdmin = true,
                    Active = true,
                    IsProtected = true
                };

                await _Driver.Users.CreateAsync(admin).ConfigureAwait(false);
                _Logging.Alert(_Header + "created default admin user: " + defaultAdminEmail);
                _Logging.Alert(_Header + "IMPORTANT: change the default admin password after first login!");
            }
            else if (admin != null && (!admin.IsAdmin || !admin.IsTenantAdmin || !admin.Active || !admin.IsProtected))
            {
                admin.IsAdmin = true;
                admin.IsTenantAdmin = true;
                admin.Active = true;
                admin.IsProtected = true;
                await _Driver.Users.UpdateAsync(admin).ConfigureAwait(false);
                _Logging.Alert(_Header + "repaired default admin user privileges: " + defaultAdminEmail);
            }

            if (admin != null)
            {
                ApiKey? defaultCredential = await _Driver.ApiKeys.ReadByKeyAsync(defaultAccessKey).ConfigureAwait(false);
                if (defaultCredential == null)
                {
                    defaultCredential = new ApiKey("Default User Credential", false)
                    {
                        Id = defaultCredentialId,
                        TenantId = defaultTenantId,
                        UserId = admin.Id,
                        Key = defaultAccessKey,
                        Active = true,
                        IsAdmin = false,
                        SecretKeySha256 = Credential.HashSecret(defaultSecretKey),
                        SecretKeyLast4 = defaultSecretKey.Substring(defaultSecretKey.Length - 4)
                    };

                    await _Driver.ApiKeys.CreateAsync(defaultCredential).ConfigureAwait(false);
                    _Logging.Alert(_Header + "created default credential: " + defaultAccessKey);
                }
            }

            if (seedFullFactoryDefaults && admin != null)
            {
                Account? defaultAccount = await _Driver.Accounts.ReadByIdAsync(defaultAccountId).ConfigureAwait(false);
                if (defaultAccount == null)
                {
                    defaultAccount = new Account
                    {
                        Id = defaultAccountId,
                        TenantId = defaultTenantId,
                        Name = "Default Account",
                        Notes = "Factory default account",
                        Active = true
                    };

                    await _Driver.Accounts.CreateAsync(defaultAccount).ConfigureAwait(false);
                    _Logging.Alert(_Header + "created default account: " + defaultAccountId);
                }

                bool accountMapped = await _Driver.AccountUserMaps.ExistsAsync(defaultTenantId, defaultAccountId, admin.Id).ConfigureAwait(false);
                if (!accountMapped)
                {
                    AccountUserMap map = new AccountUserMap
                    {
                        TenantId = defaultTenantId,
                        AccountId = defaultAccountId,
                        UserId = admin.Id
                    };

                    await _Driver.AccountUserMaps.CreateAsync(map).ConfigureAwait(false);
                    _Logging.Alert(_Header + "mapped default admin user to default account");
                }
            }
        }

        #endregion
    }
}
