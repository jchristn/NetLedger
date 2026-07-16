namespace NetLedger.Server.API.Agnostic
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Server.Authentication;
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
        private readonly AuthorizationService _AuthorizationService;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="ledger">Ledger instance.</param>
        internal BalanceHandler(ServerSettings settings, LoggingModule logging, Ledger ledger, AuthorizationService authorizationService)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            _AuthorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));

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
            if (String.IsNullOrEmpty(req.AccountId))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Account identifier is required");
            }

            ResponseContext? authz = await AuthorizeAsync(req, "Balance", "Read", null, token).ConfigureAwait(false);
            if (authz != null) return authz;

            // Verify account exists
            Account? account = await _Ledger.GetAccountByIdAsync(req.AccountId, token).ConfigureAwait(false);
            if (account == null)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, "Account not found");
            }

            Balance balance = await _Ledger.GetBalanceAsync(req.AccountId, true, token).ConfigureAwait(false);

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
            if (String.IsNullOrEmpty(req.AccountId))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Account identifier is required");
            }

            if (!req.AsOfUtc.HasValue)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "asOf query parameter is required");
            }

            ResponseContext? authz = await AuthorizeAsync(req, "Balance", "Read", null, token).ConfigureAwait(false);
            if (authz != null) return authz;

            // Verify account exists
            Account? account = await _Ledger.GetAccountByIdAsync(req.AccountId, token).ConfigureAwait(false);
            if (account == null)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, "Account not found");
            }

            decimal balance = await _Ledger.GetBalanceAsOfAsync(
                req.AccountId,
                req.AsOfUtc.Value,
                token).ConfigureAwait(false);

            return new ResponseContext(req, new
            {
                accountId = req.AccountId,
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
            ResponseContext? authz = await AuthorizeAsync(req, "Balance", "Read", null, token).ConfigureAwait(false);
            if (authz != null) return authz;

            Dictionary<string, Balance> balances;
            string? tenantId = ResolveEnumerationTenant(req);
            if (req.Auth?.IsAdmin == true && String.IsNullOrEmpty(tenantId))
            {
                balances = await _Ledger.GetAllBalancesAsync(token).ConfigureAwait(false);
            }
            else
            {
                EnumerationResult<Account> accounts = await _Ledger.EnumerateAccountsAsync(new EnumerationQuery
                {
                    TenantId = tenantId
                }, token).ConfigureAwait(false);

                balances = await _Ledger.GetBalancesForAccountsAsync(accounts.Objects, true, token).ConfigureAwait(false);
            }

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
            if (String.IsNullOrEmpty(req.AccountId))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Account identifier is required");
            }

            ResponseContext? authz = await AuthorizeAsync(req, "Balance", "Execute", null, token).ConfigureAwait(false);
            if (authz != null) return authz;

            // Verify account exists
            Account? account = await _Ledger.GetAccountByIdAsync(req.AccountId, token).ConfigureAwait(false);
            if (account == null)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, "Account not found");
            }

            CommitRequest? commitReq = req.DeserializeBody<CommitRequest>();
            List<string>? entryIds = commitReq?.EntryIds;

            Balance balance = await _Ledger.CommitEntriesAsync(
                req.AccountId,
                entryIds,
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
            if (String.IsNullOrEmpty(req.AccountId))
            {
                return ResponseContext.FromError(req, ApiErrorEnum.BadRequest, null, "Account identifier is required");
            }

            ResponseContext? authz = await AuthorizeAsync(req, "Balance", "Read", null, token).ConfigureAwait(false);
            if (authz != null) return authz;

            // Verify account exists
            Account? account = await _Ledger.GetAccountByIdAsync(req.AccountId, token).ConfigureAwait(false);
            if (account == null)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.NotFound, null, "Account not found");
            }

            bool isValid = await _Ledger.VerifyBalanceChainAsync(req.AccountId, token).ConfigureAwait(false);

            return new ResponseContext(req, new
            {
                accountId = req.AccountId,
                isValid
            });
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

        private string? ResolveEnumerationTenant(RequestContext req)
        {
            if (req.Auth?.IsAdmin == true) return req.TenantId;
            if (req.Auth?.IsAuthenticated == true) return req.TenantId ?? req.Auth.TenantId;
            return req.TenantId;
        }

        #endregion
    }
}




