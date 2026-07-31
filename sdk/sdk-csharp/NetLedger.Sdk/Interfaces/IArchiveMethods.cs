namespace NetLedger.Sdk.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Archive Server cold data and metadata operations.
    /// </summary>
    public interface IArchiveMethods
    {
        /// <summary>
        /// Read Archive Server health.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Archive health.</returns>
        Task<ArchiveHealth> HealthAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Enumerate archive ranges.
        /// </summary>
        /// <param name="query">Archive query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Archive ranges.</returns>
        Task<List<ArchiveRangeInfo>> RangesAsync(ArchiveQuery? query = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Enumerate archive manifests.
        /// </summary>
        /// <param name="query">Archive query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Archive manifests.</returns>
        Task<List<ArchiveManifestInfo>> ManifestsAsync(ArchiveQuery? query = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Read one archive manifest.
        /// </summary>
        /// <param name="manifestId">Manifest identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Archive manifest.</returns>
        Task<ArchiveManifestInfo> ManifestAsync(string manifestId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Enumerate objects for one archive manifest.
        /// </summary>
        /// <param name="manifestId">Manifest identifier.</param>
        /// <param name="query">Archive query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Archive objects.</returns>
        Task<List<ArchiveObjectInfo>> ManifestObjectsAsync(string manifestId, ArchiveQuery? query = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Enumerate balance checkpoints for one archive manifest.
        /// </summary>
        /// <param name="manifestId">Manifest identifier.</param>
        /// <param name="query">Archive query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Archive balance checkpoints.</returns>
        Task<List<ArchiveBalanceCheckpointInfo>> ManifestCheckpointsAsync(string manifestId, ArchiveQuery? query = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Verify a manifest.
        /// </summary>
        /// <param name="manifestId">Manifest identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task VerifyManifestAsync(string manifestId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Quarantine a manifest.
        /// </summary>
        /// <param name="manifestId">Manifest identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task QuarantineManifestAsync(string manifestId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Supersede a manifest.
        /// </summary>
        /// <param name="manifestId">Manifest identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task SupersedeManifestAsync(string manifestId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Enumerate storage pools.
        /// </summary>
        /// <param name="query">Archive query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Storage pools.</returns>
        Task<List<ArchiveStoragePoolInfo>> StoragePoolsAsync(ArchiveQuery? query = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Read archive storage pool health.
        /// </summary>
        /// <param name="storagePoolId">Storage pool identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Archive storage pool health.</returns>
        Task<ArchiveStoragePoolHealthInfo> StoragePoolHealthAsync(string storagePoolId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Enumerate archive migrations.
        /// </summary>
        /// <param name="query">Archive query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Archive migrations.</returns>
        Task<List<ArchiveMigrationInfo>> MigrationsAsync(ArchiveQuery? query = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Read one archive migration.
        /// </summary>
        /// <param name="migrationId">Migration identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Archive migration.</returns>
        Task<ArchiveMigrationInfo> MigrationAsync(string migrationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Enumerate batches for one archive migration.
        /// </summary>
        /// <param name="migrationId">Migration identifier.</param>
        /// <param name="query">Archive query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Archive migration batches.</returns>
        Task<List<ArchiveMigrationBatchInfo>> MigrationBatchesAsync(string migrationId, ArchiveQuery? query = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Create an Archive Server migration.
        /// </summary>
        /// <param name="request">Migration request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Archive migration.</returns>
        Task<ArchiveMigrationInfo> CreateMigrationAsync(ArchiveMigrationRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Create metadata for one Archive Server migration batch.
        /// </summary>
        /// <param name="migrationId">Migration identifier.</param>
        /// <param name="request">Batch metadata request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Archive migration batch.</returns>
        Task<ArchiveMigrationBatchInfo> CreateMigrationBatchAsync(string migrationId, ArchiveMigrationBatchRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Upload compressed JSONL content for one Archive Server migration batch.
        /// </summary>
        /// <param name="migrationId">Migration identifier.</param>
        /// <param name="batchId">Batch identifier.</param>
        /// <param name="content">Batch content stream.</param>
        /// <param name="contentHashSha256">Optional expected SHA-256 content hash header.</param>
        /// <param name="contentType">Request content type.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Updated archive migration batch.</returns>
        Task<ArchiveMigrationBatchInfo> UploadMigrationBatchContentAsync(string migrationId, string batchId, Stream content, string? contentHashSha256 = null, string contentType = "application/gzip", CancellationToken cancellationToken = default);

        /// <summary>
        /// Seal an Archive Server migration after all batches are uploaded.
        /// </summary>
        /// <param name="migrationId">Migration identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Archive migration.</returns>
        Task<ArchiveMigrationInfo> SealMigrationAsync(string migrationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Commit an Archive Server migration and create its manifest.
        /// </summary>
        /// <param name="migrationId">Migration identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Archive manifest.</returns>
        Task<ArchiveManifestInfo> CommitMigrationAsync(string migrationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Abort an Archive Server migration and delete temporary content.
        /// </summary>
        /// <param name="migrationId">Migration identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Archive migration.</returns>
        Task<ArchiveMigrationInfo> AbortMigrationAsync(string migrationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Start an active NetLedger Server export of committed entries to NetLedger Archive Server.
        /// </summary>
        /// <param name="request">Archive export request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Archive export response.</returns>
        Task<ArchiveExportResponse> ExportEntriesAsync(ArchiveExportRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Start an active NetLedger Server export of request history to NetLedger Archive Server.
        /// </summary>
        /// <param name="request">Archive export request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Archive export response.</returns>
        Task<ArchiveExportResponse> ExportRequestHistoryAsync(ArchiveExportRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Start an active NetLedger Server export of committed entries for one tenant account.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="request">Archive export request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Archive export response.</returns>
        Task<ArchiveExportResponse> ExportTenantAccountEntriesAsync(string tenantId, string accountId, ArchiveExportRequest? request = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Enumerate archived entries for an account.
        /// </summary>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="query">Archive query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Archived entries.</returns>
        Task<EnumerationResult<Entry>> EntriesAsync(string accountId, ArchiveQuery? query = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Enumerate archived entries for a tenant account.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="query">Archive query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Archived entries.</returns>
        Task<EnumerationResult<Entry>> TenantEntriesAsync(string tenantId, string accountId, ArchiveQuery? query = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Read archived balance as of a point in time for an account.
        /// </summary>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="asOfUtc">As-of timestamp UTC.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Archived balance information.</returns>
        Task<ArchiveBalanceInfo> BalanceAsOfAsync(string accountId, DateTime asOfUtc, CancellationToken cancellationToken = default);

        /// <summary>
        /// Read archived balance as of a point in time for a tenant account.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="asOfUtc">As-of timestamp UTC.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Archived balance information.</returns>
        Task<ArchiveBalanceInfo> TenantBalanceAsOfAsync(string tenantId, string accountId, DateTime asOfUtc, CancellationToken cancellationToken = default);

        /// <summary>
        /// Verify archived balance chain and object integrity for an account.
        /// </summary>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="query">Archive query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Archive verification result.</returns>
        Task<ArchiveVerificationResult> VerifyAccountAsync(string accountId, ArchiveQuery? query = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Verify archived balance chain and object integrity for a tenant account.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="query">Archive query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Archive verification result.</returns>
        Task<ArchiveVerificationResult> VerifyTenantAccountAsync(string tenantId, string accountId, ArchiveQuery? query = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Read archive object catalog and storage metadata.
        /// </summary>
        /// <param name="objectId">Archive object identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Archive object metadata.</returns>
        Task<ArchiveObjectMetadataInfo> ObjectMetadataAsync(string objectId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Enumerate archived NetLedger request history from Archive Server.
        /// </summary>
        /// <param name="query">Request history query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Archived request history.</returns>
        Task<EnumerationResult<RequestHistoryEntry>> RequestHistoryAsync(RequestHistoryQuery? query = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Summarize archived NetLedger request history from Archive Server.
        /// </summary>
        /// <param name="query">Request history query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Request history summary.</returns>
        Task<RequestHistorySummary> RequestHistorySummaryAsync(RequestHistoryQuery? query = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Read one archived NetLedger request history entry from Archive Server.
        /// </summary>
        /// <param name="id">Request history identifier.</param>
        /// <param name="query">Optional scope query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Request history entry.</returns>
        Task<RequestHistoryEntry> RequestHistoryEntryAsync(string id, RequestHistoryQuery? query = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Enumerate Archive Server operational request history.
        /// </summary>
        /// <param name="query">Request history query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Archive Server request history.</returns>
        Task<EnumerationResult<RequestHistoryEntry>> ArchiveServerRequestHistoryAsync(RequestHistoryQuery? query = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Summarize Archive Server operational request history.
        /// </summary>
        /// <param name="query">Request history query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Request history summary.</returns>
        Task<RequestHistorySummary> ArchiveServerRequestHistorySummaryAsync(RequestHistoryQuery? query = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Read one Archive Server operational request history entry.
        /// </summary>
        /// <param name="id">Request history identifier.</param>
        /// <param name="query">Optional scope query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Request history entry.</returns>
        Task<RequestHistoryEntry> ArchiveServerRequestHistoryEntryAsync(string id, RequestHistoryQuery? query = null, CancellationToken cancellationToken = default);
    }
}
