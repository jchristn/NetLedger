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
    /// REST handler for service endpoints.
    /// </summary>
    internal class RestServiceHandler
    {
        #region Private-Members

        private readonly string _Header = "[RestServiceHandler] ";
        private readonly ServerSettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly ServiceHandler _ServiceHandler;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="serviceHandler">Service handler.</param>
        internal RestServiceHandler(ServerSettings settings, LoggingModule logging, ServiceHandler serviceHandler)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _ServiceHandler = serviceHandler ?? throw new ArgumentNullException(nameof(serviceHandler));

            _Logging.Debug(_Header + "initialized");
        }

        #endregion

        #region Internal-Methods

        /// <summary>
        /// Handle service health check (HEAD /).
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task ExistsAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx).ConfigureAwait(false);
            ResponseContext resp = await _ServiceHandler.ExistsAsync(req).ConfigureAwait(false);
            await SendResponseAsync(ctx, resp).ConfigureAwait(false);
        }

        /// <summary>
        /// Handle get service info (GET /).
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task GetInfoAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx).ConfigureAwait(false);
            ResponseContext resp = await _ServiceHandler.GetInfoAsync(req).ConfigureAwait(false);
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
