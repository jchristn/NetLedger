namespace NetLedger.Archive.Catalog
{
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Archive.Models;

    /// <summary>
    /// Archive storage pool catalog methods.
    /// </summary>
    public interface IArchiveStoragePoolMethods
    {
        /// <summary>
        /// Upsert a storage pool.
        /// </summary>
        /// <param name="pool">Storage pool.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Stored storage pool.</returns>
        Task<ArchiveStoragePool> UpsertAsync(ArchiveStoragePool pool, CancellationToken token = default);

        /// <summary>
        /// Read a storage pool by identifier.
        /// </summary>
        /// <param name="id">Storage pool identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Storage pool if found.</returns>
        Task<ArchiveStoragePool?> ReadByIdAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate storage pools.
        /// </summary>
        /// <param name="query">Archive query.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result.</returns>
        Task<EnumerationResult<ArchiveStoragePool>> EnumerateAsync(ArchiveQuery query, CancellationToken token = default);
    }
}
