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
            public List<Guid>? EntryGuids { get; set; }
        }

        private class AddEntriesRequest
        {
            public decimal? Amount { get; set; }
            public string? Notes { get; set; }
            public List<EntryItem>? Entries { get; set; }
        }

        private class EntryItem
        {
            public decimal Amount { get; set; }
            public string? Notes { get; set; }
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
        public async Task<Entry> AddCreditAsync(Guid accountGuid, decimal amount, string? description = null, CancellationToken cancellationToken = default)
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
                $"/v1/accounts/{accountGuid}/credits",
                request,
                cancellationToken).ConfigureAwait(false);

            if (response.Data?.EntryGuids == null || response.Data.EntryGuids.Count == 0)
                throw new NetLedgerApiException(response.StatusCode, "No entry GUID returned from server.");

            return new Entry
            {
                GUID = response.Data.EntryGuids[0],
                AccountGUID = accountGuid,
                Type = EntryType.Credit,
                Amount = amount,
                Description = description,
                IsCommitted = false,
                CreatedUtc = DateTime.UtcNow
            };
        }

        /// <inheritdoc />
        public async Task<List<Entry>> AddCreditsAsync(Guid accountGuid, List<EntryInput> entries, CancellationToken cancellationToken = default)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            if (entries.Any(e => e.Amount <= 0))
                throw new NetLedgerValidationException("All amounts must be greater than zero.", nameof(entries));

            AddEntriesRequest request = new AddEntriesRequest
            {
                Entries = entries.Select(e => new EntryItem { Amount = e.Amount, Notes = e.Notes }).ToList()
            };

            ApiResponse<AddEntriesResponse> response = await _Client.SendAsync<AddEntriesResponse>(
                HttpMethod.Put,
                $"/v1/accounts/{accountGuid}/credits",
                request,
                cancellationToken).ConfigureAwait(false);

            List<Entry> result = new List<Entry>();
            if (response.Data?.EntryGuids != null)
            {
                for (int i = 0; i < response.Data.EntryGuids.Count && i < entries.Count; i++)
                {
                    result.Add(new Entry
                    {
                        GUID = response.Data.EntryGuids[i],
                        AccountGUID = accountGuid,
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
        public async Task<Entry> AddDebitAsync(Guid accountGuid, decimal amount, string? description = null, CancellationToken cancellationToken = default)
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
                $"/v1/accounts/{accountGuid}/debits",
                request,
                cancellationToken).ConfigureAwait(false);

            if (response.Data?.EntryGuids == null || response.Data.EntryGuids.Count == 0)
                throw new NetLedgerApiException(response.StatusCode, "No entry GUID returned from server.");

            return new Entry
            {
                GUID = response.Data.EntryGuids[0],
                AccountGUID = accountGuid,
                Type = EntryType.Debit,
                Amount = amount,
                Description = description,
                IsCommitted = false,
                CreatedUtc = DateTime.UtcNow
            };
        }

        /// <inheritdoc />
        public async Task<List<Entry>> AddDebitsAsync(Guid accountGuid, List<EntryInput> entries, CancellationToken cancellationToken = default)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            if (entries.Any(e => e.Amount <= 0))
                throw new NetLedgerValidationException("All amounts must be greater than zero.", nameof(entries));

            AddEntriesRequest request = new AddEntriesRequest
            {
                Entries = entries.Select(e => new EntryItem { Amount = e.Amount, Notes = e.Notes }).ToList()
            };

            ApiResponse<AddEntriesResponse> response = await _Client.SendAsync<AddEntriesResponse>(
                HttpMethod.Put,
                $"/v1/accounts/{accountGuid}/debits",
                request,
                cancellationToken).ConfigureAwait(false);

            List<Entry> result = new List<Entry>();
            if (response.Data?.EntryGuids != null)
            {
                for (int i = 0; i < response.Data.EntryGuids.Count && i < entries.Count; i++)
                {
                    result.Add(new Entry
                    {
                        GUID = response.Data.EntryGuids[i],
                        AccountGUID = accountGuid,
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
        public async Task<List<Entry>> GetAllAsync(Guid accountGuid, CancellationToken cancellationToken = default)
        {
            ApiResponse<EnumerationResult<Entry>> response = await _Client.SendAsync<EnumerationResult<Entry>>(
                HttpMethod.Get,
                $"/v1/accounts/{accountGuid}/entries",
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data?.Objects ?? new List<Entry>();
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<Entry>> EnumerateAsync(Guid accountGuid, EntryEnumerationQuery? query = null, CancellationToken cancellationToken = default)
        {
            query ??= new EntryEnumerationQuery();

            ApiResponse<EnumerationResult<Entry>> response = await _Client.SendAsync<EnumerationResult<Entry>>(
                HttpMethod.Post,
                $"/v1/accounts/{accountGuid}/entries/enumerate",
                query,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new EnumerationResult<Entry>();
        }

        /// <inheritdoc />
        public async Task<List<Entry>> GetPendingAsync(Guid accountGuid, CancellationToken cancellationToken = default)
        {
            ApiResponse<List<Entry>> response = await _Client.SendAsync<List<Entry>>(
                HttpMethod.Get,
                $"/v1/accounts/{accountGuid}/entries/pending",
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new List<Entry>();
        }

        /// <inheritdoc />
        public async Task<List<Entry>> GetPendingCreditsAsync(Guid accountGuid, CancellationToken cancellationToken = default)
        {
            ApiResponse<List<Entry>> response = await _Client.SendAsync<List<Entry>>(
                HttpMethod.Get,
                $"/v1/accounts/{accountGuid}/entries/pending/credits",
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new List<Entry>();
        }

        /// <inheritdoc />
        public async Task<List<Entry>> GetPendingDebitsAsync(Guid accountGuid, CancellationToken cancellationToken = default)
        {
            ApiResponse<List<Entry>> response = await _Client.SendAsync<List<Entry>>(
                HttpMethod.Get,
                $"/v1/accounts/{accountGuid}/entries/pending/debits",
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new List<Entry>();
        }

        /// <inheritdoc />
        public async Task CancelAsync(Guid accountGuid, Guid entryGuid, CancellationToken cancellationToken = default)
        {
            await _Client.SendAsync<object>(
                HttpMethod.Delete,
                $"/v1/accounts/{accountGuid}/entries/{entryGuid}",
                null,
                cancellationToken).ConfigureAwait(false);
        }

        #endregion
    }
}
