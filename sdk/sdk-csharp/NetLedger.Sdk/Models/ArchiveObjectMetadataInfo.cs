namespace NetLedger.Sdk
{
    /// <summary>
    /// Archive object catalog and storage metadata.
    /// </summary>
    public class ArchiveObjectMetadataInfo
    {
        /// <summary>
        /// Object identifier.
        /// </summary>
        public string? ObjectId { get; set; }

        /// <summary>
        /// Manifest identifier.
        /// </summary>
        public string? ManifestId { get; set; }

        /// <summary>
        /// Storage pool identifier.
        /// </summary>
        public string? StoragePoolId { get; set; }

        /// <summary>
        /// Catalog byte count.
        /// </summary>
        public long CatalogByteCount { get; set; }

        /// <summary>
        /// Catalog content hash.
        /// </summary>
        public string? CatalogContentHashSha256 { get; set; }

        /// <summary>
        /// Storage-provider metadata.
        /// </summary>
        public ArchiveStorageObjectMetadata Storage { get; set; } = new ArchiveStorageObjectMetadata();
    }
}
