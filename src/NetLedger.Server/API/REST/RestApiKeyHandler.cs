namespace NetLedger.Server.API.REST
{
    using System;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Server.API.Agnostic;
    using NetLedger.Server.Authentication;
    using NetLedger.Server.Models;
    using NetLedger.Server.Services;
    using NetLedger.Server.Settings;
    using SyslogLogging;
    using WatsonWebserver.Core;

    /// <summary>
    /// REST handler for credential management endpoints.
    /// </summary>
    internal class RestApiKeyHandler
    {
        #region Private-Members

        private readonly string _Header = "[RestApiKeyHandler] ";
        private readonly ServerSettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly ApiKeyHandler _ApiKeyHandler;
        private readonly AuthService _AuthService;
        private readonly RequestHistoryService _RequestHistory;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="apiKeyHandler">API key handler.</param>
        /// <param name="authService">Authentication service.</param>
        /// <param name="requestHistory">Request history service.</param>
        internal RestApiKeyHandler(
            ServerSettings settings,
            LoggingModule logging,
            ApiKeyHandler apiKeyHandler,
            AuthService authService,
            RequestHistoryService requestHistory)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _ApiKeyHandler = apiKeyHandler ?? throw new ArgumentNullException(nameof(apiKeyHandler));
            _AuthService = authService ?? throw new ArgumentNullException(nameof(authService));
            _RequestHistory = requestHistory ?? throw new ArgumentNullException(nameof(requestHistory));

            _Logging.Debug(_Header + "initialized");
        }

        #endregion

        #region Internal-Methods

        /// <summary>
        /// Handle enumerate credentials (GET /v1/credentials).
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task EnumerateAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx).ConfigureAwait(false);
            req.Auth = await _AuthService.AuthenticateAsync(ctx).ConfigureAwait(false);
            ResponseContext resp = await _ApiKeyHandler.EnumerateAsync(req).ConfigureAwait(false);
            await SendResponseAsync(ctx, req, resp).ConfigureAwait(false);
        }

        /// <summary>
        /// Handle create credential (PUT /v1/credentials).
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task CreateAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx).ConfigureAwait(false);
            req.Auth = await _AuthService.AuthenticateAsync(ctx).ConfigureAwait(false);
            ResponseContext resp = await _ApiKeyHandler.CreateAsync(req).ConfigureAwait(false);
            await SendResponseAsync(ctx, req, resp).ConfigureAwait(false);
        }

        /// <summary>
        /// Handle revoke credential (DELETE /v1/credentials/{credentialId}).
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task RevokeAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx).ConfigureAwait(false);
            req.Auth = await _AuthService.AuthenticateAsync(ctx).ConfigureAwait(false);
            ResponseContext resp = await _ApiKeyHandler.RevokeAsync(req).ConfigureAwait(false);
            await SendResponseAsync(ctx, req, resp).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        private async Task SendResponseAsync(HttpContextBase ctx, RequestContext req, ResponseContext resp)
        {
            ctx.Response.StatusCode = resp.StatusCode;
            ctx.Response.ContentType = Constants.JsonContentType;
            ctx.Response.Headers.Add(Constants.RequestGuidHeader, resp.RequestGuid.ToString());

            object? body = resp.Success ? resp.Data : (object?)resp.Error;
            string json = JsonSerializer.Serialize(body, Constants.JsonOptions);
            _RequestHistory.Capture(ctx, req, resp, json);
            await ctx.Response.Send(Encoding.UTF8.GetBytes(json)).ConfigureAwait(false);
        }

        #endregion
    }
}




