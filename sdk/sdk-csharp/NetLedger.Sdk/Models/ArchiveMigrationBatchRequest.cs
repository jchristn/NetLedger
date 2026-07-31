namespace NetLedger.Sdk
{
    /// <summary>
    /// Request to create Archive Server migration batch metadata.
    /// </summary>
    public class ArchiveMigrationBatchRequest
    {
        /// <summary>
        /// Optional caller-supplied batch identifier.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Sequence number within the migration.
        /// </summary>
        public long SequenceNumber { get; set; }

        /// <summary>
        /// Expected row count.
        /// </summary>
        public long RowCount { get; set; }

        /// <summary>
        /// Optional expected byte count.
        /// </summary>
        public long ByteCount { get; set; }

        /// <summary>
        /// Optional expected SHA-256 content hash.
        /// </summary>
        public string? ContentHashSha256 { get; set; }
    }
}
