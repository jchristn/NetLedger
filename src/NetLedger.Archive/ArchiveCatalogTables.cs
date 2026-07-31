namespace NetLedger.Archive
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Archive catalog table names.
    /// </summary>
    public static class ArchiveCatalogTables
    {
        /// <summary>
        /// Archive schema migration table.
        /// </summary>
        public const string SchemaMigrations = "archiveschemamigrations";

        /// <summary>
        /// Archive storage pool table.
        /// </summary>
        public const string StoragePools = "archivestoragepools";

        /// <summary>
        /// Archive migrations table.
        /// </summary>
        public const string Migrations = "archivemigrations";

        /// <summary>
        /// Archive migration batches table.
        /// </summary>
        public const string MigrationBatches = "archivemigrationbatches";

        /// <summary>
        /// Archive manifests table.
        /// </summary>
        public const string Manifests = "archivemanifests";

        /// <summary>
        /// Archive objects table.
        /// </summary>
        public const string Objects = "archiveobjects";

        /// <summary>
        /// Archive account ranges table.
        /// </summary>
        public const string AccountRanges = "archiveaccountranges";

        /// <summary>
        /// Archive balance checkpoints table.
        /// </summary>
        public const string BalanceCheckpoints = "archivebalancecheckpoints";

        /// <summary>
        /// Archive request history ranges table.
        /// </summary>
        public const string RequestHistoryRanges = "archiverequesthistoryranges";

        /// <summary>
        /// Archive audit records table.
        /// </summary>
        public const string AuditRecords = "archiveauditrecords";

        /// <summary>
        /// Archive server request history table.
        /// </summary>
        public const string ServerRequestHistory = "archiveserverrequesthistory";

        /// <summary>
        /// Archive object locks table.
        /// </summary>
        public const string ObjectLocks = "archiveobjectlocks";

        /// <summary>
        /// Archive nonce replay table.
        /// </summary>
        public const string NonceReplay = "archivenoncereplay";

        /// <summary>
        /// Approved archive catalog table names.
        /// </summary>
        public static IReadOnlyCollection<string> Approved
        {
            get
            {
                return _Approved;
            }
        }

        private static readonly HashSet<string> _Approved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            SchemaMigrations,
            StoragePools,
            Migrations,
            MigrationBatches,
            Manifests,
            Objects,
            AccountRanges,
            BalanceCheckpoints,
            RequestHistoryRanges,
            AuditRecords,
            ServerRequestHistory,
            ObjectLocks,
            NonceReplay
        };

        /// <summary>
        /// Determine if a table name is approved for Archive Server catalog use.
        /// </summary>
        /// <param name="tableName">Table name.</param>
        /// <returns>True if approved.</returns>
        public static bool IsApproved(string tableName)
        {
            if (String.IsNullOrWhiteSpace(tableName)) return false;
            return _Approved.Contains(tableName);
        }

        /// <summary>
        /// Validate a table name for Archive Server catalog use.
        /// </summary>
        /// <param name="tableName">Table name.</param>
        /// <exception cref="ArgumentException">Thrown when the table is not approved.</exception>
        public static void ValidateApproved(string tableName)
        {
            if (!IsApproved(tableName))
            {
                throw new ArgumentException("Table name is not approved for NetLedger Archive Server catalog use.", nameof(tableName));
            }
        }
    }
}
