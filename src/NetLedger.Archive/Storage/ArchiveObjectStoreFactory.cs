namespace NetLedger.Archive.Storage
{
    using System;
    using NetLedger.Archive.Models;
    using NetLedger.Archive.Settings;

    /// <summary>
    /// Creates archive object stores.
    /// </summary>
    public static class ArchiveObjectStoreFactory
    {
        /// <summary>
        /// Create an object store for a storage pool.
        /// </summary>
        /// <param name="pool">Storage pool.</param>
        /// <returns>Object store.</returns>
        public static IArchiveObjectStore Create(ArchiveStoragePool pool)
        {
            if (pool == null) throw new ArgumentNullException(nameof(pool));

            switch (pool.Type)
            {
                case ArchiveStoragePoolType.FileSystem:
                    return new FileSystemArchiveObjectStore(pool.BasePath ?? String.Empty);

                case ArchiveStoragePoolType.S3:
                    return new S3ArchiveObjectStore(new ArchiveStoragePoolSettings
                    {
                        Id = pool.Id,
                        Name = pool.Name,
                        Type = pool.Type,
                        BasePath = pool.BasePath ?? String.Empty,
                        Bucket = pool.Bucket,
                        Prefix = pool.Prefix,
                        Format = pool.Format,
                        Compression = pool.Compression
                    });

                default:
                    throw new NotSupportedException("Unsupported archive storage pool type '" + pool.Type + "'.");
            }
        }

        /// <summary>
        /// Create an object store for a storage pool settings object.
        /// </summary>
        /// <param name="settings">Storage pool settings.</param>
        /// <returns>Object store.</returns>
        public static IArchiveObjectStore Create(ArchiveStoragePoolSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            switch (settings.Type)
            {
                case ArchiveStoragePoolType.FileSystem:
                    return new FileSystemArchiveObjectStore(settings.BasePath);

                case ArchiveStoragePoolType.S3:
                    return new S3ArchiveObjectStore(settings);

                default:
                    throw new NotSupportedException("Unsupported archive storage pool type '" + settings.Type + "'.");
            }
        }
    }
}
