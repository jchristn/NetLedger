namespace NetLedger.Server.API.Agnostic
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger;
    using NetLedger.Server.Authentication;
    using NetLedger.Server.Models;
    using NetLedger.Server.Settings;
    using SyslogLogging;

    /// <summary>
    /// API key handler for managing API keys.
    /// </summary>
    internal class ApiKeyHandler
    {
        #region Private-Members

        private readonly string _Header = "[ApiKeyHandler] ";
        private readonly ServerSettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly AuthService _AuthService;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="authService">Authentication service.</param>
        internal ApiKeyHandler(ServerSettings settings, LoggingModule logging, AuthService authService)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _AuthService = authService ?? throw new ArgumentNullException(nameof(authService));

            _Logging.Debug(_Header + "initialized");
        }

        #endregion

        #region Internal-Methods

        /// <summary>
        /// Enumerate API keys with pagination (redacted).
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response context with enumeration result.</returns>
        internal async Task<ResponseContext> EnumerateAsync(RequestContext req, CancellationToken token = default)
        {
            if (!IsAuthenticated(req))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.Unauthorized, null, "Authentication required");
            }

            if (!CanAccessCredentials(req))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.Forbidden, null, "Credential administration access required");
            }

            ApiKeyEnumerationQuery query = new ApiKeyEnumerationQuery
            {
                MaxResults = req.MaxResults,
                Skip = req.Skip,
                ContinuationToken = req.ContinuationToken,
                Ordering = req.Ordering,
                SearchTerm = req.SearchTerm,
                TenantId = ResolveCredentialTenant(req),
                UserId = ShouldRestrictToOwnCredentials(req) ? req.Auth?.PrincipalId : null,
                CreatedAfterUtc = req.StartTimeUtc,
                CreatedBeforeUtc = req.EndTimeUtc
            };

            EnumerationResult<ApiKey> result = await _AuthService.EnumerateApiKeysAsync(query, token).ConfigureAwait(false);

            return new ResponseContext(req, result);
        }

        /// <summary>
        /// Create a new API key.
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response context with created API key.</returns>
        internal async Task<ResponseContext> CreateAsync(RequestContext req, CancellationToken token = default)
        {
            if (!IsAuthenticated(req))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.Unauthorized, null, "Authentication required");
            }

            if (!CanAccessCredentials(req))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.Forbidden, null, "Credential administration access required");
            }

            CreateApiKeyRequest? createReq = req.DeserializeBody<CreateApiKeyRequest>();
            if (createReq == null || string.IsNullOrEmpty(createReq.Name))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "API key name is required");
            }

            if (req.Auth?.IsAdmin != true)
            {
                string? requestedTenantId = createReq.TenantId ?? req.TenantId;
                if (!String.IsNullOrEmpty(requestedTenantId) && !String.Equals(requestedTenantId, req.Auth?.TenantId, StringComparison.Ordinal))
                {
                    return ResponseContext.FromError(req, ApiErrorEnum.Forbidden, null, "Credential administration access required");
                }
            }

            string tenantId = req.Auth?.IsAdmin == true
                ? createReq.TenantId ?? req.TenantId ?? req.Auth?.TenantId ?? String.Empty
                : req.Auth?.TenantId ?? String.Empty;
            string userId = ShouldRestrictToOwnCredentials(req)
                ? req.Auth?.PrincipalId ?? String.Empty
                : createReq.UserId ?? String.Empty;
            ApiKey apiKey = await _AuthService.CreateApiKeyAsync(
                createReq.Name,
                false,
                tenantId,
                userId,
                token).ConfigureAwait(false);

            ResponseContext resp = new ResponseContext(req, new CreateCredentialResponse
            {
                Credential = apiKey,
                SecretKey = apiKey.RawSecretKey
            });
            resp.StatusCode = 201;
            return resp;
        }

        /// <summary>
        /// Revoke an API key.
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response context.</returns>
        internal async Task<ResponseContext> RevokeAsync(RequestContext req, CancellationToken token = default)
        {
            if (!IsAuthenticated(req))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.Unauthorized, null, "Authentication required");
            }

            if (!CanAccessCredentials(req))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.Forbidden, null, "Credential administration access required");
            }

            if (String.IsNullOrEmpty(req.CredentialId))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "API key identifier is required");
            }

            ApiKey? apiKey = await _AuthService.GetApiKeyByIdAsync(req.CredentialId, token).ConfigureAwait(false);
            if (apiKey == null)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, "API key not found");
            }

            if (ShouldRestrictToOwnCredentials(req) &&
                !String.Equals(apiKey.UserId, req.Auth?.PrincipalId, StringComparison.Ordinal))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.Forbidden, null, "Credential administration access required");
            }

            if (req.Auth?.IsAdmin != true &&
                !String.Equals(apiKey.TenantId, req.Auth?.TenantId, StringComparison.Ordinal))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.Forbidden, null, "Credential administration access required");
            }

            bool deleted = await _AuthService.RevokeApiKeyAsync(req.CredentialId, token).ConfigureAwait(false);
            if (!deleted)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, "API key not found");
            }

            return new ResponseContext(req);
        }

        #endregion

        #region Private-Methods

        private bool CanManageCredentials(RequestContext req)
        {
            if (req.Auth == null || !req.Auth.IsAuthenticated) return false;
            if (req.Auth.IsAdmin) return true;
            if (!req.Auth.IsTenantAdmin) return false;
            if (String.IsNullOrEmpty(req.Auth.TenantId)) return false;
            return String.IsNullOrEmpty(req.TenantId) || String.Equals(req.Auth.TenantId, req.TenantId, StringComparison.Ordinal);
        }

        private bool CanAccessCredentials(RequestContext req)
        {
            if (CanManageCredentials(req)) return true;
            return req.Auth != null &&
                req.Auth.IsAuthenticated &&
                String.Equals(req.Auth.PrincipalType, "User", StringComparison.OrdinalIgnoreCase) &&
                !String.IsNullOrEmpty(req.Auth.PrincipalId) &&
                !String.IsNullOrEmpty(req.Auth.TenantId) &&
                (String.IsNullOrEmpty(req.TenantId) || String.Equals(req.Auth.TenantId, req.TenantId, StringComparison.Ordinal));
        }

        private string? ResolveCredentialTenant(RequestContext req)
        {
            if (req.Auth?.IsAdmin == true) return req.TenantId;
            return req.Auth?.TenantId;
        }

        private bool ShouldRestrictToOwnCredentials(RequestContext req)
        {
            return req.Auth != null &&
                !req.Auth.IsAdmin &&
                !req.Auth.IsTenantAdmin &&
                String.Equals(req.Auth.PrincipalType, "User", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsAuthenticated(RequestContext req)
        {
            return req.Auth != null && req.Auth.IsAuthenticated;
        }

        #endregion
    }
}
