namespace NetLedger.Archive.Settings
{
    using System;

    /// <summary>
    /// Archive storage pool settings.
    /// </summary>
    public class ArchiveStoragePoolSettings
    {
        /// <summary>
        /// Storage pool identifier.
        /// </summary>
        public string Id { get; set; } = "asp_default";

        /// <summary>
        /// Storage pool name.
        /// </summary>
        public string Name { get; set; } = "Default archive storage";

        /// <summary>
        /// Storage type.
        /// </summary>
        public ArchiveStoragePoolType Type { get; set; } = ArchiveStoragePoolType.FileSystem;

        /// <summary>
        /// Filesystem base path.
        /// </summary>
        public string BasePath { get; set; } = "./archive-data";

        /// <summary>
        /// S3 bucket.
        /// </summary>
        public string? Bucket { get; set; } = null;

        /// <summary>
        /// Storage prefix.
        /// </summary>
        public string Prefix { get; set; } = "default";

        /// <summary>
        /// S3 region.
        /// </summary>
        public string? Region { get; set; } = null;

        /// <summary>
        /// S3-compatible endpoint override.
        /// </summary>
        public string? Endpoint { get; set; } = null;

        /// <summary>
        /// Object-store access key. Prefer environment or secret-manager overrides instead of committed JSON.
        /// </summary>
        public string? AccessKey { get; set; } = null;

        /// <summary>
        /// Object-store secret key. Prefer environment or secret-manager overrides instead of committed JSON.
        /// </summary>
        public string? SecretKey { get; set; } = null;

        /// <summary>
        /// Optional object-store session token.
        /// </summary>
        public string? SessionToken { get; set; } = null;

        /// <summary>
        /// Optional server-side encryption setting.
        /// </summary>
        public string? ServerSideEncryption { get; set; } = null;

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
