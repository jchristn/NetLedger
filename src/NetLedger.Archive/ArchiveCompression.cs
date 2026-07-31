namespace NetLedger.Archive
{
    /// <summary>
    /// Archive compression type.
    /// </summary>
    public enum ArchiveCompression
    {
        /// <summary>
        /// No compression.
        /// </summary>
        None,

        /// <summary>
        /// Gzip compression.
        /// </summary>
        Gzip,

        /// <summary>
        /// Zstandard compression.
        /// </summary>
        Zstd
    }
}
