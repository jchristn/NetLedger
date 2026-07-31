namespace NetLedger.Sdk
{
    using System;

    /// <summary>
    /// Archive object metadata.
    /// </summary>
    public class ArchiveObjectInfo
    {
        /// <summary>
        /// Object identifier.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Manifest identifier.
        /// </summary>
        public string? ManifestId { get; set; }

        /// <summary>
        /// Storage pool identifier.
        /// </summary>
        public string? StoragePoolId { get; set; }

        /// <summary>
        /// Relative path.
        /// </summary>
        public string? RelativePath { get; set; }

        /// <summary>
        /// Row count.
        /// </summary>
        public long RowCount { get; set; }

        /// <summary>
        /// Byte count.
        /// </summary>
        public long ByteCount { get; set; }

        /// <summary>
        /// Content hash.
        /// </summary>
        public string? ContentHashSha256 { get; set; }

        /// <summary>
        /// Created UTC.
        /// </summary>
        public DateTime CreatedUtc { get; set; }
    }
}
