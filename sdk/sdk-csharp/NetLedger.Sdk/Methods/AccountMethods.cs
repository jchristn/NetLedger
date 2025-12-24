namespace NetLedger.Sdk.Methods
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;
    using NetLedger.Sdk.Interfaces;

    /// <summary>
    /// Implementation of account management operations for the NetLedger API.
    /// </summary>
    internal class AccountMethods : IAccountMethods
    {
        #region Private-Members

        private readonly NetLedgerClient _Client;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate account methods.
        /// </summary>
        /// <param name="client">The NetLedger client.</param>
        internal AccountMethods(NetLedgerClient client)
        {
            _Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<Account> CreateAsync(string name, string? notes = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name), "Account name cannot be null or empty.");

            Account account = new Account(name, notes);
            return await CreateAsync(account, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<Account> CreateAsync(Account account, CancellationToken cancellationToken = default)
        {
            if (account == null)
                throw new ArgumentNullException(nameof(account));

            ApiResponse<Account> response = await _Client.SendAsync<Account>(
                HttpMethod.Put,
                "/v1/accounts",
                account,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? throw new NetLedgerApiException(response.StatusCode, "No data returned from server.");
        }

        /// <inheritdoc />
        public async Task<Account> GetAsync(Guid accountGuid, CancellationToken cancellationToken = default)
        {
            ApiResponse<Account> response = await _Client.SendAsync<Account>(
                HttpMethod.Get,
                $"/v1/accounts/{accountGuid}",
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? throw new NetLedgerApiException(response.StatusCode, "No data returned from server.");
        }

        /// <inheritdoc />
        public async Task<Account> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name), "Account name cannot be null or empty.");

            string encodedName = HttpUtility.UrlEncode(name);
            ApiResponse<Account> response = await _Client.SendAsync<Account>(
                HttpMethod.Get,
                $"/v1/accounts/byname/{encodedName}",
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? throw new NetLedgerApiException(response.StatusCode, "No data returned from server.");
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(Guid accountGuid, CancellationToken cancellationToken = default)
        {
            return await _Client.HeadAsync($"/v1/accounts/{accountGuid}", cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task DeleteAsync(Guid accountGuid, CancellationToken cancellationToken = default)
        {
            await _Client.SendAsync<object>(
                HttpMethod.Delete,
                $"/v1/accounts/{accountGuid}",
                null,
                cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<Account>> EnumerateAsync(AccountEnumerationQuery? query = null, CancellationToken cancellationToken = default)
        {
            query ??= new AccountEnumerationQuery();

            string path = $"/v1/accounts?maxResults={query.MaxResults}&skip={query.Skip}";
            if (!string.IsNullOrWhiteSpace(query.SearchTerm))
            {
                path += $"&searchTerm={HttpUtility.UrlEncode(query.SearchTerm)}";
            }

            ApiResponse<EnumerationResult<Account>> response = await _Client.SendAsync<EnumerationResult<Account>>(
                HttpMethod.Get,
                path,
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new EnumerationResult<Account>();
        }

        #endregion
    }
}
