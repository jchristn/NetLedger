namespace NetLedger.Archive.Catalog
{
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Archive.Models;

    /// <summary>
    /// Archive audit record catalog methods.
    /// </summary>
    public interface IArchiveAuditRecordMethods
    {
        /// <summary>
        /// Create an archive audit record.
        /// </summary>
        /// <param name="record">Audit record.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created audit record.</returns>
        Task<ArchiveAuditRecord> CreateAsync(ArchiveAuditRecord record, CancellationToken token = default);
    }
}
