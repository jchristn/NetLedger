namespace NetLedger.Sdk.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Balance operations for the NetLedger API.
    /// </summary>
    public interface IBalanceMethods
    {
        /// <summary>
        /// Get the current balance for an account.
        /// </summary>
        /// <param name="accountGuid">The GUID of the account.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The account balance including pending transactions.</returns>
        /// <exception cref="NetLedgerConnectionException">Thrown when unable to connect to the server.</exception>
        /// <exception cref="NetLedgerApiException">Thrown when the server returns an error (404 if account not found).</exception>
        Task<Balance> GetAsync(Guid accountGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get the historical balance for an account as of a specific date/time.
        /// </summary>
        /// <param name="accountGuid">The GUID of the account.</param>
        /// <param name="asOfUtc">The UTC timestamp to get the balance as of.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The account balance as of the specified time.</returns>
        /// <exception cref="NetLedgerConnectionException">Thrown when unable to connect to the server.</exception>
        /// <exception cref="NetLedgerApiException">Thrown when the server returns an error (404 if account not found).</exception>
        Task<Balance> GetAsOfAsync(Guid accountGuid, DateTime asOfUtc, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get balances for all accounts.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Dictionary mapping account GUIDs to their balances.</returns>
        /// <exception cref="NetLedgerConnectionException">Thrown when unable to connect to the server.</exception>
        /// <exception cref="NetLedgerApiException">Thrown when the server returns an error.</exception>
        Task<List<Balance>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Commit all pending entries for an account.
        /// </summary>
        /// <param name="accountGuid">The GUID of the account.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The result of the commit operation.</returns>
        /// <exception cref="NetLedgerConnectionException">Thrown when unable to connect to the server.</exception>
        /// <exception cref="NetLedgerApiException">Thrown when the server returns an error (404 if account not found).</exception>
        Task<CommitResult> CommitAsync(Guid accountGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Commit specific pending entries for an account.
        /// </summary>
        /// <param name="accountGuid">The GUID of the account.</param>
        /// <param name="entryGuids">The GUIDs of the entries to commit.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The result of the commit operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when entryGuids is null.</exception>
        /// <exception cref="NetLedgerConnectionException">Thrown when unable to connect to the server.</exception>
        /// <exception cref="NetLedgerApiException">Thrown when the server returns an error (404 if account not found).</exception>
        Task<CommitResult> CommitAsync(Guid accountGuid, List<Guid> entryGuids, CancellationToken cancellationToken = default);

        /// <summary>
        /// Verify the balance chain integrity for an account.
        /// </summary>
        /// <param name="accountGuid">The GUID of the account.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the balance chain is valid, false otherwise.</returns>
        /// <exception cref="NetLedgerConnectionException">Thrown when unable to connect to the server.</exception>
        /// <exception cref="NetLedgerApiException">Thrown when the server returns an error (404 if account not found).</exception>
        Task<bool> VerifyAsync(Guid accountGuid, CancellationToken cancellationToken = default);
    }
}
