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
    /// Balance handler for balance queries and commit operations.
    /// </summary>
    internal class BalanceHandler
    {
        #region Private-Members

        private readonly string _Header = "[BalanceHandler] ";
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
        internal BalanceHandler(ServerSettings settings, LoggingModule logging, Ledger ledger)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));

            _Logging.Debug(_Header + "initialized");
        }

        #endregion

        #region Internal-Methods

        /// <summary>
        /// Get current balance for an account.
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response context with balance.</returns>
        internal async Task<ResponseContext> GetBalanceAsync(RequestContext req, CancellationToken token = default)
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

            Balance balance = await _Ledger.GetBalanceAsync(req.AccountGuid.Value, true, token).ConfigureAwait(false);

            return new ResponseContext(req, balance);
        }

        /// <summary>
        /// Get historical balance at a specific point in time.
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response context with historical balance.</returns>
        internal async Task<ResponseContext> GetBalanceAsOfAsync(RequestContext req, CancellationToken token = default)
        {
            if (!req.AccountGuid.HasValue)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Account GUID is required");
            }

            if (!req.AsOfUtc.HasValue)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "asOf query parameter is required");
            }

            // Verify account exists
            Account? account = await _Ledger.GetAccountByGuidAsync(req.AccountGuid.Value, token).ConfigureAwait(false);
            if (account == null)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, "Account not found");
            }

            decimal balance = await _Ledger.GetBalanceAsOfAsync(
                req.AccountGuid.Value,
                req.AsOfUtc.Value,
                token).ConfigureAwait(false);

            return new ResponseContext(req, new
            {
                accountGuid = req.AccountGuid.Value,
                asOfUtc = req.AsOfUtc.Value,
                balance
            });
        }

        /// <summary>
        /// Get balances for all accounts.
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response context with all balances.</returns>
        internal async Task<ResponseContext> GetAllBalancesAsync(RequestContext req, CancellationToken token = default)
        {
            Dictionary<Guid, Balance> balances = await _Ledger.GetAllBalancesAsync(token).ConfigureAwait(false);

            return new ResponseContext(req, balances);
        }

        /// <summary>
        /// Commit pending entries.
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response context with balance after commit.</returns>
        internal async Task<ResponseContext> CommitAsync(RequestContext req, CancellationToken token = default)
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

            CommitRequest? commitReq = req.DeserializeBody<CommitRequest>();
            List<Guid>? entryGuids = commitReq?.EntryGuids;

            Balance balance = await _Ledger.CommitEntriesAsync(
                req.AccountGuid.Value,
                entryGuids,
                true,
                token).ConfigureAwait(false);

            return new ResponseContext(req, balance);
        }

        /// <summary>
        /// Verify balance chain integrity.
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response context with verification result.</returns>
        internal async Task<ResponseContext> VerifyBalanceChainAsync(RequestContext req, CancellationToken token = default)
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

            bool isValid = await _Ledger.VerifyBalanceChainAsync(req.AccountGuid.Value, token).ConfigureAwait(false);

            return new ResponseContext(req, new
            {
                accountGuid = req.AccountGuid.Value,
                isValid
            });
        }

        #endregion

        #region Private-Classes

        private class CommitRequest
        {
            public List<Guid>? EntryGuids { get; set; }
        }

        #endregion
    }
}
