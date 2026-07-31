namespace NetLedger.Archive.Requests
{
    /// <summary>
    /// Request to create archive migration batch metadata.
    /// </summary>
    public class CreateArchiveMigrationBatchRequest
    {
        /// <summary>
        /// Optional caller-supplied batch identifier.
        /// </summary>
        public string? Id { get; set; } = null;

        /// <summary>
        /// Sequence number within the migration.
        /// </summary>
        public long SequenceNumber { get; set; } = 0;

        /// <summary>
        /// Expected row count.
        /// </summary>
        public long RowCount { get; set; } = 0;

        /// <summary>
        /// Optional expected byte count.
        /// </summary>
        public long ByteCount { get; set; } = 0;

        /// <summary>
        /// Optional expected SHA-256 content hash.
        /// </summary>
        public string? ContentHashSha256 { get; set; } = null;
    }
}
