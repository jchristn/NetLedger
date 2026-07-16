namespace NetLedger.Sdk.Methods
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;
    using NetLedger.Sdk.Interfaces;

    /// <summary>
    /// Implementation of balance operations for the NetLedger API.
    /// </summary>
    internal class BalanceMethods : IBalanceMethods
    {
        #region Private-Members

        private readonly NetLedgerClient _Client;

        #endregion

        #region Private-Classes

        private class HistoricalBalanceResponse
        {
            public string AccountId { get; set; } = string.Empty;
            public DateTime AsOfUtc { get; set; }
            public decimal Balance { get; set; }
        }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate balance methods.
        /// </summary>
        /// <param name="client">The NetLedger client.</param>
        internal BalanceMethods(NetLedgerClient client)
        {
            _Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<Balance> GetAsync(string accountId, CancellationToken cancellationToken = default)
        {
            ApiResponse<Balance> response = await _Client.SendAsync<Balance>(
                HttpMethod.Get,
                $"/v1/accounts/{accountId}/balance",
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? throw new NetLedgerApiException(response.StatusCode, "No data returned from server.");
        }

        /// <inheritdoc />
        public async Task<Balance> GetAsOfAsync(string accountId, DateTime asOfUtc, CancellationToken cancellationToken = default)
        {
            string timestamp = asOfUtc.ToUniversalTime().ToString("o");
            ApiResponse<HistoricalBalanceResponse> response = await _Client.SendAsync<HistoricalBalanceResponse>(
                HttpMethod.Get,
                $"/v1/accounts/{accountId}/balance/asof?asOf={timestamp}",
                null,
                cancellationToken).ConfigureAwait(false);

            if (response.Data == null)
                throw new NetLedgerApiException(response.StatusCode, "No data returned from server.");

            return new Balance
            {
                AccountId = response.Data.AccountId,
                CommittedBalance = response.Data.Balance,
                PendingBalance = response.Data.Balance
            };
        }

        /// <inheritdoc />
        public async Task<List<Balance>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            ApiResponse<Dictionary<string, Balance>> response = await _Client.SendAsync<Dictionary<string, Balance>>(
                HttpMethod.Get,
                "/v1/balances",
                null,
                cancellationToken).ConfigureAwait(false);

            if (response.Data == null)
                return new List<Balance>();

            return new List<Balance>(response.Data.Values);
        }

        /// <inheritdoc />
        public async Task<CommitResult> CommitAsync(string accountId, CancellationToken cancellationToken = default)
        {
            ApiResponse<CommitResult> response = await _Client.SendAsync<CommitResult>(
                HttpMethod.Post,
                $"/v1/accounts/{accountId}/commit",
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new CommitResult();
        }

        /// <inheritdoc />
        public async Task<CommitResult> CommitAsync(string accountId, List<string> entryIds, CancellationToken cancellationToken = default)
        {
            if (entryIds == null)
                throw new ArgumentNullException(nameof(entryIds));

            CommitRequest request = new CommitRequest(entryIds);
            ApiResponse<CommitResult> response = await _Client.SendAsync<CommitResult>(
                HttpMethod.Post,
                $"/v1/accounts/{accountId}/commit",
                request,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new CommitResult();
        }

        /// <inheritdoc />
        public async Task<bool> VerifyAsync(string accountId, CancellationToken cancellationToken = default)
        {
            try
            {
                ApiResponse<object> response = await _Client.SendAsync<object>(
                    HttpMethod.Get,
                    $"/v1/accounts/{accountId}/verify",
                    null,
                    cancellationToken).ConfigureAwait(false);

                return response.StatusCode == 200;
            }
            catch (NetLedgerApiException ex) when (ex.StatusCode == 409)
            {
                return false;
            }
        }

        #endregion
    }
}

