namespace NetLedger.Archive
{
    /// <summary>
    /// Archive migration status.
    /// </summary>
    public enum ArchiveMigrationStatus
    {
        /// <summary>
        /// Migration has been created.
        /// </summary>
        Pending,

        /// <summary>
        /// Migration is receiving batches.
        /// </summary>
        Receiving,

        /// <summary>
        /// Migration is sealing.
        /// </summary>
        Sealing,

        /// <summary>
        /// Migration committed successfully.
        /// </summary>
        Committed,

        /// <summary>
        /// Migration was aborted.
        /// </summary>
        Aborted,

        /// <summary>
        /// Migration failed.
        /// </summary>
        Failed
    }
}
