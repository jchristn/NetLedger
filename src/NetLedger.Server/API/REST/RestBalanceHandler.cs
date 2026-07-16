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
    /// REST handler for balance and commit endpoints.
    /// </summary>
    internal class RestBalanceHandler
    {
        #region Private-Members

        private readonly string _Header = "[RestBalanceHandler] ";
        private readonly ServerSettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly BalanceHandler _BalanceHandler;
        private readonly AuthService _AuthService;
        private readonly RequestHistoryService _RequestHistory;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="balanceHandler">Balance handler.</param>
        internal RestBalanceHandler(
            ServerSettings settings,
            LoggingModule logging,
            BalanceHandler balanceHandler,
            AuthService authService,
            RequestHistoryService requestHistory)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _BalanceHandler = balanceHandler ?? throw new ArgumentNullException(nameof(balanceHandler));
            _AuthService = authService ?? throw new ArgumentNullException(nameof(authService));
            _RequestHistory = requestHistory ?? throw new ArgumentNullException(nameof(requestHistory));

            _Logging.Debug(_Header + "initialized");
        }

        #endregion

        #region Internal-Methods

        /// <summary>
        /// Handle get balance (GET /v1/accounts/{accountId}/balance).
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task GetBalanceAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx).ConfigureAwait(false);
            req.Auth = await _AuthService.AuthenticateAsync(ctx).ConfigureAwait(false);
            ResponseContext resp = await _BalanceHandler.GetBalanceAsync(req).ConfigureAwait(false);
            await SendResponseAsync(ctx, req, resp).ConfigureAwait(false);
        }

        /// <summary>
        /// Handle get historical balance (GET /v1/accounts/{accountId}/balance/asof).
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task GetBalanceAsOfAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx).ConfigureAwait(false);
            req.Auth = await _AuthService.AuthenticateAsync(ctx).ConfigureAwait(false);
            ResponseContext resp = await _BalanceHandler.GetBalanceAsOfAsync(req).ConfigureAwait(false);
            await SendResponseAsync(ctx, req, resp).ConfigureAwait(false);
        }

        /// <summary>
        /// Handle get all balances (GET /v1/balances).
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task GetAllBalancesAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx).ConfigureAwait(false);
            req.Auth = await _AuthService.AuthenticateAsync(ctx).ConfigureAwait(false);
            ResponseContext resp = await _BalanceHandler.GetAllBalancesAsync(req).ConfigureAwait(false);
            await SendResponseAsync(ctx, req, resp).ConfigureAwait(false);
        }

        /// <summary>
        /// Handle commit entries (POST /v1/accounts/{accountId}/commit).
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task CommitAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx).ConfigureAwait(false);
            req.Auth = await _AuthService.AuthenticateAsync(ctx).ConfigureAwait(false);
            ResponseContext resp = await _BalanceHandler.CommitAsync(req).ConfigureAwait(false);
            await SendResponseAsync(ctx, req, resp).ConfigureAwait(false);
        }

        /// <summary>
        /// Handle verify balance chain (GET /v1/accounts/{accountId}/verify).
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task VerifyBalanceChainAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx).ConfigureAwait(false);
            req.Auth = await _AuthService.AuthenticateAsync(ctx).ConfigureAwait(false);
            ResponseContext resp = await _BalanceHandler.VerifyBalanceChainAsync(req).ConfigureAwait(false);
            await SendResponseAsync(ctx, req, resp).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        private async Task SendResponseAsync(HttpContextBase ctx, RequestContext req, ResponseContext resp)
        {
            ctx.Response.StatusCode = resp.StatusCode;
            ctx.Response.ContentType = Constants.JsonContentType;
            ctx.Response.Headers.Add(Constants.RequestIdHeader, resp.RequestId.ToString());

            object? body = resp.Success ? resp.Data : (object?)resp.Error;
            string json = JsonSerializer.Serialize(body, Constants.JsonOptions);
            _RequestHistory.Capture(ctx, req, resp, json);
            await ctx.Response.Send(Encoding.UTF8.GetBytes(json)).ConfigureAwait(false);
        }

        #endregion
    }
}




