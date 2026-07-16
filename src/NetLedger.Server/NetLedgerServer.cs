namespace NetLedger.Server
{
    using NetLedger.Server.API.Agnostic;
    using NetLedger.Server.API.REST;
    using NetLedger.Server.Authentication;
    using NetLedger.Server.Models;
    using NetLedger.Server.Services;
    using NetLedger.Server.Settings;
    using SyslogLogging;
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.IO;
    using System.Reflection;
    using System.Runtime.Loader;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// NetLedger REST API server.
    /// </summary>
    public class NetLedgerServer
    {
        private static readonly string _Header = "[NetLedgerServer] ";
        private static string _SettingsFile = "./netledger.json";
        private static string _Hostname = Environment.MachineName;

        private static ServerSettings _Settings = null!;
        private static LoggingModule _Logging = null!;
        private static Ledger _Ledger = null!;
        private static AuthService _AuthService = null!;
        private static AuthorizationService _AuthorizationService = null!;
        private static RequestHistoryService _RequestHistoryService = null!;
        private static Webserver _Webserver = null!;

        private static ServiceHandler _ServiceHandler = null!;
        private static AccountHandler _AccountHandler = null!;
        private static EntryHandler _EntryHandler = null!;
        private static BalanceHandler _BalanceHandler = null!;

        private static ApiKeyHandler _ApiKeyHandler = null!;
        private static IdentityHandler _IdentityHandler = null!;

        private static RestServiceHandler _RestServiceHandler = null!;
        private static RestAccountHandler _RestAccountHandler = null!;
        private static RestEntryHandler _RestEntryHandler = null!;
        private static RestBalanceHandler _RestBalanceHandler = null!;
        private static RestApiKeyHandler _RestApiKeyHandler = null!;
        private static RestIdentityHandler _RestIdentityHandler = null!;
        private static RestRequestHistoryHandler _RestRequestHistoryHandler = null!;

        /// <summary>
        /// Main entry point.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <returns>Exit code.</returns>
        public static async Task<int> Main(string[] args)
        {
            Welcome();
            ParseArguments(args);
            InitializeSettings();
            await InitializeGlobals().ConfigureAwait(false);

            // Graceful shutdown handling
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

            _Logging.Info(_Header + "server running, press CTRL+C to exit");

            // Wait for termination signal
            bool waitHandleSignal = false;
            do
            {
                waitHandleSignal = waitHandle.WaitOne(1000);
            }
            while (!waitHandleSignal);

            _Logging.Info(_Header + "shutting down");

            // Cleanup
            await CleanupAsync().ConfigureAwait(false);

            _Logging.Info(_Header + "terminated");
            return 0;
        }

        private static void Welcome()
        {
            Version? version = Assembly.GetExecutingAssembly().GetName().Version;
            Console.WriteLine(
                """

                            _   _          _                 
                 _ __   ___| |_| | ___  __| | __ _  ___ _ __ 
                | '_ \ / _ \ __| |/ _ \/ _` |/ _` |/ _ \ '__|
                | | | |  __/ |_| |  __/ (_| | (_| |  __/ |   
                |_| |_|\___|\__|_|\___|\__,_|\__, |\___|_|   
                                             |___/           

                """);
            Console.WriteLine(" Version " + (version?.ToString() ?? "1.0.0"));
            Console.WriteLine(" (c)2025 Joel Christner");
            Console.WriteLine();
        }

        private static void ParseArguments(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i].Equals("-f") || args[i].Equals("--file"))
                    {
                        if (i + 1 < args.Length)
                        {
                            _SettingsFile = args[i + 1];
                            i++;
                        }
                    }
                }
            }
        }

        private static void InitializeSettings()
        {
            if (!File.Exists(_SettingsFile))
            {
                Console.WriteLine("Settings file not found, creating default: " + _SettingsFile);

                _Settings = new ServerSettings();

                string json = JsonSerializer.Serialize(_Settings, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                });

                File.WriteAllText(_SettingsFile, json, Encoding.UTF8);

                Console.WriteLine("Created default settings file, please review, modify, and restart the server");
            }
            else
            {
                string json = File.ReadAllText(_SettingsFile, Encoding.UTF8);
                _Settings = JsonSerializer.Deserialize<ServerSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                }) ?? new ServerSettings();

                Console.WriteLine("Loaded settings from: " + _SettingsFile);
            }
        }

        private static async Task InitializeGlobals()
        {
            // Initialize logging
            _Logging = new LoggingModule();
            _Logging.Settings.EnableConsole = _Settings.Logging.EnableConsole;  

            // Initialize ledger
            _Ledger = new Ledger(_Settings.Database);
            LogDatabaseConfiguration();

            // Initialize authentication
            _AuthService = new AuthService(_Settings, _Logging, _Ledger.Driver);
            _AuthorizationService = new AuthorizationService(_Ledger.Driver, _Logging);
            _RequestHistoryService = new RequestHistoryService(_Ledger.Driver, _Settings.RequestHistory, _Logging);

            // Initialize agnostic handlers
            _ServiceHandler = new ServiceHandler(_Settings, _Logging);
            _AccountHandler = new AccountHandler(_Settings, _Logging, _Ledger, _AuthorizationService);
            _EntryHandler = new EntryHandler(_Settings, _Logging, _Ledger, _AuthorizationService);
            _BalanceHandler = new BalanceHandler(_Settings, _Logging, _Ledger, _AuthorizationService);
            _ApiKeyHandler = new ApiKeyHandler(_Settings, _Logging, _AuthService);
            _IdentityHandler = new IdentityHandler(_Settings, _Logging, _Ledger.Driver, _AuthService, _AuthorizationService);

            // Initialize REST handlers
            _RestServiceHandler = new RestServiceHandler(_Settings, _Logging, _ServiceHandler);
            _RestAccountHandler = new RestAccountHandler(_Settings, _Logging, _AccountHandler, _AuthService, _RequestHistoryService);
            _RestEntryHandler = new RestEntryHandler(_Settings, _Logging, _EntryHandler, _AuthService, _RequestHistoryService);
            _RestBalanceHandler = new RestBalanceHandler(_Settings, _Logging, _BalanceHandler, _AuthService, _RequestHistoryService);
            _RestApiKeyHandler = new RestApiKeyHandler(_Settings, _Logging, _ApiKeyHandler, _AuthService, _RequestHistoryService);
            _RestIdentityHandler = new RestIdentityHandler(_Settings, _Logging, _IdentityHandler, _AuthService, _RequestHistoryService);
            _RestRequestHistoryHandler = new RestRequestHistoryHandler(_Ledger.Driver, _AuthService, _Logging);

            // Initialize webserver
            WatsonWebserver.Core.WebserverSettings wsSettings = new WatsonWebserver.Core.WebserverSettings(
                _Settings.Webserver.Hostname,
                _Settings.Webserver.Port,
                _Settings.Webserver.Ssl);

            _Webserver = new Webserver(wsSettings, DefaultRoute);
            _Webserver.Events.ExceptionEncountered += WebserverException;
            _Webserver.Routes.Preflight = PreflightHandler;
            _Webserver.Routes.PreRouting = PreRoutingHandler;
            _Webserver.Routes.PostRouting = PostRoutingHandler;

            // Register routes
            RegisterRoutes();

            // Start webserver
            _Logging.Info(_Header + "webserver starting on " +
                (_Settings.Webserver.Ssl ? "https" : "http") + "://" +
                _Settings.Webserver.Hostname + ":" + _Settings.Webserver.Port);
            await _Webserver.StartAsync().ConfigureAwait(false);
        }

        private static async Task PreflightHandler(HttpContextBase ctx)
        {
            NameValueCollection responseHeaders = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);

            string[] requestedHeaders = null;
            string headers = "";

            if (ctx.Request.Headers != null)
            {
                for (int i = 0; i < ctx.Request.Headers.Count; i++)
                {
                    string key = ctx.Request.Headers.GetKey(i);
                    string value = ctx.Request.Headers.Get(i);
                    if (String.IsNullOrEmpty(key)) continue;
                    if (String.IsNullOrEmpty(value)) continue;
                    if (String.Compare(key.ToLower(), "access-control-request-headers") == 0)
                    {
                        requestedHeaders = value.Split(',');
                        break;
                    }
                }
            }

            if (requestedHeaders != null)
            {
                foreach (string curr in requestedHeaders)
                {
                    headers += ", " + curr;
                }
            }

            responseHeaders.Add("Access-Control-Allow-Methods", "OPTIONS, HEAD, GET, PUT, POST, DELETE");
            responseHeaders.Add("Access-Control-Allow-Headers", "*, Content-Type, X-Requested-With, " + headers);
            responseHeaders.Add("Access-Control-Expose-Headers", "Content-Type, X-Requested-With, " + headers);
            responseHeaders.Add("Access-Control-Allow-Origin", "*");
            responseHeaders.Add("Accept", "*/*");
            responseHeaders.Add("Accept-Language", "en-US, en");
            responseHeaders.Add("Accept-Charset", "ISO-8859-1, utf-8");
            responseHeaders.Add("Connection", "keep-alive");

            ctx.Response.StatusCode = 200;
            ctx.Response.Headers = responseHeaders;
            await ctx.Response.Send().ConfigureAwait(false);
            return;
        }

        private static void RegisterRoutes()
        {
            // Service endpoints (unauthenticated)
            _Webserver.Routes.PreAuthentication.Static.Add(HttpMethod.HEAD, "/", _RestServiceHandler.ExistsAsync);
            _Webserver.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/", _RestServiceHandler.GetInfoAsync);
            _Webserver.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/openapi.json", OpenApiAsync);
            _Webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.POST, "/v1/auth/tenants", _RestIdentityHandler.DiscoverTenantsAsync, ExceptionHandler);
            _Webserver.Routes.PreAuthentication.Parameter.Add(HttpMethod.POST, "/v1/auth/login", _RestIdentityHandler.LoginAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1/auth/logout", _RestIdentityHandler.LogoutAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/me/permissions", _RestIdentityHandler.GetEffectivePermissionsAsync, ExceptionHandler);

            // Tenant and security administration
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/tenants", _RestIdentityHandler.EnumerateTenantsAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1/tenants", _RestIdentityHandler.CreateTenantAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/tenants/{tenantId}", _RestIdentityHandler.ReadTenantAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1/tenants/{tenantId}", _RestIdentityHandler.DeleteTenantAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/tenants/{tenantId}/users", _RestIdentityHandler.EnumerateUsersAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1/tenants/{tenantId}/users", _RestIdentityHandler.CreateUserAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/tenants/{tenantId}/users/{userId}", _RestIdentityHandler.ReadUserAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/tenants/{tenantId}/accounts/{accountGuid}/users", _RestIdentityHandler.EnumerateAccountUsersAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1/tenants/{tenantId}/accounts/{accountGuid}/users/{userId}", _RestIdentityHandler.MapAccountUserAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1/tenants/{tenantId}/accounts/{accountGuid}/users/{userId}", _RestIdentityHandler.DeleteAccountUserAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/tenants/{tenantId}/sessions", _RestIdentityHandler.EnumerateSessionsAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1/tenants/{tenantId}/sessions/{sessionId}", _RestIdentityHandler.RevokeSessionAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/tenants/{tenantId}/audit", _RestIdentityHandler.EnumerateAuditAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/tenants/{tenantId}/roles", _RestIdentityHandler.EnumerateRolesAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1/tenants/{tenantId}/roles", _RestIdentityHandler.CreateRoleAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/tenants/{tenantId}/permissions", _RestIdentityHandler.EnumeratePermissionsAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1/tenants/{tenantId}/permissions", _RestIdentityHandler.CreatePermissionAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1/tenants/{tenantId}/users/{userId}/roles", _RestIdentityHandler.AssignUserRoleAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1/tenants/{tenantId}/roles/{roleId}/permissions/{permissionId}", _RestIdentityHandler.MapRolePermissionAsync, ExceptionHandler);

            // Account endpoints
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/accounts", _RestAccountHandler.EnumerateAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1/accounts", _RestAccountHandler.CreateAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.HEAD, "/v1/accounts/{accountGuid}", _RestAccountHandler.ExistsAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/accounts/{accountGuid}", _RestAccountHandler.ReadAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1/accounts/{accountGuid}", _RestAccountHandler.DeleteAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/accounts/byname/{accountName}", _RestAccountHandler.ReadByNameAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/tenants/{tenantId}/accounts", _RestAccountHandler.EnumerateAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1/tenants/{tenantId}/accounts", _RestAccountHandler.CreateAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.HEAD, "/v1/tenants/{tenantId}/accounts/{accountGuid}", _RestAccountHandler.ExistsAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/tenants/{tenantId}/accounts/{accountGuid}", _RestAccountHandler.ReadAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1/tenants/{tenantId}/accounts/{accountGuid}", _RestAccountHandler.DeleteAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/tenants/{tenantId}/accounts/byname/{accountName}", _RestAccountHandler.ReadByNameAsync, ExceptionHandler);

            // Entry endpoints
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/accounts/{accountGuid}/entries", _RestEntryHandler.GetEntriesAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/accounts/{accountGuid}/entries/pending", _RestEntryHandler.GetPendingEntriesAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/accounts/{accountGuid}/entries/pending/credits", _RestEntryHandler.GetPendingCreditsAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/accounts/{accountGuid}/entries/pending/debits", _RestEntryHandler.GetPendingDebitsAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1/accounts/{accountGuid}/entries/enumerate", _RestEntryHandler.EnumerateAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1/accounts/{accountGuid}/credits", _RestEntryHandler.AddCreditsAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1/accounts/{accountGuid}/debits", _RestEntryHandler.AddDebitsAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1/accounts/{accountGuid}/entries/{entryGuid}", _RestEntryHandler.CancelEntryAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/tenants/{tenantId}/accounts/{accountGuid}/entries", _RestEntryHandler.GetEntriesAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/tenants/{tenantId}/accounts/{accountGuid}/entries/pending", _RestEntryHandler.GetPendingEntriesAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/tenants/{tenantId}/accounts/{accountGuid}/entries/pending/credits", _RestEntryHandler.GetPendingCreditsAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/tenants/{tenantId}/accounts/{accountGuid}/entries/pending/debits", _RestEntryHandler.GetPendingDebitsAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1/tenants/{tenantId}/accounts/{accountGuid}/entries/enumerate", _RestEntryHandler.EnumerateAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1/tenants/{tenantId}/accounts/{accountGuid}/credits", _RestEntryHandler.AddCreditsAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1/tenants/{tenantId}/accounts/{accountGuid}/debits", _RestEntryHandler.AddDebitsAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1/tenants/{tenantId}/accounts/{accountGuid}/entries/{entryGuid}", _RestEntryHandler.CancelEntryAsync, ExceptionHandler);

            // Balance and commit endpoints
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/accounts/{accountGuid}/balance", _RestBalanceHandler.GetBalanceAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/accounts/{accountGuid}/balance/asof", _RestBalanceHandler.GetBalanceAsOfAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/balances", _RestBalanceHandler.GetAllBalancesAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1/accounts/{accountGuid}/commit", _RestBalanceHandler.CommitAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/accounts/{accountGuid}/verify", _RestBalanceHandler.VerifyBalanceChainAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/tenants/{tenantId}/accounts/{accountGuid}/balance", _RestBalanceHandler.GetBalanceAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/tenants/{tenantId}/accounts/{accountGuid}/balance/asof", _RestBalanceHandler.GetBalanceAsOfAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, "/v1/tenants/{tenantId}/accounts/{accountGuid}/commit", _RestBalanceHandler.CommitAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/tenants/{tenantId}/accounts/{accountGuid}/verify", _RestBalanceHandler.VerifyBalanceChainAsync, ExceptionHandler);

            // Credential management endpoints
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/credentials", _RestApiKeyHandler.EnumerateAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1/credentials", _RestApiKeyHandler.CreateAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1/credentials/{credentialId}", _RestApiKeyHandler.RevokeAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/tenants/{tenantId}/credentials", _RestApiKeyHandler.EnumerateAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, "/v1/tenants/{tenantId}/credentials", _RestApiKeyHandler.CreateAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1/tenants/{tenantId}/credentials/{credentialId}", _RestApiKeyHandler.RevokeAsync, ExceptionHandler);

            // Request history endpoints
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/request-history", _RestRequestHistoryHandler.EnumerateAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/request-history/summary", _RestRequestHistoryHandler.SummarizeAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1/request-history", _RestRequestHistoryHandler.DeleteManyAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1/request-history/{id}", _RestRequestHistoryHandler.ReadAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1/request-history/{id}", _RestRequestHistoryHandler.DeleteAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/request-history", _RestRequestHistoryHandler.EnumerateAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/request-history/summary", _RestRequestHistoryHandler.SummarizeAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/api/request-history", _RestRequestHistoryHandler.DeleteManyAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/v1.0/api/request-history/{id}", _RestRequestHistoryHandler.ReadAsync, ExceptionHandler);
            _Webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, "/v1.0/api/request-history/{id}", _RestRequestHistoryHandler.DeleteAsync, ExceptionHandler);

            _Logging.Debug(_Header + "routes registered");
        }

        private static async Task PreRoutingHandler(HttpContextBase ctx)
        {
            ctx.Response.Headers.Add(Constants.HostnameHeader, _Hostname);
            ctx.Response.Headers.Add(Constants.ApiVersionHeader, Constants.CurrentApiVersion);
            ctx.Response.ContentType = Constants.JsonContentType;

            // Authenticate requests to PostAuthentication routes
            string path = ctx.Request.Url.RawWithoutQuery;
            bool preAuthRoute =
                path.Equals("/", StringComparison.Ordinal) ||
                path.Equals("/openapi.json", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/v1/auth/tenants", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/v1/auth/login", StringComparison.OrdinalIgnoreCase);

            if (!preAuthRoute)
            {
                AuthContext auth = await _AuthService.AuthenticateAsync(ctx).ConfigureAwait(false);
                if (!auth.IsAuthenticated)
                {
                    _Logging.Warn(_Header + "authentication failed from " + ctx.Request.Source.IpAddress + ": " + auth.Result);
                    ctx.Response.StatusCode = 401;
                    await ctx.Response.Send(JsonSerializer.Serialize(
                        new NetLedger.Server.Models.ApiErrorResponse(ApiErrorEnum.Unauthorized, null, auth.ErrorMessage),
                        Constants.JsonOptions)).ConfigureAwait(false);
                    return;
                }
            }
        }

        private static async Task PostRoutingHandler(HttpContextBase ctx)
        {
            if (_Settings.Logging.LogRequests)
            {
                double? totalMs = ctx.Timestamp.TotalMs;
                string timing = totalMs.HasValue ? $" ({totalMs.Value:F2}ms)" : "";
                _Logging.Debug(_Header +
                    ctx.Request.Method + " " +
                    ctx.Request.Url.RawWithQuery + " " +
                    ctx.Response.StatusCode +
                    timing);
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        private static async Task OpenApiAsync(HttpContextBase ctx)
        {
            Dictionary<string, object> paths = new Dictionary<string, object>(StringComparer.Ordinal);
            List<OpenApiRouteDescriptor> routes = BuildOpenApiRoutes();

            foreach (OpenApiRouteDescriptor route in routes)
            {
                if (!paths.ContainsKey(route.Path))
                {
                    paths[route.Path] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                }

                Dictionary<string, object> pathItem = (Dictionary<string, object>)paths[route.Path];
                pathItem[route.Method.ToLowerInvariant()] = new Dictionary<string, object>
                {
                    ["summary"] = route.Summary,
                    ["tags"] = new List<string> { route.Tag },
                    ["parameters"] = BuildOpenApiParameters(route.Path, route.Method),
                    ["responses"] = new Dictionary<string, object>
                    {
                        ["200"] = new Dictionary<string, object>
                        {
                            ["description"] = "Success"
                        },
                        ["400"] = new Dictionary<string, object>
                        {
                            ["description"] = "Bad request"
                        },
                        ["401"] = new Dictionary<string, object>
                        {
                            ["description"] = "Unauthorized"
                        },
                        ["403"] = new Dictionary<string, object>
                        {
                            ["description"] = "Forbidden"
                        }
                    }
                };
            }

            Dictionary<string, object> document = new Dictionary<string, object>
            {
                ["openapi"] = "3.0.3",
                ["info"] = new Dictionary<string, object>
                {
                    ["title"] = "NetLedger API",
                    ["version"] = Constants.CurrentApiVersion
                },
                ["paths"] = paths
            };

            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = Constants.JsonContentType;
            await ctx.Response.Send(JsonSerializer.Serialize(document, Constants.JsonOptions)).ConfigureAwait(false);
        }

        private static List<OpenApiRouteDescriptor> BuildOpenApiRoutes()
        {
            List<OpenApiRouteDescriptor> routes = new List<OpenApiRouteDescriptor>();
            AddRoute(routes, "GET", "/", "Get service information", "Service");
            AddRoute(routes, "POST", "/v1/auth/tenants", "Discover tenants for an email address", "Identity");
            AddRoute(routes, "POST", "/v1/auth/login", "Authenticate a user", "Identity");
            AddRoute(routes, "POST", "/v1/auth/logout", "Logout the current session", "Identity");
            AddRoute(routes, "GET", "/v1/me/permissions", "Get effective permissions", "Identity");
            AddRoute(routes, "GET", "/v1/tenants", "List tenants", "Tenants");
            AddRoute(routes, "PUT", "/v1/tenants", "Create tenant", "Tenants");
            AddRoute(routes, "GET", "/v1/tenants/{tenantId}", "Read tenant", "Tenants");
            AddRoute(routes, "DELETE", "/v1/tenants/{tenantId}", "Delete tenant", "Tenants");
            AddRoute(routes, "GET", "/v1/tenants/{tenantId}/users", "List users", "Users");
            AddRoute(routes, "PUT", "/v1/tenants/{tenantId}/users", "Create user", "Users");
            AddRoute(routes, "GET", "/v1/tenants/{tenantId}/users/{userId}", "Read user", "Users");
            AddRoute(routes, "GET", "/v1/tenants/{tenantId}/sessions", "List sessions", "Security");
            AddRoute(routes, "DELETE", "/v1/tenants/{tenantId}/sessions/{sessionId}", "Revoke session", "Security");
            AddRoute(routes, "GET", "/v1/tenants/{tenantId}/audit", "List audit records", "Security");
            AddRoute(routes, "GET", "/v1/tenants/{tenantId}/roles", "List roles", "Security");
            AddRoute(routes, "PUT", "/v1/tenants/{tenantId}/roles", "Create role", "Security");
            AddRoute(routes, "GET", "/v1/tenants/{tenantId}/permissions", "List permissions", "Security");
            AddRoute(routes, "PUT", "/v1/tenants/{tenantId}/permissions", "Create permission", "Security");
            AddRoute(routes, "GET", "/v1/accounts", "List accounts", "Accounts");
            AddRoute(routes, "PUT", "/v1/accounts", "Create account", "Accounts");
            AddRoute(routes, "GET", "/v1/accounts/{accountGuid}", "Read account", "Accounts");
            AddRoute(routes, "DELETE", "/v1/accounts/{accountGuid}", "Delete account", "Accounts");
            AddRoute(routes, "GET", "/v1/accounts/byname/{accountName}", "Read account by name", "Accounts");
            AddRoute(routes, "GET", "/v1/accounts/{accountGuid}/entries", "List entries", "Entries");
            AddRoute(routes, "GET", "/v1/accounts/{accountGuid}/entries/pending", "List pending entries", "Entries");
            AddRoute(routes, "POST", "/v1/accounts/{accountGuid}/entries/enumerate", "Enumerate entries", "Entries");
            AddRoute(routes, "PUT", "/v1/accounts/{accountGuid}/credits", "Add credits", "Entries");
            AddRoute(routes, "PUT", "/v1/accounts/{accountGuid}/debits", "Add debits", "Entries");
            AddRoute(routes, "DELETE", "/v1/accounts/{accountGuid}/entries/{entryGuid}", "Cancel entry", "Entries");
            AddRoute(routes, "GET", "/v1/accounts/{accountGuid}/balance", "Get account balance", "Balances");
            AddRoute(routes, "GET", "/v1/accounts/{accountGuid}/balance/asof", "Get account balance as of time", "Balances");
            AddRoute(routes, "GET", "/v1/balances", "List balances", "Balances");
            AddRoute(routes, "POST", "/v1/accounts/{accountGuid}/commit", "Commit pending entries", "Balances");
            AddRoute(routes, "GET", "/v1/accounts/{accountGuid}/verify", "Verify balance chain", "Balances");
            AddRoute(routes, "GET", "/v1/credentials", "List credentials", "Credentials");
            AddRoute(routes, "PUT", "/v1/credentials", "Create credential", "Credentials");
            AddRoute(routes, "DELETE", "/v1/credentials/{credentialId}", "Revoke credential", "Credentials");
            AddRoute(routes, "GET", "/v1/request-history", "List request history", "Request History");
            AddRoute(routes, "GET", "/v1/request-history/summary", "Summarize request history", "Request History");
            AddRoute(routes, "DELETE", "/v1/request-history", "Delete matching request history", "Request History");
            AddRoute(routes, "GET", "/v1/request-history/{id}", "Read request history entry", "Request History");
            AddRoute(routes, "DELETE", "/v1/request-history/{id}", "Delete request history entry", "Request History");
            AddRoute(routes, "GET", "/v1.0/api/request-history", "List request history", "Request History");
            AddRoute(routes, "GET", "/v1.0/api/request-history/summary", "Summarize request history", "Request History");
            AddRoute(routes, "DELETE", "/v1.0/api/request-history", "Delete matching request history", "Request History");
            AddRoute(routes, "GET", "/v1.0/api/request-history/{id}", "Read request history entry", "Request History");
            AddRoute(routes, "DELETE", "/v1.0/api/request-history/{id}", "Delete request history entry", "Request History");
            return routes;
        }

        private static void AddRoute(List<OpenApiRouteDescriptor> routes, string method, string path, string summary, string tag)
        {
            routes.Add(new OpenApiRouteDescriptor
            {
                Method = method,
                Path = path,
                Summary = summary,
                Tag = tag
            });
        }

        private static List<Dictionary<string, object>> BuildOpenApiParameters(string path, string method)
        {
            List<Dictionary<string, object>> parameters = new List<Dictionary<string, object>>();
            string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                if (part.StartsWith("{", StringComparison.Ordinal) && part.EndsWith("}", StringComparison.Ordinal))
                {
                    parameters.Add(new Dictionary<string, object>
                    {
                        ["name"] = part.Substring(1, part.Length - 2),
                        ["in"] = "path",
                        ["required"] = true,
                        ["schema"] = new Dictionary<string, object> { ["type"] = "string" }
                    });
                }
            }

            if (method.Equals("GET", StringComparison.OrdinalIgnoreCase) || path.Contains("request-history", StringComparison.OrdinalIgnoreCase))
            {
                AddQueryParameter(parameters, "tenantId", "string");
                AddQueryParameter(parameters, "maxResults", "integer");
                AddQueryParameter(parameters, "skip", "integer");
            }

            if (path.Contains("request-history", StringComparison.OrdinalIgnoreCase))
            {
                AddQueryParameter(parameters, "principalId", "string");
                AddQueryParameter(parameters, "method", "string");
                AddQueryParameter(parameters, "statusCode", "integer");
                AddQueryParameter(parameters, "pathContains", "string");
                AddQueryParameter(parameters, "fromUtc", "string");
                AddQueryParameter(parameters, "toUtc", "string");
                AddQueryParameter(parameters, "bucketMinutes", "integer");
            }

            return parameters;
        }

        private static void AddQueryParameter(List<Dictionary<string, object>> parameters, string name, string type)
        {
            parameters.Add(new Dictionary<string, object>
            {
                ["name"] = name,
                ["in"] = "query",
                ["required"] = false,
                ["schema"] = new Dictionary<string, object> { ["type"] = type }
            });
        }

        private static async Task DefaultRoute(HttpContextBase ctx)
        {
            _Logging.Warn(_Header + "default route: " + ctx.Request.Method + " " + ctx.Request.Url.RawWithQuery);
            ctx.Response.StatusCode = 404;
            await ctx.Response.Send(JsonSerializer.Serialize(
                new NetLedger.Server.Models.ApiErrorResponse(ApiErrorEnum.NotFound, null, "Route not found"),
                Constants.JsonOptions)).ConfigureAwait(false);
        }

        private static async Task ExceptionHandler(HttpContextBase ctx, Exception e)
        {
            ApiErrorEnum errorCode;
            int statusCode;

            switch (e)
            {
                case ArgumentOutOfRangeException:
                case ArgumentNullException:
                case ArgumentException:
                case FormatException:
                case JsonException:
                    _Logging.Warn(_Header + "argument/format exception: " + e.Message);
                    errorCode = ApiErrorEnum.BadRequest;
                    statusCode = 400;
                    break;

                case FileNotFoundException:
                case KeyNotFoundException:
                    _Logging.Warn(_Header + "not found exception: " + e.Message);
                    errorCode = ApiErrorEnum.NotFound;
                    statusCode = 404;
                    break;

                case InvalidOperationException:
                    _Logging.Warn(_Header + "invalid operation exception: " + e.Message);
                    errorCode = ApiErrorEnum.Conflict;
                    statusCode = 409;
                    break;

                case TaskCanceledException:
                case OperationCanceledException:
                    _Logging.Warn(_Header + "cancellation exception: " + e.Message);
                    errorCode = ApiErrorEnum.Timeout;
                    statusCode = 408;
                    break;

                default:
                    _Logging.Alert(_Header + "exception: " + e.ToString());
                    errorCode = ApiErrorEnum.InternalError;
                    statusCode = 500;
                    break;
            }

            ctx.Response.StatusCode = statusCode;
            await ctx.Response.Send(JsonSerializer.Serialize(
                new NetLedger.Server.Models.ApiErrorResponse(errorCode, null, e.Message),
                Constants.JsonOptions)).ConfigureAwait(false);
        }

        private static void WebserverException(object? sender, ExceptionEventArgs e)
        {
            _Logging.Alert(_Header + "webserver exception in " + e.Method + " " + e.Url + " from " + e.Ip + ": " + e.Exception.ToString());
        }

        private static void LogDatabaseConfiguration()
        {
            Database.DatabaseSettings db = _Settings.Database;

            if (db.Type == Database.DatabaseTypeEnum.Sqlite)
            {
                _Logging.Info(_Header + "database: " + db.Type + " file=" + db.Filename);
            }
            else
            {
                string connInfo = db.Type.ToString() +
                    " host=" + db.Hostname +
                    " port=" + db.GetEffectivePort() +
                    " database=" + db.DatabaseName;

                if (!string.IsNullOrEmpty(db.Username))
                {
                    connInfo += " user=" + db.Username;
                }

                if (db.Type == Database.DatabaseTypeEnum.Postgresql && !string.IsNullOrEmpty(db.Schema))
                {
                    connInfo += " schema=" + db.Schema;
                }

                if (db.Type == Database.DatabaseTypeEnum.SqlServer && !string.IsNullOrEmpty(db.Instance))
                {
                    connInfo += " instance=" + db.Instance;
                }

                if (db.RequireEncryption)
                {
                    connInfo += " encryption=required";
                }

                _Logging.Info(_Header + "database: " + connInfo);
            }
        }

        private static async Task CleanupAsync()
        {
            if (_Webserver != null)
            {
                _Webserver.Stop();
                _Logging.Debug(_Header + "webserver stopped");
            }

            if (_AuthService != null)
            {
                _AuthService.Dispose();
                _Logging.Debug(_Header + "auth service disposed");
            }

            if (_Ledger != null)
            {
                await _Ledger.DisposeAsync().ConfigureAwait(false);
                _Logging.Debug(_Header + "ledger disposed");
            }
        }
    }
}




