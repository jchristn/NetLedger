namespace NetLedger.Sdk
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Sdk.Interfaces;
    using NetLedger.Sdk.Methods;

    /// <summary>
    /// Client for interacting with the NetLedger Server REST API.
    /// Provides access to account, entry, balance, and API key management operations.
    /// </summary>
    /// <remarks>
    /// <para>This client is thread-safe and can be reused across multiple operations.</para>
    /// <para>The client uses the Bearer token authentication scheme.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// using NetLedgerClient client = new NetLedgerClient("http://localhost:8080", "your-api-key");
    ///
    /// // Check service health
    /// bool isHealthy = await client.Service.HealthCheckAsync();
    ///
    /// // Create an account
    /// Account account = await client.Account.CreateAsync("My Account");
    ///
    /// // Add a credit
    /// Entry credit = await client.Entry.AddCreditAsync(account.Id, 100.00m, "Initial deposit");
    ///
    /// // Get balance
    /// Balance balance = await client.Balance.GetAsync(account.Id);
    /// </code>
    /// </example>
    public class NetLedgerClient : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Service operations including health checks and service information.
        /// </summary>
        public IServiceMethods Service
        {
            get { return _Service; }
        }

        /// <summary>
        /// Account management operations including create, read, delete, and enumeration.
        /// </summary>
        public IAccountMethods Account
        {
            get { return _Account; }
        }

        /// <summary>
        /// Entry operations including adding credits and debits, enumeration, and cancellation.
        /// </summary>
        public IEntryMethods Entry
        {
            get { return _Entry; }
        }

        /// <summary>
        /// Balance operations including retrieving balances, committing entries, and verification.
        /// </summary>
        public IBalanceMethods Balance
        {
            get { return _Balance; }
        }

        /// <summary>
        /// API key management operations including create, enumerate, and revoke.
        /// </summary>
        public IApiKeyMethods ApiKey
        {
            get { return _ApiKeyMethods; }
        }

        /// <summary>
        /// Identity and security administration operations.
        /// </summary>
        public IIdentityMethods Identity
        {
            get { return _IdentityMethods; }
        }

        /// <summary>
        /// Request history operations.
        /// </summary>
        public IRequestHistoryMethods RequestHistory
        {
            get { return _RequestHistoryMethods; }
        }

        /// <summary>
        /// Archive Server cold data and metadata operations.
        /// </summary>
        public IArchiveMethods Archive
        {
            get { return _ArchiveMethods; }
        }

        /// <summary>
        /// The base URL of the NetLedger server.
        /// </summary>
        public string BaseUrl
        {
            get { return _BaseUrl; }
        }

        /// <summary>
        /// Request timeout in milliseconds.
        /// Default is 30000 (30 seconds). Minimum is 1000 (1 second). Maximum is 300000 (5 minutes).
        /// </summary>
        public int TimeoutMs
        {
            get { return _TimeoutMs; }
            set
            {
                if (value < 1000) _TimeoutMs = 1000;
                else if (value > 300000) _TimeoutMs = 300000;
                else _TimeoutMs = value;
            }
        }

        #endregion

        #region Private-Members

        private readonly string _BaseUrl;
        private readonly string _ApiKey;
        private readonly string? _TenantId;
        private readonly HttpClient _HttpClient;
        private readonly bool _OwnsHttpClient;
        private readonly JsonSerializerOptions _SerializeOptions;
        private readonly JsonSerializerOptions _DeserializeOptions;
        private int _TimeoutMs = 30000;
        private bool _Disposed = false;

        private readonly IServiceMethods _Service;
        private readonly IAccountMethods _Account;
        private readonly IEntryMethods _Entry;
        private readonly IBalanceMethods _Balance;
        private readonly IApiKeyMethods _ApiKeyMethods;
        private readonly IIdentityMethods _IdentityMethods;
        private readonly IRequestHistoryMethods _RequestHistoryMethods;
        private readonly IArchiveMethods _ArchiveMethods;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a new NetLedger client.
        /// </summary>
        /// <param name="baseUrl">The base URL of the NetLedger server (e.g., "http://localhost:8080").</param>
        /// <param name="apiKey">The API key for authentication.</param>
        /// <exception cref="ArgumentNullException">Thrown when baseUrl or apiKey is null or empty.</exception>
        public NetLedgerClient(string baseUrl, string apiKey)
            : this(baseUrl, apiKey, null)
        {
        }

        /// <summary>
        /// Instantiate a new tenant-scoped NetLedger client.
        /// </summary>
        /// <param name="baseUrl">The base URL of the NetLedger server.</param>
        /// <param name="apiKey">The API key or credential access key for authentication.</param>
        /// <param name="tenantId">Tenant identifier for x-tenant-id.</param>
        /// <exception cref="ArgumentNullException">Thrown when baseUrl or apiKey is null or empty.</exception>
        public NetLedgerClient(string baseUrl, string apiKey, string? tenantId)
            : this(baseUrl, apiKey, tenantId, new HttpClient(), true)
        {
        }

        /// <summary>
        /// Instantiate a new tenant-scoped NetLedger client using an externally-managed HTTP client.
        /// </summary>
        /// <param name="baseUrl">The base URL of the NetLedger server.</param>
        /// <param name="apiKey">The API key or credential access key for authentication.</param>
        /// <param name="tenantId">Tenant identifier for x-tenant-id.</param>
        /// <param name="httpClient">Externally-managed HTTP client.</param>
        /// <exception cref="ArgumentNullException">Thrown when baseUrl, apiKey, or httpClient is null or empty.</exception>
        public NetLedgerClient(string baseUrl, string apiKey, string? tenantId, HttpClient httpClient)
            : this(baseUrl, apiKey, tenantId, httpClient, false)
        {
        }

        private NetLedgerClient(string baseUrl, string apiKey, string? tenantId, HttpClient httpClient, bool ownsHttpClient)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new ArgumentNullException(nameof(baseUrl), "Base URL cannot be null or empty.");
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentNullException(nameof(apiKey), "API key cannot be null or empty.");
            if (httpClient == null)
                throw new ArgumentNullException(nameof(httpClient));

            _BaseUrl = baseUrl.TrimEnd('/');
            _ApiKey = apiKey;
            _TenantId = tenantId;

            _HttpClient = httpClient;
            _OwnsHttpClient = ownsHttpClient;
            _HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _ApiKey);
            if (!String.IsNullOrEmpty(_TenantId))
            {
                _HttpClient.DefaultRequestHeaders.Add("x-tenant-id", _TenantId);
            }
            _HttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            _SerializeOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = false
            };

            _DeserializeOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = false,
                Converters = { new JsonStringEnumConverter() }
            };

            _Service = new ServiceMethods(this);
            _Account = new AccountMethods(this);
            _Entry = new EntryMethods(this);
            _Balance = new BalanceMethods(this);
            _ApiKeyMethods = new ApiKeyMethods(this);
            _IdentityMethods = new IdentityMethods(this);
            _RequestHistoryMethods = new RequestHistoryMethods(this);
            _ArchiveMethods = new ArchiveMethods(this);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Releases all resources used by the NetLedgerClient.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Internal-Methods

        /// <summary>
        /// Send an HTTP request to the server.
        /// </summary>
        internal async Task<ApiResponse<T>> SendAsync<T>(
            HttpMethod method,
            string path,
            object? body = null,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_Disposed, nameof(NetLedgerClient));

            string url = $"{_BaseUrl}{path}";

            using HttpRequestMessage request = new HttpRequestMessage(method, url);

            if (body != null)
            {
                string json = JsonSerializer.Serialize(body, _SerializeOptions);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_TimeoutMs);

            try
            {
                using HttpResponseMessage response = await _HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                    ErrorResponse? error = null;
                    try
                    {
                        error = JsonSerializer.Deserialize<ErrorResponse>(responseBody, _DeserializeOptions);
                    }
                    catch
                    {
                        // Failed to parse error response
                    }

                    throw new NetLedgerApiException(
                        (int)response.StatusCode,
                        error?.Message ?? response.ReasonPhrase ?? "Unknown error",
                        error?.Description);
                }

                if (response.StatusCode == HttpStatusCode.NoContent || response.Content.Headers.ContentLength == 0)
                {
                    return new ApiResponse<T>(default, (int)response.StatusCode);
                }

                await using Stream responseStream = await response.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
                T? data = await JsonSerializer.DeserializeAsync<T>(responseStream, _DeserializeOptions, cts.Token).ConfigureAwait(false);
                return new ApiResponse<T>(data, (int)response.StatusCode);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new NetLedgerConnectionException("Request timed out.", null);
            }
            catch (HttpRequestException ex)
            {
                throw new NetLedgerConnectionException("Failed to connect to the server.", ex);
            }
        }

        /// <summary>
        /// Send an HTTP request with caller-provided content.
        /// </summary>
        internal async Task<ApiResponse<T>> SendContentAsync<T>(
            HttpMethod method,
            string path,
            HttpContent content,
            Dictionary<string, string>? headers = null,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_Disposed, nameof(NetLedgerClient));
            if (content == null) throw new ArgumentNullException(nameof(content));

            string url = $"{_BaseUrl}{path}";

            using HttpRequestMessage request = new HttpRequestMessage(method, url);
            request.Content = content;

            if (headers != null)
            {
                foreach (KeyValuePair<string, string> header in headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_TimeoutMs);

            try
            {
                using HttpResponseMessage response = await _HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                    ErrorResponse? error = null;
                    try
                    {
                        error = JsonSerializer.Deserialize<ErrorResponse>(responseBody, _DeserializeOptions);
                    }
                    catch
                    {
                        // Failed to parse error response
                    }

                    throw new NetLedgerApiException(
                        (int)response.StatusCode,
                        error?.Message ?? response.ReasonPhrase ?? "Unknown error",
                        error?.Description ?? responseBody);
                }

                if (response.StatusCode == HttpStatusCode.NoContent || response.Content.Headers.ContentLength == 0)
                {
                    return new ApiResponse<T>(default, (int)response.StatusCode);
                }

                await using Stream responseStream = await response.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
                T? data = await JsonSerializer.DeserializeAsync<T>(responseStream, _DeserializeOptions, cts.Token).ConfigureAwait(false);
                return new ApiResponse<T>(data, (int)response.StatusCode);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new NetLedgerConnectionException("Request timed out.", null);
            }
            catch (HttpRequestException ex)
            {
                throw new NetLedgerConnectionException("Failed to connect to the server.", ex);
            }
        }

        /// <summary>
        /// Send an HTTP HEAD request to check if a resource exists.
        /// </summary>
        internal async Task<bool> HeadAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_Disposed, nameof(NetLedgerClient));

            string url = $"{_BaseUrl}{path}";

            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, url);

            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_TimeoutMs);

            try
            {
                using HttpResponseMessage response = await _HttpClient.SendAsync(request, cts.Token).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new NetLedgerConnectionException("Request timed out.", null);
            }
            catch (HttpRequestException ex)
            {
                throw new NetLedgerConnectionException("Failed to connect to the server.", ex);
            }
        }

        /// <summary>
        /// Send an HTTP request and return the raw response body.
        /// </summary>
        internal async Task<string> SendRawStringAsync(
            HttpMethod method,
            string path,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_Disposed, nameof(NetLedgerClient));

            string url = $"{_BaseUrl}{path}";

            using HttpRequestMessage request = new HttpRequestMessage(method, url);
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_TimeoutMs);

            try
            {
                using HttpResponseMessage response = await _HttpClient.SendAsync(request, cts.Token).ConfigureAwait(false);
                string responseBody = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new NetLedgerApiException((int)response.StatusCode, response.ReasonPhrase ?? "Unknown error", responseBody);
                }

                return responseBody;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new NetLedgerConnectionException("Request timed out.", null);
            }
            catch (HttpRequestException ex)
            {
                throw new NetLedgerConnectionException("Failed to connect to the server.", ex);
            }
        }

        /// <summary>
        /// Get the JSON deserializer options.
        /// </summary>
        internal JsonSerializerOptions GetJsonOptions()
        {
            return _DeserializeOptions;
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// Releases the unmanaged resources and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_Disposed)
            {
                if (disposing)
                {
                    if (_OwnsHttpClient)
                    {
                        _HttpClient?.Dispose();
                    }
                }

                _Disposed = true;
            }
        }

        #endregion
    }
}

