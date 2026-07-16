namespace NetLedger.Server.API.REST
{
    using System;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using NetLedger.Server.API.Agnostic;
    using NetLedger.Server.Authentication;
    using NetLedger.Server.Models;
    using NetLedger.Server.Services;
    using NetLedger.Server.Settings;
    using SyslogLogging;
    using WatsonWebserver.Core;

    /// <summary>
    /// REST handler for identity and security endpoints.
    /// </summary>
    internal class RestIdentityHandler
    {
        private readonly string _Header = "[RestIdentityHandler] ";
        private readonly ServerSettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly IdentityHandler _IdentityHandler;
        private readonly AuthService _AuthService;
        private readonly RequestHistoryService _RequestHistory;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="identityHandler">Identity handler.</param>
        /// <param name="authService">Authentication service.</param>
        /// <param name="requestHistory">Request history service.</param>
        internal RestIdentityHandler(
            ServerSettings settings,
            LoggingModule logging,
            IdentityHandler identityHandler,
            AuthService authService,
            RequestHistoryService requestHistory)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _IdentityHandler = identityHandler ?? throw new ArgumentNullException(nameof(identityHandler));
            _AuthService = authService ?? throw new ArgumentNullException(nameof(authService));
            _RequestHistory = requestHistory ?? throw new ArgumentNullException(nameof(requestHistory));
            _Logging.Debug(_Header + "initialized");
        }

        internal async Task DiscoverTenantsAsync(HttpContextBase ctx)
        {
            await ExecuteAsync(ctx, _IdentityHandler.DiscoverTenantsAsync, false).ConfigureAwait(false);
        }

        internal async Task LoginAsync(HttpContextBase ctx)
        {
            await ExecuteAsync(ctx, _IdentityHandler.LoginAsync, false).ConfigureAwait(false);
        }

        internal async Task LogoutAsync(HttpContextBase ctx)
        {
            await ExecuteAsync(ctx, _IdentityHandler.LogoutAsync, true).ConfigureAwait(false);
        }

        internal async Task EnumerateTenantsAsync(HttpContextBase ctx)
        {
            await ExecuteAsync(ctx, _IdentityHandler.EnumerateTenantsAsync, true).ConfigureAwait(false);
        }

        internal async Task CreateTenantAsync(HttpContextBase ctx)
        {
            await ExecuteAsync(ctx, _IdentityHandler.CreateTenantAsync, true).ConfigureAwait(false);
        }

        internal async Task ReadTenantAsync(HttpContextBase ctx)
        {
            await ExecuteAsync(ctx, _IdentityHandler.ReadTenantAsync, true).ConfigureAwait(false);
        }

        internal async Task DeleteTenantAsync(HttpContextBase ctx)
        {
            await ExecuteAsync(ctx, _IdentityHandler.DeleteTenantAsync, true).ConfigureAwait(false);
        }

        internal async Task EnumerateUsersAsync(HttpContextBase ctx)
        {
            await ExecuteAsync(ctx, _IdentityHandler.EnumerateUsersAsync, true).ConfigureAwait(false);
        }

        internal async Task CreateUserAsync(HttpContextBase ctx)
        {
            await ExecuteAsync(ctx, _IdentityHandler.CreateUserAsync, true).ConfigureAwait(false);
        }

        internal async Task ReadUserAsync(HttpContextBase ctx)
        {
            await ExecuteAsync(ctx, _IdentityHandler.ReadUserAsync, true).ConfigureAwait(false);
        }

        internal async Task EnumerateAccountUsersAsync(HttpContextBase ctx)
        {
            await ExecuteAsync(ctx, _IdentityHandler.EnumerateAccountUsersAsync, true).ConfigureAwait(false);
        }

        internal async Task MapAccountUserAsync(HttpContextBase ctx)
        {
            await ExecuteAsync(ctx, _IdentityHandler.MapAccountUserAsync, true).ConfigureAwait(false);
        }

        internal async Task DeleteAccountUserAsync(HttpContextBase ctx)
        {
            await ExecuteAsync(ctx, _IdentityHandler.DeleteAccountUserAsync, true).ConfigureAwait(false);
        }

        internal async Task EnumerateSessionsAsync(HttpContextBase ctx)
        {
            await ExecuteAsync(ctx, _IdentityHandler.EnumerateSessionsAsync, true).ConfigureAwait(false);
        }

        internal async Task RevokeSessionAsync(HttpContextBase ctx)
        {
            await ExecuteAsync(ctx, _IdentityHandler.RevokeSessionAsync, true).ConfigureAwait(false);
        }

        internal async Task EnumerateAuditAsync(HttpContextBase ctx)
        {
            await ExecuteAsync(ctx, _IdentityHandler.EnumerateAuditAsync, true).ConfigureAwait(false);
        }

        internal async Task GetEffectivePermissionsAsync(HttpContextBase ctx)
        {
            await ExecuteAsync(ctx, _IdentityHandler.GetEffectivePermissionsAsync, true).ConfigureAwait(false);
        }

        internal async Task EnumerateRolesAsync(HttpContextBase ctx)
        {
            await ExecuteAsync(ctx, _IdentityHandler.EnumerateRolesAsync, true).ConfigureAwait(false);
        }

        internal async Task CreateRoleAsync(HttpContextBase ctx)
        {
            await ExecuteAsync(ctx, _IdentityHandler.CreateRoleAsync, true).ConfigureAwait(false);
        }

        internal async Task EnumeratePermissionsAsync(HttpContextBase ctx)
        {
            await ExecuteAsync(ctx, _IdentityHandler.EnumeratePermissionsAsync, true).ConfigureAwait(false);
        }

        internal async Task CreatePermissionAsync(HttpContextBase ctx)
        {
            await ExecuteAsync(ctx, _IdentityHandler.CreatePermissionAsync, true).ConfigureAwait(false);
        }

        internal async Task AssignUserRoleAsync(HttpContextBase ctx)
        {
            await ExecuteAsync(ctx, _IdentityHandler.AssignUserRoleAsync, true).ConfigureAwait(false);
        }

        internal async Task MapRolePermissionAsync(HttpContextBase ctx)
        {
            await ExecuteAsync(ctx, _IdentityHandler.MapRolePermissionAsync, true).ConfigureAwait(false);
        }

        private async Task ExecuteAsync(
            HttpContextBase ctx,
            Func<RequestContext, System.Threading.CancellationToken, Task<ResponseContext>> handler,
            bool authenticate)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx).ConfigureAwait(false);
            if (authenticate)
            {
                req.Auth = await _AuthService.AuthenticateAsync(ctx).ConfigureAwait(false);
            }
            else
            {
                req.Auth = AuthContext.NotRequired();
            }

            ResponseContext resp = await handler(req, default).ConfigureAwait(false);
            await SendResponseAsync(ctx, req, resp).ConfigureAwait(false);
        }

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
    }
}
