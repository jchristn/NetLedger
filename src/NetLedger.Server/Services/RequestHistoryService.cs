namespace NetLedger.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Database;
    using NetLedger.Server.Models;
    using NetLedger.Server.Settings;
    using SyslogLogging;
    using WatsonWebserver.Core;

    /// <summary>
    /// Captures HTTP request and response history.
    /// </summary>
    internal sealed class RequestHistoryService
    {
        private readonly DatabaseDriverBase _Driver;
        private readonly RequestHistorySettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[RequestHistoryService] ";

        /// <summary>
        /// Instantiate request history service.
        /// </summary>
        /// <param name="driver">Database driver.</param>
        /// <param name="settings">Request history settings.</param>
        /// <param name="logging">Logging module.</param>
        internal RequestHistoryService(DatabaseDriverBase driver, RequestHistorySettings settings, LoggingModule logging)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        /// <summary>
        /// Whether request history capture is enabled.
        /// </summary>
        internal bool Enabled
        {
            get
            {
                return _Settings.Enabled;
            }
        }

        /// <summary>
        /// Capture a request and response without blocking the response path.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <param name="req">Request context.</param>
        /// <param name="resp">Response context.</param>
        /// <param name="responseBody">Serialized response body.</param>
        internal void Capture(HttpContextBase ctx, RequestContext req, ResponseContext resp, string? responseBody)
        {
            if (!_Settings.Enabled) return;
            if (ctx == null || req == null || resp == null) return;
            if (!ShouldCapture(req.Url)) return;

            DateTime completedUtc = DateTime.UtcNow;
            RequestHistoryEntry entry = BuildEntry(ctx, req, resp, responseBody, completedUtc);

            _ = Task.Run(async () =>
            {
                try
                {
                    await _Driver.RequestHistory.CreateAsync(entry, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    _Logging.Warn(_Header + "failed to capture request history: " + e.Message);
                }
            });
        }

        /// <summary>
        /// Prune old request history records.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Deleted record count.</returns>
        internal async Task<long> PruneAsync(CancellationToken token = default)
        {
            DateTime cutoffUtc = DateTime.UtcNow.AddDays(-_Settings.RetentionDays);
            return await _Driver.RequestHistory.PruneAsync(cutoffUtc, token).ConfigureAwait(false);
        }

        private RequestHistoryEntry BuildEntry(HttpContextBase ctx, RequestContext req, ResponseContext resp, string? responseBody, DateTime completedUtc)
        {
            string? requestBody = req.Data == null ? null : Encoding.UTF8.GetString(req.Data);
            long requestBodyBytes = req.Data == null ? 0 : req.Data.Length;
            long responseBodyBytes = responseBody == null ? 0 : Encoding.UTF8.GetByteCount(responseBody);

            return new RequestHistoryEntry
            {
                TenantId = req.Auth?.TenantId ?? req.TenantId,
                PrincipalId = req.Auth?.PrincipalId,
                PrincipalType = req.Auth?.PrincipalType,
                Method = req.Method.ToString().ToUpperInvariant(),
                Path = req.Url,
                Url = req.RawUrlWithQuery,
                StatusCode = resp.StatusCode,
                DurationMs = ctx.Timestamp.TotalMs ?? Math.Max(0, (completedUtc - req.TimestampUtc).TotalMilliseconds),
                SourceIp = req.SourceIp,
                RequestHeaders = RedactHeaders(ctx.Request.Headers),
                RequestBody = Truncate(requestBody, _Settings.MaxRequestBodyBytes),
                RequestBodyBytes = requestBodyBytes,
                RequestBodyTruncated = requestBody != null && Encoding.UTF8.GetByteCount(requestBody) > _Settings.MaxRequestBodyBytes,
                ResponseHeaders = CaptureResponseHeaders(ctx.Response.Headers),
                ResponseBody = Truncate(responseBody, _Settings.MaxResponseBodyBytes),
                ResponseBodyBytes = responseBodyBytes,
                ResponseBodyTruncated = responseBody != null && responseBodyBytes > _Settings.MaxResponseBodyBytes,
                CreatedUtc = req.TimestampUtc,
                CompletedUtc = completedUtc
            };
        }

        private static bool ShouldCapture(string path)
        {
            if (String.IsNullOrEmpty(path)) return false;
            if (path.Equals("/", StringComparison.Ordinal)) return false;
            if (path.Equals("/openapi.json", StringComparison.OrdinalIgnoreCase)) return false;
            if (path.StartsWith("/v1/auth/", StringComparison.OrdinalIgnoreCase)) return false;
            if (path.StartsWith("/v1/request-history", StringComparison.OrdinalIgnoreCase)) return false;
            if (path.StartsWith("/v1.0/api/request-history", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        private static Dictionary<string, string> RedactHeaders(NameValueCollection headers)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (headers == null) return result;

            for (int i = 0; i < headers.Count; i++)
            {
                string? key = headers.GetKey(i);
                if (String.IsNullOrEmpty(key)) continue;
                string value = headers.Get(i) ?? String.Empty;
                result[key] = IsSensitiveHeader(key) ? "[redacted]" : value;
            }

            return result;
        }

        private static Dictionary<string, string> CaptureResponseHeaders(NameValueCollection headers)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (headers == null) return result;

            for (int i = 0; i < headers.Count; i++)
            {
                string? key = headers.GetKey(i);
                if (String.IsNullOrEmpty(key)) continue;
                result[key] = headers.Get(i) ?? String.Empty;
            }

            return result;
        }

        private static bool IsSensitiveHeader(string key)
        {
            return key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("Cookie", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("api-key", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("token", StringComparison.OrdinalIgnoreCase);
        }

        private static string? Truncate(string? value, int maxBytes)
        {
            if (value == null) return null;
            if (maxBytes <= 0) return String.Empty;

            byte[] bytes = Encoding.UTF8.GetBytes(value);
            if (bytes.Length <= maxBytes) return value;
            return Encoding.UTF8.GetString(bytes, 0, maxBytes);
        }
    }
}
