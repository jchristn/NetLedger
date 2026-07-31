namespace NetLedger.Archive.Server
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.IO;
    using System.IO.Compression;
    using System.Net;
    using System.Reflection;
    using System.Runtime.Loader;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger;
    using NetLedger.Archive.Catalog;
    using NetLedger.Archive.Catalog.Sql;
    using NetLedger.Archive.Models;
    using NetLedger.Archive.Requests;
    using NetLedger.Archive.Responses;
    using NetLedger.Archive.Server.API.Routes;
    using NetLedger.Archive.Server.Authentication;
    using NetLedger.Archive.Server.Models;
    using NetLedger.Archive.Server.Settings;
    using NetLedger.Archive.Settings;
    using NetLedger.Archive.Storage;
    using NetLedger.Database;
    using SyslogLogging;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using ArchiveLoggingSettings = NetLedger.Archive.Server.Settings.LoggingSettings;
    using ArchiveWebserverSettings = NetLedger.Archive.Server.Settings.WebserverSettings;

    /// <summary>
    /// NetLedger Archive REST API server.
    /// </summary>
    public class NetLedgerArchiveServer
    {
        private const long ArchiveCoverageToleranceTicks = 10;
        private static readonly string _Header = "[NetLedgerArchiveServer] ";
        private static readonly DateTime _StartTimeUtc = DateTime.UtcNow;
        private static string _SettingsFile = "./netledger.json";
        private static string _Hostname = Environment.MachineName;
        private static ArchiveServerSettings _Settings = null!;
        private static LoggingModule _Logging = null!;
        private static Webserver _Webserver = null!;
        private static IArchiveCatalog _Catalog = null!;
        private static ArchiveIntrospectionClient _AuthenticationService = null!;
        private static Dictionary<string, IArchiveObjectStore> _ObjectStores = new Dictionary<string, IArchiveObjectStore>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Run the archive server.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <returns>Exit code.</returns>
        public static async Task<int> RunAsync(string[] args)
        {
            ParseArguments(args);
            InitializeSettings();
            InitializeLogging();
            _AuthenticationService = new ArchiveIntrospectionClient(_Settings.Authentication, _Logging);
            await InitializeCatalogAsync().ConfigureAwait(false);
            InitializeWebserver();

            EventWaitHandle waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);

            AssemblyLoadContext.Default.Unloading += (ctx) =>
            {
                _Logging.Info(_Header + "received unload signal");
                waitHandle.Set();
            };

            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                _Logging.Info(_Header + "received cancel signal");
                eventArgs.Cancel = true;
                waitHandle.Set();
            };

            _Logging.Info(_Header + "archive server running, press CTRL+C to exit");
            await Task.Run(() => waitHandle.WaitOne()).ConfigureAwait(false);

            _Logging.Info(_Header + "shutting down");
            _Webserver.Stop();
            if (_AuthenticationService != null) _AuthenticationService.Dispose();
            if (_Catalog != null) await _Catalog.DisposeAsync().ConfigureAwait(false);
            return 0;
        }

        private static void ParseArguments(string[] args)
        {
            if (args == null) return;

            for (int i = 0; i < args.Length; i++)
            {
                if (String.Equals(args[i], "-f", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(args[i], "--file", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length)
                    {
                        _SettingsFile = args[i + 1];
                        i++;
                    }
                }
            }
        }

        private static void InitializeSettings()
        {
            if (!File.Exists(_SettingsFile))
            {
                _Settings = new ArchiveServerSettings();
                string json = JsonSerializer.Serialize(_Settings, Constants.JsonOptions);
                File.WriteAllText(_SettingsFile, json, Encoding.UTF8);
            }
            else
            {
                string json = File.ReadAllText(_SettingsFile, Encoding.UTF8);
                _Settings = JsonSerializer.Deserialize<ArchiveServerSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                }) ?? new ArchiveServerSettings();
            }

            ApplyEnvironmentOverrides();
            ValidateSettings();
        }

        private static void ApplyEnvironmentOverrides()
        {
            if (_Settings.Catalog == null) _Settings.Catalog = new ArchiveCatalogSettings();
            if (_Settings.Archive == null) _Settings.Archive = new ArchiveRuntimeSettings();
            if (_Settings.StoragePools == null) _Settings.StoragePools = new List<ArchiveStoragePoolSettings>();

            ApplyDatabaseEnvironmentOverrides(_Settings.Catalog, "NETLEDGER_ARCHIVE_CATALOG_");
            ApplyStringEnvironmentOverride("NETLEDGER_ARCHIVE_DEFAULT_STORAGE_POOL_ID", value => _Settings.Archive.DefaultStoragePoolId = value);
            ApplyBoolEnvironmentOverride("NETLEDGER_ARCHIVE_REQUIRE_COMPLETE_COVERAGE", value => _Settings.Archive.RequireCompleteCoverage = value);
            ApplyIntEnvironmentOverride("NETLEDGER_ARCHIVE_MAX_ENUMERATION_RESULTS", value => _Settings.Archive.MaxEnumerationResults = value);
            ApplyIntEnvironmentOverride("NETLEDGER_ARCHIVE_MAX_MIGRATION_BATCH_ROWS", value => _Settings.Archive.MaxMigrationBatchRows = value);
            ApplyLongEnvironmentOverride("NETLEDGER_ARCHIVE_MAX_MIGRATION_BATCH_BYTES", value => _Settings.Archive.MaxMigrationBatchBytes = value);

            for (int i = 0; i < _Settings.StoragePools.Count; i++)
            {
                ArchiveStoragePoolSettings pool = _Settings.StoragePools[i];
                ApplyStoragePoolEnvironmentOverrides(pool, "NETLEDGER_ARCHIVE_STORAGE_" + NormalizeEnvironmentSegment(pool.Id) + "_");
                if (i == 0 || String.Equals(pool.Id, _Settings.Archive.DefaultStoragePoolId, StringComparison.OrdinalIgnoreCase))
                {
                    ApplyStoragePoolEnvironmentOverrides(pool, "NETLEDGER_ARCHIVE_STORAGE_");
                }
            }
        }

        private static void ApplyDatabaseEnvironmentOverrides(DatabaseSettings settings, string prefix)
        {
            ApplyDatabaseTypeEnvironmentOverride(prefix + "TYPE", value => settings.Type = value);
            ApplyStringEnvironmentOverride(prefix + "FILENAME", value => settings.Filename = value);
            ApplyStringEnvironmentOverride(prefix + "HOSTNAME", value => settings.Hostname = value);
            ApplyIntEnvironmentOverride(prefix + "PORT", value => settings.Port = value);
            ApplyStringEnvironmentOverride(prefix + "USERNAME", value => settings.Username = value);
            ApplyStringEnvironmentOverride(prefix + "PASSWORD", value => settings.Password = value);
            ApplyStringEnvironmentOverride(prefix + "DATABASE_NAME", value => settings.DatabaseName = value);
            ApplyStringEnvironmentOverride(prefix + "INSTANCE", value => settings.Instance = value);
            ApplyStringEnvironmentOverride(prefix + "SCHEMA", value => settings.Schema = value);
            ApplyBoolEnvironmentOverride(prefix + "LOG_QUERIES", value => settings.LogQueries = value);
            ApplyBoolEnvironmentOverride(prefix + "REQUIRE_ENCRYPTION", value => settings.RequireEncryption = value);
            ApplyIntEnvironmentOverride(prefix + "CONNECTION_TIMEOUT_SECONDS", value => settings.ConnectionTimeoutSeconds = value);
            ApplyIntEnvironmentOverride(prefix + "MAX_POOL_SIZE", value => settings.MaxPoolSize = value);
        }

        private static void ApplyStoragePoolEnvironmentOverrides(ArchiveStoragePoolSettings pool, string prefix)
        {
            ApplyStringEnvironmentOverride(prefix + "BASE_PATH", value => pool.BasePath = value);
            ApplyStringEnvironmentOverride(prefix + "BUCKET", value => pool.Bucket = value);
            ApplyStringEnvironmentOverride(prefix + "PREFIX", value => pool.Prefix = value);
            ApplyStringEnvironmentOverride(prefix + "REGION", value => pool.Region = value);
            ApplyStringEnvironmentOverride(prefix + "ENDPOINT", value => pool.Endpoint = value);
            ApplyStringEnvironmentOverride(prefix + "ACCESS_KEY", value => pool.AccessKey = value);
            ApplyStringEnvironmentOverride(prefix + "SECRET_KEY", value => pool.SecretKey = value);
            ApplyStringEnvironmentOverride(prefix + "SESSION_TOKEN", value => pool.SessionToken = value);
            ApplyStringEnvironmentOverride(prefix + "SERVER_SIDE_ENCRYPTION", value => pool.ServerSideEncryption = value);
            ApplyArchiveStoragePoolTypeEnvironmentOverride(prefix + "TYPE", value => pool.Type = value);
            ApplyArchiveFormatEnvironmentOverride(prefix + "FORMAT", value => pool.Format = value);
            ApplyArchiveCompressionEnvironmentOverride(prefix + "COMPRESSION", value => pool.Compression = value);
        }

        private static string NormalizeEnvironmentSegment(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return String.Empty;
            StringBuilder builder = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (Char.IsLetterOrDigit(c))
                {
                    builder.Append(Char.ToUpperInvariant(c));
                }
                else
                {
                    builder.Append('_');
                }
            }

            return builder.ToString();
        }

        private static void ApplyStringEnvironmentOverride(string name, Action<string> setter)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            if (value == null) return;
            setter(value);
        }

        private static void ApplyBoolEnvironmentOverride(string name, Action<bool> setter)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            if (String.IsNullOrWhiteSpace(value)) return;
            if (!Boolean.TryParse(value, out bool parsed))
            {
                throw new InvalidOperationException("Environment variable " + name + " must be true or false.");
            }

            setter(parsed);
        }

        private static void ApplyIntEnvironmentOverride(string name, Action<int> setter)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            if (String.IsNullOrWhiteSpace(value)) return;
            if (!Int32.TryParse(value, out int parsed))
            {
                throw new InvalidOperationException("Environment variable " + name + " must be an integer.");
            }

            setter(parsed);
        }

        private static void ApplyLongEnvironmentOverride(string name, Action<long> setter)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            if (String.IsNullOrWhiteSpace(value)) return;
            if (!Int64.TryParse(value, out long parsed))
            {
                throw new InvalidOperationException("Environment variable " + name + " must be an integer.");
            }

            setter(parsed);
        }

        private static void ApplyDatabaseTypeEnvironmentOverride(string name, Action<DatabaseTypeEnum> setter)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            if (String.IsNullOrWhiteSpace(value)) return;
            if (!Enum.TryParse(value, true, out DatabaseTypeEnum parsed))
            {
                throw new InvalidOperationException("Environment variable " + name + " must be a valid database provider.");
            }

            setter(parsed);
        }

        private static void ApplyArchiveStoragePoolTypeEnvironmentOverride(string name, Action<ArchiveStoragePoolType> setter)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            if (String.IsNullOrWhiteSpace(value)) return;
            if (!Enum.TryParse(value, true, out ArchiveStoragePoolType parsed))
            {
                throw new InvalidOperationException("Environment variable " + name + " must be a valid archive storage pool type.");
            }

            setter(parsed);
        }

        private static void ApplyArchiveFormatEnvironmentOverride(string name, Action<ArchiveFormat> setter)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            if (String.IsNullOrWhiteSpace(value)) return;
            if (!Enum.TryParse(value, true, out ArchiveFormat parsed))
            {
                throw new InvalidOperationException("Environment variable " + name + " must be a valid archive format.");
            }

            setter(parsed);
        }

        private static void ApplyArchiveCompressionEnvironmentOverride(string name, Action<ArchiveCompression> setter)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            if (String.IsNullOrWhiteSpace(value)) return;
            if (!Enum.TryParse(value, true, out ArchiveCompression parsed))
            {
                throw new InvalidOperationException("Environment variable " + name + " must be a valid archive compression value.");
            }

            setter(parsed);
        }

        private static void ValidateSettings()
        {
            if (_Settings.Webserver == null) _Settings.Webserver = new ArchiveWebserverSettings();
            if (_Settings.Webserver.Cors == null) _Settings.Webserver.Cors = new CorsSettings();
            if (_Settings.Logging == null) _Settings.Logging = new ArchiveLoggingSettings();
            if (_Settings.Authentication == null) _Settings.Authentication = new AuthSettings();
            if (_Settings.Catalog == null) _Settings.Catalog = new ArchiveCatalogSettings();
            if (_Settings.Archive == null) _Settings.Archive = new ArchiveRuntimeSettings();
            if (_Settings.RequestHistory == null) _Settings.RequestHistory = new RequestHistorySettings();
            if (_Settings.StoragePools == null) _Settings.StoragePools = new List<ArchiveStoragePoolSettings>();

            ValidateSupportedFormats();

            if (_Settings.Webserver.Cors.AllowCredentials && ContainsWildcard(_Settings.Webserver.Cors.AllowedOrigins))
            {
                throw new InvalidOperationException("Webserver.Cors.AllowCredentials cannot be true when Webserver.Cors.AllowedOrigins contains '*'.");
            }

            if (_Settings.Authentication.Enabled)
            {
                if (String.IsNullOrWhiteSpace(_Settings.Authentication.Mode))
                {
                    throw new InvalidOperationException("Authentication.Mode is required when Authentication.Enabled is true.");
                }

                if (String.Equals(_Settings.Authentication.Mode, "NetLedgerIntrospection", StringComparison.OrdinalIgnoreCase))
                {
                    if (String.IsNullOrWhiteSpace(_Settings.Authentication.NetLedgerServerUrl))
                    {
                        throw new InvalidOperationException("Authentication.NetLedgerServerUrl is required for NetLedgerIntrospection mode.");
                    }

                    if (!Uri.TryCreate(_Settings.Authentication.NetLedgerServerUrl, UriKind.Absolute, out Uri? netLedgerUri))
                    {
                        throw new InvalidOperationException("Authentication.NetLedgerServerUrl must be an absolute URI.");
                    }

                    bool httpScheme = String.Equals(netLedgerUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
                    bool httpsScheme = String.Equals(netLedgerUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
                    if (!httpScheme && !httpsScheme)
                    {
                        throw new InvalidOperationException("Authentication.NetLedgerServerUrl must use HTTP or HTTPS.");
                    }

                    if (_Settings.Authentication.RequireTlsForSecrets && httpScheme && !netLedgerUri.IsLoopback)
                    {
                        throw new InvalidOperationException("Authentication.RequireTlsForSecrets requires HTTPS for non-loopback NetLedgerServerUrl values.");
                    }
                }
            }

            foreach (ArchiveStoragePoolSettings pool in _Settings.StoragePools)
            {
                if (pool == null) continue;
                if (String.IsNullOrWhiteSpace(pool.Id)) throw new InvalidOperationException("Storage pool Id is required.");
                if (String.IsNullOrWhiteSpace(pool.Name)) throw new InvalidOperationException("Storage pool Name is required.");
                if (pool.Format != ArchiveFormat.JsonlGzip)
                {
                    throw new InvalidOperationException("Storage pool Format must be JsonlGzip in this v4 build.");
                }

                if (pool.Type == ArchiveStoragePoolType.FileSystem && String.IsNullOrWhiteSpace(pool.BasePath))
                {
                    throw new InvalidOperationException("Filesystem storage pools require BasePath.");
                }

                if (pool.Type == ArchiveStoragePoolType.S3 && String.IsNullOrWhiteSpace(pool.Bucket))
                {
                    throw new InvalidOperationException("S3 storage pools require Bucket.");
                }
            }
        }

        private static void ValidateSupportedFormats()
        {
            if (_Settings.Archive.PreferredFormat != ArchiveFormat.JsonlGzip)
            {
                throw new InvalidOperationException("Archive.PreferredFormat must be JsonlGzip in this v4 build.");
            }

            if (_Settings.Archive.AcceptedFormats == null)
            {
                _Settings.Archive.AcceptedFormats = new List<ArchiveFormat> { ArchiveFormat.JsonlGzip };
                return;
            }

            if (_Settings.Archive.AcceptedFormats.Count == 0)
            {
                _Settings.Archive.AcceptedFormats.Add(ArchiveFormat.JsonlGzip);
                return;
            }

            foreach (ArchiveFormat format in _Settings.Archive.AcceptedFormats)
            {
                if (format != ArchiveFormat.JsonlGzip)
                {
                    throw new InvalidOperationException("Archive.AcceptedFormats can contain only JsonlGzip in this v4 build.");
                }
            }
        }

        private static void InitializeLogging()
        {
            _Logging = new LoggingModule();
            _Logging.Settings.EnableConsole = _Settings.Logging.EnableConsole;
        }

        private static async Task InitializeCatalogAsync()
        {
            _Catalog = new ArchiveSqlCatalog(_Settings.Catalog);
            await _Catalog.InitializeAsync().ConfigureAwait(false);

            foreach (ArchiveStoragePoolSettings poolSettings in _Settings.StoragePools)
            {
                ArchiveStoragePool pool = ToStoragePool(poolSettings);
                await _Catalog.StoragePools.UpsertAsync(pool).ConfigureAwait(false);
                _ObjectStores[pool.Id] = ArchiveObjectStoreFactory.Create(poolSettings);

                _Logging.Info(_Header + "storage pool loaded: " + pool.Id + " (" + pool.Name + ", " + pool.Type + ")");
            }

            _Logging.Info(_Header + "archive catalog initialized using " + _Settings.Catalog.Type + ".");
        }

        private static void InitializeWebserver()
        {
            WatsonWebserver.Core.WebserverSettings wsSettings = new WatsonWebserver.Core.WebserverSettings(
                _Settings.Webserver.Hostname,
                _Settings.Webserver.Port,
                _Settings.Webserver.Ssl);

            _Webserver = new Webserver(wsSettings, DefaultRoute);
            _Webserver.Events.ExceptionEncountered += WebserverException;
            _Webserver.Routes.Preflight = PreflightHandler;
            _Webserver.Routes.PreRouting = PreRoutingHandler;
            _Webserver.Routes.PostRouting = PostRoutingHandler;
            RegisterRoutes();

            _Logging.Info(_Header + "webserver starting on " +
                (_Settings.Webserver.Ssl ? "https" : "http") + "://" +
                _Settings.Webserver.Hostname + ":" + _Settings.Webserver.Port);
            _Webserver.Start();
        }

        private static void RegisterRoutes()
        {
            ArchiveServiceRoutes.Register(_Webserver, ExistsAsync, GetServiceAsync, GetHealthAsync, OpenApiAsync);

            string[] prefixes = new string[] { "/v1", "/api/v1" };
            foreach (string prefix in prefixes)
            {
                ArchiveMetadataRoutes.Register(
                    _Webserver,
                    prefix,
                    GetArchiveRangesAsync,
                    GetArchiveManifestsAsync,
                    GetArchiveManifestAsync,
                    GetArchiveManifestObjectsAsync,
                    GetArchiveObjectMetadataAsync,
                    GetArchiveManifestCheckpointsAsync,
                    ArchiveMetadataActionAsync,
                    GetArchiveStoragePoolsAsync,
                    GetStoragePoolHealthAsync,
                    ExceptionHandler);

                ArchivedLedgerRoutes.Register(
                    _Webserver,
                    prefix,
                    GetArchivedEntriesAsync,
                    GetArchivedBalanceAsOfAsync,
                    VerifyArchivedBalanceChainAsync,
                    ExceptionHandler);

                ArchivedRequestHistoryRoutes.Register(
                    _Webserver,
                    prefix,
                    GetArchivedRequestHistoryAsync,
                    GetArchivedRequestHistorySummaryAsync,
                    GetArchivedRequestHistoryEntryAsync,
                    ExceptionHandler);

                ArchiveMigrationRoutes.Register(
                    _Webserver,
                    prefix,
                    GetArchiveMigrationsAsync,
                    CreateArchiveMigrationAsync,
                    GetArchiveMigrationAsync,
                    GetArchiveMigrationBatchesAsync,
                    CreateArchiveMigrationBatchAsync,
                    UploadArchiveMigrationBatchContentAsync,
                    SealArchiveMigrationAsync,
                    CommitArchiveMigrationAsync,
                    AbortArchiveMigrationAsync,
                    ExceptionHandler);

                ArchiveServerRequestHistoryRoutes.Register(
                    _Webserver,
                    prefix,
                    GetArchiveServerRequestHistoryAsync,
                    GetArchiveServerRequestHistorySummaryAsync,
                    GetArchiveServerRequestHistoryEntryAsync,
                    ExceptionHandler);

                ArchiveMutationRejectionRoutes.Register(_Webserver, prefix, MutationNotAllowedAsync, ExceptionHandler);
            }
        }

        private static async Task PreflightHandler(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 200;
            AddCorsHeaders(ctx, true);
            await ctx.Response.Send().ConfigureAwait(false);
        }

        private static async Task PreRoutingHandler(HttpContextBase ctx)
        {
            SetHeader(ctx, Constants.HostnameHeader, _Hostname);
            SetHeader(ctx, Constants.RequestIdHeader, ctx.Guid.ToString());
            SetHeader(ctx, Constants.ApiVersionHeader, Constants.CurrentApiVersion);
            SetHeader(ctx, Constants.DataScopeHeader, "archive");
            ctx.Response.ContentType = Constants.JsonContentType;
            AddCorsHeaders(ctx, false);

            if (IsPreAuthenticationRoute(ctx))
            {
                await Task.CompletedTask.ConfigureAwait(false);
                return;
            }

            ArchiveAuthContext auth = await _AuthenticationService.AuthenticateAsync(ctx, ctx.Token).ConfigureAwait(false);
            if (!auth.IsAuthenticated)
            {
                _Logging.Warn(_Header + "authentication failed from " + ctx.Request.Source.IpAddress + ".");
                await SendErrorAsync(ctx, ArchiveApiErrorCode.Unauthorized, auth.ErrorMessage ?? "Authentication failed.").ConfigureAwait(false);
                return;
            }

            ctx.Metadata = auth;
        }

        private static async Task PostRoutingHandler(HttpContextBase ctx)
        {
            if (_Settings.Logging.LogRequests)
            {
                _Logging.Debug(_Header + ctx.Request.Method + " " + ctx.Request.Url.RawWithQuery + " " + ctx.Response.StatusCode);
            }

            CaptureArchiveServerRequestHistory(ctx);
            await Task.CompletedTask.ConfigureAwait(false);
        }

        private static async Task ExistsAsync(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send().ConfigureAwait(false);
        }

        private static async Task GetServiceAsync(HttpContextBase ctx)
        {
            Version? version = Assembly.GetExecutingAssembly().GetName().Version;
            TimeSpan uptime = DateTime.UtcNow - _StartTimeUtc;
            ArchiveServiceInfo info = new ArchiveServiceInfo
            {
                Version = version?.ToString() ?? "4.0.0",
                StartTimeUtc = _StartTimeUtc,
                UptimeSeconds = (long)uptime.TotalSeconds
            };

            await SendJsonAsync(ctx, 200, info).ConfigureAwait(false);
        }

        private static async Task GetHealthAsync(HttpContextBase ctx)
        {
            ArchiveHealthResponse health = new ArchiveHealthResponse
            {
                Healthy = true,
                Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "4.0.0"
            };
            health.Details.Add("Archive server process is running.");
            health.Details.Add("Catalog provider is configured as " + _Settings.Catalog.Type + ".");
            health.Details.Add("Storage pools configured: " + _Settings.StoragePools.Count + ".");
            await SendJsonAsync(ctx, 200, health).ConfigureAwait(false);
        }

        private static async Task GetArchiveRangesAsync(HttpContextBase ctx)
        {
            ArchiveQuery query = BuildArchiveQuery(ctx);
            if (!await AuthorizeArchiveReadAsync(ctx, query.TenantId, "Archive", query.AccountId).ConfigureAwait(false)) return;

            EnumerationResult<ArchiveRangeInfo> result = await _Catalog.Ranges.EnumerateAsync(query).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, result.Objects).ConfigureAwait(false);
        }

        private static async Task GetArchiveManifestsAsync(HttpContextBase ctx)
        {
            ArchiveQuery query = BuildArchiveQuery(ctx);
            if (!await AuthorizeArchiveReadAsync(ctx, query.TenantId, "Archive", query.AccountId).ConfigureAwait(false)) return;

            EnumerationResult<ArchiveManifest> result = await _Catalog.Manifests.EnumerateAsync(query).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, result.Objects).ConfigureAwait(false);
        }

        private static async Task GetArchiveManifestAsync(HttpContextBase ctx)
        {
            string? manifestId = GetRouteParameter(ctx, "manifestId");
            if (String.IsNullOrWhiteSpace(manifestId))
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "Manifest ID is required.").ConfigureAwait(false);
                return;
            }

            ArchiveManifest? manifest = await _Catalog.Manifests.ReadByIdAsync(manifestId).ConfigureAwait(false);
            if (manifest == null)
            {
                await SendNotFoundAsync(ctx, "Archive manifest was not found.").ConfigureAwait(false);
                return;
            }

            if (!await AuthorizeArchiveReadAsync(ctx, manifest.TenantId, "Archive", manifest.AccountId).ConfigureAwait(false)) return;

            await SendJsonAsync(ctx, 200, manifest).ConfigureAwait(false);
        }

        private static async Task GetArchiveManifestObjectsAsync(HttpContextBase ctx)
        {
            string? manifestId = GetRouteParameter(ctx, "manifestId");
            if (String.IsNullOrWhiteSpace(manifestId))
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "Manifest ID is required.").ConfigureAwait(false);
                return;
            }

            ArchiveManifest? manifest = await _Catalog.Manifests.ReadByIdAsync(manifestId).ConfigureAwait(false);
            if (manifest == null)
            {
                await SendNotFoundAsync(ctx, "Archive manifest was not found.").ConfigureAwait(false);
                return;
            }

            if (!await AuthorizeArchiveReadAsync(ctx, manifest.TenantId, "Archive", manifest.AccountId).ConfigureAwait(false)) return;

            EnumerationResult<ArchiveObject> result = await _Catalog.Objects.EnumerateByManifestAsync(manifestId, BuildArchiveQuery(ctx)).ConfigureAwait(false);
            if (!CanExposeArchiveObjectDebugMetadata(ctx))
            {
                result.Objects = SanitizeArchiveObjects(result.Objects);
            }

            await SendJsonAsync(ctx, 200, result.Objects).ConfigureAwait(false);
        }

        private static async Task GetArchiveObjectMetadataAsync(HttpContextBase ctx)
        {
            string? objectId = GetRouteParameter(ctx, "objectId");
            if (String.IsNullOrWhiteSpace(objectId))
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "Archive object ID is required.").ConfigureAwait(false);
                return;
            }

            ArchiveObject? archiveObject = await _Catalog.Objects.ReadByIdAsync(objectId).ConfigureAwait(false);
            if (archiveObject == null)
            {
                await SendNotFoundAsync(ctx, "Archive object was not found.").ConfigureAwait(false);
                return;
            }

            ArchiveManifest? manifest = await _Catalog.Manifests.ReadByIdAsync(archiveObject.ManifestId).ConfigureAwait(false);
            if (manifest == null)
            {
                await SendNotFoundAsync(ctx, "Archive manifest was not found.").ConfigureAwait(false);
                return;
            }

            if (!await AuthorizeArchiveAdminAsync(ctx, manifest.TenantId, "ArchiveObject", objectId, "Read").ConfigureAwait(false)) return;

            if (!TryGetObjectStore(archiveObject.StoragePoolId, out IArchiveObjectStore? store))
            {
                await SendNotImplementedAsync(ctx, "Configured archive object storage provider is not available.").ConfigureAwait(false);
                return;
            }

            ArchiveObjectMetadata metadata = await store!.ReadMetadataAsync(archiveObject.RelativePath, ctx.Token).ConfigureAwait(false);
            object response = new
            {
                ObjectId = archiveObject.Id,
                ManifestId = archiveObject.ManifestId,
                StoragePoolId = archiveObject.StoragePoolId,
                CatalogByteCount = archiveObject.ByteCount,
                CatalogContentHashSha256 = archiveObject.ContentHashSha256,
                Storage = metadata
            };
            await SendJsonAsync(ctx, 200, response).ConfigureAwait(false);
        }

        private static async Task GetArchiveManifestCheckpointsAsync(HttpContextBase ctx)
        {
            string? manifestId = GetRouteParameter(ctx, "manifestId");
            if (String.IsNullOrWhiteSpace(manifestId))
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "Manifest ID is required.").ConfigureAwait(false);
                return;
            }

            ArchiveManifest? manifest = await _Catalog.Manifests.ReadByIdAsync(manifestId).ConfigureAwait(false);
            if (manifest == null)
            {
                await SendNotFoundAsync(ctx, "Archive manifest was not found.").ConfigureAwait(false);
                return;
            }

            if (!await AuthorizeArchiveReadAsync(ctx, manifest.TenantId, "Archive", manifest.AccountId).ConfigureAwait(false)) return;

            EnumerationResult<ArchiveBalanceCheckpoint> result = await _Catalog.BalanceCheckpoints.EnumerateByManifestAsync(manifestId, BuildArchiveQuery(ctx)).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, result.Objects).ConfigureAwait(false);
        }

        private static async Task ArchiveMetadataActionAsync(HttpContextBase ctx)
        {
            string? manifestId = GetRouteParameter(ctx, "manifestId");
            if (String.IsNullOrWhiteSpace(manifestId))
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "Manifest ID is required.").ConfigureAwait(false);
                return;
            }

            ArchiveManifest? existing = await _Catalog.Manifests.ReadByIdAsync(manifestId).ConfigureAwait(false);
            if (existing == null)
            {
                await SendNotFoundAsync(ctx, "Archive manifest was not found.").ConfigureAwait(false);
                return;
            }

            if (!await AuthorizeArchiveManageAsync(ctx, existing.TenantId, "ArchiveManifest", manifestId, "Update").ConfigureAwait(false)) return;

            ArchiveManifestStatus status;
            string actionName;
            string rawPath = ctx.Request.Url.RawWithoutQuery ?? String.Empty;
            if (rawPath.EndsWith("/verify", StringComparison.OrdinalIgnoreCase))
            {
                status = ArchiveManifestStatus.Committed;
                actionName = "verify";
            }
            else if (rawPath.EndsWith("/quarantine", StringComparison.OrdinalIgnoreCase))
            {
                status = ArchiveManifestStatus.Quarantined;
                actionName = "quarantine";
            }
            else if (rawPath.EndsWith("/supersede", StringComparison.OrdinalIgnoreCase))
            {
                status = ArchiveManifestStatus.Superseded;
                actionName = "supersede";
            }
            else
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "Unsupported archive metadata action.").ConfigureAwait(false);
                return;
            }

            if (!IsManifestStatusTransitionAllowed(existing.Status, status))
            {
                await WriteArchiveAuditAsync(ctx, existing.TenantId, "ManifestStatusUpdateDenied", "ArchiveManifest", existing.Id, "Denied", "Invalid " + actionName + " transition from " + existing.Status.ToString()).ConfigureAwait(false);
                await SendErrorAsync(ctx, ArchiveApiErrorCode.Conflict, "Invalid archive manifest status transition from " + existing.Status.ToString() + " to " + status.ToString() + ".").ConfigureAwait(false);
                return;
            }

            if (existing.Status == status)
            {
                await SendJsonAsync(ctx, 200, existing).ConfigureAwait(false);
                return;
            }

            ArchiveManifest manifest = await _Catalog.Manifests.UpdateStatusAsync(manifestId, status).ConfigureAwait(false);
            await WriteArchiveAuditAsync(ctx, manifest.TenantId, "ManifestStatusUpdated", "ArchiveManifest", manifest.Id, "Permit", "Status set to " + status.ToString()).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, manifest).ConfigureAwait(false);
        }

        private static async Task GetArchiveStoragePoolsAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeArchiveAdminAsync(ctx, null, "ArchiveStoragePool", null, "Read").ConfigureAwait(false)) return;

            EnumerationResult<ArchiveStoragePool> result = await _Catalog.StoragePools.EnumerateAsync(BuildArchiveQuery(ctx)).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, result.Objects).ConfigureAwait(false);
        }

        private static async Task GetStoragePoolHealthAsync(HttpContextBase ctx)
        {
            string? storagePoolId = GetRouteParameter(ctx, "storagePoolId");
            if (String.IsNullOrWhiteSpace(storagePoolId))
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "Storage pool ID is required.").ConfigureAwait(false);
                return;
            }

            if (!await AuthorizeArchiveAdminAsync(ctx, null, "ArchiveStoragePool", storagePoolId, "Read").ConfigureAwait(false)) return;

            ArchiveStoragePool? pool = await _Catalog.StoragePools.ReadByIdAsync(storagePoolId).ConfigureAwait(false);
            if (pool == null)
            {
                await SendNotFoundAsync(ctx, "Archive storage pool was not found.").ConfigureAwait(false);
                return;
            }

            string detail = "Storage pool metadata is available.";
            bool healthy = true;
            if (pool.Type == ArchiveStoragePoolType.FileSystem)
            {
                string basePath = pool.BasePath ?? String.Empty;
                healthy = !String.IsNullOrWhiteSpace(basePath) && Directory.Exists(basePath);
                detail = healthy ? "Filesystem path exists." : "Filesystem path does not exist.";
            }
            else if (pool.Type == ArchiveStoragePoolType.S3)
            {
                healthy = !String.IsNullOrWhiteSpace(pool.Bucket) && TryGetObjectStore(pool.Id, out _);
                detail = healthy ? "S3-compatible storage pool is configured." : "S3-compatible storage pool is missing bucket configuration.";
            }

            object data = new
            {
                Healthy = healthy,
                StoragePoolId = storagePoolId,
                Type = pool.Type.ToString(),
                Detail = detail,
                CheckedUtc = DateTime.UtcNow
            };
            await SendJsonAsync(ctx, 200, data).ConfigureAwait(false);
        }

        private static async Task GetArchivedEntriesAsync(HttpContextBase ctx)
        {
            string? accountId = GetRouteParameter(ctx, "accountId");
            if (String.IsNullOrWhiteSpace(accountId))
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "Account ID is required.").ConfigureAwait(false);
                return;
            }

            string tenantId = GetTenantId(ctx);
            if (String.IsNullOrWhiteSpace(tenantId))
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "Tenant ID is required.").ConfigureAwait(false);
                return;
            }

            if (!await AuthorizeArchiveReadAsync(ctx, tenantId, "Entry", accountId).ConfigureAwait(false)) return;

            ArchiveQuery query = BuildArchiveQuery(ctx);
            if (ctx.Request.Method == HttpMethod.POST)
            {
                EnumerationQuery? bodyQuery = await DeserializeRequestBodyAsync<EnumerationQuery>(ctx).ConfigureAwait(false);
                if (bodyQuery != null)
                {
                    query.MaxResults = bodyQuery.MaxResults;
                    query.Skip = bodyQuery.Skip;
                    query.ContinuationToken = bodyQuery.ContinuationToken;
                    query.Ordering = bodyQuery.Ordering;
                    query.Search = bodyQuery.SearchTerm;
                    query.FromUtc = bodyQuery.CreatedAfterUtc;
                    query.ToUtc = bodyQuery.CreatedBeforeUtc;
                    query.AmountMinimum = bodyQuery.AmountMinimum;
                    query.AmountMaximum = bodyQuery.AmountMaximum;
                    query.CreditMinimum = bodyQuery.CreditMinimum;
                    query.CreditMaximum = bodyQuery.CreditMaximum;
                    query.DebitMinimum = bodyQuery.DebitMinimum;
                    query.DebitMaximum = bodyQuery.DebitMaximum;
                    query.Labels = bodyQuery.Labels;
                    query.Tags = bodyQuery.Tags;
                }
            }

            query.TenantId = tenantId;
            query.AccountId = accountId;
            query.EntityType = ArchiveEntityType.Entries;
            query.ManifestStatus = ArchiveManifestStatus.Committed;

            if (!await EnsureArchiveCoverageAsync(ctx, query).ConfigureAwait(false)) return;

            EnumerationResult<Entry> result;
            try
            {
                result = await EnumerateArchivedEntriesFromObjectsAsync(tenantId, accountId, query, ctx.Token).ConfigureAwait(false);
            }
            catch (NotSupportedException e)
            {
                await SendNotImplementedAsync(ctx, e.Message).ConfigureAwait(false);
                return;
            }
            catch (InvalidDataException e)
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, e.Message).ConfigureAwait(false);
                return;
            }

            await SendJsonAsync(ctx, 200, result).ConfigureAwait(false);
        }

        private static async Task GetArchivedBalanceAsOfAsync(HttpContextBase ctx)
        {
            string? accountId = GetRouteParameter(ctx, "accountId");
            if (String.IsNullOrWhiteSpace(accountId))
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "Account ID is required.").ConfigureAwait(false);
                return;
            }

            string tenantId = GetTenantId(ctx);
            if (String.IsNullOrWhiteSpace(tenantId))
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "Tenant ID is required.").ConfigureAwait(false);
                return;
            }

            if (!await AuthorizeArchiveReadAsync(ctx, tenantId, "Balance", accountId).ConfigureAwait(false)) return;

            string? asOfValue = GetQueryString(ctx, "asOf") ?? GetQueryString(ctx, "asOfUtc");
            if (String.IsNullOrWhiteSpace(asOfValue) || !DateTime.TryParse(asOfValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime asOfUtc))
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "Valid asOf timestamp is required.").ConfigureAwait(false);
                return;
            }

            ArchiveBalanceCheckpoint? checkpoint = await _Catalog.BalanceCheckpoints.ReadAsOfAsync(tenantId, accountId, asOfUtc.ToUniversalTime()).ConfigureAwait(false);
            if (checkpoint == null)
            {
                await SendNotFoundAsync(ctx, "Archived balance checkpoint was not found.").ConfigureAwait(false);
                return;
            }

            object response = new
            {
                AccountId = accountId,
                TenantId = tenantId,
                AsOfUtc = checkpoint.AsOfUtc,
                Balance = checkpoint.Balance,
                ManifestId = checkpoint.ManifestId
            };
            await SendJsonAsync(ctx, 200, response).ConfigureAwait(false);
        }

        private static async Task VerifyArchivedBalanceChainAsync(HttpContextBase ctx)
        {
            string? accountId = GetRouteParameter(ctx, "accountId");
            if (String.IsNullOrWhiteSpace(accountId))
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "Account ID is required.").ConfigureAwait(false);
                return;
            }

            string tenantId = GetTenantId(ctx);
            if (String.IsNullOrWhiteSpace(tenantId))
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "Tenant ID is required.").ConfigureAwait(false);
                return;
            }

            if (!await AuthorizeArchiveReadAsync(ctx, tenantId, "Balance", accountId).ConfigureAwait(false)) return;

            ArchiveQuery query = BuildArchiveQuery(ctx);
            query.TenantId = tenantId;
            query.AccountId = accountId;
            query.EntityType = ArchiveEntityType.Entries;
            query.ManifestStatus = ArchiveManifestStatus.Committed;

            ArchiveVerificationResult result = new ArchiveVerificationResult
            {
                TenantId = tenantId,
                AccountId = accountId
            };

            List<ArchiveManifest> manifests = await ReadAllManifestsAsync(query, ctx.Token).ConfigureAwait(false);
            if (manifests.Count == 0)
            {
                result.Details.Add("No committed archived entry manifests matched the requested tenant/account scope.");
                await SendJsonAsync(ctx, 200, result).ConfigureAwait(false);
                return;
            }

            manifests.Sort((left, right) =>
            {
                int compare = left.FromUtc.CompareTo(right.FromUtc);
                return compare != 0 ? compare : String.CompareOrdinal(left.Id, right.Id);
            });

            DateTime? previousCheckpointUtc = null;
            decimal? previousCheckpointBalance = null;
            foreach (ArchiveManifest manifest in manifests)
            {
                result.CheckedManifests++;
                await VerifyArchiveManifestAsync(manifest, result, ctx.Token).ConfigureAwait(false);
                List<ArchiveBalanceCheckpoint> checkpoints = await ReadAllBalanceCheckpointsAsync(manifest.Id, ctx.Token).ConfigureAwait(false);
                result.CheckedBalanceCheckpoints += checkpoints.Count;
                foreach (ArchiveBalanceCheckpoint checkpoint in checkpoints)
                {
                    if (!String.Equals(checkpoint.TenantId, tenantId, StringComparison.Ordinal) ||
                        !String.Equals(checkpoint.AccountId, accountId, StringComparison.Ordinal))
                    {
                        AddVerificationError(result, "Checkpoint " + checkpoint.Id + " is outside the requested tenant/account scope.");
                    }

                    DateTime checkpointUtc = checkpoint.AsOfUtc.ToUniversalTime();
                    if (previousCheckpointUtc.HasValue && checkpointUtc < previousCheckpointUtc.Value)
                    {
                        AddVerificationError(result, "Checkpoint " + checkpoint.Id + " is earlier than the previous checkpoint.");
                    }

                    previousCheckpointUtc = checkpointUtc;
                    previousCheckpointBalance = checkpoint.Balance;
                }
            }

            if (previousCheckpointBalance.HasValue)
            {
                result.Details.Add("Latest archived checkpoint balance is " + previousCheckpointBalance.Value.ToString(CultureInfo.InvariantCulture) + ".");
            }

            await WriteArchiveAuditAsync(ctx, tenantId, "ArchiveBalanceVerified", "Balance", accountId, result.IsValid ? "Permit" : "Denied", result.IsValid ? "Archive verification completed." : "Archive verification found errors.").ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, result).ConfigureAwait(false);
        }

        private static async Task GetArchivedRequestHistoryAsync(HttpContextBase ctx)
        {
            RequestHistoryFilter filter = BuildRequestHistoryFilter(ctx);
            if (!await ApplyRequestHistoryScopeAsync(ctx, filter, false).ConfigureAwait(false)) return;

            ArchiveQuery coverageQuery = BuildArchiveQuery(ctx);
            coverageQuery.TenantId = filter.TenantId;
            coverageQuery.EntityType = ArchiveEntityType.RequestHistory;
            coverageQuery.ManifestStatus = ArchiveManifestStatus.Committed;
            coverageQuery.FromUtc = filter.FromUtc;
            coverageQuery.ToUtc = filter.ToUtc;
            if (!await EnsureArchiveCoverageAsync(ctx, coverageQuery).ConfigureAwait(false)) return;

            RequestHistoryResult result;
            try
            {
                result = await EnumerateArchivedRequestHistoryFromObjectsAsync(filter, false, true, ctx.Token).ConfigureAwait(false);
            }
            catch (NotSupportedException e)
            {
                await SendNotImplementedAsync(ctx, e.Message).ConfigureAwait(false);
                return;
            }
            catch (InvalidDataException e)
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, e.Message).ConfigureAwait(false);
                return;
            }

            await SendJsonAsync(ctx, 200, result).ConfigureAwait(false);
        }

        private static async Task GetArchivedRequestHistorySummaryAsync(HttpContextBase ctx)
        {
            RequestHistoryFilter filter = BuildRequestHistoryFilter(ctx);
            if (!await ApplyRequestHistoryScopeAsync(ctx, filter, false).ConfigureAwait(false)) return;

            ArchiveQuery coverageQuery = BuildArchiveQuery(ctx);
            coverageQuery.TenantId = filter.TenantId;
            coverageQuery.EntityType = ArchiveEntityType.RequestHistory;
            coverageQuery.ManifestStatus = ArchiveManifestStatus.Committed;
            coverageQuery.FromUtc = filter.FromUtc;
            coverageQuery.ToUtc = filter.ToUtc;
            if (!await EnsureArchiveCoverageAsync(ctx, coverageQuery).ConfigureAwait(false)) return;

            RequestHistoryResult result = await EnumerateArchivedRequestHistoryFromObjectsAsync(filter, true, false, ctx.Token).ConfigureAwait(false);
            RequestHistorySummary summary = BuildRequestHistorySummary(result.Objects, filter);
            await SendJsonAsync(ctx, 200, summary).ConfigureAwait(false);
        }

        private static async Task GetArchivedRequestHistoryEntryAsync(HttpContextBase ctx)
        {
            string? id = GetRouteParameter(ctx, "id");
            if (String.IsNullOrWhiteSpace(id))
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "Request history identifier is required.").ConfigureAwait(false);
                return;
            }

            RequestHistoryFilter filter = BuildRequestHistoryFilter(ctx);
            if (!await ApplyRequestHistoryScopeAsync(ctx, filter, false).ConfigureAwait(false)) return;
            filter.MaxResults = 1000;
            filter.Skip = 0;

            RequestHistoryEntry? entry = await ReadArchivedRequestHistoryEntryFromObjectsAsync(id, filter, ctx.Token).ConfigureAwait(false);
            if (entry == null)
            {
                await SendNotFoundAsync(ctx, "Archived request history entry was not found.").ConfigureAwait(false);
                return;
            }

            await SendJsonAsync(ctx, 200, entry).ConfigureAwait(false);
        }

        private static async Task GetArchiveServerRequestHistoryAsync(HttpContextBase ctx)
        {
            RequestHistoryFilter filter = BuildRequestHistoryFilter(ctx);
            if (!await ApplyRequestHistoryScopeAsync(ctx, filter, true).ConfigureAwait(false)) return;
            EnumerationResult<ArchiveServerRequestHistoryRecord> result = await _Catalog.ServerRequestHistory.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, result).ConfigureAwait(false);
        }

        private static async Task GetArchiveServerRequestHistorySummaryAsync(HttpContextBase ctx)
        {
            RequestHistoryFilter filter = BuildRequestHistoryFilter(ctx);
            if (!await ApplyRequestHistoryScopeAsync(ctx, filter, true).ConfigureAwait(false)) return;
            RequestHistorySummary summary = await _Catalog.ServerRequestHistory.SummarizeAsync(filter, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, summary).ConfigureAwait(false);
        }

        private static async Task GetArchiveServerRequestHistoryEntryAsync(HttpContextBase ctx)
        {
            string? id = GetRouteParameter(ctx, "id");
            if (String.IsNullOrWhiteSpace(id))
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "Request history identifier is required.").ConfigureAwait(false);
                return;
            }

            RequestHistoryFilter filter = BuildRequestHistoryFilter(ctx);
            if (!await ApplyRequestHistoryScopeAsync(ctx, filter, true).ConfigureAwait(false)) return;
            ArchiveServerRequestHistoryRecord? record = await _Catalog.ServerRequestHistory.ReadAsync(filter.TenantId, id, ctx.Token).ConfigureAwait(false);
            if (record == null || !CanAccessRequestHistoryRecord(GetAuthContext(ctx), record.TenantId, record.PrincipalId))
            {
                await SendNotFoundAsync(ctx, "Archive Server request history entry was not found.").ConfigureAwait(false);
                return;
            }

            await SendJsonAsync(ctx, 200, record).ConfigureAwait(false);
        }

        private static async Task GetArchiveMigrationsAsync(HttpContextBase ctx)
        {
            ArchiveQuery query = BuildArchiveQuery(ctx);
            if (!await AuthorizeArchiveManageAsync(ctx, query.TenantId, "ArchiveMigration", null, "Read").ConfigureAwait(false)) return;

            EnumerationResult<ArchiveMigration> result = await _Catalog.Migrations.EnumerateAsync(query).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, result.Objects).ConfigureAwait(false);
        }

        private static async Task GetArchiveMigrationAsync(HttpContextBase ctx)
        {
            string? migrationId = GetRouteParameter(ctx, "migrationId");
            if (String.IsNullOrWhiteSpace(migrationId))
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "Migration ID is required.").ConfigureAwait(false);
                return;
            }

            ArchiveMigration? migration = await _Catalog.Migrations.ReadByIdAsync(migrationId).ConfigureAwait(false);
            if (migration == null)
            {
                await SendNotFoundAsync(ctx, "Archive migration was not found.").ConfigureAwait(false);
                return;
            }

            if (!await AuthorizeArchiveManageAsync(ctx, migration.TenantId, "ArchiveMigration", migration.Id, "Read").ConfigureAwait(false)) return;

            await SendJsonAsync(ctx, 200, migration).ConfigureAwait(false);
        }

        private static async Task GetArchiveMigrationBatchesAsync(HttpContextBase ctx)
        {
            string? migrationId = GetRouteParameter(ctx, "migrationId");
            if (String.IsNullOrWhiteSpace(migrationId))
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "Migration ID is required.").ConfigureAwait(false);
                return;
            }

            ArchiveMigration? migration = await _Catalog.Migrations.ReadByIdAsync(migrationId).ConfigureAwait(false);
            if (migration == null)
            {
                await SendNotFoundAsync(ctx, "Archive migration was not found.").ConfigureAwait(false);
                return;
            }

            if (!await AuthorizeArchiveManageAsync(ctx, migration.TenantId, "ArchiveMigration", migration.Id, "Read").ConfigureAwait(false)) return;

            EnumerationResult<ArchiveMigrationBatch> result = await _Catalog.Migrations.EnumerateBatchesAsync(migrationId, BuildArchiveQuery(ctx)).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, result.Objects).ConfigureAwait(false);
        }

        private static async Task CreateArchiveMigrationAsync(HttpContextBase ctx)
        {
            CreateArchiveMigrationRequest? request = await DeserializeRequestBodyAsync<CreateArchiveMigrationRequest>(ctx).ConfigureAwait(false);
            if (request == null)
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "Migration request body is required.").ConfigureAwait(false);
                return;
            }

            string tenantId = FirstNonEmpty(request.TenantId, GetTenantId(ctx));
            if (String.IsNullOrWhiteSpace(tenantId))
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "Tenant ID is required.").ConfigureAwait(false);
                return;
            }

            if (!await AuthorizeArchiveManageAsync(ctx, tenantId, "ArchiveMigration", null, "Create").ConfigureAwait(false)) return;

            if (!request.FromUtc.HasValue || !request.ToUtc.HasValue)
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "FromUtc and ToUtc are required.").ConfigureAwait(false);
                return;
            }

            DateTime fromUtc = request.FromUtc.Value.ToUniversalTime();
            DateTime toUtc = request.ToUtc.Value.ToUniversalTime();
            if (toUtc < fromUtc)
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "ToUtc must be greater than or equal to FromUtc.").ConfigureAwait(false);
                return;
            }

            string idempotencyKey = FirstNonEmpty(GetRequestHeader(ctx.Request.Headers, "Idempotency-Key"), request.IdempotencyKey);
            if (String.IsNullOrWhiteSpace(idempotencyKey))
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "Idempotency-Key is required.").ConfigureAwait(false);
                return;
            }

            string storagePoolId = FirstNonEmpty(request.StoragePoolId, _Settings.Archive.DefaultStoragePoolId);
            ArchiveStoragePool? pool = await _Catalog.StoragePools.ReadByIdAsync(storagePoolId).ConfigureAwait(false);
            if (pool == null)
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "Storage pool was not found.").ConfigureAwait(false);
                return;
            }

            ArchiveFormat format = request.Format ?? pool.Format;
            if (!IsAcceptedFormat(format))
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "Archive format is not accepted by this server.").ConfigureAwait(false);
                return;
            }

            ArchiveCompression compression = request.Compression ?? pool.Compression;
            ArchiveMigration? existing = await _Catalog.Migrations.ReadByIdempotencyKeyAsync(idempotencyKey).ConfigureAwait(false);
            if (existing != null)
            {
                if (!IsSameMigrationRequest(existing, tenantId, request.AccountId, request.EntityType, storagePoolId, format, compression, fromUtc, toUtc))
                {
                    await SendErrorAsync(ctx, ArchiveApiErrorCode.Conflict, "Idempotency key was already used for a different archive migration.").ConfigureAwait(false);
                    return;
                }

                await SendJsonAsync(ctx, 200, existing).ConfigureAwait(false);
                return;
            }

            DateTime now = DateTime.UtcNow;
            ArchiveMigration migration = new ArchiveMigration
            {
                TenantId = tenantId,
                AccountId = EmptyToNull(request.AccountId),
                EntityType = request.EntityType,
                StoragePoolId = storagePoolId,
                Format = format,
                Compression = compression,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                Status = ArchiveMigrationStatus.Pending,
                IdempotencyKey = idempotencyKey,
                CreatedUtc = now,
                LastUpdateUtc = now
            };

            migration = await _Catalog.Migrations.CreateAsync(migration).ConfigureAwait(false);
            await WriteArchiveAuditAsync(ctx, migration.TenantId, "MigrationCreated", "ArchiveMigration", migration.Id, "Permit", null).ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, migration).ConfigureAwait(false);
        }

        private static async Task CreateArchiveMigrationBatchAsync(HttpContextBase ctx)
        {
            ArchiveMigration? migration = await ReadMigrationFromRouteAsync(ctx).ConfigureAwait(false);
            if (migration == null) return;

            if (!await AuthorizeArchiveManageAsync(ctx, migration.TenantId, "ArchiveMigrationBatch", migration.Id, "Create").ConfigureAwait(false)) return;

            if (migration.Status == ArchiveMigrationStatus.Aborted || migration.Status == ArchiveMigrationStatus.Committed)
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.Conflict, "Cannot add batches to a terminal migration.").ConfigureAwait(false);
                return;
            }

            CreateArchiveMigrationBatchRequest? request = await DeserializeRequestBodyAsync<CreateArchiveMigrationBatchRequest>(ctx).ConfigureAwait(false);
            if (request == null)
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "Migration batch request body is required.").ConfigureAwait(false);
                return;
            }

            if (request.SequenceNumber < 0)
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "SequenceNumber must be zero or greater.").ConfigureAwait(false);
                return;
            }

            if (request.RowCount < 0 || request.RowCount > _Settings.Archive.MaxMigrationBatchRows)
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "RowCount is outside the configured migration batch row limit.").ConfigureAwait(false);
                return;
            }

            if (request.ByteCount < 0 || request.ByteCount > _Settings.Archive.MaxMigrationBatchBytes)
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "ByteCount is outside the configured migration batch byte limit.").ConfigureAwait(false);
                return;
            }

            string batchId = FirstNonEmpty(request.Id, ArchiveId.Generate(ArchiveIdentifierPrefixes.MigrationBatch));
            ArchiveMigrationBatch? existing = await _Catalog.Migrations.ReadBatchAsync(migration.Id, batchId).ConfigureAwait(false);
            if (existing != null)
            {
                if (existing.SequenceNumber != request.SequenceNumber ||
                    existing.RowCount != request.RowCount ||
                    !String.Equals(existing.ContentHashSha256, request.ContentHashSha256 ?? String.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    await SendErrorAsync(ctx, ArchiveApiErrorCode.Conflict, "Batch identifier already exists for different metadata.").ConfigureAwait(false);
                    return;
                }

                await SendJsonAsync(ctx, 200, existing).ConfigureAwait(false);
                return;
            }

            ArchiveStoragePool? pool = await _Catalog.StoragePools.ReadByIdAsync(migration.StoragePoolId).ConfigureAwait(false);
            if (pool == null)
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "Storage pool was not found.").ConfigureAwait(false);
                return;
            }

            DateTime now = DateTime.UtcNow;
            ArchiveMigrationBatch batch = new ArchiveMigrationBatch
            {
                Id = batchId,
                MigrationId = migration.Id,
                StoragePoolId = migration.StoragePoolId,
                TenantId = migration.TenantId,
                AccountId = migration.AccountId,
                SequenceNumber = request.SequenceNumber,
                RowCount = request.RowCount,
                ByteCount = request.ByteCount,
                ContentHashSha256 = request.ContentHashSha256 ?? String.Empty,
                TemporaryRelativePath = BuildTemporaryObjectPath(migration, batchId),
                CommittedRelativePath = BuildCommittedObjectPath(pool, migration, batchId, request.SequenceNumber),
                Status = ArchiveMigrationBatchStatus.Pending,
                CreatedUtc = now,
                LastUpdateUtc = now
            };

            batch = await _Catalog.Migrations.CreateBatchAsync(batch).ConfigureAwait(false);
            if (migration.Status == ArchiveMigrationStatus.Pending)
            {
                await _Catalog.Migrations.UpdateStatusAsync(migration.Id, ArchiveMigrationStatus.Receiving).ConfigureAwait(false);
            }

            await WriteArchiveAuditAsync(ctx, migration.TenantId, "MigrationBatchCreated", "ArchiveMigrationBatch", batch.Id, "Permit", null).ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, batch).ConfigureAwait(false);
        }

        private static async Task UploadArchiveMigrationBatchContentAsync(HttpContextBase ctx)
        {
            string? migrationId = GetRouteParameter(ctx, "migrationId");
            string? batchId = GetRouteParameter(ctx, "batchId");
            if (String.IsNullOrWhiteSpace(migrationId) || String.IsNullOrWhiteSpace(batchId))
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "Migration ID and batch ID are required.").ConfigureAwait(false);
                return;
            }

            ArchiveMigrationBatch? batch = await _Catalog.Migrations.ReadBatchAsync(migrationId, batchId).ConfigureAwait(false);
            if (batch == null)
            {
                await SendNotFoundAsync(ctx, "Archive migration batch was not found.").ConfigureAwait(false);
                return;
            }

            if (!await AuthorizeArchiveManageAsync(ctx, batch.TenantId, "ArchiveMigrationBatch", batch.Id, "Update").ConfigureAwait(false)) return;

            if (batch.Status == ArchiveMigrationBatchStatus.Verified)
            {
                await SendJsonAsync(ctx, 200, batch).ConfigureAwait(false);
                return;
            }

            if (ctx.Request.ContentLength <= 0 || ctx.Request.Data == null)
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "Batch content body is required.").ConfigureAwait(false);
                return;
            }

            if (ctx.Request.ContentLength > _Settings.Archive.MaxMigrationBatchBytes)
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "Batch content exceeds the configured byte limit.").ConfigureAwait(false);
                return;
            }

            if (!TryGetObjectStore(batch.StoragePoolId, out IArchiveObjectStore? store))
            {
                await SendNotImplementedAsync(ctx, "Configured archive object storage provider is not available.").ConfigureAwait(false);
                return;
            }

            ArchiveMigration? migration = await _Catalog.Migrations.ReadByIdAsync(batch.MigrationId).ConfigureAwait(false);
            if (migration == null)
            {
                await SendNotFoundAsync(ctx, "Archive migration was not found.").ConfigureAwait(false);
                return;
            }

            string temporaryFile = Path.Combine(Path.GetTempPath(), ArchiveId.Generate("aul_") + ".tmp");
            try
            {
                ArchiveUploadResult upload = await ReceiveUploadAsync(ctx.Request.Data, temporaryFile, _Settings.Archive.MaxMigrationBatchBytes).ConfigureAwait(false);
                string expectedHash = FirstNonEmpty(GetRequestHeader(ctx.Request.Headers, "x-content-sha256"), batch.ContentHashSha256);
                if (!String.IsNullOrWhiteSpace(expectedHash) && !String.Equals(expectedHash, upload.ContentHashSha256, StringComparison.OrdinalIgnoreCase))
                {
                    batch.Status = ArchiveMigrationBatchStatus.Failed;
                    await _Catalog.Migrations.UpdateBatchAsync(batch).ConfigureAwait(false);
                    await SendErrorAsync(ctx, ArchiveApiErrorCode.Conflict, "Batch content hash does not match expected SHA-256.").ConfigureAwait(false);
                    return;
                }

                if (batch.ByteCount > 0 && batch.ByteCount != upload.ByteCount)
                {
                    batch.Status = ArchiveMigrationBatchStatus.Failed;
                    await _Catalog.Migrations.UpdateBatchAsync(batch).ConfigureAwait(false);
                    await SendErrorAsync(ctx, ArchiveApiErrorCode.Conflict, "Batch byte count does not match expected byte count.").ConfigureAwait(false);
                    return;
                }

                try
                {
                    await ValidateUploadedBatchContentAsync(migration, batch, temporaryFile).ConfigureAwait(false);
                }
                catch (InvalidDataException e)
                {
                    batch.Status = ArchiveMigrationBatchStatus.Failed;
                    await _Catalog.Migrations.UpdateBatchAsync(batch).ConfigureAwait(false);
                    await SendErrorAsync(ctx, ArchiveApiErrorCode.Conflict, e.Message).ConfigureAwait(false);
                    return;
                }

                using (FileStream stream = new FileStream(temporaryFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    await store!.WriteTemporaryAsync(batch.TemporaryRelativePath, stream, BuildObjectStorageMetadata(migration, batch, null, upload.ContentHashSha256)).ConfigureAwait(false);
                }

                batch.ByteCount = upload.ByteCount;
                batch.ContentHashSha256 = upload.ContentHashSha256;
                batch.Status = ArchiveMigrationBatchStatus.Uploaded;
                batch.LastUpdateUtc = DateTime.UtcNow;
                batch = await _Catalog.Migrations.UpdateBatchAsync(batch).ConfigureAwait(false);
                await WriteArchiveAuditAsync(ctx, batch.TenantId, "MigrationBatchUploaded", "ArchiveMigrationBatch", batch.Id, "Permit", null).ConfigureAwait(false);
                await SendJsonAsync(ctx, 200, batch).ConfigureAwait(false);
            }
            finally
            {
                if (File.Exists(temporaryFile))
                {
                    File.Delete(temporaryFile);
                }
            }
        }

        private static async Task SealArchiveMigrationAsync(HttpContextBase ctx)
        {
            ArchiveMigration? migration = await ReadMigrationFromRouteAsync(ctx).ConfigureAwait(false);
            if (migration == null) return;

            if (!await AuthorizeArchiveManageAsync(ctx, migration.TenantId, "ArchiveMigration", migration.Id, "Update").ConfigureAwait(false)) return;

            if (migration.Status == ArchiveMigrationStatus.Committed)
            {
                await SendJsonAsync(ctx, 200, migration).ConfigureAwait(false);
                return;
            }

            if (migration.Status == ArchiveMigrationStatus.Aborted)
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.Conflict, "Cannot seal an aborted migration.").ConfigureAwait(false);
                return;
            }

            EnumerationResult<ArchiveMigrationBatch> batches = await _Catalog.Migrations.EnumerateBatchesAsync(migration.Id, new ArchiveQuery { MaxResults = 1000 }).ConfigureAwait(false);
            if (batches.Objects.Count == 0)
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.Conflict, "Cannot seal a migration with no batches.").ConfigureAwait(false);
                return;
            }

            foreach (ArchiveMigrationBatch batch in batches.Objects)
            {
                if (batch.Status == ArchiveMigrationBatchStatus.Pending || batch.Status == ArchiveMigrationBatchStatus.Failed)
                {
                    await SendErrorAsync(ctx, ArchiveApiErrorCode.Conflict, "Cannot seal a migration while any batch is pending or failed.").ConfigureAwait(false);
                    return;
                }
            }

            migration = await _Catalog.Migrations.UpdateStatusAsync(migration.Id, ArchiveMigrationStatus.Sealing).ConfigureAwait(false);
            await WriteArchiveAuditAsync(ctx, migration.TenantId, "MigrationSealed", "ArchiveMigration", migration.Id, "Permit", null).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, migration).ConfigureAwait(false);
        }

        private static async Task CommitArchiveMigrationAsync(HttpContextBase ctx)
        {
            ArchiveMigration? migration = await ReadMigrationFromRouteAsync(ctx).ConfigureAwait(false);
            if (migration == null) return;

            if (!await AuthorizeArchiveManageAsync(ctx, migration.TenantId, "ArchiveMigration", migration.Id, "Update").ConfigureAwait(false)) return;

            if (migration.Status == ArchiveMigrationStatus.Aborted)
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.Conflict, "Cannot commit an aborted migration.").ConfigureAwait(false);
                return;
            }

            EnumerationResult<ArchiveManifest> existingManifests = await _Catalog.Manifests.EnumerateAsync(new ArchiveQuery
            {
                TenantId = migration.TenantId,
                MigrationId = migration.Id,
                MaxResults = 1
            }).ConfigureAwait(false);
            if (existingManifests.Objects.Count > 0)
            {
                await _Catalog.Migrations.UpdateStatusAsync(migration.Id, ArchiveMigrationStatus.Committed).ConfigureAwait(false);
                await SendJsonAsync(ctx, 200, existingManifests.Objects[0]).ConfigureAwait(false);
                return;
            }

            EnumerationResult<ArchiveMigrationBatch> batches = await _Catalog.Migrations.EnumerateBatchesAsync(migration.Id, new ArchiveQuery { MaxResults = 1000 }).ConfigureAwait(false);
            if (batches.Objects.Count == 0)
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.Conflict, "Cannot commit a migration with no batches.").ConfigureAwait(false);
                return;
            }

            foreach (ArchiveMigrationBatch batch in batches.Objects)
            {
                if (batch.Status == ArchiveMigrationBatchStatus.Pending || batch.Status == ArchiveMigrationBatchStatus.Failed)
                {
                    await SendErrorAsync(ctx, ArchiveApiErrorCode.Conflict, "Cannot commit a migration while any batch is pending or failed.").ConfigureAwait(false);
                    return;
                }
            }

            foreach (ArchiveMigrationBatch batch in batches.Objects)
            {
                if (batch.Status == ArchiveMigrationBatchStatus.Uploaded)
                {
                    if (!TryGetObjectStore(batch.StoragePoolId, out IArchiveObjectStore? store))
                    {
                        await SendNotImplementedAsync(ctx, "Configured archive object storage provider is not available.").ConfigureAwait(false);
                        return;
                    }

                    await store!.CommitAsync(batch.TemporaryRelativePath, batch.CommittedRelativePath).ConfigureAwait(false);
                    batch.Status = ArchiveMigrationBatchStatus.Verified;
                    batch.LastUpdateUtc = DateTime.UtcNow;
                    await _Catalog.Migrations.UpdateBatchAsync(batch).ConfigureAwait(false);
                }
            }

            ArchiveManifest manifest;
            try
            {
                manifest = await CreateManifestForMigrationAsync(migration, batches.Objects).ConfigureAwait(false);
            }
            catch (InvalidDataException e)
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.Conflict, e.Message).ConfigureAwait(false);
                return;
            }

            await _Catalog.Migrations.UpdateStatusAsync(migration.Id, ArchiveMigrationStatus.Committed).ConfigureAwait(false);
            await WriteArchiveAuditAsync(ctx, migration.TenantId, "MigrationCommitted", "ArchiveMigration", migration.Id, "Permit", null).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, manifest).ConfigureAwait(false);
        }

        private static async Task AbortArchiveMigrationAsync(HttpContextBase ctx)
        {
            ArchiveMigration? migration = await ReadMigrationFromRouteAsync(ctx).ConfigureAwait(false);
            if (migration == null) return;

            if (!await AuthorizeArchiveManageAsync(ctx, migration.TenantId, "ArchiveMigration", migration.Id, "Update").ConfigureAwait(false)) return;

            if (migration.Status == ArchiveMigrationStatus.Committed)
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.Conflict, "Cannot abort a committed migration. Quarantine or supersede its manifest instead.").ConfigureAwait(false);
                return;
            }

            EnumerationResult<ArchiveMigrationBatch> batches = await _Catalog.Migrations.EnumerateBatchesAsync(migration.Id, new ArchiveQuery { MaxResults = 1000 }).ConfigureAwait(false);
            foreach (ArchiveMigrationBatch batch in batches.Objects)
            {
                if (TryGetObjectStore(batch.StoragePoolId, out IArchiveObjectStore? store))
                {
                    try
                    {
                        await store!.DeleteTemporaryAsync(batch.TemporaryRelativePath).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        _Logging.Warn(_Header + "failed to delete temporary archive object for batch " + batch.Id + ": " + e.Message);
                    }
                }

                if (batch.Status != ArchiveMigrationBatchStatus.Verified)
                {
                    batch.Status = ArchiveMigrationBatchStatus.Failed;
                    batch.LastUpdateUtc = DateTime.UtcNow;
                    await _Catalog.Migrations.UpdateBatchAsync(batch).ConfigureAwait(false);
                }
            }

            migration = await _Catalog.Migrations.UpdateStatusAsync(migration.Id, ArchiveMigrationStatus.Aborted).ConfigureAwait(false);
            await WriteArchiveAuditAsync(ctx, migration.TenantId, "MigrationAborted", "ArchiveMigration", migration.Id, "Permit", null).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, migration).ConfigureAwait(false);
        }

        private static async Task MutationNotAllowedAsync(HttpContextBase ctx)
        {
            await WriteArchiveAuditAsync(ctx, GetTenantId(ctx), "ActiveMutationRejected", "ActiveRoute", ctx.Request.Url.RawWithoutQuery, "Denied", "Archive Server does not support active NetLedger mutations.").ConfigureAwait(false);
            await SendErrorAsync(ctx, ArchiveApiErrorCode.MethodNotAllowed, "Archive Server is read-only for active NetLedger mutations. Use NetLedger Server for active data changes.").ConfigureAwait(false);
        }

        private static async Task OpenApiAsync(HttpContextBase ctx)
        {
            Dictionary<string, object> document = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["openapi"] = "3.0.3",
                ["info"] = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["title"] = "NetLedger Archive Server API",
                    ["version"] = "4.0.0"
                },
                ["paths"] = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["/v1/health"] = DescribePath("get", "Archive server health"),
                    ["/v1/service"] = DescribePath("get", "Archive server service metadata"),
                    ["/v1/archive/ranges"] = DescribePath("get", "List archive ranges"),
                    ["/v1/tenants/{tenantId}/archive/ranges"] = DescribePath("get", "List tenant archive ranges"),
                    ["/v1/tenants/{tenantId}/accounts/{accountId}/archive/ranges"] = DescribePath("get", "List account archive ranges"),
                    ["/v1/archive/manifests"] = DescribePath("get", "List archive manifests"),
                    ["/v1/archive/manifests/{manifestId}"] = DescribePath("get", "Read archive manifest"),
                    ["/v1/archive/manifests/{manifestId}/objects"] = DescribePath("get", "List archive manifest objects"),
                    ["/v1/archive/objects/{objectId}/metadata"] = DescribePath("get", "Read archive object metadata"),
                    ["/v1/archive/manifests/{manifestId}/checkpoints"] = DescribePath("get", "List archive manifest balance checkpoints"),
                    ["/v1/archive/manifests/{manifestId}/verify"] = DescribePath("post", "Verify archive manifest metadata"),
                    ["/v1/archive/manifests/{manifestId}/quarantine"] = DescribePath("post", "Quarantine archive manifest metadata"),
                    ["/v1/archive/manifests/{manifestId}/supersede"] = DescribePath("post", "Supersede archive manifest metadata"),
                    ["/v1/archive/storage-pools"] = DescribePath("get", "List archive storage pools"),
                    ["/v1/archive/storage-pools/{storagePoolId}/health"] = DescribePath("get", "Read archive storage pool health"),
                    ["/v1/archive/accounts/{accountId}/entries"] = DescribePath("get", "List archived entries for an account"),
                    ["/v1/archive/accounts/{accountId}/balance/asof"] = DescribePath("get", "Read archived balance as of a point in time"),
                    ["/v1/archive/accounts/{accountId}/verify"] = DescribePath("get", "Verify archived balance chain for an account"),
                    ["/v1/request-history"] = DescribePath("get", "List archived NetLedger request history"),
                    ["/v1/request-history/summary"] = DescribePath("get", "Summarize archived NetLedger request history"),
                    ["/v1/request-history/{id}"] = DescribePath("get", "Read archived NetLedger request history entry"),
                    ["/v1/tenants/{tenantId}/accounts/{accountId}/entries"] = DescribePath("get", "List archived entries for a tenant account"),
                    ["/v1/tenants/{tenantId}/accounts/{accountId}/entries/enumerate"] = DescribePath("post", "Enumerate archived entries for a tenant account"),
                    ["/v1/tenants/{tenantId}/accounts/{accountId}/balance/asof"] = DescribePath("get", "Read archived tenant account balance as of a point in time"),
                    ["/v1/tenants/{tenantId}/accounts/{accountId}/verify"] = DescribePath("get", "Verify archived tenant account balance chain"),
                    ["/v1/archive/migrations"] = DescribePath(new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["get"] = "List archive migrations",
                        ["post"] = "Create archive migration"
                    }),
                    ["/v1/archive/migrations/{migrationId}"] = DescribePath("get", "Read archive migration"),
                    ["/v1/archive/migrations/{migrationId}/batches"] = DescribePath("get", "List archive migration batches"),
                    ["/v1/archive/migrations/{migrationId}/batches/{batchId}/content"] = DescribePath("put", "Upload archive migration batch content"),
                    ["/v1/archive/migrations/{migrationId}/seal"] = DescribePath("post", "Seal archive migration"),
                    ["/v1/archive/migrations/{migrationId}/commit"] = DescribePath("post", "Commit archive migration"),
                    ["/v1/archive/migrations/{migrationId}/abort"] = DescribePath("post", "Abort archive migration"),
                    ["/v1/archive-server/request-history"] = DescribePath("get", "List Archive Server operational request history"),
                    ["/v1/archive-server/request-history/summary"] = DescribePath("get", "Summarize Archive Server operational request history"),
                    ["/v1/archive-server/request-history/{id}"] = DescribePath("get", "Read Archive Server operational request history entry"),
                    ["/api/v1/archive/ranges"] = DescribePath("get", "List archive ranges alias"),
                    ["/api/v1/archive/manifests"] = DescribePath("get", "List archive manifests alias"),
                    ["/api/v1/archive/migrations"] = DescribePath(new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["get"] = "List archive migrations alias",
                        ["post"] = "Create archive migration alias"
                    })
                }
            };

            await SendJsonAsync(ctx, 200, document).ConfigureAwait(false);
        }

        private static int GetQueryInt(HttpContextBase ctx, string name, int defaultValue, int minValue, int maxValue)
        {
            string? value = GetQueryString(ctx, name);
            if (String.IsNullOrWhiteSpace(value) || !Int32.TryParse(value, out int parsed))
            {
                return defaultValue;
            }

            return Math.Clamp(parsed, minValue, maxValue);
        }

        private static async Task<T?> DeserializeRequestBodyAsync<T>(HttpContextBase ctx) where T : class
        {
            if (ctx.Request.ContentLength <= 0 || ctx.Request.Data == null) return null;

            using (MemoryStream stream = new MemoryStream())
            {
                await ctx.Request.Data.CopyToAsync(stream).ConfigureAwait(false);
                string json = Encoding.UTF8.GetString(stream.ToArray());
                return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                });
            }
        }

        private static async Task<ArchiveMigration?> ReadMigrationFromRouteAsync(HttpContextBase ctx)
        {
            string? migrationId = GetRouteParameter(ctx, "migrationId");
            if (String.IsNullOrWhiteSpace(migrationId))
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.BadRequest, "Migration ID is required.").ConfigureAwait(false);
                return null;
            }

            ArchiveMigration? migration = await _Catalog.Migrations.ReadByIdAsync(migrationId).ConfigureAwait(false);
            if (migration == null)
            {
                await SendNotFoundAsync(ctx, "Archive migration was not found.").ConfigureAwait(false);
                return null;
            }

            return migration;
        }

        private static async Task<ArchiveUploadResult> ReceiveUploadAsync(Stream input, string temporaryFile, long maxBytes)
        {
            using (FileStream output = new FileStream(temporaryFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] buffer = new byte[81920];
                    long byteCount = 0;
                    int read;
                    while ((read = await input.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                    {
                        byteCount += read;
                        if (byteCount > maxBytes)
                        {
                            throw new InvalidOperationException("Batch content exceeds the configured byte limit.");
                        }

                        sha256.TransformBlock(buffer, 0, read, null, 0);
                        await output.WriteAsync(buffer, 0, read).ConfigureAwait(false);
                    }

                    sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    return new ArchiveUploadResult
                    {
                        ByteCount = byteCount,
                        ContentHashSha256 = ToHex(sha256.Hash ?? Array.Empty<byte>())
                    };
                }
            }
        }

        private static async Task<ArchiveManifest> CreateManifestForMigrationAsync(ArchiveMigration migration, List<ArchiveMigrationBatch> batches)
        {
            long rowCount = 0;
            string contentHash = BuildAggregateHash(batches);
            DateTime now = DateTime.UtcNow;
            foreach (ArchiveMigrationBatch batch in batches)
            {
                rowCount += batch.RowCount;
            }

            ArchiveEntryManifestStats? entryStats = null;
            if (migration.EntityType == ArchiveEntityType.Entries && migration.Format == ArchiveFormat.JsonlGzip)
            {
                entryStats = await ComputeEntryManifestStatsAsync(migration, batches).ConfigureAwait(false);
                rowCount = entryStats.RowCount;
            }

            ArchiveRequestHistoryManifestStats? requestHistoryStats = null;
            if (migration.EntityType == ArchiveEntityType.RequestHistory && migration.Format == ArchiveFormat.JsonlGzip)
            {
                requestHistoryStats = await ComputeRequestHistoryManifestStatsAsync(migration, batches).ConfigureAwait(false);
                rowCount = requestHistoryStats.RowCount;
            }

            ArchiveManifest manifest = new ArchiveManifest
            {
                TenantId = migration.TenantId,
                AccountId = migration.AccountId,
                MigrationId = migration.Id,
                EntityType = migration.EntityType,
                StoragePoolId = migration.StoragePoolId,
                FromUtc = migration.FromUtc,
                ToUtc = migration.ToUtc,
                RowCount = rowCount,
                CreditTotal = entryStats?.CreditTotal ?? 0m,
                DebitTotal = entryStats?.DebitTotal ?? 0m,
                ContentHashSha256 = contentHash,
                ManifestHashSha256 = BuildManifestHash(migration, contentHash, rowCount, entryStats?.CreditTotal ?? 0m, entryStats?.DebitTotal ?? 0m),
                Status = ArchiveManifestStatus.Committed,
                CreatedUtc = now,
                LastUpdateUtc = now
            };

            manifest = await _Catalog.Manifests.CreateAsync(manifest).ConfigureAwait(false);
            foreach (ArchiveMigrationBatch batch in batches)
            {
                ArchiveObject archiveObject = new ArchiveObject
                {
                    ManifestId = manifest.Id,
                    StoragePoolId = batch.StoragePoolId,
                    RelativePath = batch.CommittedRelativePath,
                    RowCount = batch.RowCount,
                    ByteCount = batch.ByteCount,
                    ContentHashSha256 = batch.ContentHashSha256,
                    CreatedUtc = now
                };
                await _Catalog.Objects.CreateAsync(archiveObject).ConfigureAwait(false);
                if (TryGetObjectStore(batch.StoragePoolId, out IArchiveObjectStore? store))
                {
                    await store!.UpdateMetadataAsync(batch.CommittedRelativePath, BuildObjectStorageMetadata(migration, batch, manifest, batch.ContentHashSha256)).ConfigureAwait(false);
                }
            }

            ArchiveRangeInfo range = new ArchiveRangeInfo
            {
                TenantId = manifest.TenantId,
                AccountId = manifest.AccountId,
                ManifestId = manifest.Id,
                EntityType = manifest.EntityType,
                FromUtc = manifest.FromUtc,
                ToUtc = manifest.ToUtc,
                RowCount = manifest.RowCount
            };
            await _Catalog.Ranges.CreateAsync(range).ConfigureAwait(false);

            if (requestHistoryStats != null)
            {
                ArchiveRequestHistoryRange requestHistoryRange = new ArchiveRequestHistoryRange
                {
                    TenantId = manifest.TenantId,
                    ManifestId = manifest.Id,
                    FromUtc = requestHistoryStats.MinCreatedUtc ?? manifest.FromUtc,
                    ToUtc = requestHistoryStats.MaxCreatedUtc ?? manifest.ToUtc,
                    RowCount = requestHistoryStats.RowCount,
                    MethodCountsJson = JsonSerializer.Serialize(requestHistoryStats.MethodCounts, Constants.JsonOptions),
                    StatusCodeCountsJson = JsonSerializer.Serialize(requestHistoryStats.StatusCodeCounts, Constants.JsonOptions),
                    CreatedUtc = now
                };
                await _Catalog.RequestHistoryRanges.CreateAsync(requestHistoryRange).ConfigureAwait(false);
            }

            if (entryStats != null)
            {
                foreach (ArchiveBalanceCheckpoint checkpoint in entryStats.BalanceCheckpoints)
                {
                    checkpoint.ManifestId = manifest.Id;
                    await _Catalog.BalanceCheckpoints.CreateAsync(checkpoint).ConfigureAwait(false);
                }
            }

            await WriteManifestSidecarsAsync(manifest, batches).ConfigureAwait(false);
            return manifest;
        }

        private static async Task ValidateUploadedBatchContentAsync(ArchiveMigration migration, ArchiveMigrationBatch batch, string temporaryFile)
        {
            if (migration.Format != ArchiveFormat.JsonlGzip)
            {
                return;
            }

            using (FileStream stream = new FileStream(temporaryFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (migration.EntityType == ArchiveEntityType.Entries)
                {
                    ArchiveEntryManifestStats stats = await ReadEntryBatchStatsAsync(migration, batch, stream).ConfigureAwait(false);
                    if (stats.RowCount != batch.RowCount)
                    {
                        throw new InvalidDataException("Archive batch row count does not match the uploaded entry payload.");
                    }
                }
                else if (migration.EntityType == ArchiveEntityType.RequestHistory)
                {
                    ArchiveRequestHistoryManifestStats stats = await ReadRequestHistoryBatchStatsAsync(migration, batch, stream).ConfigureAwait(false);
                    if (stats.RowCount != batch.RowCount)
                    {
                        throw new InvalidDataException("Archive batch row count does not match the uploaded request-history payload.");
                    }
                }
            }
        }

        private static async Task<ArchiveEntryManifestStats> ComputeEntryManifestStatsAsync(ArchiveMigration migration, List<ArchiveMigrationBatch> batches)
        {
            ArchiveEntryManifestStats stats = new ArchiveEntryManifestStats();
            foreach (ArchiveMigrationBatch batch in batches)
            {
                if (!TryGetObjectStore(batch.StoragePoolId, out IArchiveObjectStore? store))
                {
                    throw new NotSupportedException("Configured archive object storage provider is not available.");
                }

                using (Stream objectStream = await store!.ReadAsync(batch.CommittedRelativePath).ConfigureAwait(false))
                {
                    ArchiveEntryManifestStats batchStats = await ReadEntryBatchStatsAsync(migration, batch, objectStream).ConfigureAwait(false);
                    if (batchStats.RowCount != batch.RowCount)
                    {
                        throw new InvalidDataException("Committed archive object row count does not match migration batch metadata.");
                    }

                    stats.RowCount += batchStats.RowCount;
                    stats.CreditTotal += batchStats.CreditTotal;
                    stats.DebitTotal += batchStats.DebitTotal;
                    stats.BalanceCheckpoints.AddRange(batchStats.BalanceCheckpoints);
                }
            }

            return stats;
        }

        private static async Task<ArchiveRequestHistoryManifestStats> ComputeRequestHistoryManifestStatsAsync(ArchiveMigration migration, List<ArchiveMigrationBatch> batches)
        {
            ArchiveRequestHistoryManifestStats stats = new ArchiveRequestHistoryManifestStats();
            foreach (ArchiveMigrationBatch batch in batches)
            {
                if (!TryGetObjectStore(batch.StoragePoolId, out IArchiveObjectStore? store))
                {
                    throw new NotSupportedException("Configured archive object storage provider is not available.");
                }

                using (Stream objectStream = await store!.ReadAsync(batch.CommittedRelativePath).ConfigureAwait(false))
                {
                    ArchiveRequestHistoryManifestStats batchStats = await ReadRequestHistoryBatchStatsAsync(migration, batch, objectStream).ConfigureAwait(false);
                    if (batchStats.RowCount != batch.RowCount)
                    {
                        throw new InvalidDataException("Committed archive object row count does not match request-history migration batch metadata.");
                    }

                    stats.RowCount += batchStats.RowCount;
                    MergeCounts(stats.MethodCounts, batchStats.MethodCounts);
                    MergeCounts(stats.StatusCodeCounts, batchStats.StatusCodeCounts);
                    if (batchStats.MinCreatedUtc.HasValue && (!stats.MinCreatedUtc.HasValue || batchStats.MinCreatedUtc.Value < stats.MinCreatedUtc.Value))
                    {
                        stats.MinCreatedUtc = batchStats.MinCreatedUtc.Value;
                    }

                    if (batchStats.MaxCreatedUtc.HasValue && (!stats.MaxCreatedUtc.HasValue || batchStats.MaxCreatedUtc.Value > stats.MaxCreatedUtc.Value))
                    {
                        stats.MaxCreatedUtc = batchStats.MaxCreatedUtc.Value;
                    }
                }
            }

            return stats;
        }

        private static async Task<ArchiveEntryManifestStats> ReadEntryBatchStatsAsync(ArchiveMigration migration, ArchiveMigrationBatch batch, Stream stream)
        {
            ArchiveEntryManifestStats stats = new ArchiveEntryManifestStats();
            DateTime? previousCreatedUtc = null;
            string? previousId = null;
            using (GZipStream gzip = new GZipStream(stream, CompressionMode.Decompress, true))
            {
                using (StreamReader reader = new StreamReader(gzip, Encoding.UTF8))
                {
                    string? line;
                    while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                    {
                        if (String.IsNullOrWhiteSpace(line)) continue;

                        Entry? entry;
                        try
                        {
                            entry = JsonSerializer.Deserialize<Entry>(line, Constants.JsonOptions);
                        }
                        catch (JsonException e)
                        {
                            throw new InvalidDataException("Archive batch contains invalid entry JSON: " + e.Message, e);
                        }

                        if (entry == null)
                        {
                            throw new InvalidDataException("Archive batch contains an empty entry row.");
                        }

                        ValidateArchivedEntry(migration, entry);
                        DateTime createdUtc = entry.CreatedUtc.ToUniversalTime();
                        ValidateRowOrder(previousCreatedUtc, previousId, createdUtc, entry.Id, "entry");
                        previousCreatedUtc = createdUtc;
                        previousId = entry.Id;
                        stats.RowCount++;
                        if (entry.Type == EntryType.Credit)
                        {
                            stats.CreditTotal += entry.Amount;
                        }
                        else if (entry.Type == EntryType.Debit)
                        {
                            stats.DebitTotal += entry.Amount;
                        }
                        else if (entry.Type == EntryType.Balance)
                        {
                            stats.BalanceCheckpoints.Add(new ArchiveBalanceCheckpoint
                            {
                                TenantId = entry.TenantId,
                                AccountId = entry.AccountId,
                                ManifestId = String.Empty,
                                AsOfUtc = (entry.CommittedUtc ?? entry.CreatedUtc).ToUniversalTime(),
                                Balance = entry.Amount,
                                CreatedUtc = DateTime.UtcNow
                            });
                        }
                    }
                }
            }

            return stats;
        }

        private static async Task<ArchiveRequestHistoryManifestStats> ReadRequestHistoryBatchStatsAsync(ArchiveMigration migration, ArchiveMigrationBatch batch, Stream stream)
        {
            ArchiveRequestHistoryManifestStats stats = new ArchiveRequestHistoryManifestStats();
            DateTime? previousCreatedUtc = null;
            string? previousId = null;
            using (GZipStream gzip = new GZipStream(stream, CompressionMode.Decompress, true))
            {
                using (StreamReader reader = new StreamReader(gzip, Encoding.UTF8))
                {
                    string? line;
                    while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                    {
                        if (String.IsNullOrWhiteSpace(line)) continue;

                        RequestHistoryEntry? entry;
                        try
                        {
                            entry = JsonSerializer.Deserialize<RequestHistoryEntry>(line, Constants.JsonOptions);
                        }
                        catch (JsonException e)
                        {
                            throw new InvalidDataException("Archive batch contains invalid request-history JSON: " + e.Message, e);
                        }

                        if (entry == null)
                        {
                            throw new InvalidDataException("Archive batch contains an empty request-history row.");
                        }

                        ValidateArchivedRequestHistoryEntry(migration, entry);
                        DateTime createdUtc = entry.CreatedUtc.ToUniversalTime();
                        ValidateRowOrder(previousCreatedUtc, previousId, createdUtc, entry.Id, "request-history");
                        previousCreatedUtc = createdUtc;
                        previousId = entry.Id;
                        stats.RowCount++;
                        Increment(stats.MethodCounts, String.IsNullOrWhiteSpace(entry.Method) ? "UNKNOWN" : entry.Method.ToUpperInvariant());
                        Increment(stats.StatusCodeCounts, entry.StatusCode.ToString(CultureInfo.InvariantCulture));
                        if (!stats.MinCreatedUtc.HasValue || createdUtc < stats.MinCreatedUtc.Value)
                        {
                            stats.MinCreatedUtc = createdUtc;
                        }

                        if (!stats.MaxCreatedUtc.HasValue || createdUtc > stats.MaxCreatedUtc.Value)
                        {
                            stats.MaxCreatedUtc = createdUtc;
                        }
                    }
                }
            }

            return stats;
        }

        private static void ValidateRowOrder(DateTime? previousCreatedUtc, string? previousId, DateTime createdUtc, string id, string rowName)
        {
            if (!previousCreatedUtc.HasValue) return;
            if (createdUtc < previousCreatedUtc.Value)
            {
                throw new InvalidDataException("Archive batch " + rowName + " rows must be ordered by createdutc and id.");
            }

            if (createdUtc == previousCreatedUtc.Value && String.CompareOrdinal(id ?? String.Empty, previousId ?? String.Empty) < 0)
            {
                throw new InvalidDataException("Archive batch " + rowName + " rows must be ordered by createdutc and id.");
            }
        }

        private static bool IsManifestStatusTransitionAllowed(ArchiveManifestStatus current, ArchiveManifestStatus requested)
        {
            if (current == requested) return true;
            if (current == ArchiveManifestStatus.Committed) return true;
            return false;
        }

        private static List<ArchiveObject> SanitizeArchiveObjects(List<ArchiveObject> objects)
        {
            List<ArchiveObject> sanitized = new List<ArchiveObject>();
            foreach (ArchiveObject archiveObject in objects)
            {
                sanitized.Add(new ArchiveObject
                {
                    Id = archiveObject.Id,
                    ManifestId = archiveObject.ManifestId,
                    StoragePoolId = archiveObject.StoragePoolId,
                    RelativePath = String.Empty,
                    RowCount = archiveObject.RowCount,
                    ByteCount = archiveObject.ByteCount,
                    ContentHashSha256 = String.Empty,
                    CreatedUtc = archiveObject.CreatedUtc
                });
            }

            return sanitized;
        }

        private static void ValidateArchivedEntry(ArchiveMigration migration, Entry entry)
        {
            if (!String.Equals(entry.TenantId, migration.TenantId, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Archive batch contains an entry for the wrong tenant.");
            }

            if (!String.IsNullOrWhiteSpace(migration.AccountId) && !String.Equals(entry.AccountId, migration.AccountId, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Archive batch contains an entry for the wrong account.");
            }

            if (!entry.IsCommitted)
            {
                throw new InvalidDataException("Archive batch contains a pending entry.");
            }

            DateTime createdUtc = entry.CreatedUtc.ToUniversalTime();
            if (createdUtc < migration.FromUtc.ToUniversalTime() || createdUtc > migration.ToUtc.ToUniversalTime())
            {
                throw new InvalidDataException("Archive batch contains an entry outside the migration time range.");
            }
        }

        private static void ValidateArchivedRequestHistoryEntry(ArchiveMigration migration, RequestHistoryEntry entry)
        {
            if (!String.Equals(entry.TenantId, migration.TenantId, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Archive batch contains a request-history row for the wrong tenant.");
            }

            DateTime createdUtc = entry.CreatedUtc.ToUniversalTime();
            if (createdUtc < migration.FromUtc.ToUniversalTime() || createdUtc > migration.ToUtc.ToUniversalTime())
            {
                throw new InvalidDataException("Archive batch contains a request-history row outside the migration time range.");
            }
        }

        private static async Task WriteManifestSidecarsAsync(ArchiveManifest manifest, List<ArchiveMigrationBatch> batches)
        {
            foreach (ArchiveMigrationBatch batch in batches)
            {
                if (!TryGetObjectStore(batch.StoragePoolId, out IArchiveObjectStore? store))
                {
                    continue;
                }

                string directory = RelativeDirectory(batch.CommittedRelativePath);
                string committedPath = CombineRelativePath(directory, "manifest-" + SanitizePathSegment(manifest.Id) + ".json");
                string temporaryPath = CombineRelativePath("_tmp", SanitizePathSegment(manifest.MigrationId), "manifest-" + SanitizePathSegment(manifest.Id) + ".json");
                string json = JsonSerializer.Serialize(manifest, Constants.JsonOptions);
                using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    await store!.WriteTemporaryAsync(temporaryPath, stream).ConfigureAwait(false);
                }

                await store!.CommitAsync(temporaryPath, committedPath).ConfigureAwait(false);
                break;
            }
        }

        private static async Task<EnumerationResult<Entry>> EnumerateArchivedEntriesFromObjectsAsync(string tenantId, string accountId, ArchiveQuery query, CancellationToken token)
        {
            int startOffset = ArchiveContinuationToken.ResolveRowCursor(query, "entries");
            EnumerationResult<Entry> result = new EnumerationResult<Entry>
            {
                MaxResults = query.MaxResults,
                Skip = startOffset,
                Objects = new List<Entry>()
            };

            List<Entry> matchedEntries = new List<Entry>();
            foreach (ArchiveManifest manifest in await ReadAllManifestsAsync(BuildManifestScopeQuery(query), token).ConfigureAwait(false))
            {
                foreach (ArchiveObject archiveObject in await ReadAllObjectsAsync(manifest.Id, token).ConfigureAwait(false))
                {
                    if (!TryGetObjectStore(archiveObject.StoragePoolId, out IArchiveObjectStore? store))
                    {
                        throw new NotSupportedException("Configured archive object storage provider is not available.");
                    }

                    if (!archiveObject.RelativePath.EndsWith(".jsonl.gz", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new NotSupportedException("Only JSONL.Gzip archive entry reads are implemented in this build.");
                    }

                    using (Stream objectStream = await store!.ReadAsync(archiveObject.RelativePath, token).ConfigureAwait(false))
                    {
                        using (GZipStream gzip = new GZipStream(objectStream, CompressionMode.Decompress))
                        {
                            using (StreamReader reader = new StreamReader(gzip, Encoding.UTF8))
                            {
                                string? line;
                                while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                                {
                                    if (String.IsNullOrWhiteSpace(line)) continue;

                                    Entry? entry = JsonSerializer.Deserialize<Entry>(line, Constants.JsonOptions);
                                    if (entry == null) continue;
                                    if (!EntryMatches(entry, tenantId, accountId, query)) continue;
                                    matchedEntries.Add(entry);
                                }
                            }
                        }
                    }
                }
            }

            SortEntries(matchedEntries, query.Ordering);
            result.TotalRecords = matchedEntries.Count;
            if (startOffset < matchedEntries.Count)
            {
                int count = Math.Min(query.MaxResults, matchedEntries.Count - startOffset);
                result.Objects = matchedEntries.GetRange(startOffset, count);
            }

            int nextOffset = startOffset + result.Objects.Count;
            result.RecordsRemaining = Math.Max(0, matchedEntries.Count - nextOffset);
            result.EndOfResults = result.RecordsRemaining == 0;
            if (!result.EndOfResults)
            {
                result.ContinuationToken = ArchiveContinuationToken.Create(query, "entries", nextOffset);
            }

            return result;
        }

        private static async Task<RequestHistoryResult> EnumerateArchivedRequestHistoryFromObjectsAsync(RequestHistoryFilter filter, bool includeBodies, bool applyPaging, CancellationToken token)
        {
            ArchiveQuery continuationQuery = BuildRequestHistoryContinuationQuery(filter);
            string filterHash = BuildRequestHistoryFilterHash(filter);
            int startOffset = applyPaging ? ArchiveContinuationToken.ResolveRowCursor(continuationQuery, "request-history", filterHash) : 0;
            RequestHistoryResult result = new RequestHistoryResult
            {
                MaxResults = filter.MaxResults,
                Skip = startOffset,
                Objects = new List<RequestHistoryEntry>()
            };

            ArchiveQuery query = new ArchiveQuery
            {
                TenantId = filter.TenantId,
                EntityType = ArchiveEntityType.RequestHistory,
                ManifestStatus = ArchiveManifestStatus.Committed,
                FromUtc = filter.FromUtc,
                ToUtc = filter.ToUtc,
                MaxResults = 1000,
                Skip = 0
            };

            List<RequestHistoryEntry> matchedEntries = new List<RequestHistoryEntry>();
            foreach (ArchiveManifest manifest in await ReadAllManifestsAsync(query, token).ConfigureAwait(false))
            {
                foreach (ArchiveObject archiveObject in await ReadAllObjectsAsync(manifest.Id, token).ConfigureAwait(false))
                {
                    if (!TryGetObjectStore(archiveObject.StoragePoolId, out IArchiveObjectStore? store))
                    {
                        throw new NotSupportedException("Configured archive object storage provider is not available.");
                    }

                    if (!archiveObject.RelativePath.EndsWith(".jsonl.gz", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new NotSupportedException("Only JSONL.Gzip archive request-history reads are implemented in this build.");
                    }

                    using (Stream objectStream = await store!.ReadAsync(archiveObject.RelativePath, token).ConfigureAwait(false))
                    {
                        using (GZipStream gzip = new GZipStream(objectStream, CompressionMode.Decompress))
                        {
                            using (StreamReader reader = new StreamReader(gzip, Encoding.UTF8))
                            {
                                string? line;
                                while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                                {
                                    if (String.IsNullOrWhiteSpace(line)) continue;

                                    RequestHistoryEntry? entry = JsonSerializer.Deserialize<RequestHistoryEntry>(line, Constants.JsonOptions);
                                    if (entry == null) continue;
                                    if (!RequestHistoryMatches(entry, filter)) continue;

                                    if (!includeBodies)
                                    {
                                        entry.RequestBody = null;
                                        entry.ResponseBody = null;
                                    }

                                    matchedEntries.Add(entry);
                                }
                            }
                        }
                    }
                }
            }

            matchedEntries.Sort((left, right) =>
            {
                int compare = right.CreatedUtc.CompareTo(left.CreatedUtc);
                return compare != 0 ? compare : String.CompareOrdinal(right.Id, left.Id);
            });

            result.TotalRecords = matchedEntries.Count;
            if (applyPaging)
            {
                if (startOffset < matchedEntries.Count)
                {
                    int count = Math.Min(filter.MaxResults, matchedEntries.Count - startOffset);
                    result.Objects = matchedEntries.GetRange(startOffset, count);
                }
            }
            else
            {
                result.Objects = matchedEntries;
            }

            int nextOffset = startOffset + result.Objects.Count;
            result.RecordsRemaining = applyPaging ? Math.Max(0, matchedEntries.Count - nextOffset) : 0;
            result.EndOfResults = result.RecordsRemaining == 0;
            if (applyPaging && !result.EndOfResults)
            {
                result.ContinuationToken = ArchiveContinuationToken.Create(continuationQuery, "request-history", nextOffset, filterHash);
            }

            return result;
        }

        private static async Task<RequestHistoryEntry?> ReadArchivedRequestHistoryEntryFromObjectsAsync(string id, RequestHistoryFilter filter, CancellationToken token)
        {
            RequestHistoryResult result = await EnumerateArchivedRequestHistoryFromObjectsAsync(filter, true, false, token).ConfigureAwait(false);
            foreach (RequestHistoryEntry entry in result.Objects)
            {
                if (String.Equals(entry.Id, id, StringComparison.Ordinal))
                {
                    return entry;
                }
            }

            return null;
        }

        private static RequestHistorySummary BuildRequestHistorySummary(List<RequestHistoryEntry> entries, RequestHistoryFilter filter)
        {
            RequestHistorySummary summary = new RequestHistorySummary();
            if (entries == null || entries.Count == 0) return summary;

            double totalDuration = 0d;
            Dictionary<long, RequestHistorySummaryBucket> buckets = new Dictionary<long, RequestHistorySummaryBucket>();
            Dictionary<long, double> bucketDurations = new Dictionary<long, double>();
            Dictionary<long, long> bucketCounts = new Dictionary<long, long>();
            int bucketMinutes = filter.BucketMinutes <= 0 ? 15 : filter.BucketMinutes;
            long bucketTicks = TimeSpan.FromMinutes(bucketMinutes).Ticks;

            foreach (RequestHistoryEntry entry in entries)
            {
                summary.TotalCount++;
                if (entry.StatusCode >= 200 && entry.StatusCode <= 399)
                {
                    summary.TotalSuccess++;
                }
                else
                {
                    summary.TotalFailure++;
                }

                totalDuration += entry.DurationMs;
                long startTicks = entry.CreatedUtc.Ticks - (entry.CreatedUtc.Ticks % bucketTicks);
                if (!buckets.TryGetValue(startTicks, out RequestHistorySummaryBucket? bucket))
                {
                    bucket = new RequestHistorySummaryBucket
                    {
                        BucketStartUtc = new DateTime(startTicks, DateTimeKind.Utc),
                        BucketEndUtc = new DateTime(startTicks + bucketTicks, DateTimeKind.Utc)
                    };
                    buckets[startTicks] = bucket;
                    bucketDurations[startTicks] = 0d;
                    bucketCounts[startTicks] = 0;
                }

                if (entry.StatusCode >= 200 && entry.StatusCode <= 399)
                {
                    bucket.SuccessCount++;
                }
                else
                {
                    bucket.FailureCount++;
                }

                bucketDurations[startTicks] += entry.DurationMs;
                bucketCounts[startTicks]++;
            }

            summary.AverageDurationMs = summary.TotalCount == 0 ? 0d : totalDuration / summary.TotalCount;
            List<long> keys = new List<long>(buckets.Keys);
            keys.Sort();
            foreach (long key in keys)
            {
                RequestHistorySummaryBucket bucket = buckets[key];
                long count = bucketCounts[key];
                bucket.AverageDurationMs = count == 0 ? 0d : bucketDurations[key] / count;
                summary.Buckets.Add(bucket);
            }

            return summary;
        }

        private static async Task<bool> EnsureArchiveCoverageAsync(HttpContextBase ctx, ArchiveQuery query)
        {
            if (!_Settings.Archive.RequireCompleteCoverage || query.AllowPartial) return true;
            if (!query.FromUtc.HasValue || !query.ToUtc.HasValue) return true;

            List<ArchiveManifest> manifests = await ReadAllManifestsAsync(new ArchiveQuery
            {
                TenantId = query.TenantId,
                AccountId = query.AccountId,
                EntityType = query.EntityType,
                ManifestStatus = ArchiveManifestStatus.Committed,
                FromUtc = query.FromUtc,
                ToUtc = query.ToUtc,
                MaxResults = 1000
            }, ctx.Token).ConfigureAwait(false);

            if (manifests.Count < 1)
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.NotFound, "Archived range was not found.").ConfigureAwait(false);
                return false;
            }

            manifests.Sort((left, right) => left.FromUtc.CompareTo(right.FromUtc));
            DateTime coveredUntilUtc = query.FromUtc.Value.ToUniversalTime();
            DateTime requestedToUtc = query.ToUtc.Value.ToUniversalTime();

            foreach (ArchiveManifest manifest in manifests)
            {
                DateTime manifestFromUtc = manifest.FromUtc.ToUniversalTime();
                DateTime manifestToUtc = manifest.ToUtc.ToUniversalTime();
                if (manifestToUtc < coveredUntilUtc) continue;

                if (manifestFromUtc > coveredUntilUtc.AddTicks(ArchiveCoverageToleranceTicks))
                {
                    await SendErrorAsync(ctx, ArchiveApiErrorCode.Conflict, "Archived range is only partially covered.").ConfigureAwait(false);
                    return false;
                }

                if (manifestToUtc > coveredUntilUtc)
                {
                    coveredUntilUtc = manifestToUtc;
                }

                if (coveredUntilUtc.AddTicks(ArchiveCoverageToleranceTicks) >= requestedToUtc)
                {
                    return true;
                }
            }

            await SendErrorAsync(ctx, ArchiveApiErrorCode.Conflict, "Archived range is only partially covered.").ConfigureAwait(false);
            return false;
        }

        private static async Task VerifyArchiveManifestAsync(ArchiveManifest manifest, ArchiveVerificationResult result, CancellationToken token)
        {
            if (manifest.Status != ArchiveManifestStatus.Committed)
            {
                AddVerificationError(result, "Manifest " + manifest.Id + " is not committed.");
            }

            ArchiveMigration? migration = null;
            if (String.IsNullOrWhiteSpace(manifest.MigrationId))
            {
                AddVerificationError(result, "Manifest " + manifest.Id + " does not reference a migration.");
            }
            else
            {
                migration = await _Catalog.Migrations.ReadByIdAsync(manifest.MigrationId, token).ConfigureAwait(false);
                if (migration == null)
                {
                    AddVerificationError(result, "Manifest " + manifest.Id + " references a missing migration " + manifest.MigrationId + ".");
                }
            }

            List<ArchiveObject> objects = await ReadAllObjectsAsync(manifest.Id, token).ConfigureAwait(false);
            result.CheckedObjects += objects.Count;
            long objectRows = 0L;
            foreach (ArchiveObject archiveObject in objects)
            {
                objectRows += archiveObject.RowCount;
                if (!TryGetObjectStore(archiveObject.StoragePoolId, out IArchiveObjectStore? store))
                {
                    AddVerificationError(result, "Object " + archiveObject.Id + " uses an unavailable storage pool.");
                    continue;
                }

                try
                {
                    using (Stream stream = await store!.ReadAsync(archiveObject.RelativePath, token).ConfigureAwait(false))
                    {
                        ArchiveUploadResult hash = await ComputeArchiveObjectHashAsync(stream, token).ConfigureAwait(false);
                        if (archiveObject.ByteCount != hash.ByteCount)
                        {
                            AddVerificationError(result, "Object " + archiveObject.Id + " byte count does not match storage.");
                        }

                        if (!String.Equals(archiveObject.ContentHashSha256, hash.ContentHashSha256, StringComparison.OrdinalIgnoreCase))
                        {
                            AddVerificationError(result, "Object " + archiveObject.Id + " SHA-256 hash does not match storage.");
                        }
                    }
                }
                catch (Exception e) when (e is IOException || e is InvalidOperationException || e is UnauthorizedAccessException || e is NotSupportedException)
                {
                    AddVerificationError(result, "Object " + archiveObject.Id + " could not be read: " + e.Message);
                }
            }

            if (objectRows != manifest.RowCount)
            {
                AddVerificationError(result, "Manifest " + manifest.Id + " row count does not match catalog object row totals.");
            }

            if (migration == null)
            {
                return;
            }

            List<ArchiveMigrationBatch> batches = await ReadAllMigrationBatchesAsync(migration.Id, token).ConfigureAwait(false);
            string aggregateHash = BuildAggregateHash(batches);
            if (!String.Equals(manifest.ContentHashSha256, aggregateHash, StringComparison.OrdinalIgnoreCase))
            {
                AddVerificationError(result, "Manifest " + manifest.Id + " aggregate content hash does not match migration batches.");
            }

            ArchiveEntryManifestStats stats = new ArchiveEntryManifestStats();
            foreach (ArchiveMigrationBatch batch in batches)
            {
                if (batch.Status != ArchiveMigrationBatchStatus.Verified)
                {
                    AddVerificationError(result, "Migration batch " + batch.Id + " is not verified.");
                }

                if (!TryGetObjectStore(batch.StoragePoolId, out IArchiveObjectStore? store))
                {
                    AddVerificationError(result, "Migration batch " + batch.Id + " uses an unavailable storage pool.");
                    continue;
                }

                try
                {
                    using (Stream stream = await store!.ReadAsync(batch.CommittedRelativePath, token).ConfigureAwait(false))
                    {
                        ArchiveEntryManifestStats batchStats = await ReadEntryBatchStatsAsync(migration, batch, stream).ConfigureAwait(false);
                        if (batchStats.RowCount != batch.RowCount)
                        {
                            AddVerificationError(result, "Migration batch " + batch.Id + " row count does not match object content.");
                        }

                        stats.RowCount += batchStats.RowCount;
                        stats.CreditTotal += batchStats.CreditTotal;
                        stats.DebitTotal += batchStats.DebitTotal;
                        stats.BalanceCheckpoints.AddRange(batchStats.BalanceCheckpoints);
                    }
                }
                catch (Exception e) when (e is IOException || e is InvalidDataException || e is JsonException || e is NotSupportedException || e is InvalidOperationException)
                {
                    AddVerificationError(result, "Migration batch " + batch.Id + " content verification failed: " + e.Message);
                }
            }

            if (stats.RowCount != manifest.RowCount)
            {
                AddVerificationError(result, "Manifest " + manifest.Id + " row count does not match decoded entry objects.");
            }

            if (stats.CreditTotal != manifest.CreditTotal)
            {
                AddVerificationError(result, "Manifest " + manifest.Id + " credit total does not match decoded entry objects.");
            }

            if (stats.DebitTotal != manifest.DebitTotal)
            {
                AddVerificationError(result, "Manifest " + manifest.Id + " debit total does not match decoded entry objects.");
            }

            string manifestHash = BuildManifestHash(migration, manifest.ContentHashSha256, manifest.RowCount, manifest.CreditTotal, manifest.DebitTotal);
            if (!String.Equals(manifest.ManifestHashSha256, manifestHash, StringComparison.OrdinalIgnoreCase))
            {
                AddVerificationError(result, "Manifest " + manifest.Id + " manifest hash does not match manifest fields.");
            }
        }

        private static async Task<List<ArchiveManifest>> ReadAllManifestsAsync(ArchiveQuery query, CancellationToken token)
        {
            List<ArchiveManifest> manifests = new List<ArchiveManifest>();
            int skip = 0;
            while (true)
            {
                ArchiveQuery pageQuery = CloneArchiveQuery(query);
                pageQuery.ContinuationToken = null;
                pageQuery.Skip = skip;
                pageQuery.MaxResults = 1000;
                EnumerationResult<ArchiveManifest> page = await _Catalog.Manifests.EnumerateAsync(pageQuery, token).ConfigureAwait(false);
                manifests.AddRange(page.Objects);
                if (page.EndOfResults || page.Objects.Count == 0) break;
                skip += page.Objects.Count;
            }

            return manifests;
        }

        private static async Task<List<ArchiveObject>> ReadAllObjectsAsync(string manifestId, CancellationToken token)
        {
            List<ArchiveObject> objects = new List<ArchiveObject>();
            int skip = 0;
            while (true)
            {
                ArchiveQuery query = new ArchiveQuery { MaxResults = 1000, Skip = skip };
                EnumerationResult<ArchiveObject> page = await _Catalog.Objects.EnumerateByManifestAsync(manifestId, query, token).ConfigureAwait(false);
                objects.AddRange(page.Objects);
                if (page.EndOfResults || page.Objects.Count == 0) break;
                skip += page.Objects.Count;
            }

            return objects;
        }

        private static async Task<List<ArchiveMigrationBatch>> ReadAllMigrationBatchesAsync(string migrationId, CancellationToken token)
        {
            List<ArchiveMigrationBatch> batches = new List<ArchiveMigrationBatch>();
            int skip = 0;
            while (true)
            {
                ArchiveQuery query = new ArchiveQuery { MaxResults = 1000, Skip = skip };
                EnumerationResult<ArchiveMigrationBatch> page = await _Catalog.Migrations.EnumerateBatchesAsync(migrationId, query, token).ConfigureAwait(false);
                batches.AddRange(page.Objects);
                if (page.EndOfResults || page.Objects.Count == 0) break;
                skip += page.Objects.Count;
            }

            return batches;
        }

        private static async Task<List<ArchiveBalanceCheckpoint>> ReadAllBalanceCheckpointsAsync(string manifestId, CancellationToken token)
        {
            List<ArchiveBalanceCheckpoint> checkpoints = new List<ArchiveBalanceCheckpoint>();
            int skip = 0;
            while (true)
            {
                ArchiveQuery query = new ArchiveQuery { MaxResults = 1000, Skip = skip };
                EnumerationResult<ArchiveBalanceCheckpoint> page = await _Catalog.BalanceCheckpoints.EnumerateByManifestAsync(manifestId, query, token).ConfigureAwait(false);
                checkpoints.AddRange(page.Objects);
                if (page.EndOfResults || page.Objects.Count == 0) break;
                skip += page.Objects.Count;
            }

            return checkpoints;
        }

        private static async Task<ArchiveUploadResult> ComputeArchiveObjectHashAsync(Stream stream, CancellationToken token)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] buffer = new byte[81920];
                long byteCount = 0L;
                int read;
                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
                {
                    byteCount += read;
                    sha256.TransformBlock(buffer, 0, read, null, 0);
                }

                sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return new ArchiveUploadResult
                {
                    ByteCount = byteCount,
                    ContentHashSha256 = ToHex(sha256.Hash ?? Array.Empty<byte>())
                };
            }
        }

        private static void AddVerificationError(ArchiveVerificationResult result, string message)
        {
            result.IsValid = false;
            result.Errors.Add(message);
        }

        private static bool RequestHistoryMatches(RequestHistoryEntry entry, RequestHistoryFilter filter)
        {
            if (!String.IsNullOrWhiteSpace(filter.TenantId) && !String.Equals(entry.TenantId, filter.TenantId, StringComparison.Ordinal)) return false;
            if (!String.IsNullOrWhiteSpace(filter.PrincipalId) && !String.Equals(entry.PrincipalId, filter.PrincipalId, StringComparison.Ordinal)) return false;
            if (!String.IsNullOrWhiteSpace(filter.Method) && !String.Equals(entry.Method, filter.Method, StringComparison.OrdinalIgnoreCase)) return false;
            if (filter.StatusCode.HasValue && entry.StatusCode != filter.StatusCode.Value) return false;
            if (filter.FromUtc.HasValue && entry.CreatedUtc < filter.FromUtc.Value) return false;
            if (filter.ToUtc.HasValue && entry.CreatedUtc > filter.ToUtc.Value) return false;
            if (!String.IsNullOrWhiteSpace(filter.PathContains) &&
                (entry.Path == null || entry.Path.IndexOf(filter.PathContains, StringComparison.OrdinalIgnoreCase) < 0))
            {
                return false;
            }

            return true;
        }

        private static bool EntryMatches(Entry entry, string tenantId, string accountId, ArchiveQuery query)
        {
            if (!String.Equals(entry.TenantId, tenantId, StringComparison.Ordinal)) return false;
            if (!String.Equals(entry.AccountId, accountId, StringComparison.Ordinal)) return false;
            if (query.FromUtc.HasValue && entry.CreatedUtc < query.FromUtc.Value) return false;
            if (query.ToUtc.HasValue && entry.CreatedUtc > query.ToUtc.Value) return false;
            if (query.AmountMinimum.HasValue && entry.Amount < query.AmountMinimum.Value) return false;
            if (query.AmountMaximum.HasValue && entry.Amount > query.AmountMaximum.Value) return false;
            bool hasCreditFilter = query.CreditMinimum.HasValue || query.CreditMaximum.HasValue;
            bool hasDebitFilter = query.DebitMinimum.HasValue || query.DebitMaximum.HasValue;
            if (hasCreditFilter && entry.Type != EntryType.Credit) return false;
            if (hasDebitFilter && entry.Type != EntryType.Debit) return false;
            if (entry.Type == EntryType.Credit && query.CreditMinimum.HasValue && entry.Amount < query.CreditMinimum.Value) return false;
            if (entry.Type == EntryType.Credit && query.CreditMaximum.HasValue && entry.Amount > query.CreditMaximum.Value) return false;
            if (entry.Type == EntryType.Debit && query.DebitMinimum.HasValue && entry.Amount < query.DebitMinimum.Value) return false;
            if (entry.Type == EntryType.Debit && query.DebitMaximum.HasValue && entry.Amount > query.DebitMaximum.Value) return false;
            if (!EntryHasLabels(entry, query.Labels)) return false;
            if (!EntryHasTags(entry, query.Tags)) return false;
            if (!String.IsNullOrWhiteSpace(query.Search) &&
                (entry.Description == null || entry.Description.IndexOf(query.Search, StringComparison.OrdinalIgnoreCase) < 0))
            {
                return false;
            }

            return true;
        }

        private static bool EntryHasLabels(Entry entry, List<string> labels)
        {
            if (labels == null || labels.Count == 0) return true;
            foreach (string label in labels)
            {
                bool found = false;
                foreach (string entryLabel in entry.Labels)
                {
                    if (String.Equals(entryLabel, label, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found) return false;
            }

            return true;
        }

        private static bool EntryHasTags(Entry entry, Dictionary<string, string> tags)
        {
            if (tags == null || tags.Count == 0) return true;
            foreach (KeyValuePair<string, string> tag in tags)
            {
                if (!entry.Tags.TryGetValue(tag.Key, out string? value)) return false;
                if (!String.Equals(value, tag.Value, StringComparison.Ordinal)) return false;
            }

            return true;
        }

        private static void SortEntries(List<Entry> entries, EnumerationOrderEnum ordering)
        {
            entries.Sort((left, right) =>
            {
                int compare;
                switch (ordering)
                {
                    case EnumerationOrderEnum.CreatedAscending:
                        compare = left.CreatedUtc.CompareTo(right.CreatedUtc);
                        return compare != 0 ? compare : String.CompareOrdinal(left.Id, right.Id);
                    case EnumerationOrderEnum.AmountAscending:
                        compare = left.Amount.CompareTo(right.Amount);
                        return compare != 0 ? compare : String.CompareOrdinal(left.Id, right.Id);
                    case EnumerationOrderEnum.AmountDescending:
                        compare = right.Amount.CompareTo(left.Amount);
                        return compare != 0 ? compare : String.CompareOrdinal(right.Id, left.Id);
                    case EnumerationOrderEnum.CreatedDescending:
                    default:
                        compare = right.CreatedUtc.CompareTo(left.CreatedUtc);
                        return compare != 0 ? compare : String.CompareOrdinal(right.Id, left.Id);
                }
            });
        }

        private static ArchiveQuery BuildRequestHistoryContinuationQuery(RequestHistoryFilter filter)
        {
            return new ArchiveQuery
            {
                TenantId = filter.TenantId,
                EntityType = ArchiveEntityType.RequestHistory,
                FromUtc = filter.FromUtc,
                ToUtc = filter.ToUtc,
                Search = filter.PathContains,
                ContinuationToken = filter.ContinuationToken,
                MaxResults = filter.MaxResults,
                Skip = filter.Skip
            };
        }

        private static string BuildRequestHistoryFilterHash(RequestHistoryFilter filter)
        {
            string material = "request-history|" +
                (filter.TenantId ?? String.Empty) + "|" +
                (filter.PrincipalId ?? String.Empty) + "|" +
                (filter.Method ?? String.Empty) + "|" +
                (filter.StatusCode?.ToString(CultureInfo.InvariantCulture) ?? String.Empty) + "|" +
                (filter.PathContains ?? String.Empty) + "|" +
                (filter.FromUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? String.Empty) + "|" +
                (filter.ToUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? String.Empty);
            return HashString(material);
        }

        private static ArchiveQuery CloneArchiveQuery(ArchiveQuery query)
        {
            return new ArchiveQuery
            {
                TenantId = query.TenantId,
                AccountId = query.AccountId,
                EntityType = query.EntityType,
                StoragePoolId = query.StoragePoolId,
                MigrationId = query.MigrationId,
                ManifestStatus = query.ManifestStatus,
                MigrationStatus = query.MigrationStatus,
                FromUtc = query.FromUtc,
                ToUtc = query.ToUtc,
                Search = query.Search,
                AllowPartial = query.AllowPartial,
                ContinuationToken = query.ContinuationToken,
                Ordering = query.Ordering,
                AmountMinimum = query.AmountMinimum,
                AmountMaximum = query.AmountMaximum,
                CreditMinimum = query.CreditMinimum,
                CreditMaximum = query.CreditMaximum,
                DebitMinimum = query.DebitMinimum,
                DebitMaximum = query.DebitMaximum,
                Labels = new List<string>(query.Labels),
                Tags = new Dictionary<string, string>(query.Tags, StringComparer.OrdinalIgnoreCase),
                MaxResults = query.MaxResults,
                Skip = query.Skip
            };
        }

        private static ArchiveQuery BuildManifestScopeQuery(ArchiveQuery query)
        {
            ArchiveQuery scope = CloneArchiveQuery(query);
            scope.Search = null;
            scope.AmountMinimum = null;
            scope.AmountMaximum = null;
            scope.CreditMinimum = null;
            scope.CreditMaximum = null;
            scope.DebitMinimum = null;
            scope.DebitMaximum = null;
            scope.Labels = new List<string>();
            scope.Tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return scope;
        }

        private static bool IsAcceptedFormat(ArchiveFormat format)
        {
            if (format != ArchiveFormat.JsonlGzip)
            {
                return false;
            }

            if (_Settings.Archive.AcceptedFormats == null || _Settings.Archive.AcceptedFormats.Count == 0) return true;
            foreach (ArchiveFormat accepted in _Settings.Archive.AcceptedFormats)
            {
                if (accepted == format) return true;
            }

            return false;
        }

        private static bool IsSameMigrationRequest(
            ArchiveMigration existing,
            string tenantId,
            string? accountId,
            ArchiveEntityType entityType,
            string storagePoolId,
            ArchiveFormat format,
            ArchiveCompression compression,
            DateTime fromUtc,
            DateTime toUtc)
        {
            return String.Equals(existing.TenantId, tenantId, StringComparison.Ordinal) &&
                String.Equals(existing.AccountId ?? String.Empty, accountId ?? String.Empty, StringComparison.Ordinal) &&
                existing.EntityType == entityType &&
                String.Equals(existing.StoragePoolId, storagePoolId, StringComparison.Ordinal) &&
                existing.Format == format &&
                existing.Compression == compression &&
                existing.FromUtc == fromUtc &&
                existing.ToUtc == toUtc;
        }

        private static bool TryGetObjectStore(string storagePoolId, out IArchiveObjectStore? store)
        {
            return _ObjectStores.TryGetValue(storagePoolId, out store);
        }

        private static string BuildTemporaryObjectPath(ArchiveMigration migration, string batchId)
        {
            return CombineRelativePath("_tmp", SanitizePathSegment(migration.Id), SanitizePathSegment(batchId) + "." + ExtensionFor(migration.Format));
        }

        private static string BuildCommittedObjectPath(ArchiveStoragePool pool, ArchiveMigration migration, string batchId, long sequenceNumber)
        {
            string accountSegment = String.IsNullOrWhiteSpace(migration.AccountId)
                ? "accountid=all"
                : "accountid=" + SanitizePathSegment(migration.AccountId);

            return CombineRelativePath(
                pool.Prefix,
                "v1",
                "entity=" + SanitizePathSegment(migration.EntityType.ToString().ToLowerInvariant()),
                "tenantid=" + SanitizePathSegment(migration.TenantId),
                accountSegment,
                "migration=" + SanitizePathSegment(migration.Id),
                "part-" + sequenceNumber.ToString("D12", CultureInfo.InvariantCulture) + "-" + SanitizePathSegment(batchId) + "." + ExtensionFor(migration.Format));
        }

        private static Dictionary<string, string> BuildObjectStorageMetadata(ArchiveMigration migration, ArchiveMigrationBatch batch, ArchiveManifest? manifest, string contentHashSha256)
        {
            Dictionary<string, string> metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["netledger-schema-version"] = "archive-object-v1",
                ["netledger-entity-type"] = migration.EntityType.ToString(),
                ["netledger-tenant-id"] = migration.TenantId,
                ["netledger-migration-id"] = migration.Id,
                ["netledger-batch-id"] = batch.Id,
                ["netledger-content-hash-sha256"] = contentHashSha256,
                ["netledger-row-count"] = batch.RowCount.ToString(CultureInfo.InvariantCulture)
            };

            if (!String.IsNullOrWhiteSpace(migration.AccountId))
            {
                metadata["netledger-account-id"] = migration.AccountId;
            }

            if (manifest != null)
            {
                metadata["netledger-manifest-id"] = manifest.Id;
                metadata["netledger-manifest-hash-sha256"] = manifest.ManifestHashSha256;
            }

            return metadata;
        }

        private static string CombineRelativePath(params string?[] segments)
        {
            StringBuilder builder = new StringBuilder();
            foreach (string? segment in segments)
            {
                if (String.IsNullOrWhiteSpace(segment)) continue;
                string trimmed = segment.Replace('\\', '/').Trim('/');
                if (String.IsNullOrWhiteSpace(trimmed)) continue;
                if (builder.Length > 0) builder.Append('/');
                builder.Append(trimmed);
            }

            return builder.ToString();
        }

        private static string RelativeDirectory(string relativePath)
        {
            if (String.IsNullOrWhiteSpace(relativePath)) return String.Empty;
            string normalized = relativePath.Replace('\\', '/').Trim('/');
            int index = normalized.LastIndexOf('/');
            return index <= 0 ? String.Empty : normalized.Substring(0, index);
        }

        private static string SanitizePathSegment(string? value)
        {
            if (String.IsNullOrWhiteSpace(value)) return "none";
            StringBuilder builder = new StringBuilder();
            foreach (char c in value)
            {
                if ((c >= 'a' && c <= 'z') ||
                    (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') ||
                    c == '-' ||
                    c == '_' ||
                    c == '=' ||
                    c == '.')
                {
                    builder.Append(c);
                }
                else
                {
                    builder.Append('_');
                }
            }

            return builder.Length == 0 ? "none" : builder.ToString();
        }

        private static string ExtensionFor(ArchiveFormat format)
        {
            return format == ArchiveFormat.Parquet ? "parquet" : "jsonl.gz";
        }

        private static string BuildAggregateHash(List<ArchiveMigrationBatch> batches)
        {
            StringBuilder builder = new StringBuilder();
            foreach (ArchiveMigrationBatch batch in batches)
            {
                builder.Append(batch.Id);
                builder.Append(':');
                builder.Append(batch.ContentHashSha256);
                builder.Append(':');
                builder.Append(batch.RowCount.ToString(CultureInfo.InvariantCulture));
                builder.Append(':');
                builder.Append(batch.ByteCount.ToString(CultureInfo.InvariantCulture));
                builder.Append('\n');
            }

            return HashString(builder.ToString());
        }

        private static void Increment(Dictionary<string, long> values, string key)
        {
            if (String.IsNullOrWhiteSpace(key)) key = "UNKNOWN";
            if (!values.ContainsKey(key))
            {
                values[key] = 0;
            }

            values[key]++;
        }

        private static void MergeCounts(Dictionary<string, long> target, Dictionary<string, long> source)
        {
            foreach (KeyValuePair<string, long> count in source)
            {
                if (!target.ContainsKey(count.Key))
                {
                    target[count.Key] = 0;
                }

                target[count.Key] += count.Value;
            }
        }

        private static string BuildManifestHash(ArchiveMigration migration, string contentHash, long rowCount, decimal creditTotal, decimal debitTotal)
        {
            string material = migration.Id + "|" +
                migration.TenantId + "|" +
                (migration.AccountId ?? String.Empty) + "|" +
                migration.EntityType + "|" +
                migration.StoragePoolId + "|" +
                migration.FromUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) + "|" +
                migration.ToUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) + "|" +
                rowCount.ToString(CultureInfo.InvariantCulture) + "|" +
                creditTotal.ToString(CultureInfo.InvariantCulture) + "|" +
                debitTotal.ToString(CultureInfo.InvariantCulture) + "|" +
                contentHash;
            return HashString(material);
        }

        private static string HashString(string value)
        {
            byte[] data = Encoding.UTF8.GetBytes(value);
            byte[] hash = SHA256.HashData(data);
            return ToHex(hash);
        }

        private static string ToHex(byte[] bytes)
        {
            StringBuilder builder = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static string? EmptyToNull(string? value)
        {
            return String.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static ArchiveStoragePool ToStoragePool(ArchiveStoragePoolSettings settings)
        {
            return new ArchiveStoragePool
            {
                Id = settings.Id,
                Name = settings.Name,
                Type = settings.Type,
                BasePath = settings.BasePath,
                Bucket = settings.Bucket,
                Prefix = settings.Prefix,
                Format = settings.Format,
                Compression = settings.Compression
            };
        }

        private static ArchiveQuery BuildArchiveQuery(HttpContextBase ctx)
        {
            ArchiveQuery query = new ArchiveQuery
            {
                MaxResults = GetQueryInt(ctx, "maxResults", 100, 1, 1000),
                Skip = GetQueryInt(ctx, "skip", 0, 0, Int32.MaxValue),
                TenantId = FirstNonEmpty(GetTenantId(ctx), GetAuthContext(ctx).TenantId),
                AccountId = FirstNonEmpty(GetQueryString(ctx, "accountId"), GetRouteParameter(ctx, "accountId")),
                StoragePoolId = GetQueryString(ctx, "storagePoolId"),
                Search = GetQueryString(ctx, "search"),
                ContinuationToken = GetQueryString(ctx, "continuationToken"),
                AmountMinimum = ParseQueryDecimal(ctx, "amountMin") ?? ParseQueryDecimal(ctx, "amountMinimum"),
                AmountMaximum = ParseQueryDecimal(ctx, "amountMax") ?? ParseQueryDecimal(ctx, "amountMaximum"),
                CreditMinimum = ParseQueryDecimal(ctx, "creditMin") ?? ParseQueryDecimal(ctx, "creditMinimum"),
                CreditMaximum = ParseQueryDecimal(ctx, "creditMax") ?? ParseQueryDecimal(ctx, "creditMaximum"),
                DebitMinimum = ParseQueryDecimal(ctx, "debitMin") ?? ParseQueryDecimal(ctx, "debitMinimum"),
                DebitMaximum = ParseQueryDecimal(ctx, "debitMax") ?? ParseQueryDecimal(ctx, "debitMaximum")
            };

            string? entityType = GetQueryString(ctx, "entityType");
            if (!String.IsNullOrWhiteSpace(entityType) && Enum.TryParse(entityType, true, out ArchiveEntityType parsedEntityType))
            {
                query.EntityType = parsedEntityType;
            }

            string? manifestStatus = GetQueryString(ctx, "manifestStatus") ?? GetQueryString(ctx, "status");
            if (!String.IsNullOrWhiteSpace(manifestStatus) && Enum.TryParse(manifestStatus, true, out ArchiveManifestStatus parsedManifestStatus))
            {
                query.ManifestStatus = parsedManifestStatus;
            }

            string? migrationStatus = GetQueryString(ctx, "migrationStatus") ?? GetQueryString(ctx, "status");
            if (!String.IsNullOrWhiteSpace(migrationStatus) && Enum.TryParse(migrationStatus, true, out ArchiveMigrationStatus parsedMigrationStatus))
            {
                query.MigrationStatus = parsedMigrationStatus;
            }

            string? ordering = GetQueryString(ctx, "ordering");
            if (!String.IsNullOrWhiteSpace(ordering) && Enum.TryParse(ordering, true, out EnumerationOrderEnum parsedOrdering))
            {
                query.Ordering = parsedOrdering;
            }

            query.FromUtc = ParseQueryDate(ctx, "fromUtc") ?? ParseQueryDate(ctx, "startTime");
            query.ToUtc = ParseQueryDate(ctx, "toUtc") ?? ParseQueryDate(ctx, "endTime");
            string? allowPartial = GetQueryString(ctx, "allowPartial");
            if (!String.IsNullOrWhiteSpace(allowPartial) && Boolean.TryParse(allowPartial, out bool parsedAllowPartial))
            {
                query.AllowPartial = parsedAllowPartial;
            }

            query.Labels = ParseLabels(ctx);
            query.Tags = ParseTags(ctx);
            return query;
        }

        private static RequestHistoryFilter BuildRequestHistoryFilter(HttpContextBase ctx)
        {
            RequestHistoryFilter filter = new RequestHistoryFilter
            {
                MaxResults = GetQueryInt(ctx, "maxResults", 25, 1, 1000),
                Skip = GetQueryInt(ctx, "skip", 0, 0, Int32.MaxValue),
                TenantId = FirstNonEmpty(GetTenantId(ctx), GetAuthContext(ctx).TenantId),
                PrincipalId = GetQueryString(ctx, "principalId"),
                Method = GetQueryString(ctx, "method"),
                PathContains = GetQueryString(ctx, "pathContains"),
                FromUtc = ParseQueryDate(ctx, "fromUtc") ?? ParseQueryDate(ctx, "startTime"),
                ToUtc = ParseQueryDate(ctx, "toUtc") ?? ParseQueryDate(ctx, "endTime"),
                ContinuationToken = GetQueryString(ctx, "continuationToken"),
                BucketMinutes = GetQueryInt(ctx, "bucketMinutes", 15, 1, 1440)
            };

            string? statusCode = GetQueryString(ctx, "statusCode");
            if (!String.IsNullOrWhiteSpace(statusCode) && Int32.TryParse(statusCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedStatusCode))
            {
                filter.StatusCode = parsedStatusCode;
            }

            return filter;
        }

        private static async Task<bool> ApplyRequestHistoryScopeAsync(HttpContextBase ctx, RequestHistoryFilter filter, bool archiveServerHistory)
        {
            ArchiveAuthContext auth = GetAuthContext(ctx);
            if (auth.IsNotRequired) return true;

            if (!auth.IsAuthenticated)
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.Unauthorized, "Authentication is required.").ConfigureAwait(false);
                return false;
            }

            if (auth.IsAdmin)
            {
                return true;
            }

            if (String.IsNullOrWhiteSpace(auth.TenantId))
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.Forbidden, "Tenant scope is required.").ConfigureAwait(false);
                return false;
            }

            if (!String.IsNullOrWhiteSpace(filter.TenantId) && !String.Equals(filter.TenantId, auth.TenantId, StringComparison.Ordinal))
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.Forbidden, "Request history is limited to the authenticated tenant.").ConfigureAwait(false);
                return false;
            }

            filter.TenantId = auth.TenantId;
            if (auth.IsTenantAdmin)
            {
                return true;
            }

            filter.PrincipalId = auth.PrincipalId;
            if (archiveServerHistory && String.IsNullOrWhiteSpace(filter.PrincipalId))
            {
                await SendErrorAsync(ctx, ArchiveApiErrorCode.Forbidden, "Principal scope is required.").ConfigureAwait(false);
                return false;
            }

            return true;
        }

        private static bool CanAccessRequestHistoryRecord(ArchiveAuthContext auth, string? tenantId, string? principalId)
        {
            if (auth.IsNotRequired || auth.IsAdmin) return true;
            if (String.IsNullOrWhiteSpace(tenantId) || !String.Equals(tenantId, auth.TenantId, StringComparison.Ordinal)) return false;
            if (auth.IsTenantAdmin) return true;
            return !String.IsNullOrWhiteSpace(principalId) && String.Equals(principalId, auth.PrincipalId, StringComparison.Ordinal);
        }

        private static EnumerationQuery BuildEnumerationQuery(HttpContextBase ctx, string tenantId, string accountId)
        {
            return new EnumerationQuery
            {
                TenantId = tenantId,
                AccountId = accountId,
                MaxResults = GetQueryInt(ctx, "maxResults", 100, 1, 1000),
                Skip = GetQueryInt(ctx, "skip", 0, 0, Int32.MaxValue),
                SearchTerm = GetQueryString(ctx, "search")
            };
        }

        private static DateTime? ParseQueryDate(HttpContextBase ctx, string name)
        {
            string? value = GetQueryString(ctx, name);
            if (String.IsNullOrWhiteSpace(value)) return null;
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime parsed)
                ? parsed.ToUniversalTime()
                : null;
        }

        private static decimal? ParseQueryDecimal(HttpContextBase ctx, string name)
        {
            string? value = GetQueryString(ctx, name);
            if (String.IsNullOrWhiteSpace(value)) return null;
            return Decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed)
                ? parsed
                : null;
        }

        private static List<string> ParseLabels(HttpContextBase ctx)
        {
            string? labels = GetQueryString(ctx, "labels");
            if (String.IsNullOrWhiteSpace(labels)) return new List<string>();
            return MetadataValidator.NormalizeLabels(labels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        private static Dictionary<string, string> ParseTags(HttpContextBase ctx)
        {
            Dictionary<string, string> tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string? tagsValue = GetQueryString(ctx, "tags");
            if (String.IsNullOrWhiteSpace(tagsValue)) return tags;

            string[] pairs = tagsValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string pair in pairs)
            {
                string[] parts = pair.Split('=', 2, StringSplitOptions.TrimEntries);
                if (parts.Length == 2 && !String.IsNullOrEmpty(parts[0]))
                {
                    tags[parts[0]] = parts[1];
                }
            }

            return MetadataValidator.NormalizeTags(tags);
        }

        private static string GetTenantId(HttpContextBase ctx)
        {
            return FirstNonEmpty(
                GetRouteParameter(ctx, "tenantId"),
                GetQueryString(ctx, "tenantId"),
                GetRequestHeader(ctx.Request.Headers, "x-tenant-id"),
                GetAuthContext(ctx).TenantId);
        }

        private static string? GetQueryString(HttpContextBase ctx, string name)
        {
            string? value = ctx.Request.Query?.Elements?[name];
            return value == null ? null : WebUtility.UrlDecode(value);
        }

        private static string? GetRouteParameter(HttpContextBase ctx, string name)
        {
            return ctx.Request.Url.Parameters?[name];
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            foreach (string? value in values)
            {
                if (!String.IsNullOrWhiteSpace(value)) return value;
            }

            return String.Empty;
        }

        private static Dictionary<string, object> DescribePath(string method, string summary)
        {
            Dictionary<string, string> methods = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [method] = summary
            };
            return DescribePath(methods);
        }

        private static Dictionary<string, object> DescribePath(Dictionary<string, string> methods)
        {
            Dictionary<string, object> path = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> method in methods)
            {
                path[method.Key] = DescribeOperation(method.Value);
            }

            return path;
        }

        private static Dictionary<string, object> DescribeOperation(string summary)
        {
            Dictionary<string, object> methodDescription = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["summary"] = summary,
                ["responses"] = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["200"] = new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["description"] = "Success"
                    }
                }
            };

            return methodDescription;
        }

        private static bool IsPreAuthenticationRoute(HttpContextBase ctx)
        {
            string path = ctx.Request.Url.RawWithoutQuery ?? String.Empty;
            return path.Equals("/", StringComparison.Ordinal) ||
                path.Equals("/openapi.json", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/v1/service", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/v1/health", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/api/v1/service", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/api/v1/health", StringComparison.OrdinalIgnoreCase);
        }

        private static ArchiveAuthContext GetAuthContext(HttpContextBase ctx)
        {
            if (ctx.Metadata is ArchiveAuthContext auth)
            {
                return auth;
            }

            if (_Settings != null && _Settings.Authentication != null &&
                (!_Settings.Authentication.Enabled || String.Equals(_Settings.Authentication.Mode, "None", StringComparison.OrdinalIgnoreCase)))
            {
                return ArchiveAuthContext.NotRequired();
            }

            return new ArchiveAuthContext();
        }

        private static async Task<bool> AuthorizeArchiveReadAsync(HttpContextBase ctx, string? tenantId, string resourceType, string? resourceId)
        {
            ArchiveAuthContext auth = GetAuthContext(ctx);
            if (auth.IsNotRequired) return true;

            if (!auth.CanUseTenant(tenantId))
            {
                await DenyArchiveAsync(ctx, tenantId, "ArchiveRead", resourceType, resourceId, "Authenticated tenant does not match the archive tenant.").ConfigureAwait(false);
                return false;
            }

            if (auth.IsAdmin || auth.IsTenantAdmin) return true;

            bool permitted = auth.HasPermission(resourceType, "Read") ||
                auth.HasPermission("Archive", "Read") ||
                auth.HasPermission("Account", "Read");

            if (!permitted)
            {
                await DenyArchiveAsync(ctx, tenantId, "ArchiveRead", resourceType, resourceId, "Archive read permission is required.").ConfigureAwait(false);
                return false;
            }

            if (auth.RequiresMappedAccountScope() && !auth.CanUseAccount(resourceId))
            {
                await DenyArchiveAsync(ctx, tenantId, "ArchiveRead", resourceType, resourceId, "Archive read is limited to mapped accounts for this user.").ConfigureAwait(false);
                return false;
            }

            return true;
        }

        private static async Task<bool> AuthorizeArchiveManageAsync(HttpContextBase ctx, string? tenantId, string resourceType, string? resourceId, string operationType)
        {
            ArchiveAuthContext auth = GetAuthContext(ctx);
            if (auth.IsNotRequired) return true;

            if (!auth.CanUseTenant(tenantId))
            {
                await DenyArchiveAsync(ctx, tenantId, operationType, resourceType, resourceId, "Authenticated tenant does not match the archive tenant.").ConfigureAwait(false);
                return false;
            }

            if (auth.IsAdmin || auth.IsTenantAdmin) return true;

            bool permitted = auth.HasPermission("Archive", "Admin") ||
                auth.HasPermission("Archive", operationType) ||
                auth.HasPermission(resourceType, operationType);

            if (!permitted)
            {
                await DenyArchiveAsync(ctx, tenantId, operationType, resourceType, resourceId, "Archive management permission is required.").ConfigureAwait(false);
                return false;
            }

            return true;
        }

        private static async Task<bool> AuthorizeArchiveAdminAsync(HttpContextBase ctx, string? tenantId, string resourceType, string? resourceId, string operationType)
        {
            ArchiveAuthContext auth = GetAuthContext(ctx);
            if (auth.IsNotRequired) return true;

            if (!String.IsNullOrWhiteSpace(tenantId) && !auth.CanUseTenant(tenantId))
            {
                await DenyArchiveAsync(ctx, tenantId, operationType, resourceType, resourceId, "Authenticated tenant does not match the archive tenant.").ConfigureAwait(false);
                return false;
            }

            if (auth.IsAdmin) return true;

            bool permitted = auth.HasPermission("Archive", "Admin") || auth.HasPermission(resourceType, "Admin");
            if (!permitted)
            {
                await DenyArchiveAsync(ctx, tenantId, operationType, resourceType, resourceId, "Archive administrator permission is required.").ConfigureAwait(false);
                return false;
            }

            return true;
        }

        private static bool CanExposeArchiveObjectDebugMetadata(HttpContextBase ctx)
        {
            ArchiveAuthContext auth = GetAuthContext(ctx);
            if (auth.IsNotRequired) return true;
            return auth.IsAdmin ||
                auth.IsTenantAdmin ||
                auth.HasPermission("Archive", "Admin") ||
                auth.HasPermission("ArchiveObject", "Admin");
        }

        private static async Task DenyArchiveAsync(HttpContextBase ctx, string? tenantId, string action, string targetType, string? targetId, string reason)
        {
            await WriteArchiveAuditAsync(ctx, tenantId, action, targetType, targetId, "Denied", reason).ConfigureAwait(false);
            await SendErrorAsync(ctx, ArchiveApiErrorCode.Forbidden, reason).ConfigureAwait(false);
        }

        private static async Task WriteArchiveAuditAsync(
            HttpContextBase ctx,
            string? tenantId,
            string action,
            string targetType,
            string? targetId,
            string result,
            string? reason)
        {
            if (_Catalog == null) return;

            ArchiveAuthContext auth = GetAuthContext(ctx);
            Dictionary<string, string?> metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Result"] = result,
                ["Reason"] = reason,
                ["RequestId"] = ctx.Guid.ToString(),
                ["PrincipalType"] = auth.PrincipalType,
                ["SourceIp"] = ctx.Request.Source.IpAddress
            };

            ArchiveAuditRecord record = new ArchiveAuditRecord
            {
                TenantId = EmptyToNull(tenantId),
                PrincipalId = EmptyToNull(auth.PrincipalId),
                Action = action,
                TargetType = targetType,
                TargetId = EmptyToNull(targetId),
                Metadata = JsonSerializer.Serialize(metadata, Constants.JsonOptions),
                CreatedUtc = DateTime.UtcNow
            };

            try
            {
                await _Catalog.AuditRecords.CreateAsync(record, ctx.Token).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _Logging.Warn(_Header + "failed to write archive audit record: " + e.Message);
            }
        }

        private static void CaptureArchiveServerRequestHistory(HttpContextBase ctx)
        {
            if (_Settings == null || _Settings.RequestHistory == null || !_Settings.RequestHistory.Enabled) return;
            if (_Catalog == null) return;

            ArchiveAuthContext auth = GetAuthContext(ctx);
            double totalMs = ctx.Timestamp.TotalMs.HasValue ? ctx.Timestamp.TotalMs.Value : 0d;
            ArchiveServerRequestHistoryRecord record = new ArchiveServerRequestHistoryRecord
            {
                TenantId = EmptyToNull(GetTenantId(ctx)),
                PrincipalId = EmptyToNull(auth.PrincipalId),
                Method = ctx.Request.Method.ToString(),
                Path = ctx.Request.Url.RawWithoutQuery ?? String.Empty,
                StatusCode = ctx.Response.StatusCode,
                DurationMs = Convert.ToDecimal(totalMs, CultureInfo.InvariantCulture),
                CreatedUtc = DateTime.UtcNow
            };

            Task ignored = Task.Run(async () =>
            {
                try
                {
                    await _Catalog.ServerRequestHistory.CreateAsync(record, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    _Logging.Warn(_Header + "failed to write archive request history record: " + e.Message);
                }
            });
        }

        private static async Task DefaultRoute(HttpContextBase ctx)
        {
            await SendErrorAsync(ctx, ArchiveApiErrorCode.NotFound, "Route not found.").ConfigureAwait(false);
        }

        private static async Task ExceptionHandler(HttpContextBase ctx, Exception e)
        {
            _Logging.Alert(_Header + "exception: " + e);
            await SendErrorAsync(ctx, ArchiveApiErrorCode.InternalError, e.Message).ConfigureAwait(false);
        }

        private static async Task SendNotFoundAsync(HttpContextBase ctx, string message)
        {
            await SendErrorAsync(ctx, ArchiveApiErrorCode.NotFound, message).ConfigureAwait(false);
        }

        private static async Task SendNotImplementedAsync(HttpContextBase ctx, string message)
        {
            await SendErrorAsync(ctx, ArchiveApiErrorCode.NotImplemented, message).ConfigureAwait(false);
        }

        private static async Task SendErrorAsync(HttpContextBase ctx, ArchiveApiErrorCode error, string message)
        {
            await SendJsonAsync(ctx, (int)error, new ArchiveApiErrorResponse(error, message)).ConfigureAwait(false);
        }

        private static async Task SendJsonAsync(HttpContextBase ctx, int statusCode, object data)
        {
            ctx.Response.StatusCode = statusCode;
            ctx.Response.ContentType = Constants.JsonContentType;
            await ctx.Response.Send(JsonSerializer.Serialize(data, Constants.JsonOptions)).ConfigureAwait(false);
        }

        private static void AddCorsHeaders(HttpContextBase ctx, bool preflight)
        {
            CorsSettings cors = _Settings.Webserver.Cors;
            if (cors == null || !cors.Enabled) return;

            string origin = GetRequestHeader(ctx.Request.Headers, "origin");
            string allowedOrigin = GetAllowedOrigin(cors, origin);
            if (String.IsNullOrEmpty(allowedOrigin)) return;

            SetHeader(ctx, "Access-Control-Allow-Origin", allowedOrigin);
            if (!String.Equals(allowedOrigin, "*", StringComparison.Ordinal))
            {
                SetHeader(ctx, "Vary", "Origin");
            }

            if (cors.AllowCredentials)
            {
                SetHeader(ctx, "Access-Control-Allow-Credentials", "true");
            }

            SetHeader(ctx, "Access-Control-Expose-Headers", JoinValues(cors.ExposedHeaders));

            if (preflight)
            {
                SetHeader(ctx, "Access-Control-Allow-Methods", JoinValues(cors.AllowedMethods));
                string requestedHeaders = GetRequestHeader(ctx.Request.Headers, "access-control-request-headers");
                SetHeader(ctx, "Access-Control-Allow-Headers", ContainsWildcard(cors.AllowedHeaders) && !String.IsNullOrEmpty(requestedHeaders) ? requestedHeaders : JoinValues(cors.AllowedHeaders));
                if (cors.MaxAgeSeconds > 0)
                {
                    SetHeader(ctx, "Access-Control-Max-Age", cors.MaxAgeSeconds.ToString());
                }
            }
        }

        private static string GetAllowedOrigin(CorsSettings cors, string origin)
        {
            if (ContainsWildcard(cors.AllowedOrigins) && !cors.AllowCredentials)
            {
                return "*";
            }

            if (String.IsNullOrEmpty(origin) || cors.AllowedOrigins == null)
            {
                return "";
            }

            foreach (string allowedOrigin in cors.AllowedOrigins)
            {
                if (String.Equals(allowedOrigin, origin, StringComparison.OrdinalIgnoreCase))
                {
                    return origin;
                }
            }

            return "";
        }

        private static bool ContainsWildcard(List<string>? values)
        {
            if (values == null) return false;
            foreach (string value in values)
            {
                if (String.Equals(value, "*", StringComparison.Ordinal)) return true;
            }

            return false;
        }

        private static string JoinValues(List<string>? values)
        {
            if (values == null) return "";
            StringBuilder builder = new StringBuilder();
            foreach (string value in values)
            {
                if (String.IsNullOrWhiteSpace(value)) continue;
                if (builder.Length > 0) builder.Append(", ");
                builder.Append(value.Trim());
            }

            return builder.ToString();
        }

        private static string GetRequestHeader(NameValueCollection? headers, string name)
        {
            if (headers == null) return "";
            for (int i = 0; i < headers.Count; i++)
            {
                string key = headers.GetKey(i);
                if (String.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return headers.Get(i) ?? "";
                }
            }

            return "";
        }

        private static void SetHeader(HttpContextBase ctx, string name, string value)
        {
            if (String.IsNullOrEmpty(name) || String.IsNullOrEmpty(value)) return;
            ctx.Response.Headers.Remove(name);
            ctx.Response.Headers.Add(name, value);
        }

        private static void WebserverException(object? sender, ExceptionEventArgs e)
        {
            _Logging.Alert(_Header + "webserver exception in " + e.Method + " " + e.Url + " from " + e.Ip + ": " + e.Exception);
        }
    }
}
