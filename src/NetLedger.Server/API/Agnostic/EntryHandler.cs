namespace NetLedger.Server.API.Agnostic
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Server.Models;
    using NetLedger.Server.Settings;
    using SyslogLogging;

    /// <summary>
    /// Entry handler for entry operations (credits, debits, enumeration).
    /// </summary>
    internal class EntryHandler
    {
        #region Private-Members

        private readonly string _Header = "[EntryHandler] ";
        private readonly ServerSettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly Ledger _Ledger;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="ledger">Ledger instance.</param>
        internal EntryHandler(ServerSettings settings, LoggingModule logging, Ledger ledger)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));

            _Logging.Debug(_Header + "initialized");
        }

        #endregion

        #region Internal-Methods

        /// <summary>
        /// Enumerate entries with querystring-based filtering and pagination.
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response context with enumeration result.</returns>
        internal async Task<ResponseContext> GetEntriesAsync(RequestContext req, CancellationToken token = default)
        {
            if (!req.AccountGuid.HasValue)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Account GUID is required");
            }

            // Verify account exists
            Account? account = await _Ledger.GetAccountByGuidAsync(req.AccountGuid.Value, token).ConfigureAwait(false);
            if (account == null)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, "Account not found");
            }

            EnumerationQuery query = new EnumerationQuery
            {
                AccountGUID = req.AccountGuid.Value,
                MaxResults = req.MaxResults,
                Skip = req.Skip,
                ContinuationToken = req.ContinuationToken,
                Ordering = req.Ordering,
                CreatedAfterUtc = req.StartTimeUtc,
                CreatedBeforeUtc = req.EndTimeUtc,
                AmountMinimum = req.AmountMin,
                AmountMaximum = req.AmountMax
            };

            EnumerationResult<Entry> result = await _Ledger.EnumerateTransactionsAsync(query, token).ConfigureAwait(false);

            return new ResponseContext(req, result);
        }

        /// <summary>
        /// Get pending entries.
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response context with pending entries.</returns>
        internal async Task<ResponseContext> GetPendingEntriesAsync(RequestContext req, CancellationToken token = default)
        {
            if (!req.AccountGuid.HasValue)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Account GUID is required");
            }

            // Verify account exists
            Account? account = await _Ledger.GetAccountByGuidAsync(req.AccountGuid.Value, token).ConfigureAwait(false);
            if (account == null)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, "Account not found");
            }

            List<Entry> entries = await _Ledger.GetPendingEntriesAsync(req.AccountGuid.Value, token).ConfigureAwait(false);

            return new ResponseContext(req, entries);
        }

        /// <summary>
        /// Get pending credits.
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response context with pending credits.</returns>
        internal async Task<ResponseContext> GetPendingCreditsAsync(RequestContext req, CancellationToken token = default)
        {
            if (!req.AccountGuid.HasValue)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Account GUID is required");
            }

            // Verify account exists
            Account? account = await _Ledger.GetAccountByGuidAsync(req.AccountGuid.Value, token).ConfigureAwait(false);
            if (account == null)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, "Account not found");
            }

            List<Entry> entries = await _Ledger.GetPendingCreditsAsync(req.AccountGuid.Value, token).ConfigureAwait(false);

            return new ResponseContext(req, entries);
        }

        /// <summary>
        /// Get pending debits.
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response context with pending debits.</returns>
        internal async Task<ResponseContext> GetPendingDebitsAsync(RequestContext req, CancellationToken token = default)
        {
            if (!req.AccountGuid.HasValue)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Account GUID is required");
            }

            // Verify account exists
            Account? account = await _Ledger.GetAccountByGuidAsync(req.AccountGuid.Value, token).ConfigureAwait(false);
            if (account == null)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, "Account not found");
            }

            List<Entry> entries = await _Ledger.GetPendingDebitsAsync(req.AccountGuid.Value, token).ConfigureAwait(false);

            return new ResponseContext(req, entries);
        }

        /// <summary>
        /// Enumerate entries with pagination.
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response context with enumeration result.</returns>
        internal async Task<ResponseContext> EnumerateAsync(RequestContext req, CancellationToken token = default)
        {
            if (!req.AccountGuid.HasValue)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Account GUID is required");
            }

            // Verify account exists
            Account? account = await _Ledger.GetAccountByGuidAsync(req.AccountGuid.Value, token).ConfigureAwait(false);
            if (account == null)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, "Account not found");
            }

            EnumerationQuery? queryReq = req.DeserializeBody<EnumerationQuery>();
            if (queryReq == null)
            {
                queryReq = new EnumerationQuery();
            }

            queryReq.AccountGUID = req.AccountGuid.Value;

            EnumerationResult<Entry> result = await _Ledger.EnumerateTransactionsAsync(queryReq, token).ConfigureAwait(false);

            return new ResponseContext(req, result);
        }

        /// <summary>
        /// Add credit(s) to an account.
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response context with created entry GUIDs.</returns>
        internal async Task<ResponseContext> AddCreditsAsync(RequestContext req, CancellationToken token = default)
        {
            if (!req.AccountGuid.HasValue)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Account GUID is required");
            }

            // Verify account exists
            Account? account = await _Ledger.GetAccountByGuidAsync(req.AccountGuid.Value, token).ConfigureAwait(false);
            if (account == null)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, "Account not found");
            }

            AddEntriesRequest? addReq = req.DeserializeBody<AddEntriesRequest>();
            if (addReq == null || (addReq.Entries == null && addReq.Amount == null))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Amount or entries array is required");
            }

            List<Guid> entryGuids = new List<Guid>();

            // Single entry
            if (addReq.Amount.HasValue)
            {
                Guid entryGuid = await _Ledger.AddCreditAsync(
                    req.AccountGuid.Value,
                    addReq.Amount.Value,
                    addReq.Notes,
                    null,
                    addReq.IsCommitted ?? false,
                    token).ConfigureAwait(false);
                entryGuids.Add(entryGuid);
            }
            // Batch entries
            else if (addReq.Entries != null && addReq.Entries.Count > 0)
            {
                List<BatchEntryInput> credits = new List<BatchEntryInput>();
                foreach (EntryItem item in addReq.Entries)
                {
                    credits.Add(new BatchEntryInput(item.Amount, item.Notes));
                }
                entryGuids = await _Ledger.AddCreditsAsync(
                    req.AccountGuid.Value,
                    credits,
                    addReq.IsCommitted ?? false,
                    token).ConfigureAwait(false);
            }

            ResponseContext resp = new ResponseContext(req, new { EntryGuids = entryGuids });
            resp.StatusCode = 201;
            return resp;
        }

        /// <summary>
        /// Add debit(s) to an account.
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response context with created entry GUIDs.</returns>
        internal async Task<ResponseContext> AddDebitsAsync(RequestContext req, CancellationToken token = default)
        {
            if (!req.AccountGuid.HasValue)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Account GUID is required");
            }

            // Verify account exists
            Account? account = await _Ledger.GetAccountByGuidAsync(req.AccountGuid.Value, token).ConfigureAwait(false);
            if (account == null)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, "Account not found");
            }

            AddEntriesRequest? addReq = req.DeserializeBody<AddEntriesRequest>();
            if (addReq == null || (addReq.Entries == null && addReq.Amount == null))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Amount or entries array is required");
            }

            List<Guid> entryGuids = new List<Guid>();

            // Single entry
            if (addReq.Amount.HasValue)
            {
                Guid entryGuid = await _Ledger.AddDebitAsync(
                    req.AccountGuid.Value,
                    addReq.Amount.Value,
                    addReq.Notes,
                    null,
                    addReq.IsCommitted ?? false,
                    token).ConfigureAwait(false);
                entryGuids.Add(entryGuid);
            }
            // Batch entries
            else if (addReq.Entries != null && addReq.Entries.Count > 0)
            {
                List<BatchEntryInput> debits = new List<BatchEntryInput>();
                foreach (EntryItem item in addReq.Entries)
                {
                    debits.Add(new BatchEntryInput(item.Amount, item.Notes));
                }
                entryGuids = await _Ledger.AddDebitsAsync(
                    req.AccountGuid.Value,
                    debits,
                    addReq.IsCommitted ?? false,
                    token).ConfigureAwait(false);
            }

            ResponseContext resp = new ResponseContext(req, new { EntryGuids = entryGuids });
            resp.StatusCode = 201;
            return resp;
        }

        /// <summary>
        /// Cancel a pending entry.
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response context.</returns>
        internal async Task<ResponseContext> CancelEntryAsync(RequestContext req, CancellationToken token = default)
        {
            if (!req.AccountGuid.HasValue)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Account GUID is required");
            }

            if (!req.EntryGuid.HasValue)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Entry GUID is required");
            }

            // Verify account exists
            Account? account = await _Ledger.GetAccountByGuidAsync(req.AccountGuid.Value, token).ConfigureAwait(false);
            if (account == null)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, "Account not found");
            }

            await _Ledger.CancelPendingAsync(req.AccountGuid.Value, req.EntryGuid.Value, token).ConfigureAwait(false);

            return new ResponseContext(req);
        }

        #endregion

        #region Private-Classes

        private class AddEntriesRequest
        {
            public decimal? Amount { get; set; }

            public string? Notes { get; set; }

            public bool? IsCommitted { get; set; }

            public List<EntryItem>? Entries { get; set; }
        }

        private class EntryItem
        {
            public decimal Amount { get; set; }

            public string? Notes { get; set; }
        }

        #endregion
    }
}
