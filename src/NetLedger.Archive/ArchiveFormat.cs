namespace NetLedger.Archive
{
    /// <summary>
    /// Archive object format.
    /// </summary>
    public enum ArchiveFormat
    {
        /// <summary>
        /// Gzip-compressed JSON lines.
        /// </summary>
        JsonlGzip,

        /// <summary>
        /// Parquet object format.
        /// </summary>
        Parquet
    }
}
