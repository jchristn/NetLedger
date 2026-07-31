namespace NetLedger.Sdk
{
    using System;

    /// <summary>
    /// Archive storage pool health response.
    /// </summary>
    public class ArchiveStoragePoolHealthInfo
    {
        /// <summary>
        /// Indicates whether the storage pool is healthy.
        /// </summary>
        public bool Healthy { get; set; }

        /// <summary>
        /// Storage pool identifier.
        /// </summary>
        public string? StoragePoolId { get; set; }

        /// <summary>
        /// Storage pool type.
        /// </summary>
        public string? Type { get; set; }

        /// <summary>
        /// Health detail.
        /// </summary>
        public string? Detail { get; set; }

        /// <summary>
        /// Timestamp when health was checked.
        /// </summary>
        public DateTime CheckedUtc { get; set; }
    }
}
