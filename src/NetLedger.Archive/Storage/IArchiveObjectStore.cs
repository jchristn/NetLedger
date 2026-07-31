namespace NetLedger.Archive.Storage
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Archive.Models;

    /// <summary>
    /// Archive object store abstraction.
    /// </summary>
    public interface IArchiveObjectStore
    {
        /// <summary>
        /// Write a temporary object.
        /// </summary>
        /// <param name="relativePath">Relative object path.</param>
        /// <param name="stream">Object stream.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task WriteTemporaryAsync(string relativePath, Stream stream, CancellationToken token = default);

        /// <summary>
        /// Write a temporary object with metadata.
        /// </summary>
        /// <param name="relativePath">Relative object path.</param>
        /// <param name="stream">Object stream.</param>
        /// <param name="metadata">Optional non-secret object metadata.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task WriteTemporaryAsync(string relativePath, Stream stream, Dictionary<string, string>? metadata, CancellationToken token = default);

        /// <summary>
        /// Commit a temporary object.
        /// </summary>
        /// <param name="temporaryRelativePath">Temporary relative path.</param>
        /// <param name="committedRelativePath">Committed relative path.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task CommitAsync(string temporaryRelativePath, string committedRelativePath, CancellationToken token = default);

        /// <summary>
        /// Read an object.
        /// </summary>
        /// <param name="relativePath">Relative object path.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Object stream.</returns>
        Task<Stream> ReadAsync(string relativePath, CancellationToken token = default);

        /// <summary>
        /// Read object metadata.
        /// </summary>
        /// <param name="relativePath">Relative object path.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Object metadata.</returns>
        Task<ArchiveObjectMetadata> ReadMetadataAsync(string relativePath, CancellationToken token = default);

        /// <summary>
        /// Update non-secret object metadata.
        /// </summary>
        /// <param name="relativePath">Relative object path.</param>
        /// <param name="metadata">Metadata to merge into the object.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task UpdateMetadataAsync(string relativePath, Dictionary<string, string> metadata, CancellationToken token = default);

        /// <summary>
        /// Delete a temporary object.
        /// </summary>
        /// <param name="relativePath">Relative object path.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteTemporaryAsync(string relativePath, CancellationToken token = default);
    }
}
