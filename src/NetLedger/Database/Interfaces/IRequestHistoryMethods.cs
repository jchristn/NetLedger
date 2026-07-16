namespace NetLedger.Database.Interfaces
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Request history data access methods.
    /// </summary>
    public interface IRequestHistoryMethods
    {
        /// <summary>
        /// Create a request history entry.
        /// </summary>
        /// <param name="entry">Request history entry.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created request history entry.</returns>
        Task<RequestHistoryEntry> CreateAsync(RequestHistoryEntry entry, CancellationToken token = default);

        /// <summary>
        /// Read a request history entry by identifier.
        /// </summary>
        /// <param name="tenantId">Tenant scope. Null means all tenants.</param>
        /// <param name="id">Request history identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Request history entry or null.</returns>
        Task<RequestHistoryEntry?> ReadAsync(string? tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate request history entries.
        /// </summary>
        /// <param name="filter">Search filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Paginated result.</returns>
        Task<RequestHistoryResult> EnumerateAsync(RequestHistoryFilter filter, CancellationToken token = default);

        /// <summary>
        /// Summarize request history entries.
        /// </summary>
        /// <param name="filter">Search filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Summary result.</returns>
        Task<RequestHistorySummary> SummarizeAsync(RequestHistoryFilter filter, CancellationToken token = default);

        /// <summary>
        /// Delete a request history entry by identifier.
        /// </summary>
        /// <param name="tenantId">Tenant scope. Null means all tenants.</param>
        /// <param name="id">Request history identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the entry was deleted.</returns>
        Task<bool> DeleteAsync(string? tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Delete matching request history entries.
        /// </summary>
        /// <param name="filter">Search filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Deleted row count.</returns>
        Task<long> DeleteManyAsync(RequestHistoryFilter filter, CancellationToken token = default);

        /// <summary>
        /// Delete entries older than a timestamp.
        /// </summary>
        /// <param name="olderThanUtc">Cutoff timestamp in UTC.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Deleted row count.</returns>
        Task<long> PruneAsync(DateTime olderThanUtc, CancellationToken token = default);
    }
}
