namespace NetLedger.Archive.Catalog
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provider-neutral archive catalog.
    /// </summary>
    public interface IArchiveCatalog : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Storage pool methods.
        /// </summary>
        IArchiveStoragePoolMethods StoragePools { get; }

        /// <summary>
        /// Manifest methods.
        /// </summary>
        IArchiveManifestMethods Manifests { get; }

        /// <summary>
        /// Archive object methods.
        /// </summary>
        IArchiveObjectMethods Objects { get; }

        /// <summary>
        /// Archive range methods.
        /// </summary>
        IArchiveRangeMethods Ranges { get; }

        /// <summary>
        /// Archived request-history range methods.
        /// </summary>
        IArchiveRequestHistoryRangeMethods RequestHistoryRanges { get; }

        /// <summary>
        /// Archived entry methods.
        /// </summary>
        IArchivedEntryMethods Entries { get; }

        /// <summary>
        /// Balance checkpoint methods.
        /// </summary>
        IArchiveBalanceCheckpointMethods BalanceCheckpoints { get; }

        /// <summary>
        /// Migration methods.
        /// </summary>
        IArchiveMigrationMethods Migrations { get; }

        /// <summary>
        /// Audit record methods.
        /// </summary>
        IArchiveAuditRecordMethods AuditRecords { get; }

        /// <summary>
        /// Archive Server request history methods.
        /// </summary>
        IArchiveServerRequestHistoryMethods ServerRequestHistory { get; }

        /// <summary>
        /// Initialize catalog storage.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task InitializeAsync(CancellationToken token = default);
    }
}
