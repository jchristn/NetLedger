namespace NetLedger.Sdk.Methods
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Sdk.Interfaces;

    /// <summary>
    /// Implementation of entry operations for the NetLedger API.
    /// </summary>
    internal class EntryMethods : IEntryMethods
    {
        #region Private-Members

        private readonly NetLedgerClient _Client;

        #endregion

        #region Private-Classes

        private class AddEntriesResponse
        {
            public List<string>? EntryIds { get; set; }

            public List<string> GetIds()
            {
                return EntryIds ?? new List<string>();
            }
        }

        private class AddEntriesRequest
        {
            public decimal? Amount { get; set; }
            public string? Notes { get; set; }
            public List<string>? Labels { get; set; }
            public Dictionary<string, string>? Tags { get; set; }
            public List<EntryItem>? Entries { get; set; }
        }

        private class EntryItem
        {
            public decimal Amount { get; set; }
            public string? Notes { get; set; }
            public List<string>? Labels { get; set; }
            public Dictionary<string, string>? Tags { get; set; }
        }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate entry methods.
        /// </summary>
        /// <param name="client">The NetLedger client.</param>
        internal EntryMethods(NetLedgerClient client)
        {
            _Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<Entry> AddCreditAsync(string accountId, decimal amount, string? description = null, CancellationToken cancellationToken = default)
        {
            if (amount <= 0)
                throw new NetLedgerValidationException("Amount must be greater than zero.", nameof(amount));

            AddEntriesRequest request = new AddEntriesRequest
            {
                Amount = amount,
                Notes = description
            };

            ApiResponse<AddEntriesResponse> response = await _Client.SendAsync<AddEntriesResponse>(
                HttpMethod.Put,
                $"/v1/accounts/{accountId}/credits",
                request,
                cancellationToken).ConfigureAwait(false);

            List<string> responseIds = response.Data?.GetIds() ?? new List<string>();
            if (responseIds.Count == 0)
                throw new NetLedgerApiException(response.StatusCode, "No entry Id returned from server.");

            return new Entry
            {
                Id = responseIds[0],
                AccountId = accountId,
                Type = EntryType.Credit,
                Amount = amount,
                Description = description,
                IsCommitted = false,
                CreatedUtc = DateTime.UtcNow
            };
        }

        /// <inheritdoc />
        public async Task<List<Entry>> AddCreditsAsync(string accountId, List<EntryInput> entries, CancellationToken cancellationToken = default)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            if (entries.Any(e => e.Amount <= 0))
                throw new NetLedgerValidationException("All amounts must be greater than zero.", nameof(entries));

            AddEntriesRequest request = new AddEntriesRequest
            {
                Entries = entries.Select(e => new EntryItem { Amount = e.Amount, Notes = e.Notes, Labels = e.Labels, Tags = e.Tags }).ToList()
            };

            ApiResponse<AddEntriesResponse> response = await _Client.SendAsync<AddEntriesResponse>(
                HttpMethod.Put,
                $"/v1/accounts/{accountId}/credits",
                request,
                cancellationToken).ConfigureAwait(false);

            List<Entry> result = new List<Entry>();
            List<string> responseIds = response.Data?.GetIds() ?? new List<string>();
            if (responseIds.Count > 0)
            {
                for (int i = 0; i < responseIds.Count && i < entries.Count; i++)
                {
                    result.Add(new Entry
                    {
                        Id = responseIds[i],
                        AccountId = accountId,
                        Type = EntryType.Credit,
                        Amount = entries[i].Amount,
                        Description = entries[i].Notes,
                        IsCommitted = false,
                        CreatedUtc = DateTime.UtcNow
                    });
                }
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<Entry> AddDebitAsync(string accountId, decimal amount, string? description = null, CancellationToken cancellationToken = default)
        {
            if (amount <= 0)
                throw new NetLedgerValidationException("Amount must be greater than zero.", nameof(amount));

            AddEntriesRequest request = new AddEntriesRequest
            {
                Amount = amount,
                Notes = description
            };

            ApiResponse<AddEntriesResponse> response = await _Client.SendAsync<AddEntriesResponse>(
                HttpMethod.Put,
                $"/v1/accounts/{accountId}/debits",
                request,
                cancellationToken).ConfigureAwait(false);

            List<string> responseIds = response.Data?.GetIds() ?? new List<string>();
            if (responseIds.Count == 0)
                throw new NetLedgerApiException(response.StatusCode, "No entry Id returned from server.");

            return new Entry
            {
                Id = responseIds[0],
                AccountId = accountId,
                Type = EntryType.Debit,
                Amount = amount,
                Description = description,
                IsCommitted = false,
                CreatedUtc = DateTime.UtcNow
            };
        }

        /// <inheritdoc />
        public async Task<List<Entry>> AddDebitsAsync(string accountId, List<EntryInput> entries, CancellationToken cancellationToken = default)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            if (entries.Any(e => e.Amount <= 0))
                throw new NetLedgerValidationException("All amounts must be greater than zero.", nameof(entries));

            AddEntriesRequest request = new AddEntriesRequest
            {
                Entries = entries.Select(e => new EntryItem { Amount = e.Amount, Notes = e.Notes, Labels = e.Labels, Tags = e.Tags }).ToList()
            };

            ApiResponse<AddEntriesResponse> response = await _Client.SendAsync<AddEntriesResponse>(
                HttpMethod.Put,
                $"/v1/accounts/{accountId}/debits",
                request,
                cancellationToken).ConfigureAwait(false);

            List<Entry> result = new List<Entry>();
            List<string> responseIds = response.Data?.GetIds() ?? new List<string>();
            if (responseIds.Count > 0)
            {
                for (int i = 0; i < responseIds.Count && i < entries.Count; i++)
                {
                    result.Add(new Entry
                    {
                        Id = responseIds[i],
                        AccountId = accountId,
                        Type = EntryType.Debit,
                        Amount = entries[i].Amount,
                        Description = entries[i].Notes,
                        IsCommitted = false,
                        CreatedUtc = DateTime.UtcNow
                    });
                }
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<List<Entry>> GetAllAsync(string accountId, CancellationToken cancellationToken = default)
        {
            ApiResponse<EnumerationResult<Entry>> response = await _Client.SendAsync<EnumerationResult<Entry>>(
                HttpMethod.Get,
                $"/v1/accounts/{accountId}/entries",
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data?.Objects ?? new List<Entry>();
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<Entry>> EnumerateAsync(string accountId, EntryEnumerationQuery? query = null, CancellationToken cancellationToken = default)
        {
            query ??= new EntryEnumerationQuery();

            ApiResponse<EnumerationResult<Entry>> response = await _Client.SendAsync<EnumerationResult<Entry>>(
                HttpMethod.Post,
                $"/v1/accounts/{accountId}/entries/enumerate",
                query,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new EnumerationResult<Entry>();
        }

        /// <inheritdoc />
        public async Task<List<Entry>> GetPendingAsync(string accountId, CancellationToken cancellationToken = default)
        {
            ApiResponse<List<Entry>> response = await _Client.SendAsync<List<Entry>>(
                HttpMethod.Get,
                $"/v1/accounts/{accountId}/entries/pending",
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new List<Entry>();
        }

        /// <inheritdoc />
        public async Task<List<Entry>> GetPendingCreditsAsync(string accountId, CancellationToken cancellationToken = default)
        {
            ApiResponse<List<Entry>> response = await _Client.SendAsync<List<Entry>>(
                HttpMethod.Get,
                $"/v1/accounts/{accountId}/entries/pending/credits",
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new List<Entry>();
        }

        /// <inheritdoc />
        public async Task<List<Entry>> GetPendingDebitsAsync(string accountId, CancellationToken cancellationToken = default)
        {
            ApiResponse<List<Entry>> response = await _Client.SendAsync<List<Entry>>(
                HttpMethod.Get,
                $"/v1/accounts/{accountId}/entries/pending/debits",
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new List<Entry>();
        }

        /// <inheritdoc />
        public async Task CancelAsync(string accountId, string entryId, CancellationToken cancellationToken = default)
        {
            await _Client.SendAsync<object>(
                HttpMethod.Delete,
                $"/v1/accounts/{accountId}/entries/{entryId}",
                null,
                cancellationToken).ConfigureAwait(false);
        }

        #endregion
    }
}

