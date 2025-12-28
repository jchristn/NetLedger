namespace NetLedger.Server.API.Agnostic
{
    using System;
    using System.Collections.Generic;
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
            // Require admin access
            if (req.Auth == null || !req.Auth.IsAdmin)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.Forbidden, null, "Admin access required");
            }

            ApiKeyEnumerationQuery query = new ApiKeyEnumerationQuery
            {
                MaxResults = req.MaxResults,
                Skip = req.Skip,
                ContinuationToken = req.ContinuationToken,
                Ordering = req.Ordering,
                SearchTerm = req.SearchTerm,
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
            // Require admin access
            if (req.Auth == null || !req.Auth.IsAdmin)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.Forbidden, null, "Admin access required");
            }

            CreateApiKeyRequest? createReq = req.DeserializeBody<CreateApiKeyRequest>();
            if (createReq == null || string.IsNullOrEmpty(createReq.Name))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "API key name is required");
            }

            ApiKey apiKey = await _AuthService.CreateApiKeyAsync(
                createReq.Name,
                createReq.IsAdmin ?? false,
                token).ConfigureAwait(false);

            // Return the full key (only time it's shown unredacted)
            ResponseContext resp = new ResponseContext(req, apiKey);
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
            // Require admin access
            if (req.Auth == null || !req.Auth.IsAdmin)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.Forbidden, null, "Admin access required");
            }

            if (!req.ApiKeyGuid.HasValue)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "API key GUID is required");
            }

            bool deleted = await _AuthService.RevokeApiKeyAsync(req.ApiKeyGuid.Value, token).ConfigureAwait(false);
            if (!deleted)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, "API key not found");
            }

            return new ResponseContext(req);
        }

        #endregion

        #region Private-Classes

        private class CreateApiKeyRequest
        {
            public string? Name { get; set; }

            public bool? IsAdmin { get; set; }
        }

        #endregion
    }
}
