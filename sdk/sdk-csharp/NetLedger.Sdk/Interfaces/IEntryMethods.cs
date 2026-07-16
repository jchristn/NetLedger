namespace NetLedger.Sdk.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Entry operations for the NetLedger API.
    /// </summary>
    public interface IEntryMethods
    {
        /// <summary>
        /// Add a credit entry to an account.
        /// </summary>
        /// <param name="accountId">The Id of the account.</param>
        /// <param name="amount">The credit amount (must be greater than zero).</param>
        /// <param name="description">Optional description for the entry.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created credit entry.</returns>
        /// <exception cref="NetLedgerValidationException">Thrown when amount is not positive.</exception>
        /// <exception cref="NetLedgerConnectionException">Thrown when unable to connect to the server.</exception>
        /// <exception cref="NetLedgerApiException">Thrown when the server returns an error.</exception>
        Task<Entry> AddCreditAsync(string accountId, decimal amount, string? description = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Add multiple credit entries to an account in a single operation.
        /// </summary>
        /// <param name="accountId">The Id of the account.</param>
        /// <param name="entries">The list of credit entries to add.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The list of created credit entries.</returns>
        /// <exception cref="ArgumentNullException">Thrown when entries is null.</exception>
        /// <exception cref="NetLedgerValidationException">Thrown when any entry has a non-positive amount.</exception>
        /// <exception cref="NetLedgerConnectionException">Thrown when unable to connect to the server.</exception>
        /// <exception cref="NetLedgerApiException">Thrown when the server returns an error.</exception>
        Task<List<Entry>> AddCreditsAsync(string accountId, List<EntryInput> entries, CancellationToken cancellationToken = default);

        /// <summary>
        /// Add a debit entry to an account.
        /// </summary>
        /// <param name="accountId">The Id of the account.</param>
        /// <param name="amount">The debit amount (must be greater than zero).</param>
        /// <param name="description">Optional description for the entry.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created debit entry.</returns>
        /// <exception cref="NetLedgerValidationException">Thrown when amount is not positive.</exception>
        /// <exception cref="NetLedgerConnectionException">Thrown when unable to connect to the server.</exception>
        /// <exception cref="NetLedgerApiException">Thrown when the server returns an error.</exception>
        Task<Entry> AddDebitAsync(string accountId, decimal amount, string? description = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Add multiple debit entries to an account in a single operation.
        /// </summary>
        /// <param name="accountId">The Id of the account.</param>
        /// <param name="entries">The list of debit entries to add.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The list of created debit entries.</returns>
        /// <exception cref="ArgumentNullException">Thrown when entries is null.</exception>
        /// <exception cref="NetLedgerValidationException">Thrown when any entry has a non-positive amount.</exception>
        /// <exception cref="NetLedgerConnectionException">Thrown when unable to connect to the server.</exception>
        /// <exception cref="NetLedgerApiException">Thrown when the server returns an error.</exception>
        Task<List<Entry>> AddDebitsAsync(string accountId, List<EntryInput> entries, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get all entries for an account.
        /// </summary>
        /// <param name="accountId">The Id of the account.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of all entries for the account.</returns>
        /// <exception cref="NetLedgerConnectionException">Thrown when unable to connect to the server.</exception>
        /// <exception cref="NetLedgerApiException">Thrown when the server returns an error.</exception>
        Task<List<Entry>> GetAllAsync(string accountId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Enumerate entries for an account with filtering and pagination.
        /// </summary>
        /// <param name="accountId">The Id of the account.</param>
        /// <param name="query">Query parameters for filtering and pagination.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Enumeration result containing entries.</returns>
        /// <exception cref="NetLedgerConnectionException">Thrown when unable to connect to the server.</exception>
        /// <exception cref="NetLedgerApiException">Thrown when the server returns an error.</exception>
        Task<EnumerationResult<Entry>> EnumerateAsync(string accountId, EntryEnumerationQuery? query = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get all pending (uncommitted) entries for an account.
        /// </summary>
        /// <param name="accountId">The Id of the account.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of pending entries.</returns>
        /// <exception cref="NetLedgerConnectionException">Thrown when unable to connect to the server.</exception>
        /// <exception cref="NetLedgerApiException">Thrown when the server returns an error.</exception>
        Task<List<Entry>> GetPendingAsync(string accountId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get pending credit entries for an account.
        /// </summary>
        /// <param name="accountId">The Id of the account.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of pending credit entries.</returns>
        /// <exception cref="NetLedgerConnectionException">Thrown when unable to connect to the server.</exception>
        /// <exception cref="NetLedgerApiException">Thrown when the server returns an error.</exception>
        Task<List<Entry>> GetPendingCreditsAsync(string accountId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get pending debit entries for an account.
        /// </summary>
        /// <param name="accountId">The Id of the account.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of pending debit entries.</returns>
        /// <exception cref="NetLedgerConnectionException">Thrown when unable to connect to the server.</exception>
        /// <exception cref="NetLedgerApiException">Thrown when the server returns an error.</exception>
        Task<List<Entry>> GetPendingDebitsAsync(string accountId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancel (delete) a pending entry.
        /// </summary>
        /// <param name="accountId">The Id of the account.</param>
        /// <param name="entryId">The Id of the entry to cancel.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task representing the async operation.</returns>
        /// <exception cref="NetLedgerConnectionException">Thrown when unable to connect to the server.</exception>
        /// <exception cref="NetLedgerApiException">Thrown when the server returns an error (404 if not found, 409 if already committed).</exception>
        Task CancelAsync(string accountId, string entryId, CancellationToken cancellationToken = default);
    }
}

