namespace NetLedger.Archive.Storage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Archive.Models;
    using NetLedger.Telemetry;

    /// <summary>
    /// Decorates an archive object store with telemetry: one client span per call named "service operation"
    /// (for example "s3 commit"), the netledger.integration.* duration and outcome metrics, and byte counters.
    /// Object paths are recorded on spans only. <see cref="ArchiveObjectStoreFactory"/> returns stores wrapped by
    /// this decorator. Thread safety: as thread-safe as the inner store.
    /// </summary>
    public sealed class InstrumentedArchiveObjectStore : IArchiveObjectStore, IDisposable
    {
        #region Public-Members

        /// <summary>
        /// The wrapped object store. Never null.
        /// </summary>
        public IArchiveObjectStore Inner
        {
            get { return _Inner; }
        }

        /// <summary>
        /// Bounded storage service label, for example "s3" or "filesystem".
        /// </summary>
        public string Service
        {
            get { return _Service; }
        }

        #endregion

        #region Private-Members

        private const string AttributeObjectPath = "netledger.archive.object.path";
        private readonly IArchiveObjectStore _Inner;
        private readonly string _Service;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Wrap an object store with telemetry.
        /// </summary>
        /// <param name="inner">Inner object store.</param>
        /// <param name="service">Bounded storage service label, for example "s3" or "filesystem".</param>
        /// <exception cref="ArgumentNullException">Thrown when inner or service is null or empty.</exception>
        public InstrumentedArchiveObjectStore(IArchiveObjectStore inner, string service)
        {
            _Inner = inner ?? throw new ArgumentNullException(nameof(inner));
            if (String.IsNullOrEmpty(service)) throw new ArgumentNullException(nameof(service));
            _Service = service;
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public Task WriteTemporaryAsync(string relativePath, Stream stream, CancellationToken token = default)
        {
            return WriteTemporaryAsync(relativePath, stream, null, token);
        }

        /// <inheritdoc />
        public async Task WriteTemporaryAsync(string relativePath, Stream stream, Dictionary<string, string>? metadata, CancellationToken token = default)
        {
            long bytes = TryGetLength(stream);
            using (TelemetryScope telemetry = Start("write_temporary", relativePath))
            {
                try
                {
                    await _Inner.WriteTemporaryAsync(relativePath, stream, metadata, token).ConfigureAwait(false);
                    telemetry.SetTag(TelemetryNames.AttributeByteCount, bytes);
                    NetLedgerTelemetry.RecordStorageBytes(_Service, "write", bytes);
                }
                catch (Exception e)
                {
                    telemetry.Fail(e);
                    NetLedgerTelemetry.RecordError(TelemetryNames.ComponentArchiveStorage, e);
                    throw;
                }
            }
        }

        /// <inheritdoc />
        public async Task CommitAsync(string temporaryRelativePath, string committedRelativePath, CancellationToken token = default)
        {
            using (TelemetryScope telemetry = Start("commit", committedRelativePath))
            {
                try
                {
                    await _Inner.CommitAsync(temporaryRelativePath, committedRelativePath, token).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    telemetry.Fail(e);
                    NetLedgerTelemetry.RecordError(TelemetryNames.ComponentArchiveStorage, e);
                    throw;
                }
            }
        }

        /// <inheritdoc />
        public async Task<Stream> ReadAsync(string relativePath, CancellationToken token = default)
        {
            using (TelemetryScope telemetry = Start("read", relativePath))
            {
                try
                {
                    Stream stream = await _Inner.ReadAsync(relativePath, token).ConfigureAwait(false);
                    long bytes = TryGetLength(stream);
                    telemetry.SetTag(TelemetryNames.AttributeByteCount, bytes);
                    NetLedgerTelemetry.RecordStorageBytes(_Service, "read", bytes);
                    return stream;
                }
                catch (Exception e)
                {
                    telemetry.Fail(e);
                    NetLedgerTelemetry.RecordError(TelemetryNames.ComponentArchiveStorage, e);
                    throw;
                }
            }
        }

        /// <inheritdoc />
        public async Task<ArchiveObjectMetadata> ReadMetadataAsync(string relativePath, CancellationToken token = default)
        {
            using (TelemetryScope telemetry = Start("read_metadata", relativePath))
            {
                try
                {
                    ArchiveObjectMetadata metadata = await _Inner.ReadMetadataAsync(relativePath, token).ConfigureAwait(false);
                    if (metadata != null && !metadata.Exists) telemetry.SetOutcome(TelemetryNames.OutcomeNotFound);
                    return metadata;
                }
                catch (Exception e)
                {
                    telemetry.Fail(e);
                    NetLedgerTelemetry.RecordError(TelemetryNames.ComponentArchiveStorage, e);
                    throw;
                }
            }
        }

        /// <inheritdoc />
        public async Task UpdateMetadataAsync(string relativePath, Dictionary<string, string> metadata, CancellationToken token = default)
        {
            using (TelemetryScope telemetry = Start("update_metadata", relativePath))
            {
                try
                {
                    await _Inner.UpdateMetadataAsync(relativePath, metadata, token).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    telemetry.Fail(e);
                    NetLedgerTelemetry.RecordError(TelemetryNames.ComponentArchiveStorage, e);
                    throw;
                }
            }
        }

        /// <inheritdoc />
        public async Task DeleteTemporaryAsync(string relativePath, CancellationToken token = default)
        {
            using (TelemetryScope telemetry = Start("delete_temporary", relativePath))
            {
                try
                {
                    await _Inner.DeleteTemporaryAsync(relativePath, token).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    telemetry.Fail(e);
                    NetLedgerTelemetry.RecordError(TelemetryNames.ComponentArchiveStorage, e);
                    throw;
                }
            }
        }

        /// <summary>
        /// Dispose the inner store when it is disposable.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            if (_Inner is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        #endregion

        #region Private-Methods

        private TelemetryScope Start(string operation, string? relativePath)
        {
            TelemetryScope scope = NetLedgerTelemetry.StartIntegration(_Service, operation);
            scope.SetTag(AttributeObjectPath, relativePath);
            return scope;
        }

        private static long TryGetLength(Stream? stream)
        {
            try
            {
                if (stream != null && stream.CanSeek) return stream.Length;
            }
            catch (Exception)
            {
            }

            return 0;
        }

        #endregion
    }
}
