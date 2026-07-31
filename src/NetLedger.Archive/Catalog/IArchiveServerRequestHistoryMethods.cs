namespace NetLedger.Archive.Catalog
{
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger;
    using NetLedger.Archive.Models;

    /// <summary>
    /// Archive Server operational request history catalog methods.
    /// </summary>
    public interface IArchiveServerRequestHistoryMethods
    {
        /// <summary>
        /// Create an Archive Server request history record.
        /// </summary>
        /// <param name="record">Request history record.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created request history record.</returns>
        Task<ArchiveServerRequestHistoryRecord> CreateAsync(ArchiveServerRequestHistoryRecord record, CancellationToken token = default);

        /// <summary>
        /// Enumerate Archive Server operational request history records.
        /// </summary>
        /// <param name="filter">Search filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Paginated records.</returns>
        Task<EnumerationResult<ArchiveServerRequestHistoryRecord>> EnumerateAsync(RequestHistoryFilter filter, CancellationToken token = default);

        /// <summary>
        /// Read an Archive Server operational request history record.
        /// </summary>
        /// <param name="tenantId">Tenant scope. Null means all tenants.</param>
        /// <param name="id">Request history record identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Request history record or null.</returns>
        Task<ArchiveServerRequestHistoryRecord?> ReadAsync(string? tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Summarize Archive Server operational request history records.
        /// </summary>
        /// <param name="filter">Search filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Request history summary.</returns>
        Task<RequestHistorySummary> SummarizeAsync(RequestHistoryFilter filter, CancellationToken token = default);
    }
}
