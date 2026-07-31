namespace NetLedger.Server.API.REST
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using NetLedger;
    using NetLedger.Server.Authentication;
    using NetLedger.Server.Models;
    using NetLedger.Server.Services;
    using SyslogLogging;
    using WatsonWebserver.Core;

    /// <summary>
    /// REST handler for active-to-archive export operations.
    /// </summary>
    internal sealed class RestArchiveHandler
    {
        private readonly string _Header = "[RestArchiveHandler] ";
        private readonly AuthService _AuthService;
        private readonly AuthorizationService _AuthorizationService;
        private readonly ArchiveExportService _ArchiveExportService;
        private readonly Ledger _Ledger;
        private readonly RequestHistoryService _RequestHistory;
        private readonly LoggingModule _Logging;

        /// <summary>
        /// Instantiate active archive handler.
        /// </summary>
        /// <param name="authService">Authentication service.</param>
        /// <param name="authorizationService">Authorization service.</param>
        /// <param name="archiveExportService">Archive export service.</param>
        /// <param name="ledger">Ledger instance.</param>
        /// <param name="requestHistory">Request history service.</param>
        /// <param name="logging">Logging module.</param>
        internal RestArchiveHandler(
            AuthService authService,
            AuthorizationService authorizationService,
            ArchiveExportService archiveExportService,
            Ledger ledger,
            RequestHistoryService requestHistory,
            LoggingModule logging)
        {
            _AuthService = authService ?? throw new ArgumentNullException(nameof(authService));
            _AuthorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
            _ArchiveExportService = archiveExportService ?? throw new ArgumentNullException(nameof(archiveExportService));
            _Ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            _RequestHistory = requestHistory ?? throw new ArgumentNullException(nameof(requestHistory));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Logging.Debug(_Header + "initialized");
        }

        /// <summary>
        /// Export committed active entries for one tenant account.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task ExportEntriesAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx, ctx.Token).ConfigureAwait(false);
            req.Auth = await _AuthService.AuthenticateAsync(ctx, ctx.Token).ConfigureAwait(false);

            ArchiveExportRequest? request = req.DeserializeBody<ArchiveExportRequest>();
            if (request == null)
            {
                request = new ArchiveExportRequest();
            }

            if (String.IsNullOrWhiteSpace(request.TenantId)) request.TenantId = req.TenantId;
            if (String.IsNullOrWhiteSpace(request.AccountId)) request.AccountId = req.AccountId;

            AuthorizationDecision decision = await _AuthorizationService.AuthorizeAsync(req, "Archive", "Execute", request.AccountId, ctx.Token).ConfigureAwait(false);
            if (!decision.Permitted)
            {
                ApiErrorEnum error = String.Equals(decision.Reason, "Authentication required", StringComparison.Ordinal)
                    ? ApiErrorEnum.Unauthorized
                    : ApiErrorEnum.Forbidden;
                await SendResponseAsync(ctx, req, ResponseContext.FromError(req, error, null, decision.Reason)).ConfigureAwait(false);
                return;
            }

            try
            {
                ArchiveExportResponse response = await _ArchiveExportService.ExportEntriesAsync(req, request, ctx.Request.Headers, ctx.Token).ConfigureAwait(false);
                await SendResponseAsync(ctx, req, new ResponseContext(req, response)).ConfigureAwait(false);
            }
            catch (ArgumentException e)
            {
                await SendResponseAsync(ctx, req, ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, e.Message)).ConfigureAwait(false);
            }
            catch (InvalidOperationException e)
            {
                await SendResponseAsync(ctx, req, ResponseContext.FromError(req, ApiErrorEnum.Conflict, null, e.Message)).ConfigureAwait(false);
            }
            catch (KeyNotFoundException e)
            {
                await SendResponseAsync(ctx, req, ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, e.Message)).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Export active request history rows for one tenant.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task ExportRequestHistoryAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx, ctx.Token).ConfigureAwait(false);
            req.Auth = await _AuthService.AuthenticateAsync(ctx, ctx.Token).ConfigureAwait(false);

            ArchiveExportRequest? request = req.DeserializeBody<ArchiveExportRequest>();
            if (request == null)
            {
                request = new ArchiveExportRequest();
            }

            if (String.IsNullOrWhiteSpace(request.TenantId)) request.TenantId = req.TenantId;

            AuthorizationDecision decision = await _AuthorizationService.AuthorizeAsync(req, "Archive", "Execute", null, ctx.Token).ConfigureAwait(false);
            if (!decision.Permitted)
            {
                ApiErrorEnum error = String.Equals(decision.Reason, "Authentication required", StringComparison.Ordinal)
                    ? ApiErrorEnum.Unauthorized
                    : ApiErrorEnum.Forbidden;
                await SendResponseAsync(ctx, req, ResponseContext.FromError(req, error, null, decision.Reason)).ConfigureAwait(false);
                return;
            }

            try
            {
                ArchiveExportResponse response = await _ArchiveExportService.ExportRequestHistoryAsync(req, request, ctx.Request.Headers, ctx.Token).ConfigureAwait(false);
                await SendResponseAsync(ctx, req, new ResponseContext(req, response)).ConfigureAwait(false);
            }
            catch (ArgumentException e)
            {
                await SendResponseAsync(ctx, req, ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, e.Message)).ConfigureAwait(false);
            }
            catch (InvalidOperationException e)
            {
                await SendResponseAsync(ctx, req, ResponseContext.FromError(req, ApiErrorEnum.Conflict, null, e.Message)).ConfigureAwait(false);
            }
            catch (KeyNotFoundException e)
            {
                await SendResponseAsync(ctx, req, ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, e.Message)).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Read account archival settings and automatic archival state.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task ReadAccountSettingsAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx, ctx.Token).ConfigureAwait(false);
            req.Auth = await _AuthService.AuthenticateAsync(ctx, ctx.Token).ConfigureAwait(false);

            string accountId = req.AccountId ?? String.Empty;
            AuthorizationDecision decision = await _AuthorizationService.AuthorizeAsync(req, "Account", "Read", accountId, ctx.Token).ConfigureAwait(false);
            if (!decision.Permitted)
            {
                await SendDecisionDeniedAsync(ctx, req, decision).ConfigureAwait(false);
                return;
            }

            try
            {
                Account account = await ReadRequestedAccountAsync(req, ctx.Token).ConfigureAwait(false);
                AccountArchivalSettings? settings = await _Ledger.Driver.AccountArchivalSettings
                    .ReadByAccountAsync(account.TenantId, account.Id, ctx.Token)
                    .ConfigureAwait(false);

                if (settings == null)
                {
                    settings = new AccountArchivalSettings
                    {
                        TenantId = account.TenantId,
                        AccountId = account.Id
                    };
                }

                await SendResponseAsync(ctx, req, new ResponseContext(req, settings)).ConfigureAwait(false);
            }
            catch (ArgumentException e)
            {
                await SendResponseAsync(ctx, req, ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, e.Message)).ConfigureAwait(false);
            }
            catch (KeyNotFoundException e)
            {
                await SendResponseAsync(ctx, req, ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, e.Message)).ConfigureAwait(false);
            }
            catch (InvalidOperationException e)
            {
                await SendResponseAsync(ctx, req, ResponseContext.FromError(req, ApiErrorEnum.Conflict, null, e.Message)).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Replace account archival override settings.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task UpdateAccountSettingsAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx, ctx.Token).ConfigureAwait(false);
            req.Auth = await _AuthService.AuthenticateAsync(ctx, ctx.Token).ConfigureAwait(false);

            string accountId = req.AccountId ?? String.Empty;
            AuthorizationDecision decision = await _AuthorizationService.AuthorizeAsync(req, "Account", "Update", accountId, ctx.Token).ConfigureAwait(false);
            if (!decision.Permitted)
            {
                await SendDecisionDeniedAsync(ctx, req, decision).ConfigureAwait(false);
                return;
            }

            try
            {
                UpdateAccountArchivalSettingsRequest? request = req.DeserializeBody<UpdateAccountArchivalSettingsRequest>();
                if (request == null)
                {
                    request = new UpdateAccountArchivalSettingsRequest();
                }

                Account account = await ReadRequestedAccountAsync(req, ctx.Token).ConfigureAwait(false);
                AccountArchivalSettings? settings = await _Ledger.Driver.AccountArchivalSettings
                    .ReadByAccountAsync(account.TenantId, account.Id, ctx.Token)
                    .ConfigureAwait(false);
                if (settings == null)
                {
                    settings = new AccountArchivalSettings
                    {
                        TenantId = account.TenantId,
                        AccountId = account.Id
                    };
                }

                ApplyOverrideRequest(settings, request);
                AccountArchivalSettings saved = await _Ledger.Driver.AccountArchivalSettings.UpsertAsync(settings, ctx.Token).ConfigureAwait(false);
                await SendResponseAsync(ctx, req, new ResponseContext(req, saved)).ConfigureAwait(false);
            }
            catch (ArgumentException e)
            {
                await SendResponseAsync(ctx, req, ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, e.Message)).ConfigureAwait(false);
            }
            catch (KeyNotFoundException e)
            {
                await SendResponseAsync(ctx, req, ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, e.Message)).ConfigureAwait(false);
            }
            catch (InvalidOperationException e)
            {
                await SendResponseAsync(ctx, req, ResponseContext.FromError(req, ApiErrorEnum.Conflict, null, e.Message)).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Clear account archival override settings while retaining automatic archival state.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task DeleteAccountSettingsAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx, ctx.Token).ConfigureAwait(false);
            req.Auth = await _AuthService.AuthenticateAsync(ctx, ctx.Token).ConfigureAwait(false);

            string accountId = req.AccountId ?? String.Empty;
            AuthorizationDecision decision = await _AuthorizationService.AuthorizeAsync(req, "Account", "Delete", accountId, ctx.Token).ConfigureAwait(false);
            if (!decision.Permitted)
            {
                await SendDecisionDeniedAsync(ctx, req, decision).ConfigureAwait(false);
                return;
            }

            try
            {
                Account account = await ReadRequestedAccountAsync(req, ctx.Token).ConfigureAwait(false);
                AccountArchivalSettings? settings = await _Ledger.Driver.AccountArchivalSettings
                    .ReadByAccountAsync(account.TenantId, account.Id, ctx.Token)
                    .ConfigureAwait(false);
                if (settings == null)
                {
                    settings = new AccountArchivalSettings
                    {
                        TenantId = account.TenantId,
                        AccountId = account.Id
                    };
                }
                else
                {
                    ClearOverrideFields(settings);
                    settings = await _Ledger.Driver.AccountArchivalSettings.UpsertAsync(settings, ctx.Token).ConfigureAwait(false);
                }

                await SendResponseAsync(ctx, req, new ResponseContext(req, settings)).ConfigureAwait(false);
            }
            catch (ArgumentException e)
            {
                await SendResponseAsync(ctx, req, ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, e.Message)).ConfigureAwait(false);
            }
            catch (KeyNotFoundException e)
            {
                await SendResponseAsync(ctx, req, ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, e.Message)).ConfigureAwait(false);
            }
            catch (InvalidOperationException e)
            {
                await SendResponseAsync(ctx, req, ResponseContext.FromError(req, ApiErrorEnum.Conflict, null, e.Message)).ConfigureAwait(false);
            }
        }

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

        private async Task SendDecisionDeniedAsync(HttpContextBase ctx, RequestContext req, AuthorizationDecision decision)
        {
            ApiErrorEnum error = String.Equals(decision.Reason, "Authentication required", StringComparison.Ordinal)
                ? ApiErrorEnum.Unauthorized
                : ApiErrorEnum.Forbidden;
            await SendResponseAsync(ctx, req, ResponseContext.FromError(req, error, null, decision.Reason)).ConfigureAwait(false);
        }

        private async Task<Account> ReadRequestedAccountAsync(RequestContext req, System.Threading.CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(req.AccountId))
            {
                throw new ArgumentException("Account ID is required.");
            }

            Account account = await _Ledger.GetAccountByIdAsync(req.AccountId, token).ConfigureAwait(false);
            if (account == null)
            {
                throw new KeyNotFoundException("Account was not found.");
            }

            string tenantId = FirstNonEmpty(req.TenantId, req.Auth?.TenantId, account.TenantId);
            if (!String.Equals(account.TenantId, tenantId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Account does not belong to the requested tenant.");
            }

            return account;
        }

        private void ApplyOverrideRequest(AccountArchivalSettings settings, UpdateAccountArchivalSettingsRequest request)
        {
            settings.Enabled = request.Enabled;
            settings.MaxRetentionDays = request.MaxRetentionDays;
            settings.IntervalSeconds = request.IntervalSeconds;
            settings.MaxBatchRows = request.MaxBatchRows;
            settings.DeleteAfterCommit = request.DeleteAfterCommit;
            settings.StoragePoolId = String.IsNullOrWhiteSpace(request.StoragePoolId) ? null : request.StoragePoolId;
            settings.RetryMaxAttempts = request.RetryMaxAttempts;
            settings.RetryInitialDelaySeconds = request.RetryInitialDelaySeconds;
            settings.RetryMaxDelaySeconds = request.RetryMaxDelaySeconds;
        }

        private void ClearOverrideFields(AccountArchivalSettings settings)
        {
            settings.Enabled = null;
            settings.MaxRetentionDays = null;
            settings.IntervalSeconds = null;
            settings.MaxBatchRows = null;
            settings.DeleteAfterCommit = null;
            settings.StoragePoolId = null;
            settings.RetryMaxAttempts = null;
            settings.RetryInitialDelaySeconds = null;
            settings.RetryMaxDelaySeconds = null;
        }

        private string FirstNonEmpty(params string?[] values)
        {
            foreach (string? value in values)
            {
                if (!String.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return String.Empty;
        }
    }
}
