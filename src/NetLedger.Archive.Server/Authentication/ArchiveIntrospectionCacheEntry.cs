namespace NetLedger.Archive.Server.Authentication
{
    using System;

    /// <summary>
    /// Cached NetLedger introspection result.
    /// </summary>
    internal sealed class ArchiveIntrospectionCacheEntry
    {
        /// <summary>
        /// Authentication context.
        /// </summary>
        public ArchiveAuthContext Context { get; set; } = new ArchiveAuthContext();

        /// <summary>
        /// UTC cache expiration time.
        /// </summary>
        public DateTime ExpiresUtc { get; set; } = DateTime.UtcNow;
    }
}
