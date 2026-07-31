namespace NetLedger.Archive.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Archive object storage metadata.
    /// </summary>
    public class ArchiveObjectMetadata
    {
        /// <summary>
        /// Whether the object exists in storage.
        /// </summary>
        public bool Exists { get; set; } = false;

        /// <summary>
        /// Object byte count, if known.
        /// </summary>
        public long? ByteCount { get; set; } = null;

        /// <summary>
        /// Object last modified timestamp, if known.
        /// </summary>
        public DateTime? LastModifiedUtc { get; set; } = null;

        /// <summary>
        /// Whether the object is read-only where the storage provider can report it.
        /// </summary>
        public bool? IsReadOnly { get; set; } = null;

        /// <summary>
        /// Provider-specific non-secret metadata.
        /// </summary>
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
