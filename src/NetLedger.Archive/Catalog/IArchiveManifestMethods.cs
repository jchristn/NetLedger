namespace NetLedger.Archive.Catalog
{
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Archive.Models;

    /// <summary>
    /// Archive manifest catalog methods.
    /// </summary>
    public interface IArchiveManifestMethods
    {
        /// <summary>
        /// Create a manifest.
        /// </summary>
        /// <param name="manifest">Manifest.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created manifest.</returns>
        Task<ArchiveManifest> CreateAsync(ArchiveManifest manifest, CancellationToken token = default);

        /// <summary>
        /// Read a manifest by identifier.
        /// </summary>
        /// <param name="id">Manifest identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Manifest if found.</returns>
        Task<ArchiveManifest?> ReadByIdAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate manifests.
        /// </summary>
        /// <param name="query">Archive query.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result.</returns>
        Task<EnumerationResult<ArchiveManifest>> EnumerateAsync(ArchiveQuery query, CancellationToken token = default);

        /// <summary>
        /// Update a manifest status.
        /// </summary>
        /// <param name="id">Manifest identifier.</param>
        /// <param name="status">Manifest status.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated manifest.</returns>
        Task<ArchiveManifest> UpdateStatusAsync(string id, ArchiveManifestStatus status, CancellationToken token = default);
    }
}
