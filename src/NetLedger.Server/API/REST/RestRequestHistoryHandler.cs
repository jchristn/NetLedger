namespace NetLedger.Server.API.REST
{
    using System;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using NetLedger.Database;
    using NetLedger.Server.Authentication;
    using NetLedger.Server.Models;
    using NetLedger.Server.Services;
    using NetLedger.Server.Settings;
    using SyslogLogging;
    using WatsonWebserver.Core;

    /// <summary>
    /// REST handler for request history endpoints.
    /// </summary>
    internal sealed class RestRequestHistoryHandler
    {
        private readonly string _Header = "[RestRequestHistoryHandler] ";
        private readonly DatabaseDriverBase _Driver;
        private readonly AuthService _AuthService;
        private readonly LoggingModule _Logging;
        private readonly ActiveArchiveBoundaryService _ArchiveBoundary;

        /// <summary>
        /// Instantiate request history handler.
        /// </summary>
        /// <param name="driver">Database driver.</param>
        /// <param name="settings">Server settings.</param>
        /// <param name="authService">Authentication service.</param>
        /// <param name="logging">Logging module.</param>
        internal RestRequestHistoryHandler(DatabaseDriverBase driver, ServerSettings settings, AuthService authService, LoggingModule logging)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _AuthService = authService ?? throw new ArgumentNullException(nameof(authService));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _ArchiveBoundary = new ActiveArchiveBoundaryService(settings);
            _Logging.Debug(_Header + "initialized");
        }

        /// <summary>
        /// Enumerate request history entries.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task EnumerateAsync(HttpContextBase ctx)
        {
            RequestContext req = await BuildAuthenticatedRequestAsync(ctx).ConfigureAwait(false);
            RequestHistoryFilter filter = BuildFilter(req);
            ResponseContext scopeError = ApplyScope(req, filter, false);
            if (!scopeError.Success)
            {
                await SendResponseAsync(ctx, scopeError).ConfigureAwait(false);
                return;
            }

            ResponseContext? archiveRangeError = ApplyActiveHistoryRange(req, filter);
            if (archiveRangeError != null)
            {
                await SendResponseAsync(ctx, archiveRangeError).ConfigureAwait(false);
                return;
            }

            RequestHistoryResult result = await _Driver.RequestHistory.EnumerateAsync(filter).ConfigureAwait(false);
            foreach (RequestHistoryEntry entry in result.Objects)
            {
                entry.RequestBody = null;
                entry.ResponseBody = null;
            }

            await SendResponseAsync(ctx, new ResponseContext(req, result)).ConfigureAwait(false);
        }

        /// <summary>
        /// Summarize request history entries.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task SummarizeAsync(HttpContextBase ctx)
        {
            RequestContext req = await BuildAuthenticatedRequestAsync(ctx).ConfigureAwait(false);
            RequestHistoryFilter filter = BuildFilter(req);
            ResponseContext scopeError = ApplyScope(req, filter, false);
            if (!scopeError.Success)
            {
                await SendResponseAsync(ctx, scopeError).ConfigureAwait(false);
                return;
            }

            ResponseContext? archiveRangeError = ApplyActiveHistoryRange(req, filter);
            if (archiveRangeError != null)
            {
                await SendResponseAsync(ctx, archiveRangeError).ConfigureAwait(false);
                return;
            }

            RequestHistorySummary summary = await _Driver.RequestHistory.SummarizeAsync(filter).ConfigureAwait(false);
            await SendResponseAsync(ctx, new ResponseContext(req, summary)).ConfigureAwait(false);
        }

        /// <summary>
        /// Read one request history entry.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task ReadAsync(HttpContextBase ctx)
        {
            RequestContext req = await BuildAuthenticatedRequestAsync(ctx).ConfigureAwait(false);
            string? id = req.UrlParameters["id"];
            if (String.IsNullOrEmpty(id)) id = req.UrlParameters["requestId"];
            if (String.IsNullOrEmpty(id))
            {
                await SendResponseAsync(ctx, ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Request history identifier is required.")).ConfigureAwait(false);
                return;
            }

            RequestHistoryFilter filter = new RequestHistoryFilter();
            ResponseContext scopeError = ApplyScope(req, filter, false);
            if (!scopeError.Success)
            {
                await SendResponseAsync(ctx, scopeError).ConfigureAwait(false);
                return;
            }

            RequestHistoryEntry? entry = await _Driver.RequestHistory.ReadAsync(filter.TenantId, id).ConfigureAwait(false);
            if (entry == null || !CanAccessEntry(req, entry))
            {
                await SendResponseAsync(ctx, ResponseContext.FromError(req, ApiErrorEnum.NotFound, id, "Request history entry was not found.")).ConfigureAwait(false);
                return;
            }

            DateTime? fromUtc = entry.CreatedUtc;
            ResponseContext? archiveRangeError = _ArchiveBoundary.ApplyActiveRange(req, ref fromUtc, entry.CreatedUtc, "RequestHistory");
            if (archiveRangeError != null)
            {
                await SendResponseAsync(ctx, archiveRangeError).ConfigureAwait(false);
                return;
            }

            await SendResponseAsync(ctx, new ResponseContext(req, entry)).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete one request history entry.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task DeleteAsync(HttpContextBase ctx)
        {
            RequestContext req = await BuildAuthenticatedRequestAsync(ctx).ConfigureAwait(false);
            string? id = req.UrlParameters["id"];
            if (String.IsNullOrEmpty(id)) id = req.UrlParameters["requestId"];
            if (String.IsNullOrEmpty(id))
            {
                await SendResponseAsync(ctx, ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Request history identifier is required.")).ConfigureAwait(false);
                return;
            }

            RequestHistoryFilter filter = new RequestHistoryFilter();
            ResponseContext scopeError = ApplyScope(req, filter, true);
            if (!scopeError.Success)
            {
                await SendResponseAsync(ctx, scopeError).ConfigureAwait(false);
                return;
            }

            bool deleted = await _Driver.RequestHistory.DeleteAsync(filter.TenantId, id).ConfigureAwait(false);
            await SendResponseAsync(ctx, new ResponseContext(req, new RequestHistoryDeleteResult { DeletedCount = deleted ? 1 : 0 })).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete matching request history entries.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Task.</returns>
        internal async Task DeleteManyAsync(HttpContextBase ctx)
        {
            RequestContext req = await BuildAuthenticatedRequestAsync(ctx).ConfigureAwait(false);
            RequestHistoryFilter filter = BuildFilter(req);
            ResponseContext scopeError = ApplyScope(req, filter, true);
            if (!scopeError.Success)
            {
                await SendResponseAsync(ctx, scopeError).ConfigureAwait(false);
                return;
            }

            ResponseContext? archiveRangeError = ApplyActiveHistoryRange(req, filter);
            if (archiveRangeError != null)
            {
                await SendResponseAsync(ctx, archiveRangeError).ConfigureAwait(false);
                return;
            }

            long deletedCount = await _Driver.RequestHistory.DeleteManyAsync(filter).ConfigureAwait(false);
            await SendResponseAsync(ctx, new ResponseContext(req, new RequestHistoryDeleteResult { DeletedCount = deletedCount })).ConfigureAwait(false);
        }

        private async Task<RequestContext> BuildAuthenticatedRequestAsync(HttpContextBase ctx)
        {
            RequestContext req = await RequestContext.FromHttpContextAsync(ctx).ConfigureAwait(false);
            req.Auth = await _AuthService.AuthenticateAsync(ctx).ConfigureAwait(false);
            return req;
        }

        private static RequestHistoryFilter BuildFilter(RequestContext req)
        {
            RequestHistoryFilter filter = new RequestHistoryFilter
            {
                TenantId = req.QueryString["tenantId"],
                PrincipalId = req.QueryString["principalId"],
                Method = req.QueryString["method"],
                PathContains = req.QueryString["pathContains"],
                MaxResults = req.MaxResults,
                Skip = req.Skip
            };

            string? statusCode = req.QueryString["statusCode"];
            if (!String.IsNullOrEmpty(statusCode) && Int32.TryParse(statusCode, out int parsedStatusCode))
            {
                filter.StatusCode = parsedStatusCode;
            }

            string? bucketMinutes = req.QueryString["bucketMinutes"];
            if (!String.IsNullOrEmpty(bucketMinutes) && Int32.TryParse(bucketMinutes, out int parsedBucketMinutes))
            {
                filter.BucketMinutes = parsedBucketMinutes;
            }

            string? fromUtc = req.QueryString["fromUtc"];
            if (!String.IsNullOrEmpty(fromUtc) && DateTime.TryParse(fromUtc, out DateTime parsedFromUtc))
            {
                filter.FromUtc = parsedFromUtc.ToUniversalTime();
            }

            string? toUtc = req.QueryString["toUtc"];
            if (!String.IsNullOrEmpty(toUtc) && DateTime.TryParse(toUtc, out DateTime parsedToUtc))
            {
                filter.ToUtc = parsedToUtc.ToUniversalTime();
            }

            return filter;
        }

        private static ResponseContext ApplyScope(RequestContext req, RequestHistoryFilter filter, bool deleteOperation)
        {
            if (req.Auth == null || !req.Auth.IsAuthenticated)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.Unauthorized, null, "Authentication is required.");
            }

            if (req.Auth.IsAdmin)
            {
                return new ResponseContext(req);
            }

            if (String.IsNullOrEmpty(req.Auth.TenantId))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.Forbidden, null, "Tenant scope is required.");
            }

            if (!String.IsNullOrEmpty(filter.TenantId) && !String.Equals(filter.TenantId, req.Auth.TenantId, StringComparison.Ordinal))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.Forbidden, null, "Request history is limited to the authenticated tenant.");
            }

            filter.TenantId = req.Auth.TenantId;

            if (req.Auth.IsTenantAdmin)
            {
                return new ResponseContext(req);
            }

            filter.PrincipalId = req.Auth.PrincipalId;
            if (deleteOperation)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.Forbidden, null, "Regular users cannot delete request history.");
            }

            return new ResponseContext(req);
        }

        private ResponseContext? ApplyActiveHistoryRange(RequestContext req, RequestHistoryFilter filter)
        {
            DateTime? fromUtc = filter.FromUtc;
            ResponseContext? error = _ArchiveBoundary.ApplyActiveRange(req, ref fromUtc, filter.ToUtc, "RequestHistory");
            if (error != null) return error;
            filter.FromUtc = fromUtc;
            return null;
        }

        private static bool CanAccessEntry(RequestContext req, RequestHistoryEntry entry)
        {
            if (req.Auth?.IsAdmin == true) return true;
            if (!String.Equals(entry.TenantId, req.Auth?.TenantId, StringComparison.Ordinal)) return false;
            if (req.Auth?.IsTenantAdmin == true) return true;
            return String.Equals(entry.PrincipalId, req.Auth?.PrincipalId, StringComparison.Ordinal);
        }

        private static async Task SendResponseAsync(HttpContextBase ctx, ResponseContext resp)
        {
            ctx.Response.StatusCode = resp.StatusCode;
            ctx.Response.ContentType = Constants.JsonContentType;
            ctx.Response.Headers.Add(Constants.RequestIdHeader, resp.RequestId.ToString());

            object? body = resp.Success ? resp.Data : (object?)resp.Error;
            string json = JsonSerializer.Serialize(body, Constants.JsonOptions);
            await ctx.Response.Send(Encoding.UTF8.GetBytes(json)).ConfigureAwait(false);
        }
    }
}
