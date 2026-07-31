namespace NetLedger.Sdk
{
    using System.Collections.Generic;

    /// <summary>
    /// Archive export response.
    /// </summary>
    public class ArchiveExportResponse
    {
        /// <summary>
        /// Archive migration identifier.
        /// </summary>
        public string? MigrationId { get; set; } = null;

        /// <summary>
        /// Archive manifest identifier returned by commit.
        /// </summary>
        public string? ManifestId { get; set; } = null;

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Account identifier.
        /// </summary>
        public string? AccountId { get; set; } = null;

        /// <summary>
        /// Number of exported rows.
        /// </summary>
        public long RowsExported { get; set; } = 0;

        /// <summary>
        /// Number of uploaded bytes.
        /// </summary>
        public long BytesUploaded { get; set; } = 0;

        /// <summary>
        /// Whether active cleanup was executed.
        /// </summary>
        public bool ActiveCleanupExecuted { get; set; } = false;

        /// <summary>
        /// Number of active rows deleted after archive commit.
        /// </summary>
        public long ActiveCleanupRowsDeleted { get; set; } = 0;

        /// <summary>
        /// Batch upload results.
        /// </summary>
        public List<ArchiveExportBatchResult> Batches { get; set; } = new List<ArchiveExportBatchResult>();
    }
}
