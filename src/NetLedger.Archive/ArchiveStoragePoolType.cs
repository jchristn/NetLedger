namespace NetLedger.Archive
{
    /// <summary>
    /// Archive storage pool type.
    /// </summary>
    public enum ArchiveStoragePoolType
    {
        /// <summary>
        /// Local filesystem storage.
        /// </summary>
        FileSystem,

        /// <summary>
        /// S3-compatible object storage.
        /// </summary>
        S3
    }
}
