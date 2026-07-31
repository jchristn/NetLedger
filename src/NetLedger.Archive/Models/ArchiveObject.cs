namespace NetLedger.Archive.Models
{
    using System;

    /// <summary>
    /// Archive object model.
    /// </summary>
    public class ArchiveObject
    {
        /// <summary>
        /// Object identifier.
        /// </summary>
        public string Id { get; set; } = ArchiveId.Generate(ArchiveIdentifierPrefixes.Object);

        /// <summary>
        /// Manifest identifier.
        /// </summary>
        public string ManifestId { get; set; } = String.Empty;

        /// <summary>
        /// Storage pool identifier.
        /// </summary>
        public string StoragePoolId { get; set; } = String.Empty;

        /// <summary>
        /// Relative path.
        /// </summary>
        public string RelativePath { get; set; } = String.Empty;

        /// <summary>
        /// Row count.
        /// </summary>
        public long RowCount { get; set; } = 0;

        /// <summary>
        /// Byte count.
        /// </summary>
        public long ByteCount { get; set; } = 0;

        /// <summary>
        /// Content hash.
        /// </summary>
        public string ContentHashSha256 { get; set; } = String.Empty;

        /// <summary>
        /// Created UTC.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }
}
