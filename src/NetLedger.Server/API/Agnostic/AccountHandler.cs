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
    /// Account handler for account CRUD operations.
    /// </summary>
    internal class AccountHandler
    {
        #region Private-Members

        private readonly string _Header = "[AccountHandler] ";
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
        internal AccountHandler(ServerSettings settings, LoggingModule logging, Ledger ledger)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));

            _Logging.Debug(_Header + "initialized");
        }

        #endregion

        #region Internal-Methods

        /// <summary>
        /// Check if an account exists.
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response context.</returns>
        internal async Task<ResponseContext> ExistsAsync(RequestContext req, CancellationToken token = default)
        {
            if (!req.AccountGuid.HasValue)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Account GUID is required");
            }

            Account? account = await _Ledger.GetAccountByGuidAsync(req.AccountGuid.Value, token).ConfigureAwait(false);
            if (account == null)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, "Account not found");
            }

            return new ResponseContext(req);
        }

        /// <summary>
        /// Enumerate accounts with pagination.
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response context with enumeration result.</returns>
        internal async Task<ResponseContext> EnumerateAsync(RequestContext req, CancellationToken token = default)
        {
            EnumerationQuery query = new EnumerationQuery
            {
                MaxResults = req.MaxResults,
                Skip = req.Skip,
                ContinuationToken = req.ContinuationToken,
                Ordering = req.Ordering,
                SearchTerm = req.SearchTerm,
                CreatedAfterUtc = req.StartTimeUtc,
                CreatedBeforeUtc = req.EndTimeUtc,
                BalanceMinimum = req.BalanceMinimum,
                BalanceMaximum = req.BalanceMaximum
            };

            EnumerationResult<Account> result = await _Ledger.EnumerateAccountsAsync(query, token).ConfigureAwait(false);

            return new ResponseContext(req, result);
        }

        /// <summary>
        /// Get an account by GUID.
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response context with account.</returns>
        internal async Task<ResponseContext> ReadAsync(RequestContext req, CancellationToken token = default)
        {
            if (!req.AccountGuid.HasValue)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Account GUID is required");
            }

            Account? account = await _Ledger.GetAccountByGuidAsync(req.AccountGuid.Value, token).ConfigureAwait(false);
            if (account == null)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, "Account not found");
            }

            return new ResponseContext(req, account);
        }

        /// <summary>
        /// Get an account by name.
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response context with account.</returns>
        internal async Task<ResponseContext> ReadByNameAsync(RequestContext req, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(req.AccountName))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Account name is required");
            }

            Account? account = await _Ledger.GetAccountByNameAsync(req.AccountName, token).ConfigureAwait(false);
            if (account == null)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, "Account not found");
            }

            return new ResponseContext(req, account);
        }

        /// <summary>
        /// Create a new account.
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response context with created account GUID.</returns>
        internal async Task<ResponseContext> CreateAsync(RequestContext req, CancellationToken token = default)
        {
            CreateAccountRequest? createReq = req.DeserializeBody<CreateAccountRequest>();
            if (createReq == null || string.IsNullOrEmpty(createReq.Name))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Account name is required");
            }

            Guid accountGuid = await _Ledger.CreateAccountAsync(
                createReq.Name,
                createReq.InitialBalance,
                token).ConfigureAwait(false);

            Account? account = await _Ledger.GetAccountByGuidAsync(accountGuid, token).ConfigureAwait(false);

            ResponseContext resp = new ResponseContext(req, account);
            resp.StatusCode = 201;
            return resp;
        }

        /// <summary>
        /// Delete an account.
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response context.</returns>
        internal async Task<ResponseContext> DeleteAsync(RequestContext req, CancellationToken token = default)
        {
            if (!req.AccountGuid.HasValue)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Account GUID is required");
            }

            Account? account = await _Ledger.GetAccountByGuidAsync(req.AccountGuid.Value, token).ConfigureAwait(false);
            if (account == null)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, "Account not found");
            }

            await _Ledger.DeleteAccountByGuidAsync(req.AccountGuid.Value, token).ConfigureAwait(false);

            return new ResponseContext(req);
        }

        #endregion

        #region Private-Classes

        private class CreateAccountRequest
        {
            public string? Name { get; set; }
            public decimal? InitialBalance { get; set; }
        }

        #endregion
    }
}
