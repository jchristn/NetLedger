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
        /// Read an entry by GUID.
        /// </summary>
        /// <param name="guid">Entry GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Entry if found, null otherwise.</returns>
        Task<Entry> ReadByGuidAsync(Guid guid, CancellationToken token = default);

        /// <summary>
        /// Read entries by multiple GUIDs.
        /// </summary>
        /// <param name="guids">List of entry GUIDs.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of entries.</returns>
        Task<List<Entry>> ReadByGuidsAsync(List<Guid> guids, CancellationToken token = default);

        /// <summary>
        /// Read entries by account GUID.
        /// </summary>
        /// <param name="accountGuid">Account GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of entries for the account.</returns>
        Task<List<Entry>> ReadByAccountGuidAsync(Guid accountGuid, CancellationToken token = default);

        /// <summary>
        /// Read pending entries by account GUID.
        /// </summary>
        /// <param name="accountGuid">Account GUID.</param>
        /// <param name="entryType">Optional entry type filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of pending entries.</returns>
        Task<List<Entry>> ReadPendingByAccountGuidAsync(Guid accountGuid, EntryType? entryType = null, CancellationToken token = default);

        /// <summary>
        /// Read the latest balance entry for an account.
        /// </summary>
        /// <param name="accountGuid">Account GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Latest balance entry if found, null otherwise.</returns>
        Task<Entry> ReadLatestBalanceAsync(Guid accountGuid, CancellationToken token = default);

        /// <summary>
        /// Read balance entry as of a specific timestamp.
        /// </summary>
        /// <param name="accountGuid">Account GUID.</param>
        /// <param name="asOfUtc">Timestamp in UTC.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Balance entry if found, null otherwise.</returns>
        Task<Entry> ReadBalanceAsOfAsync(Guid accountGuid, DateTime asOfUtc, CancellationToken token = default);

        /// <summary>
        /// Read entries with filtering.
        /// </summary>
        /// <param name="accountGuid">Account GUID.</param>
        /// <param name="filter">Filter builder with search criteria.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of filtered entries.</returns>
        Task<List<Entry>> ReadWithFilterAsync(Guid accountGuid, FilterBuilder filter, CancellationToken token = default);

        /// <summary>
        /// Enumerate entries with pagination and filtering.
        /// </summary>
        /// <param name="accountGuid">Account GUID.</param>
        /// <param name="query">Enumeration query parameters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result with entries.</returns>
        Task<EnumerationResult<Entry>> EnumerateAsync(Guid accountGuid, EnumerationQuery query, CancellationToken token = default);

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
        /// Delete an entry by GUID.
        /// </summary>
        /// <param name="guid">Entry GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByGuidAsync(Guid guid, CancellationToken token = default);

        /// <summary>
        /// Delete all entries by account GUID.
        /// </summary>
        /// <param name="accountGuid">Account GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByAccountGuidAsync(Guid accountGuid, CancellationToken token = default);

        /// <summary>
        /// Check if an entry exists by GUID.
        /// </summary>
        /// <param name="guid">Entry GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if entry exists.</returns>
        Task<bool> ExistsByGuidAsync(Guid guid, CancellationToken token = default);

        /// <summary>
        /// Get count of entries for an account.
        /// </summary>
        /// <param name="accountGuid">Account GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of entries.</returns>
        Task<int> GetCountByAccountGuidAsync(Guid accountGuid, CancellationToken token = default);

        /// <summary>
        /// Sum pending credits for an account.
        /// </summary>
        /// <param name="accountGuid">Account GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Sum of pending credits.</returns>
        Task<decimal> SumPendingCreditsAsync(Guid accountGuid, CancellationToken token = default);

        /// <summary>
        /// Sum pending debits for an account.
        /// </summary>
        /// <param name="accountGuid">Account GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Sum of pending debits.</returns>
        Task<decimal> SumPendingDebitsAsync(Guid accountGuid, CancellationToken token = default);
    }
}
