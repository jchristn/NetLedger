namespace NetLedger.Archive.Catalog
{
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Archive.Models;

    /// <summary>
    /// Archive object catalog methods.
    /// </summary>
    public interface IArchiveObjectMethods
    {
        /// <summary>
        /// Create an archive object record.
        /// </summary>
        /// <param name="archiveObject">Archive object.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created archive object.</returns>
        Task<ArchiveObject> CreateAsync(ArchiveObject archiveObject, CancellationToken token = default);

        /// <summary>
        /// Read an archive object by identifier.
        /// </summary>
        /// <param name="id">Archive object identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Archive object if found.</returns>
        Task<ArchiveObject?> ReadByIdAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate objects for a manifest.
        /// </summary>
        /// <param name="manifestId">Manifest identifier.</param>
        /// <param name="query">Archive query.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result.</returns>
        Task<EnumerationResult<ArchiveObject>> EnumerateByManifestAsync(string manifestId, ArchiveQuery query, CancellationToken token = default);
    }
}
