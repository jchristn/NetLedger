namespace NetLedger.Archive.Server.Models
{
    /// <summary>
    /// Result from receiving archive upload content.
    /// </summary>
    internal class ArchiveUploadResult
    {
        /// <summary>
        /// Number of bytes received.
        /// </summary>
        public long ByteCount { get; set; } = 0;

        /// <summary>
        /// SHA-256 hash for the received content.
        /// </summary>
        public string ContentHashSha256 { get; set; } = string.Empty;
    }
}
