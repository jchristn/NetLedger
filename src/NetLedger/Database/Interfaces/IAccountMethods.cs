namespace NetLedger.Database.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for account CRUD operations.
    /// </summary>
    public interface IAccountMethods
    {
        /// <summary>
        /// Create a new account.
        /// </summary>
        /// <param name="account">Account to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created account.</returns>
        Task<Account> CreateAsync(Account account, CancellationToken token = default);

        /// <summary>
        /// Read an account by GUID.
        /// </summary>
        /// <param name="guid">Account GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Account if found, null otherwise.</returns>
        Task<Account> ReadByGuidAsync(Guid guid, CancellationToken token = default);

        /// <summary>
        /// Read an account by name.
        /// </summary>
        /// <param name="name">Account name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Account if found, null otherwise.</returns>
        Task<Account> ReadByNameAsync(string name, CancellationToken token = default);

        /// <summary>
        /// Read all accounts.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of all accounts.</returns>
        Task<List<Account>> ReadAllAsync(CancellationToken token = default);

        /// <summary>
        /// Search accounts by name.
        /// </summary>
        /// <param name="searchTerm">Search term for account name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of matching accounts.</returns>
        Task<List<Account>> SearchByNameAsync(string searchTerm, CancellationToken token = default);

        /// <summary>
        /// Enumerate accounts with pagination and filtering.
        /// </summary>
        /// <param name="query">Enumeration query parameters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result with accounts.</returns>
        Task<EnumerationResult<Account>> EnumerateAsync(EnumerationQuery query, CancellationToken token = default);

        /// <summary>
        /// Update an account.
        /// </summary>
        /// <param name="account">Account to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated account.</returns>
        Task<Account> UpdateAsync(Account account, CancellationToken token = default);

        /// <summary>
        /// Delete an account by GUID.
        /// </summary>
        /// <param name="guid">Account GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByGuidAsync(Guid guid, CancellationToken token = default);

        /// <summary>
        /// Check if an account exists by GUID.
        /// </summary>
        /// <param name="guid">Account GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if account exists.</returns>
        Task<bool> ExistsByGuidAsync(Guid guid, CancellationToken token = default);

        /// <summary>
        /// Check if an account exists by name.
        /// </summary>
        /// <param name="name">Account name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if account exists.</returns>
        Task<bool> ExistsByNameAsync(string name, CancellationToken token = default);

        /// <summary>
        /// Get total account count.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of accounts.</returns>
        Task<int> GetCountAsync(CancellationToken token = default);
    }
}
