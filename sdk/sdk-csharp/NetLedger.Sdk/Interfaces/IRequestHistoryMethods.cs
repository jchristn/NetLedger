namespace NetLedger.Sdk.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Request history operations.
    /// </summary>
    public interface IRequestHistoryMethods
    {
        /// <summary>
        /// Enumerate request history entries.
        /// </summary>
        /// <param name="query">Request history query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Paginated request history result.</returns>
        Task<EnumerationResult<RequestHistoryEntry>> EnumerateAsync(RequestHistoryQuery? query = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Summarize request history entries.
        /// </summary>
        /// <param name="query">Request history query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Request history summary.</returns>
        Task<RequestHistorySummary> SummarizeAsync(RequestHistoryQuery? query = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Read one request history entry.
        /// </summary>
        /// <param name="id">Request history identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Request history entry.</returns>
        Task<RequestHistoryEntry> ReadAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete one request history entry.
        /// </summary>
        /// <param name="id">Request history identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Delete result.</returns>
        Task<RequestHistoryDeleteResult> DeleteAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete matching request history entries.
        /// </summary>
        /// <param name="query">Request history query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Delete result.</returns>
        Task<RequestHistoryDeleteResult> DeleteManyAsync(RequestHistoryQuery? query = null, CancellationToken cancellationToken = default);
    }
}
