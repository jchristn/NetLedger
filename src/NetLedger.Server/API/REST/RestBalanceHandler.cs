namespace NetLedger.Server.API.REST
{
    using System;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Server.API.Agnostic;
    using NetLedger.Server.Models;
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

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="balanceHandler">Balance handler.</param>
        internal RestBalanceHandler(ServerSettings settings, LoggingModule logging, BalanceHandler balanceHandler)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _BalanceHandler = balanceHandler ?? throw new ArgumentNullException(nameof(balanceHandler));

            _Logging.Debug(_Header + "initialized");
        }

        #endregion

        #region Internal-Methods

        /// <summary>
        /// Handle get balance (GET /v1/accounts/{guid}/balance).
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task GetBalanceAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx).ConfigureAwait(false);
            ResponseContext resp = await _BalanceHandler.GetBalanceAsync(req).ConfigureAwait(false);
            await SendResponseAsync(ctx, resp).ConfigureAwait(false);
        }

        /// <summary>
        /// Handle get historical balance (GET /v1/accounts/{guid}/balance/asof).
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task GetBalanceAsOfAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx).ConfigureAwait(false);
            ResponseContext resp = await _BalanceHandler.GetBalanceAsOfAsync(req).ConfigureAwait(false);
            await SendResponseAsync(ctx, resp).ConfigureAwait(false);
        }

        /// <summary>
        /// Handle get all balances (GET /v1/balances).
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task GetAllBalancesAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx).ConfigureAwait(false);
            ResponseContext resp = await _BalanceHandler.GetAllBalancesAsync(req).ConfigureAwait(false);
            await SendResponseAsync(ctx, resp).ConfigureAwait(false);
        }

        /// <summary>
        /// Handle commit entries (POST /v1/accounts/{guid}/commit).
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task CommitAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx).ConfigureAwait(false);
            ResponseContext resp = await _BalanceHandler.CommitAsync(req).ConfigureAwait(false);
            await SendResponseAsync(ctx, resp).ConfigureAwait(false);
        }

        /// <summary>
        /// Handle verify balance chain (GET /v1/accounts/{guid}/verify).
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task VerifyBalanceChainAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx).ConfigureAwait(false);
            ResponseContext resp = await _BalanceHandler.VerifyBalanceChainAsync(req).ConfigureAwait(false);
            await SendResponseAsync(ctx, resp).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        private async Task SendResponseAsync(HttpContextBase ctx, ResponseContext resp)
        {
            ctx.Response.StatusCode = resp.StatusCode;
            ctx.Response.ContentType = Constants.JsonContentType;
            ctx.Response.Headers.Add(Constants.RequestGuidHeader, resp.RequestGuid.ToString());

            object? body = resp.Success ? resp.Data : (object?)resp.Error;
            string json = JsonSerializer.Serialize(body, Constants.JsonOptions);
            await ctx.Response.Send(Encoding.UTF8.GetBytes(json)).ConfigureAwait(false);
        }

        #endregion
    }
}
