namespace NetLedger.Server.Authentication
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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

            InitializeDefaultApiKeyAsync().Wait();

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

            // Extract Authorization header
            string? authHeader = ctx.Request.Headers.Get(Constants.AuthorizationHeader);
            if (string.IsNullOrEmpty(authHeader))
            {
                return AuthContext.Failed(AuthResult.NoCredentials, "No Authorization header provided");
            }

            // Check for Bearer token
            if (!authHeader.StartsWith(Constants.BearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return AuthContext.Failed(AuthResult.InvalidApiKey, "Invalid Authorization header format. Expected: Bearer <api-key>");
            }

            string apiKeyValue = authHeader.Substring(Constants.BearerPrefix.Length).Trim();
            if (string.IsNullOrEmpty(apiKeyValue))
            {
                return AuthContext.Failed(AuthResult.InvalidApiKey, "Empty API key");
            }

            // Look up API key
            ApiKey? apiKey = await _Driver.ApiKeys.ReadByKeyAsync(apiKeyValue, token).ConfigureAwait(false);
            if (apiKey == null)
            {
                // Check if this matches the default admin key from settings
                if (apiKeyValue == _Settings.Authentication.DefaultAdminKey)
                {
                    ApiKey defaultAdminKey = new ApiKey("Default Admin (Settings)", true)
                    {
                        Key = apiKeyValue
                    };
                    return AuthContext.Success(defaultAdminKey);
                }

                _Logging.Warn(_Header + "invalid API key attempt from " + ctx.Request.Source.IpAddress);
                return AuthContext.Failed(AuthResult.InvalidApiKey, "Invalid API key");
            }

            if (!apiKey.Active)
            {
                _Logging.Warn(_Header + "inactive API key attempt from " + ctx.Request.Source.IpAddress);
                return AuthContext.Failed(AuthResult.InactiveApiKey, "API key is inactive");
            }

            return AuthContext.Success(apiKey);
        }

        /// <summary>
        /// Create a new API key.
        /// </summary>
        /// <param name="name">Display name for the key.</param>
        /// <param name="isAdmin">Whether this is an admin key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created API key.</returns>
        public async Task<ApiKey> CreateApiKeyAsync(string name, bool isAdmin = false, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

            ApiKey apiKey = new ApiKey(name, isAdmin);
            apiKey = await _Driver.ApiKeys.CreateAsync(apiKey, token).ConfigureAwait(false);

            _Logging.Info(_Header + "created API key: " + apiKey.GUID + " (" + name + ")");
            return apiKey;
        }

        /// <summary>
        /// Get an API key by its GUID.
        /// </summary>
        /// <param name="guid">API key GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>API key or null if not found.</returns>
        public async Task<ApiKey?> GetApiKeyByGuidAsync(Guid guid, CancellationToken token = default)
        {
            return await _Driver.ApiKeys.ReadByGuidAsync(guid, token).ConfigureAwait(false);
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
            if (query.ContinuationToken != null && query.Skip > 0)
                throw new ArgumentException("Skip count and enumeration tokens cannot be used in the same enumeration request.");

            // Convert to the core EnumerationQuery
            EnumerationQuery coreQuery = new EnumerationQuery
            {
                MaxResults = query.MaxResults,
                Skip = query.Skip,
                ContinuationToken = query.ContinuationToken,
                Ordering = query.Ordering,
                SearchTerm = query.SearchTerm,
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
        /// <param name="guid">API key GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if deleted, false if not found.</returns>
        public async Task<bool> RevokeApiKeyAsync(Guid guid, CancellationToken token = default)
        {
            ApiKey? apiKey = await _Driver.ApiKeys.ReadByGuidAsync(guid, token).ConfigureAwait(false);
            if (apiKey == null) return false;

            await _Driver.ApiKeys.DeleteByGuidAsync(guid, token).ConfigureAwait(false);
            _Logging.Info(_Header + "revoked API key: " + guid);
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

                await _Driver.ApiKeys.CreateAsync(defaultKey).ConfigureAwait(false);

                _Logging.Alert(_Header + "created default admin API key: " + keyValue);
                _Logging.Alert(_Header + "IMPORTANT: save this API key, it will not be shown again!");
            }
        }

        #endregion
    }
}
