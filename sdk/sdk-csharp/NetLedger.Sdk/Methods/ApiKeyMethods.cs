namespace NetLedger.Sdk.Methods
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Sdk.Interfaces;

    /// <summary>
    /// Implementation of API key management operations for the NetLedger API.
    /// </summary>
    internal class ApiKeyMethods : IApiKeyMethods
    {
        #region Private-Members

        private readonly NetLedgerClient _Client;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate API key methods.
        /// </summary>
        /// <param name="client">The NetLedger client.</param>
        internal ApiKeyMethods(NetLedgerClient client)
        {
            _Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<ApiKeyInfo> CreateAsync(string name, bool isAdmin = false, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name), "API key name cannot be null or empty.");

            ApiKeyInfo apiKey = new ApiKeyInfo(name, isAdmin);
            return await CreateAsync(apiKey, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<ApiKeyInfo> CreateAsync(ApiKeyInfo apiKey, CancellationToken cancellationToken = default)
        {
            if (apiKey == null)
                throw new ArgumentNullException(nameof(apiKey));

            ApiResponse<ApiKeyInfo> response = await _Client.SendAsync<ApiKeyInfo>(
                HttpMethod.Put,
                "/v1/apikeys",
                apiKey,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? throw new NetLedgerApiException(response.StatusCode, "No data returned from server.");
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<ApiKeyInfo>> EnumerateAsync(ApiKeyEnumerationQuery? query = null, CancellationToken cancellationToken = default)
        {
            query ??= new ApiKeyEnumerationQuery();

            string path = $"/v1/apikeys?maxResults={query.MaxResults}&skip={query.Skip}";

            ApiResponse<EnumerationResult<ApiKeyInfo>> response = await _Client.SendAsync<EnumerationResult<ApiKeyInfo>>(
                HttpMethod.Get,
                path,
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new EnumerationResult<ApiKeyInfo>();
        }

        /// <inheritdoc />
        public async Task RevokeAsync(Guid apiKeyGuid, CancellationToken cancellationToken = default)
        {
            await _Client.SendAsync<object>(
                HttpMethod.Delete,
                $"/v1/apikeys/{apiKeyGuid}",
                null,
                cancellationToken).ConfigureAwait(false);
        }

        #endregion
    }
}
