namespace NetLedger.Archive
{
    /// <summary>
    /// Archive migration batch status.
    /// </summary>
    public enum ArchiveMigrationBatchStatus
    {
        /// <summary>
        /// Batch has been created but content has not been fully received.
        /// </summary>
        Pending,

        /// <summary>
        /// Batch content has been uploaded.
        /// </summary>
        Uploaded,

        /// <summary>
        /// Batch content has been verified.
        /// </summary>
        Verified,

        /// <summary>
        /// Batch content failed validation.
        /// </summary>
        Failed
    }
}
