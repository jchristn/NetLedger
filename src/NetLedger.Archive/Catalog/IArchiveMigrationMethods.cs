namespace NetLedger.Archive.Catalog
{
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Archive.Models;

    /// <summary>
    /// Archive migration catalog methods.
    /// </summary>
    public interface IArchiveMigrationMethods
    {
        /// <summary>
        /// Create a migration.
        /// </summary>
        /// <param name="migration">Migration.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created migration.</returns>
        Task<ArchiveMigration> CreateAsync(ArchiveMigration migration, CancellationToken token = default);

        /// <summary>
        /// Read a migration by identifier.
        /// </summary>
        /// <param name="id">Migration identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Migration if found.</returns>
        Task<ArchiveMigration?> ReadByIdAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Read a migration by idempotency key.
        /// </summary>
        /// <param name="idempotencyKey">Idempotency key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Migration if found.</returns>
        Task<ArchiveMigration?> ReadByIdempotencyKeyAsync(string idempotencyKey, CancellationToken token = default);

        /// <summary>
        /// Enumerate migrations.
        /// </summary>
        /// <param name="query">Archive query.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result.</returns>
        Task<EnumerationResult<ArchiveMigration>> EnumerateAsync(ArchiveQuery query, CancellationToken token = default);

        /// <summary>
        /// Update a migration status.
        /// </summary>
        /// <param name="id">Migration identifier.</param>
        /// <param name="status">Migration status.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated migration.</returns>
        Task<ArchiveMigration> UpdateStatusAsync(string id, ArchiveMigrationStatus status, CancellationToken token = default);

        /// <summary>
        /// Create a migration batch.
        /// </summary>
        /// <param name="batch">Migration batch.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created migration batch.</returns>
        Task<ArchiveMigrationBatch> CreateBatchAsync(ArchiveMigrationBatch batch, CancellationToken token = default);

        /// <summary>
        /// Read a migration batch by identifier.
        /// </summary>
        /// <param name="migrationId">Migration identifier.</param>
        /// <param name="batchId">Batch identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Migration batch if found.</returns>
        Task<ArchiveMigrationBatch?> ReadBatchAsync(string migrationId, string batchId, CancellationToken token = default);

        /// <summary>
        /// Update a migration batch.
        /// </summary>
        /// <param name="batch">Migration batch.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated migration batch.</returns>
        Task<ArchiveMigrationBatch> UpdateBatchAsync(ArchiveMigrationBatch batch, CancellationToken token = default);

        /// <summary>
        /// Enumerate batches for a migration.
        /// </summary>
        /// <param name="migrationId">Migration identifier.</param>
        /// <param name="query">Archive query.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result.</returns>
        Task<EnumerationResult<ArchiveMigrationBatch>> EnumerateBatchesAsync(string migrationId, ArchiveQuery query, CancellationToken token = default);
    }
}
