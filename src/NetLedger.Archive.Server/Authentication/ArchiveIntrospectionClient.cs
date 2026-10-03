namespace NetLedger.Archive.Server.Authentication
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Specialized;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Archive.Server.Services;
    using NetLedger.Archive.Server.Settings;
    using NetLedger.Telemetry;
    using SyslogLogging;
    using WatsonWebserver.Core;

    /// <summary>
    /// NetLedger Server introspection client for Archive Server authentication.
    /// </summary>
    internal sealed class ArchiveIntrospectionClient : IDisposable
    {
        private readonly AuthSettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly HttpClient _HttpClient;
        private readonly ConcurrentDictionary<string, ArchiveIntrospectionCacheEntry> _Cache = new ConcurrentDictionary<string, ArchiveIntrospectionCacheEntry>(StringComparer.Ordinal);
        private bool _Disposed = false;

        /// <summary>
        /// Instantiate the introspection client.
        /// </summary>
        /// <param name="settings">Authentication settings.</param>
        /// <param name="logging">Logging module.</param>
        public ArchiveIntrospectionClient(AuthSettings settings, LoggingModule logging)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _HttpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            ArchiveServerTelemetry.SetCacheSizeProvider(() => _Cache.Count);
        }

        /// <summary>
        /// Authenticate a Watson HTTP request through NetLedger Server introspection.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Archive authentication context.</returns>
        public async Task<ArchiveAuthContext> AuthenticateAsync(HttpContextBase ctx, CancellationToken token = default)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            using (TelemetryScope telemetry = ArchiveServerTelemetry.StartAuthentication(ResolveMethod()))
            {
                try
                {
                    ArchiveAuthContext auth = await AuthenticateCoreAsync(ctx, telemetry, token).ConfigureAwait(false);
                    telemetry.AddLabel(TelemetryNames.LabelResult, auth.IsNotRequired ? "notrequired" : auth.IsAuthenticated ? "success" : "failed");
                    telemetry.SetTag(TelemetryNames.AttributeTenantId, auth.TenantId);
                    telemetry.SetTag(TelemetryNames.AttributePrincipalId, auth.PrincipalId);
                    if (!auth.IsAuthenticated) telemetry.SetOutcome(TelemetryNames.OutcomeRejected);
                    return auth;
                }
                catch (Exception e)
                {
                    telemetry.AddLabel(TelemetryNames.LabelResult, "error");
                    telemetry.Fail(e);
                    throw;
                }
            }
        }

        private async Task<ArchiveAuthContext> AuthenticateCoreAsync(HttpContextBase ctx, TelemetryScope telemetry, CancellationToken token)
        {

            if (!_Settings.Enabled || String.Equals(_Settings.Mode, "None", StringComparison.OrdinalIgnoreCase))
            {
                return ArchiveAuthContext.NotRequired();
            }

            if (!String.Equals(_Settings.Mode, "NetLedgerIntrospection", StringComparison.OrdinalIgnoreCase))
            {
                return ArchiveAuthContext.Failed("Unsupported archive authentication mode.");
            }

            string cacheKey = BuildCacheKey(ctx.Request.Headers);
            if (!String.IsNullOrEmpty(cacheKey) &&
                _Cache.TryGetValue(cacheKey, out ArchiveIntrospectionCacheEntry? cached) &&
                cached.ExpiresUtc > DateTime.UtcNow)
            {
                ArchiveServerTelemetry.RecordCacheLookup(true);
                telemetry.SetTag("netledger.auth.cache", "hit");
                return cached.Context;
            }

            if (!String.IsNullOrEmpty(cacheKey))
            {
                ArchiveServerTelemetry.RecordCacheLookup(false);
                telemetry.SetTag("netledger.auth.cache", "miss");
            }

            ArchiveAuthContext auth = await IntrospectAsync(ctx, token).ConfigureAwait(false);
            if (auth.IsAuthenticated && !String.IsNullOrEmpty(cacheKey) && _Settings.IntrospectionCacheSeconds > 0)
            {
                _Cache[cacheKey] = new ArchiveIntrospectionCacheEntry
                {
                    Context = auth,
                    ExpiresUtc = DateTime.UtcNow.AddSeconds(_Settings.IntrospectionCacheSeconds)
                };
            }

            return auth;
        }

        /// <summary>
        /// Dispose managed resources.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            _HttpClient.Dispose();
        }

        private async Task<ArchiveAuthContext> IntrospectAsync(HttpContextBase ctx, CancellationToken token)
        {
            Uri endpoint = BuildEndpoint();
            using (TelemetryScope telemetry = NetLedgerTelemetry.StartIntegration(TelemetryNames.ServiceNetLedgerServer, "introspect"))
            using (HttpRequestMessage request = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, endpoint))
            {
                telemetry.SetTag(TelemetryNames.AttributeHttpMethod, "GET");
                CopyHeader(ctx.Request.Headers, request, "authorization");
                CopyHeader(ctx.Request.Headers, request, "x-token");
                CopyHeader(ctx.Request.Headers, request, "x-access-key");
                CopyHeader(ctx.Request.Headers, request, "x-secret-key");
                CopyHeader(ctx.Request.Headers, request, "x-tenant-id");
                NetLedgerTelemetry.InjectTraceContext(telemetry.Activity, request, (carrier, name, value) =>
                {
                    HttpRequestMessage message = (HttpRequestMessage)carrier;
                    message.Headers.Remove(name);
                    message.Headers.TryAddWithoutValidation(name, value);
                });

                try
                {
                    using (HttpResponseMessage response = await _HttpClient.SendAsync(request, token).ConfigureAwait(false))
                    {
                        int statusCode = (int)response.StatusCode;
                        telemetry.SetTag(TelemetryNames.AttributeHttpStatusCode, statusCode);
                        string body = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                        if (!response.IsSuccessStatusCode)
                        {
                            if (statusCode >= 500) telemetry.Fail("http_" + statusCode.ToString(), "NetLedger introspection returned HTTP " + statusCode.ToString());
                            else telemetry.SetOutcome(TelemetryNames.OutcomeRejected);
                            _Logging.Warn("[ArchiveIntrospectionClient] NetLedger introspection failed with HTTP " + ((int)response.StatusCode).ToString() + ".");
                            return ArchiveAuthContext.Failed("Authentication failed.");
                        }

                        ArchiveEffectivePermissionsResponse? permissions = JsonSerializer.Deserialize<ArchiveEffectivePermissionsResponse>(body, Constants.JsonOptions);
                        if (permissions == null)
                        {
                            return ArchiveAuthContext.Failed("Authentication introspection response was empty.");
                        }

                        return new ArchiveAuthContext
                        {
                            IsAuthenticated = true,
                            TenantId = permissions.TenantId,
                            PrincipalId = permissions.PrincipalId,
                            PrincipalType = permissions.PrincipalType,
                            IsAdmin = permissions.IsAdmin,
                            IsTenantAdmin = permissions.IsTenantAdmin,
                            Permissions = permissions.Permissions ?? new System.Collections.Generic.List<NetLedger.EffectivePermission>(),
                            MappedAccountIds = permissions.MappedAccountIds ?? new System.Collections.Generic.List<string>()
                        };
                    }
                }
                catch (OperationCanceledException e)
                {
                    telemetry.Fail(e);
                    throw;
                }
                catch (Exception e)
                {
                    telemetry.Fail(e);
                    NetLedgerTelemetry.RecordError(TelemetryNames.ComponentArchiveServer, e);
                    ArchiveServerTelemetry.LogWarning(e, "NetLedger introspection failed: {ErrorType}", NetLedgerTelemetry.GetErrorType(e));
                    _Logging.Warn("[ArchiveIntrospectionClient] NetLedger introspection error: " + e.Message);
                    return ArchiveAuthContext.Failed("Authentication introspection failed.");
                }
            }
        }

        private string ResolveMethod()
        {
            if (!_Settings.Enabled || String.Equals(_Settings.Mode, "None", StringComparison.OrdinalIgnoreCase)) return "disabled";
            return "introspection";
        }

        private Uri BuildEndpoint()
        {
            Uri baseUri = new Uri(_Settings.NetLedgerServerUrl.TrimEnd('/') + "/", UriKind.Absolute);
            return new Uri(baseUri, "v1/me/permissions");
        }

        private static void CopyHeader(NameValueCollection headers, HttpRequestMessage request, string name)
        {
            string value = GetHeader(headers, name);
            if (String.IsNullOrWhiteSpace(value)) return;
            request.Headers.TryAddWithoutValidation(name, value);
        }

        private static string BuildCacheKey(NameValueCollection headers)
        {
            StringBuilder builder = new StringBuilder();
            AddCacheHeader(builder, headers, "authorization");
            AddCacheHeader(builder, headers, "x-token");
            AddCacheHeader(builder, headers, "x-access-key");
            AddCacheHeader(builder, headers, "x-secret-key");
            AddCacheHeader(builder, headers, "x-tenant-id");
            if (builder.Length < 1) return String.Empty;

            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
            return Convert.ToHexString(hash);
        }

        private static void AddCacheHeader(StringBuilder builder, NameValueCollection headers, string name)
        {
            string value = GetHeader(headers, name);
            if (String.IsNullOrWhiteSpace(value)) return;
            builder.Append(name);
            builder.Append('=');
            builder.Append(value);
            builder.Append(';');
        }

        private static string GetHeader(NameValueCollection headers, string name)
        {
            if (headers == null) return String.Empty;
            for (int i = 0; i < headers.Count; i++)
            {
                string? key = headers.GetKey(i);
                if (String.IsNullOrEmpty(key)) continue;
                if (String.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return headers.Get(i) ?? String.Empty;
                }
            }

            return String.Empty;
        }
    }
}
