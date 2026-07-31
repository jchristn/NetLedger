namespace NetLedger.Archive.Models
{
    using System;

    /// <summary>
    /// Archive storage pool model.
    /// </summary>
    public class ArchiveStoragePool
    {
        /// <summary>
        /// Storage pool identifier.
        /// </summary>
        public string Id { get; set; } = ArchiveId.Generate(ArchiveIdentifierPrefixes.StoragePool);

        /// <summary>
        /// Storage pool name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Storage pool type.
        /// </summary>
        public ArchiveStoragePoolType Type { get; set; } = ArchiveStoragePoolType.FileSystem;

        /// <summary>
        /// Optional base path for filesystem storage.
        /// </summary>
        public string? BasePath { get; set; } = null;

        /// <summary>
        /// Optional S3 bucket.
        /// </summary>
        public string? Bucket { get; set; } = null;

        /// <summary>
        /// Storage prefix.
        /// </summary>
        public string Prefix { get; set; } = String.Empty;

        /// <summary>
        /// Archive format.
        /// </summary>
        public ArchiveFormat Format { get; set; } = ArchiveFormat.JsonlGzip;

        /// <summary>
        /// Archive compression.
        /// </summary>
        public ArchiveCompression Compression { get; set; } = ArchiveCompression.Gzip;
    }
}
