namespace NetLedger.Archive.Catalog
{
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger;
    using NetLedger.Archive.Models;

    /// <summary>
    /// Archive request-history range catalog methods.
    /// </summary>
    public interface IArchiveRequestHistoryRangeMethods
    {
        /// <summary>
        /// Create an archived request-history coverage range.
        /// </summary>
        /// <param name="range">Request-history range.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created range.</returns>
        Task<ArchiveRequestHistoryRange> CreateAsync(ArchiveRequestHistoryRange range, CancellationToken token = default);

        /// <summary>
        /// Enumerate archived request-history coverage ranges.
        /// </summary>
        /// <param name="query">Archive query.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Paginated ranges.</returns>
        Task<EnumerationResult<ArchiveRequestHistoryRange>> EnumerateAsync(ArchiveQuery query, CancellationToken token = default);
    }
}
