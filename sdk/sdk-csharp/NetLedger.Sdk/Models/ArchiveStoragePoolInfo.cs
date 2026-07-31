namespace NetLedger.Sdk
{
    /// <summary>
    /// Archive storage pool metadata.
    /// </summary>
    public class ArchiveStoragePoolInfo
    {
        /// <summary>
        /// Storage pool identifier.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Storage pool name.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Storage type.
        /// </summary>
        public string? Type { get; set; }

        /// <summary>
        /// Storage prefix.
        /// </summary>
        public string? Prefix { get; set; }

        /// <summary>
        /// Archive format.
        /// </summary>
        public string? Format { get; set; }

        /// <summary>
        /// Archive compression.
        /// </summary>
        public string? Compression { get; set; }
    }
}
