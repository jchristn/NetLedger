namespace NetLedger.Server.API.Agnostic
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Server.Authentication;
    using NetLedger.Server.Models;
    using NetLedger.Server.Services;
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
        private readonly AuthorizationService _AuthorizationService;
        private readonly ActiveArchiveBoundaryService _ArchiveBoundary;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="ledger">Ledger instance.</param>
        internal EntryHandler(ServerSettings settings, LoggingModule logging, Ledger ledger, AuthorizationService authorizationService)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            _AuthorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
            _ArchiveBoundary = new ActiveArchiveBoundaryService(_Settings);

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
            if (String.IsNullOrEmpty(req.AccountId))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Account identifier is required");
            }

            ResponseContext? authz = await AuthorizeAsync(req, "Entry", "Read", null, token).ConfigureAwait(false);
            if (authz != null) return authz;

            // Verify account exists
            Account? account = await _Ledger.GetAccountByIdAsync(req.AccountId, token).ConfigureAwait(false);
            if (account == null)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, "Account not found");
            }

            DateTime? fromUtc = req.StartTimeUtc;
            ResponseContext? archiveRangeError = _ArchiveBoundary.ApplyActiveRange(req, ref fromUtc, req.EndTimeUtc, "Entry");
            if (archiveRangeError != null) return archiveRangeError;
            req.StartTimeUtc = fromUtc;

            EnumerationQuery query = new EnumerationQuery
            {
                AccountId = req.AccountId,
                TenantId = req.TenantId,
                MaxResults = req.MaxResults,
                Skip = req.Skip,
                ContinuationToken = req.ContinuationToken,
                Ordering = req.Ordering,
                SearchTerm = req.SearchTerm,
                CreatedAfterUtc = req.StartTimeUtc,
                CreatedBeforeUtc = req.EndTimeUtc,
                AmountMinimum = req.AmountMin,
                AmountMaximum = req.AmountMax,
                CreditMinimum = req.CreditMinimum,
                CreditMaximum = req.CreditMaximum,
                DebitMinimum = req.DebitMinimum,
                DebitMaximum = req.DebitMaximum,
                Labels = req.Labels,
                Tags = req.Tags
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
            if (String.IsNullOrEmpty(req.AccountId))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Account identifier is required");
            }

            ResponseContext? authz = await AuthorizeAsync(req, "Entry", "Read", null, token).ConfigureAwait(false);
            if (authz != null) return authz;

            // Verify account exists
            Account? account = await _Ledger.GetAccountByIdAsync(req.AccountId, token).ConfigureAwait(false);
            if (account == null)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, "Account not found");
            }

            List<Entry> entries = await _Ledger.GetPendingEntriesAsync(req.AccountId, token).ConfigureAwait(false);

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
            if (String.IsNullOrEmpty(req.AccountId))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Account identifier is required");
            }

            ResponseContext? authz = await AuthorizeAsync(req, "Entry", "Read", null, token).ConfigureAwait(false);
            if (authz != null) return authz;

            // Verify account exists
            Account? account = await _Ledger.GetAccountByIdAsync(req.AccountId, token).ConfigureAwait(false);
            if (account == null)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, "Account not found");
            }

            List<Entry> entries = await _Ledger.GetPendingCreditsAsync(req.AccountId, token).ConfigureAwait(false);

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
            if (String.IsNullOrEmpty(req.AccountId))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Account identifier is required");
            }

            ResponseContext? authz = await AuthorizeAsync(req, "Entry", "Read", null, token).ConfigureAwait(false);
            if (authz != null) return authz;

            // Verify account exists
            Account? account = await _Ledger.GetAccountByIdAsync(req.AccountId, token).ConfigureAwait(false);
            if (account == null)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, "Account not found");
            }

            List<Entry> entries = await _Ledger.GetPendingDebitsAsync(req.AccountId, token).ConfigureAwait(false);

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
            if (String.IsNullOrEmpty(req.AccountId))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Account identifier is required");
            }

            ResponseContext? authz = await AuthorizeAsync(req, "Entry", "Read", null, token).ConfigureAwait(false);
            if (authz != null) return authz;

            // Verify account exists
            Account? account = await _Ledger.GetAccountByIdAsync(req.AccountId, token).ConfigureAwait(false);
            if (account == null)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, "Account not found");
            }

            EnumerationQuery? queryReq = req.DeserializeBody<EnumerationQuery>();
            if (queryReq == null)
            {
                queryReq = new EnumerationQuery();
            }

            DateTime? fromUtc = queryReq.CreatedAfterUtc ?? req.StartTimeUtc;
            DateTime? toUtc = queryReq.CreatedBeforeUtc ?? req.EndTimeUtc;
            ResponseContext? archiveRangeError = _ArchiveBoundary.ApplyActiveRange(req, ref fromUtc, toUtc, "Entry");
            if (archiveRangeError != null) return archiveRangeError;

            queryReq.AccountId = req.AccountId;
            queryReq.TenantId = req.TenantId;
            queryReq.CreatedAfterUtc = fromUtc;
            queryReq.CreatedBeforeUtc = toUtc;

            EnumerationResult<Entry> result = await _Ledger.EnumerateTransactionsAsync(queryReq, token).ConfigureAwait(false);

            return new ResponseContext(req, result);
        }

        /// <summary>
        /// Add credit(s) to an account.
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response context with created entry identifiers.</returns>
        internal async Task<ResponseContext> AddCreditsAsync(RequestContext req, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(req.AccountId))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Account identifier is required");
            }

            ResponseContext? authz = await AuthorizeAsync(req, "Entry", "Create", null, token).ConfigureAwait(false);
            if (authz != null) return authz;

            // Verify account exists
            Account? account = await _Ledger.GetAccountByIdAsync(req.AccountId, token).ConfigureAwait(false);
            if (account == null)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, "Account not found");
            }

            AddEntriesRequest? addReq = req.DeserializeBody<AddEntriesRequest>();
            if (addReq == null || (addReq.Entries == null && addReq.Amount == null))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Amount or entries array is required");
            }

            List<string> entryIds = new List<string>();

            // Single entry
            if (addReq.Amount.HasValue)
            {
                string entryId = await _Ledger.AddCreditAsync(
                    req.AccountId,
                    addReq.Amount.Value,
                    addReq.Notes,
                    null,
                    addReq.IsCommitted ?? false,
                    addReq.Labels,
                    addReq.Tags,
                    req.TenantId,
                    token).ConfigureAwait(false);
                entryIds.Add(entryId);
            }
            // Batch entries
            else if (addReq.Entries != null && addReq.Entries.Count > 0)
            {
                List<BatchEntryInput> credits = new List<BatchEntryInput>();
                foreach (EntryItem item in addReq.Entries)
                {
                    BatchEntryInput input = new BatchEntryInput(item.Amount, item.Notes);
                    input.Labels = item.Labels ?? new List<string>();
                    input.Tags = item.Tags ?? new Dictionary<string, string>();
                    credits.Add(input);
                }
                entryIds = await _Ledger.AddCreditsAsync(
                    req.AccountId,
                    credits,
                    addReq.IsCommitted ?? false,
                    token).ConfigureAwait(false);
            }

            ResponseContext resp = new ResponseContext(req, new { EntryIds = entryIds });
            resp.StatusCode = 201;
            return resp;
        }

        /// <summary>
        /// Add debit(s) to an account.
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response context with created entry identifiers.</returns>
        internal async Task<ResponseContext> AddDebitsAsync(RequestContext req, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(req.AccountId))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Account identifier is required");
            }

            ResponseContext? authz = await AuthorizeAsync(req, "Entry", "Create", null, token).ConfigureAwait(false);
            if (authz != null) return authz;

            // Verify account exists
            Account? account = await _Ledger.GetAccountByIdAsync(req.AccountId, token).ConfigureAwait(false);
            if (account == null)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, "Account not found");
            }

            AddEntriesRequest? addReq = req.DeserializeBody<AddEntriesRequest>();
            if (addReq == null || (addReq.Entries == null && addReq.Amount == null))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Amount or entries array is required");
            }

            List<string> entryIds = new List<string>();

            // Single entry
            if (addReq.Amount.HasValue)
            {
                string entryId = await _Ledger.AddDebitAsync(
                    req.AccountId,
                    addReq.Amount.Value,
                    addReq.Notes,
                    null,
                    addReq.IsCommitted ?? false,
                    addReq.Labels,
                    addReq.Tags,
                    req.TenantId,
                    token).ConfigureAwait(false);
                entryIds.Add(entryId);
            }
            // Batch entries
            else if (addReq.Entries != null && addReq.Entries.Count > 0)
            {
                List<BatchEntryInput> debits = new List<BatchEntryInput>();
                foreach (EntryItem item in addReq.Entries)
                {
                    BatchEntryInput input = new BatchEntryInput(item.Amount, item.Notes);
                    input.Labels = item.Labels ?? new List<string>();
                    input.Tags = item.Tags ?? new Dictionary<string, string>();
                    debits.Add(input);
                }
                entryIds = await _Ledger.AddDebitsAsync(
                    req.AccountId,
                    debits,
                    addReq.IsCommitted ?? false,
                    token).ConfigureAwait(false);
            }

            ResponseContext resp = new ResponseContext(req, new { EntryIds = entryIds });
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
            if (String.IsNullOrEmpty(req.AccountId))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Account identifier is required");
            }

            if (String.IsNullOrEmpty(req.EntryId))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Entry identifier is required");
            }

            ResponseContext? authz = await AuthorizeAsync(req, "Entry", "Delete", req.EntryId, token).ConfigureAwait(false);
            if (authz != null) return authz;

            // Verify account exists
            Account? account = await _Ledger.GetAccountByIdAsync(req.AccountId, token).ConfigureAwait(false);
            if (account == null)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, "Account not found");
            }

            await _Ledger.CancelPendingAsync(req.AccountId, req.EntryId, token).ConfigureAwait(false);

            return new ResponseContext(req);
        }

        #endregion

        #region Private-Methods

        private async Task<ResponseContext?> AuthorizeAsync(RequestContext req, string resourceType, string operationType, string? resourceId, CancellationToken token)
        {
            AuthorizationDecision decision = await _AuthorizationService.AuthorizeAsync(req, resourceType, operationType, resourceId, token).ConfigureAwait(false);
            if (decision.Permitted) return null;
            ApiErrorEnum error = String.Equals(decision.Reason, "Authentication required", StringComparison.Ordinal)
                ? ApiErrorEnum.Unauthorized
                : ApiErrorEnum.Forbidden;
            return ResponseContext.FromError(req, error, null, decision.Reason);
        }

        #endregion
    }
}

