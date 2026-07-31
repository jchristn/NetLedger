namespace NetLedger.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Archive storage-provider object metadata.
    /// </summary>
    public class ArchiveStorageObjectMetadata
    {
        /// <summary>
        /// Whether the object exists in storage.
        /// </summary>
        public bool Exists { get; set; }

        /// <summary>
        /// Object byte count, when reported by the provider.
        /// </summary>
        public long? ByteCount { get; set; }

        /// <summary>
        /// Last modified UTC timestamp, when reported by the provider.
        /// </summary>
        public DateTime? LastModifiedUtc { get; set; }

        /// <summary>
        /// Whether the object is read-only, when reported by the provider.
        /// </summary>
        public bool? IsReadOnly { get; set; }

        /// <summary>
        /// Provider-specific non-secret metadata.
        /// </summary>
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
