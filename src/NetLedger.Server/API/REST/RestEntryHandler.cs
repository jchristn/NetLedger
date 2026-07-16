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
    /// REST handler for entry endpoints.
    /// </summary>
    internal class RestEntryHandler
    {
        #region Private-Members

        private readonly string _Header = "[RestEntryHandler] ";
        private readonly ServerSettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly EntryHandler _EntryHandler;
        private readonly AuthService _AuthService;
        private readonly RequestHistoryService _RequestHistory;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="entryHandler">Entry handler.</param>
        internal RestEntryHandler(
            ServerSettings settings,
            LoggingModule logging,
            EntryHandler entryHandler,
            AuthService authService,
            RequestHistoryService requestHistory)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _EntryHandler = entryHandler ?? throw new ArgumentNullException(nameof(entryHandler));
            _AuthService = authService ?? throw new ArgumentNullException(nameof(authService));
            _RequestHistory = requestHistory ?? throw new ArgumentNullException(nameof(requestHistory));

            _Logging.Debug(_Header + "initialized");
        }

        #endregion

        #region Internal-Methods

        /// <summary>
        /// Handle enumerate entries (GET /v1/accounts/{accountId}/entries).
        /// Querystring parameters: maxResults, skip, continuationToken, ordering, search, startTime, endTime, amountMin, amountMax, creditMin, creditMax, debitMin, debitMax, labels, tags.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task GetEntriesAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx).ConfigureAwait(false);
            req.Auth = await _AuthService.AuthenticateAsync(ctx).ConfigureAwait(false);
            ResponseContext resp = await _EntryHandler.GetEntriesAsync(req).ConfigureAwait(false);
            await SendResponseAsync(ctx, req, resp).ConfigureAwait(false);
        }

        /// <summary>
        /// Handle get pending entries (GET /v1/accounts/{accountId}/entries/pending).
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task GetPendingEntriesAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx).ConfigureAwait(false);
            req.Auth = await _AuthService.AuthenticateAsync(ctx).ConfigureAwait(false);
            ResponseContext resp = await _EntryHandler.GetPendingEntriesAsync(req).ConfigureAwait(false);
            await SendResponseAsync(ctx, req, resp).ConfigureAwait(false);
        }

        /// <summary>
        /// Handle get pending credits (GET /v1/accounts/{accountId}/entries/pending/credits).
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task GetPendingCreditsAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx).ConfigureAwait(false);
            req.Auth = await _AuthService.AuthenticateAsync(ctx).ConfigureAwait(false);
            ResponseContext resp = await _EntryHandler.GetPendingCreditsAsync(req).ConfigureAwait(false);
            await SendResponseAsync(ctx, req, resp).ConfigureAwait(false);
        }

        /// <summary>
        /// Handle get pending debits (GET /v1/accounts/{accountId}/entries/pending/debits).
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task GetPendingDebitsAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx).ConfigureAwait(false);
            req.Auth = await _AuthService.AuthenticateAsync(ctx).ConfigureAwait(false);
            ResponseContext resp = await _EntryHandler.GetPendingDebitsAsync(req).ConfigureAwait(false);
            await SendResponseAsync(ctx, req, resp).ConfigureAwait(false);
        }

        /// <summary>
        /// Handle enumerate entries (POST /v1/accounts/{accountId}/entries/enumerate).
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task EnumerateAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx).ConfigureAwait(false);
            req.Auth = await _AuthService.AuthenticateAsync(ctx).ConfigureAwait(false);
            ResponseContext resp = await _EntryHandler.EnumerateAsync(req).ConfigureAwait(false);
            await SendResponseAsync(ctx, req, resp).ConfigureAwait(false);
        }

        /// <summary>
        /// Handle add credits (PUT /v1/accounts/{accountId}/credits).
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task AddCreditsAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx).ConfigureAwait(false);
            req.Auth = await _AuthService.AuthenticateAsync(ctx).ConfigureAwait(false);
            ResponseContext resp = await _EntryHandler.AddCreditsAsync(req).ConfigureAwait(false);
            await SendResponseAsync(ctx, req, resp).ConfigureAwait(false);
        }

        /// <summary>
        /// Handle add debits (PUT /v1/accounts/{accountId}/debits).
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task AddDebitsAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx).ConfigureAwait(false);
            req.Auth = await _AuthService.AuthenticateAsync(ctx).ConfigureAwait(false);
            ResponseContext resp = await _EntryHandler.AddDebitsAsync(req).ConfigureAwait(false);
            await SendResponseAsync(ctx, req, resp).ConfigureAwait(false);
        }

        /// <summary>
        /// Handle cancel entry (DELETE /v1/accounts/{accountId}/entries/{entryId}).
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task CancelEntryAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx).ConfigureAwait(false);
            req.Auth = await _AuthService.AuthenticateAsync(ctx).ConfigureAwait(false);
            ResponseContext resp = await _EntryHandler.CancelEntryAsync(req).ConfigureAwait(false);
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




