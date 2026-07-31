namespace NetLedger.Server.Models
{
    /// <summary>
    /// Archive export batch result.
    /// </summary>
    public class ArchiveExportBatchResult
    {
        /// <summary>
        /// Archive migration batch identifier.
        /// </summary>
        public string? BatchId { get; set; } = null;

        /// <summary>
        /// Batch sequence number.
        /// </summary>
        public long SequenceNumber { get; set; } = 0;

        /// <summary>
        /// Number of exported rows.
        /// </summary>
        public long RowCount { get; set; } = 0;

        /// <summary>
        /// Number of uploaded bytes.
        /// </summary>
        public long ByteCount { get; set; } = 0;

        /// <summary>
        /// SHA-256 content hash.
        /// </summary>
        public string? ContentHashSha256 { get; set; } = null;
    }
}
