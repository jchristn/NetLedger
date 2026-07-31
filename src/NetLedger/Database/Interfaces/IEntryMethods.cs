namespace NetLedger.Database.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for entry CRUD operations.
    /// </summary>
    public interface IEntryMethods
    {
        /// <summary>
        /// Create a new entry.
        /// </summary>
        /// <param name="entry">Entry to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created entry.</returns>
        Task<Entry> CreateAsync(Entry entry, CancellationToken token = default);

        /// <summary>
        /// Create multiple entries in a batch.
        /// </summary>
        /// <param name="entries">Entries to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of created entries.</returns>
        Task<List<Entry>> CreateManyAsync(List<Entry> entries, CancellationToken token = default);

        /// <summary>
        /// Read an entry by identifier.
        /// </summary>
        /// <param name="id">Entry identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Entry if found, null otherwise.</returns>
        Task<Entry> ReadByIdAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Read entries by multiple identifiers.
        /// </summary>
        /// <param name="ids">List of entry identifiers.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of entries.</returns>
        Task<List<Entry>> ReadByIdsAsync(List<string> ids, CancellationToken token = default);

        /// <summary>
        /// Read entries by account identifier.
        /// </summary>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of entries for the account.</returns>
        Task<List<Entry>> ReadByAccountIdAsync(string accountId, CancellationToken token = default);

        /// <summary>
        /// Read pending entries by account identifier.
        /// </summary>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="entryType">Optional entry type filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of pending entries.</returns>
        Task<List<Entry>> ReadPendingByAccountIdAsync(string accountId, EntryType? entryType = null, CancellationToken token = default);

        /// <summary>
        /// Read the latest balance entry for an account.
        /// </summary>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Latest balance entry if found, null otherwise.</returns>
        Task<Entry> ReadLatestBalanceAsync(string accountId, CancellationToken token = default);

        /// <summary>
        /// Read balance entry as of a specific timestamp.
        /// </summary>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="asOfUtc">Timestamp in UTC.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Balance entry if found, null otherwise.</returns>
        Task<Entry> ReadBalanceAsOfAsync(string accountId, DateTime asOfUtc, CancellationToken token = default);

        /// <summary>
        /// Read the first balance entry after a specific timestamp.
        /// </summary>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="afterUtc">Timestamp in UTC.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>First balance entry after the timestamp if found, null otherwise.</returns>
        Task<Entry> ReadFirstBalanceAfterAsync(string accountId, DateTime afterUtc, CancellationToken token = default);

        /// <summary>
        /// Read entries with filtering.
        /// </summary>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="filter">Filter builder with search criteria.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of filtered entries.</returns>
        Task<List<Entry>> ReadWithFilterAsync(string accountId, FilterBuilder filter, CancellationToken token = default);

        /// <summary>
        /// Enumerate entries with pagination and filtering.
        /// </summary>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="query">Enumeration query parameters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result with entries.</returns>
        Task<EnumerationResult<Entry>> EnumerateAsync(string accountId, EnumerationQuery query, CancellationToken token = default);

        /// <summary>
        /// Update an entry.
        /// </summary>
        /// <param name="entry">Entry to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated entry.</returns>
        Task<Entry> UpdateAsync(Entry entry, CancellationToken token = default);

        /// <summary>
        /// Update multiple entries in a batch.
        /// </summary>
        /// <param name="entries">Entries to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task UpdateManyAsync(List<Entry> entries, CancellationToken token = default);

        /// <summary>
        /// Atomically apply a commit by updating summarized entries and inserting the resulting balance entry.
        /// </summary>
        /// <param name="committedEntries">Entries being summarized.</param>
        /// <param name="balanceEntry">New balance entry.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task ApplyCommitAsync(List<Entry> committedEntries, Entry balanceEntry, CancellationToken token = default);

        /// <summary>
        /// Delete an entry by identifier.
        /// </summary>
        /// <param name="id">Entry identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByIdAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Delete all entries by account identifier.
        /// </summary>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByAccountIdAsync(string accountId, CancellationToken token = default);

        /// <summary>
        /// Delete committed entries up to a timestamp for one tenant account in a bounded batch.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="beforeUtc">Inclusive UTC cutoff timestamp.</param>
        /// <param name="maxRows">Maximum rows to delete.</param>
        /// <param name="preserveEntryId">Optional entry identifier to preserve from deletion.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Deleted row count.</returns>
        Task<long> DeleteCommittedBeforeAsync(string tenantId, string accountId, DateTime beforeUtc, int maxRows, string? preserveEntryId = null, CancellationToken token = default);

        /// <summary>
        /// Check if an entry exists by identifier.
        /// </summary>
        /// <param name="id">Entry identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if entry exists.</returns>
        Task<bool> ExistsByIdAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Get count of entries for an account.
        /// </summary>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of entries.</returns>
        Task<int> GetCountByAccountIdAsync(string accountId, CancellationToken token = default);

        /// <summary>
        /// Count pending non-balance entries up to a timestamp for one account.
        /// </summary>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="beforeUtc">Inclusive UTC cutoff timestamp.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Pending row count.</returns>
        Task<long> CountPendingBeforeAsync(string accountId, DateTime beforeUtc, CancellationToken token = default);

        /// <summary>
        /// Sum pending credits for an account.
        /// </summary>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Sum of pending credits.</returns>
        Task<decimal> SumPendingCreditsAsync(string accountId, CancellationToken token = default);

        /// <summary>
        /// Sum pending debits for an account.
        /// </summary>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Sum of pending debits.</returns>
        Task<decimal> SumPendingDebitsAsync(string accountId, CancellationToken token = default);
    }
}



