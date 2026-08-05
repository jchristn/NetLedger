namespace NetLedger.Sdk.Interfaces
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Account management operations for the NetLedger API.
    /// </summary>
    public interface IAccountMethods
    {
        /// <summary>
        /// Create a new account.
        /// </summary>
        /// <param name="name">The name of the account.</param>
        /// <param name="notes">Optional notes for the account.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created account.</returns>
        /// <exception cref="ArgumentNullException">Thrown when name is null or empty.</exception>
        /// <exception cref="NetLedgerConnectionException">Thrown when unable to connect to the server.</exception>
        /// <exception cref="NetLedgerApiException">Thrown when the server returns an error.</exception>
        Task<Account> CreateAsync(string name, string? notes = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Create a new account with a specific Id.
        /// </summary>
        /// <param name="account">The account to create (Id will be used if provided).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created account.</returns>
        /// <exception cref="ArgumentNullException">Thrown when account is null.</exception>
        /// <exception cref="NetLedgerConnectionException">Thrown when unable to connect to the server.</exception>
        /// <exception cref="NetLedgerApiException">Thrown when the server returns an error.</exception>
        Task<Account> CreateAsync(Account account, CancellationToken cancellationToken = default);

        /// <summary>
        /// Update an existing account.
        /// </summary>
        /// <param name="account">The account to update (Id must be provided).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The updated account.</returns>
        /// <exception cref="ArgumentNullException">Thrown when account is null.</exception>
        /// <exception cref="ArgumentException">Thrown when account Id is null or empty.</exception>
        /// <exception cref="NetLedgerConnectionException">Thrown when unable to connect to the server.</exception>
        /// <exception cref="NetLedgerApiException">Thrown when the server returns an error.</exception>
        Task<Account> UpdateAsync(Account account, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get an account by its Id.
        /// </summary>
        /// <param name="accountId">The Id of the account.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The account.</returns>
        /// <exception cref="NetLedgerConnectionException">Thrown when unable to connect to the server.</exception>
        /// <exception cref="NetLedgerApiException">Thrown when the server returns an error (404 if not found).</exception>
        Task<Account> GetAsync(string accountId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get an account by its name.
        /// </summary>
        /// <param name="name">The name of the account.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The account.</returns>
        /// <exception cref="ArgumentNullException">Thrown when name is null or empty.</exception>
        /// <exception cref="NetLedgerConnectionException">Thrown when unable to connect to the server.</exception>
        /// <exception cref="NetLedgerApiException">Thrown when the server returns an error (404 if not found).</exception>
        Task<Account> GetByNameAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if an account exists.
        /// </summary>
        /// <param name="accountId">The Id of the account.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the account exists, false otherwise.</returns>
        /// <exception cref="NetLedgerConnectionException">Thrown when unable to connect to the server.</exception>
        Task<bool> ExistsAsync(string accountId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete an account.
        /// </summary>
        /// <param name="accountId">The Id of the account to delete.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task representing the async operation.</returns>
        /// <exception cref="NetLedgerConnectionException">Thrown when unable to connect to the server.</exception>
        /// <exception cref="NetLedgerApiException">Thrown when the server returns an error (404 if not found).</exception>
        Task DeleteAsync(string accountId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Enumerate accounts with optional filtering and pagination.
        /// </summary>
        /// <param name="query">Query parameters for filtering and pagination.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Enumeration result containing accounts.</returns>
        /// <exception cref="NetLedgerConnectionException">Thrown when unable to connect to the server.</exception>
        /// <exception cref="NetLedgerApiException">Thrown when the server returns an error.</exception>
        Task<EnumerationResult<Account>> EnumerateAsync(AccountEnumerationQuery? query = null, CancellationToken cancellationToken = default);
    }
}

