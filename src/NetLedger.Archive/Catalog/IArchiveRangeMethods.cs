namespace NetLedger.Archive.Catalog
{
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Archive.Models;

    /// <summary>
    /// Archive coverage range catalog methods.
    /// </summary>
    public interface IArchiveRangeMethods
    {
        /// <summary>
        /// Create a coverage range.
        /// </summary>
        /// <param name="range">Archive range.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created range.</returns>
        Task<ArchiveRangeInfo> CreateAsync(ArchiveRangeInfo range, CancellationToken token = default);

        /// <summary>
        /// Enumerate coverage ranges.
        /// </summary>
        /// <param name="query">Archive query.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result.</returns>
        Task<EnumerationResult<ArchiveRangeInfo>> EnumerateAsync(ArchiveQuery query, CancellationToken token = default);
    }
}
