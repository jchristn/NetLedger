namespace NetLedger.Archive.Storage
{
    using System;
    using NetLedger.Archive.Models;
    using NetLedger.Archive.Settings;
    using NetLedger.Telemetry;

    /// <summary>
    /// Creates archive object stores.
    /// </summary>
    public static class ArchiveObjectStoreFactory
    {
        /// <summary>
        /// Create an object store for a storage pool.
        /// </summary>
        /// <param name="pool">Storage pool.</param>
        /// <returns>Object store wrapped with <see cref="InstrumentedArchiveObjectStore"/> telemetry.</returns>
        public static IArchiveObjectStore Create(ArchiveStoragePool pool)
        {
            if (pool == null) throw new ArgumentNullException(nameof(pool));

            switch (pool.Type)
            {
                case ArchiveStoragePoolType.FileSystem:
                    return new InstrumentedArchiveObjectStore(new FileSystemArchiveObjectStore(pool.BasePath ?? String.Empty), TelemetryNames.ServiceFilesystem);

                case ArchiveStoragePoolType.S3:
                    return new InstrumentedArchiveObjectStore(new S3ArchiveObjectStore(new ArchiveStoragePoolSettings
                    {
                        Id = pool.Id,
                        Name = pool.Name,
                        Type = pool.Type,
                        BasePath = pool.BasePath ?? String.Empty,
                        Bucket = pool.Bucket,
                        Prefix = pool.Prefix,
                        Format = pool.Format,
                        Compression = pool.Compression
                    }), TelemetryNames.ServiceS3);

                default:
                    throw new NotSupportedException("Unsupported archive storage pool type '" + pool.Type + "'.");
            }
        }

        /// <summary>
        /// Create an object store for a storage pool settings object.
        /// </summary>
        /// <param name="settings">Storage pool settings.</param>
        /// <returns>Object store wrapped with <see cref="InstrumentedArchiveObjectStore"/> telemetry.</returns>
        public static IArchiveObjectStore Create(ArchiveStoragePoolSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            switch (settings.Type)
            {
                case ArchiveStoragePoolType.FileSystem:
                    return new InstrumentedArchiveObjectStore(new FileSystemArchiveObjectStore(settings.BasePath), TelemetryNames.ServiceFilesystem);

                case ArchiveStoragePoolType.S3:
                    return new InstrumentedArchiveObjectStore(new S3ArchiveObjectStore(settings), TelemetryNames.ServiceS3);

                default:
                    throw new NotSupportedException("Unsupported archive storage pool type '" + settings.Type + "'.");
            }
        }
    }
}
