namespace NetLedger.Archive.Storage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Archive.Models;

    /// <summary>
    /// Filesystem archive object store.
    /// </summary>
    public class FileSystemArchiveObjectStore : IArchiveObjectStore
    {
        private readonly string _BasePath;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="basePath">Base path.</param>
        public FileSystemArchiveObjectStore(string basePath)
        {
            if (String.IsNullOrWhiteSpace(basePath)) throw new ArgumentNullException(nameof(basePath));
            _BasePath = Path.GetFullPath(basePath);
            Directory.CreateDirectory(_BasePath);
        }

        /// <inheritdoc />
        public Task WriteTemporaryAsync(string relativePath, Stream stream, CancellationToken token = default)
        {
            return WriteTemporaryAsync(relativePath, stream, null, token);
        }

        /// <inheritdoc />
        public async Task WriteTemporaryAsync(string relativePath, Stream stream, Dictionary<string, string>? metadata, CancellationToken token = default)
        {
            string path = ResolvePath(relativePath);
            string? directory = Path.GetDirectoryName(path);
            if (!String.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (FileStream file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await stream.CopyToAsync(file, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public Task CommitAsync(string temporaryRelativePath, string committedRelativePath, CancellationToken token = default)
        {
            string temporaryPath = ResolvePath(temporaryRelativePath);
            string committedPath = ResolvePath(committedRelativePath);
            string? directory = Path.GetDirectoryName(committedPath);
            if (!String.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(committedPath))
            {
                if (!File.Exists(temporaryPath))
                {
                    return Task.CompletedTask;
                }

                throw new InvalidOperationException("Committed archive object already exists.");
            }

            if (!File.Exists(temporaryPath))
            {
                throw new FileNotFoundException("Temporary archive object was not found.", temporaryPath);
            }

            File.Move(temporaryPath, committedPath);
            File.SetAttributes(committedPath, File.GetAttributes(committedPath) | FileAttributes.ReadOnly);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<Stream> ReadAsync(string relativePath, CancellationToken token = default)
        {
            string path = ResolvePath(relativePath);
            Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Task.FromResult(stream);
        }

        /// <inheritdoc />
        public Task<ArchiveObjectMetadata> ReadMetadataAsync(string relativePath, CancellationToken token = default)
        {
            string path = ResolvePath(relativePath);
            FileInfo file = new FileInfo(path);
            ArchiveObjectMetadata metadata = new ArchiveObjectMetadata
            {
                Exists = file.Exists
            };

            if (file.Exists)
            {
                metadata.ByteCount = file.Length;
                metadata.LastModifiedUtc = file.LastWriteTimeUtc;
                metadata.IsReadOnly = file.IsReadOnly;
                metadata.Properties["Provider"] = "FileSystem";
            }

            return Task.FromResult(metadata);
        }

        /// <inheritdoc />
        public Task UpdateMetadataAsync(string relativePath, Dictionary<string, string> metadata, CancellationToken token = default)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task DeleteTemporaryAsync(string relativePath, CancellationToken token = default)
        {
            string path = ResolvePath(relativePath);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return Task.CompletedTask;
        }

        private string ResolvePath(string relativePath)
        {
            if (String.IsNullOrWhiteSpace(relativePath)) throw new ArgumentNullException(nameof(relativePath));
            string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
            string path = Path.GetFullPath(Path.Combine(_BasePath, normalized));
            string relative = Path.GetRelativePath(_BasePath, path);
            if (String.IsNullOrWhiteSpace(relative) ||
                relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                String.Equals(relative, "..", StringComparison.Ordinal) ||
                Path.IsPathRooted(relative))
            {
                throw new InvalidOperationException("Archive object path escapes the storage pool base path.");
            }

            return path;
        }
    }
}
