namespace NetLedger.Server.Authentication
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Durable;
    using Durable.Sqlite;
    using NetLedger;
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
        private readonly SqliteConnectionFactory _ConnectionFactory;
        private readonly SqliteRepository<ApiKey> _ApiKeyRepository;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the authentication service.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="logging">Logging module.</param>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
        public AuthService(ServerSettings settings, LoggingModule logging)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));

            ConnectionPoolOptions poolOptions = new ConnectionPoolOptions
            {
                MaxPoolSize = 500,
                ConnectionTimeout = TimeSpan.FromSeconds(120)
            };

            _ConnectionFactory = new SqliteConnectionFactory(
                $"Data Source={_Settings.Database.Filename}",
                poolOptions);

            _ApiKeyRepository = new SqliteRepository<ApiKey>(_ConnectionFactory);

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
            ApiKey? apiKey = await GetApiKeyByValueAsync(apiKeyValue, token).ConfigureAwait(false);
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
            await _ApiKeyRepository.CreateAsync(apiKey, null, token).ConfigureAwait(false);

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
            List<ApiKey> keys = (await _ApiKeyRepository.Query()
                .Where(k => k.GUID == guid)
                .ExecuteAsync(token)
                .ConfigureAwait(false)).ToList();

            return keys.Count > 0 ? keys[0] : null;
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

            List<ApiKey> keys = (await _ApiKeyRepository.Query()
                .Where(k => k.Key == key)
                .ExecuteAsync(token)
                .ConfigureAwait(false)).ToList();

            return keys.Count > 0 ? keys[0] : null;
        }

        /// <summary>
        /// Get all API keys.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of API keys.</returns>
        public async Task<List<ApiKey>> GetAllApiKeysAsync(CancellationToken token = default)
        {
            return (await _ApiKeyRepository.Query()
                .OrderByDescending(k => k.CreatedUtc)
                .ExecuteAsync(token)
                .ConfigureAwait(false)).ToList();
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

            EnumerationResult<ApiKey> result = new EnumerationResult<ApiKey>
            {
                MaxResults = query.MaxResults,
                Skip = query.Skip
            };

            // Build base query for total count
            IQueryBuilder<ApiKey> countQuery = _ApiKeyRepository.Query()
                .Where(k => k.Id > 0);

            // Apply search filter
            if (!string.IsNullOrEmpty(query.SearchTerm))
                countQuery = countQuery.Where(k => k.Name.Contains(query.SearchTerm));

            // Apply date filters
            if (query.CreatedAfterUtc != null)
                countQuery = countQuery.Where(k => k.CreatedUtc >= query.CreatedAfterUtc.Value);

            if (query.CreatedBeforeUtc != null)
                countQuery = countQuery.Where(k => k.CreatedUtc <= query.CreatedBeforeUtc.Value);

            List<ApiKey> totalKeys = (await countQuery.ExecuteAsync(token).ConfigureAwait(false)).ToList();
            result.TotalRecords = totalKeys.Count;

            // Build query for this page
            IQueryBuilder<ApiKey> pageQuery = _ApiKeyRepository.Query()
                .Where(k => k.Id > 0);

            // Apply search filter
            if (!string.IsNullOrEmpty(query.SearchTerm))
                pageQuery = pageQuery.Where(k => k.Name.Contains(query.SearchTerm));

            // Apply date filters
            if (query.CreatedAfterUtc != null)
                pageQuery = pageQuery.Where(k => k.CreatedUtc >= query.CreatedAfterUtc.Value);

            if (query.CreatedBeforeUtc != null)
                pageQuery = pageQuery.Where(k => k.CreatedUtc <= query.CreatedBeforeUtc.Value);

            // Handle continuation token
            if (query.ContinuationToken != null)
            {
                ApiKey? continuationKey = await GetApiKeyByGuidAsync(query.ContinuationToken.Value, token).ConfigureAwait(false);
                if (continuationKey != null)
                {
                    if (query.Ordering == EnumerationOrderEnum.CreatedDescending)
                        pageQuery = pageQuery.Where(k => k.CreatedUtc < continuationKey.CreatedUtc);
                    else if (query.Ordering == EnumerationOrderEnum.CreatedAscending)
                        pageQuery = pageQuery.Where(k => k.CreatedUtc > continuationKey.CreatedUtc);
                }
            }

            // Apply ordering
            switch (query.Ordering)
            {
                case EnumerationOrderEnum.CreatedAscending:
                    pageQuery = pageQuery.OrderBy(k => k.CreatedUtc);
                    break;
                case EnumerationOrderEnum.CreatedDescending:
                default:
                    pageQuery = pageQuery.OrderByDescending(k => k.CreatedUtc);
                    break;
            }

            // Handle skip
            if (query.Skip > 0 && query.ContinuationToken == null)
            {
                pageQuery = pageQuery.Skip(query.Skip);
            }

            // Count remaining records
            List<ApiKey> remainingKeys = (await pageQuery.ExecuteAsync(token).ConfigureAwait(false)).ToList();
            result.RecordsRemaining = remainingKeys.Count;

            // Get the actual page
            pageQuery = pageQuery.Take(query.MaxResults);
            List<ApiKey> keys = (await pageQuery.ExecuteAsync(token).ConfigureAwait(false)).ToList();
            result.IterationsRequired = 1;

            // Redact keys
            result.Objects = keys.Select(k => k.Redact()).ToList();

            result.RecordsRemaining -= result.Objects.Count;

            if (result.Objects != null
                && result.Objects.Count > 0
                && result.RecordsRemaining > 0)
            {
                result.EndOfResults = false;
                result.ContinuationToken = result.Objects.Last().GUID;
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
            ApiKey? apiKey = await GetApiKeyByGuidAsync(guid, token).ConfigureAwait(false);
            if (apiKey == null) return false;

            await _ApiKeyRepository.DeleteAsync(apiKey, null, token).ConfigureAwait(false);
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

            if (_ConnectionFactory != null)
            {
                _ConnectionFactory.Dispose();
            }

            _Logging.Debug(_Header + "disposed");
        }

        #endregion

        #region Private-Methods

        private async Task InitializeDefaultApiKeyAsync()
        {
            // Check if any API keys exist
            List<ApiKey> existingKeys = (await _ApiKeyRepository.Query()
                .Take(1)
                .ExecuteAsync()
                .ConfigureAwait(false)).ToList();

            if (existingKeys.Count == 0)
            {
                // Create default admin API key
                string keyValue = _Settings.Authentication.DefaultAdminKey ?? ApiKey.GenerateApiKey();
                ApiKey defaultKey = new ApiKey("Default Admin", true)
                {
                    Key = keyValue
                };

                await _ApiKeyRepository.CreateAsync(defaultKey, null).ConfigureAwait(false);

                _Logging.Alert(_Header + "created default admin API key: " + keyValue);
                _Logging.Alert(_Header + "IMPORTANT: save this API key, it will not be shown again!");
            }
        }

        #endregion
    }
}
