namespace NetLedger.Sdk
{
    using System;
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
    /// using var client = new NetLedgerClient("http://localhost:8080", "your-api-key");
    ///
    /// // Check service health
    /// bool isHealthy = await client.Service.HealthCheckAsync();
    ///
    /// // Create an account
    /// Account account = await client.Account.CreateAsync("My Account");
    ///
    /// // Add a credit
    /// Entry credit = await client.Entry.AddCreditAsync(account.GUID, 100.00m, "Initial deposit");
    ///
    /// // Get balance
    /// Balance balance = await client.Balance.GetAsync(account.GUID);
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
        private readonly HttpClient _HttpClient;
        private readonly JsonSerializerOptions _SerializeOptions;
        private readonly JsonSerializerOptions _DeserializeOptions;
        private int _TimeoutMs = 30000;
        private bool _Disposed = false;

        private readonly IServiceMethods _Service;
        private readonly IAccountMethods _Account;
        private readonly IEntryMethods _Entry;
        private readonly IBalanceMethods _Balance;
        private readonly IApiKeyMethods _ApiKeyMethods;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a new NetLedger client.
        /// </summary>
        /// <param name="baseUrl">The base URL of the NetLedger server (e.g., "http://localhost:8080").</param>
        /// <param name="apiKey">The API key for authentication.</param>
        /// <exception cref="ArgumentNullException">Thrown when baseUrl or apiKey is null or empty.</exception>
        public NetLedgerClient(string baseUrl, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new ArgumentNullException(nameof(baseUrl), "Base URL cannot be null or empty.");
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentNullException(nameof(apiKey), "API key cannot be null or empty.");

            _BaseUrl = baseUrl.TrimEnd('/');
            _ApiKey = apiKey;

            _HttpClient = new HttpClient();
            _HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _ApiKey);
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
                using HttpResponseMessage response = await _HttpClient.SendAsync(request, cts.Token).ConfigureAwait(false);

                string responseBody = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
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

                if (string.IsNullOrWhiteSpace(responseBody))
                {
                    return new ApiResponse<T>(default, (int)response.StatusCode);
                }

                T? data = JsonSerializer.Deserialize<T>(responseBody, _DeserializeOptions);
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
                    _HttpClient?.Dispose();
                }

                _Disposed = true;
            }
        }

        #endregion
    }
}
