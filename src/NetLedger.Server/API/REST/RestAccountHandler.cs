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
    /// REST handler for account endpoints.
    /// </summary>
    internal class RestAccountHandler
    {
        #region Private-Members

        private readonly string _Header = "[RestAccountHandler] ";
        private readonly ServerSettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly AccountHandler _AccountHandler;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="accountHandler">Account handler.</param>
        internal RestAccountHandler(ServerSettings settings, LoggingModule logging, AccountHandler accountHandler)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _AccountHandler = accountHandler ?? throw new ArgumentNullException(nameof(accountHandler));

            _Logging.Debug(_Header + "initialized");
        }

        #endregion

        #region Internal-Methods

        /// <summary>
        /// Handle account exists check (HEAD /v1/accounts/{guid}).
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task ExistsAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx).ConfigureAwait(false);
            ResponseContext resp = await _AccountHandler.ExistsAsync(req).ConfigureAwait(false);
            await SendResponseAsync(ctx, resp).ConfigureAwait(false);
        }

        /// <summary>
        /// Handle enumerate accounts (GET /v1/accounts).
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task EnumerateAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx).ConfigureAwait(false);
            ResponseContext resp = await _AccountHandler.EnumerateAsync(req).ConfigureAwait(false);
            await SendResponseAsync(ctx, resp).ConfigureAwait(false);
        }

        /// <summary>
        /// Handle read account (GET /v1/accounts/{guid}).
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task ReadAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx).ConfigureAwait(false);
            ResponseContext resp = await _AccountHandler.ReadAsync(req).ConfigureAwait(false);
            await SendResponseAsync(ctx, resp).ConfigureAwait(false);
        }

        /// <summary>
        /// Handle read account by name (GET /v1/accounts/byname/{name}).
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task ReadByNameAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx).ConfigureAwait(false);
            ResponseContext resp = await _AccountHandler.ReadByNameAsync(req).ConfigureAwait(false);
            await SendResponseAsync(ctx, resp).ConfigureAwait(false);
        }

        /// <summary>
        /// Handle create account (PUT /v1/accounts).
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task CreateAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx).ConfigureAwait(false);
            ResponseContext resp = await _AccountHandler.CreateAsync(req).ConfigureAwait(false);
            await SendResponseAsync(ctx, resp).ConfigureAwait(false);
        }

        /// <summary>
        /// Handle delete account (DELETE /v1/accounts/{guid}).
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task DeleteAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx).ConfigureAwait(false);
            ResponseContext resp = await _AccountHandler.DeleteAsync(req).ConfigureAwait(false);
            await SendResponseAsync(ctx, resp).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        private async Task SendResponseAsync(HttpContextBase ctx, ResponseContext resp)
        {
            ctx.Response.StatusCode = resp.StatusCode;
            ctx.Response.ContentType = Constants.JsonContentType;
            ctx.Response.Headers.Add(Constants.RequestGuidHeader, resp.RequestGuid.ToString());

            if (ctx.Request.Method == HttpMethod.HEAD)
            {
                await ctx.Response.Send().ConfigureAwait(false);
            }
            else
            {
                object? body = resp.Success ? resp.Data : (object?)resp.Error;
                string json = JsonSerializer.Serialize(body, Constants.JsonOptions);
                await ctx.Response.Send(Encoding.UTF8.GetBytes(json)).ConfigureAwait(false);
            }
        }

        #endregion
    }
}
