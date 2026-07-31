namespace NetLedger.Archive.Catalog
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Archive.Models;

    /// <summary>
    /// Archive balance checkpoint catalog methods.
    /// </summary>
    public interface IArchiveBalanceCheckpointMethods
    {
        /// <summary>
        /// Create a balance checkpoint.
        /// </summary>
        /// <param name="checkpoint">Checkpoint.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created checkpoint.</returns>
        Task<ArchiveBalanceCheckpoint> CreateAsync(ArchiveBalanceCheckpoint checkpoint, CancellationToken token = default);

        /// <summary>
        /// Read the closest checkpoint at or before a timestamp.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="asOfUtc">Timestamp UTC.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Checkpoint if found.</returns>
        Task<ArchiveBalanceCheckpoint?> ReadAsOfAsync(string tenantId, string accountId, DateTime asOfUtc, CancellationToken token = default);

        /// <summary>
        /// Enumerate checkpoints for a manifest.
        /// </summary>
        /// <param name="manifestId">Manifest identifier.</param>
        /// <param name="query">Archive query.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result.</returns>
        Task<EnumerationResult<ArchiveBalanceCheckpoint>> EnumerateByManifestAsync(string manifestId, ArchiveQuery query, CancellationToken token = default);
    }
}
