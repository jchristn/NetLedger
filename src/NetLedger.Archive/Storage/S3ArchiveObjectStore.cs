namespace NetLedger.Archive.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon;
    using Amazon.Runtime;
    using Amazon.S3;
    using Amazon.S3.Model;
    using Amazon.S3.Transfer;
    using NetLedger.Archive.Models;
    using NetLedger.Archive.Settings;

    /// <summary>
    /// S3-compatible archive object store.
    /// </summary>
    public sealed class S3ArchiveObjectStore : IArchiveObjectStore, IDisposable
    {
        private readonly AmazonS3Client _Client;
        private readonly string _Bucket;
        private readonly string? _ServerSideEncryption;
        private bool _Disposed = false;

        /// <summary>
        /// Instantiate an S3 archive object store.
        /// </summary>
        /// <param name="settings">Storage pool settings.</param>
        public S3ArchiveObjectStore(ArchiveStoragePoolSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (String.IsNullOrWhiteSpace(settings.Bucket)) throw new ArgumentException("S3 archive storage requires a bucket.", nameof(settings));

            _Bucket = settings.Bucket;
            _ServerSideEncryption = settings.ServerSideEncryption;

            AmazonS3Config config = new AmazonS3Config();
            if (!String.IsNullOrWhiteSpace(settings.Region))
            {
                config.RegionEndpoint = RegionEndpoint.GetBySystemName(settings.Region);
            }

            if (!String.IsNullOrWhiteSpace(settings.Endpoint))
            {
                config.ServiceURL = settings.Endpoint;
                config.ForcePathStyle = true;
            }

            AWSCredentials? credentials = BuildCredentials(settings);
            _Client = credentials == null
                ? new AmazonS3Client(config)
                : new AmazonS3Client(credentials, config);
        }

        /// <inheritdoc />
        public Task WriteTemporaryAsync(string relativePath, Stream stream, CancellationToken token = default)
        {
            return WriteTemporaryAsync(relativePath, stream, null, token);
        }

        /// <inheritdoc />
        public async Task WriteTemporaryAsync(string relativePath, Stream stream, Dictionary<string, string>? metadata, CancellationToken token = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            string key = NormalizeKey(relativePath);

            await UploadKeyAsync(key, stream, metadata, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task CommitAsync(string temporaryRelativePath, string committedRelativePath, CancellationToken token = default)
        {
            string temporaryKey = NormalizeKey(temporaryRelativePath);
            string committedKey = NormalizeKey(committedRelativePath);

            ArchiveObjectMetadata existing = await ReadMetadataAsync(committedRelativePath, token).ConfigureAwait(false);
            if (existing.Exists)
            {
                await DeleteObjectIfExistsAsync(temporaryKey, token).ConfigureAwait(false);
                return;
            }

            ArchiveObjectMetadata temporaryMetadata = await ReadMetadataAsync(temporaryRelativePath, token).ConfigureAwait(false);
            if (!temporaryMetadata.Exists)
            {
                throw new FileNotFoundException("Temporary archive object was not found.", temporaryRelativePath);
            }

            using (Stream source = await ReadAsync(temporaryRelativePath, token).ConfigureAwait(false))
            {
                await UploadKeyAsync(committedKey, source, UserMetadataFromObjectMetadata(temporaryMetadata), token).ConfigureAwait(false);
            }

            await DeleteObjectIfExistsAsync(temporaryKey, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<Stream> ReadAsync(string relativePath, CancellationToken token = default)
        {
            GetObjectResponse response = await _Client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _Bucket,
                Key = NormalizeKey(relativePath)
            }, token).ConfigureAwait(false);

            return response.ResponseStream;
        }

        /// <inheritdoc />
        public async Task<ArchiveObjectMetadata> ReadMetadataAsync(string relativePath, CancellationToken token = default)
        {
            try
            {
                GetObjectMetadataResponse response = await _Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
                {
                    BucketName = _Bucket,
                    Key = NormalizeKey(relativePath)
                }, token).ConfigureAwait(false);

                ArchiveObjectMetadata metadata = new ArchiveObjectMetadata
                {
                    Exists = true,
                    ByteCount = response.ContentLength,
                    LastModifiedUtc = response.LastModified.HasValue ? response.LastModified.Value.ToUniversalTime() : null,
                    IsReadOnly = true
                };
                metadata.Properties["Provider"] = "S3";
                metadata.Properties["Bucket"] = _Bucket;
                metadata.Properties["ETag"] = response.ETag ?? String.Empty;

                foreach (string key in response.Metadata.Keys)
                {
                    metadata.Properties[StripMetadataPrefix(key)] = response.Metadata[key];
                }

                return metadata;
            }
            catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
                return new ArchiveObjectMetadata { Exists = false };
            }
        }

        /// <inheritdoc />
        public async Task UpdateMetadataAsync(string relativePath, Dictionary<string, string> metadata, CancellationToken token = default)
        {
            if (metadata == null || metadata.Count == 0) return;
            string key = NormalizeKey(relativePath);

            Dictionary<string, string> merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ArchiveObjectMetadata existing = await ReadMetadataAsync(relativePath, token).ConfigureAwait(false);
            if (!existing.Exists)
            {
                throw new FileNotFoundException("Archive object was not found.", relativePath);
            }

            foreach (KeyValuePair<string, string> property in UserMetadataFromObjectMetadata(existing))
            {
                merged[property.Key] = property.Value;
            }

            foreach (KeyValuePair<string, string> property in metadata)
            {
                merged[property.Key] = property.Value;
            }

            string tempFile = Path.Combine(Path.GetTempPath(), "netledger-archive-s3-" + Guid.NewGuid().ToString("N"));
            try
            {
                using (Stream source = await ReadAsync(relativePath, token).ConfigureAwait(false))
                using (FileStream tempWrite = new FileStream(tempFile, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                {
                    await source.CopyToAsync(tempWrite, token).ConfigureAwait(false);
                }

                using (FileStream tempRead = new FileStream(tempFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    await UploadKeyAsync(key, tempRead, merged, token).ConfigureAwait(false);
                }
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        /// <inheritdoc />
        public async Task DeleteTemporaryAsync(string relativePath, CancellationToken token = default)
        {
            await DeleteObjectIfExistsAsync(NormalizeKey(relativePath), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Dispose managed resources.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            _Client.Dispose();
        }

        private async Task UploadKeyAsync(string key, Stream stream, Dictionary<string, string>? metadata, CancellationToken token)
        {
            TransferUtilityUploadRequest request = new TransferUtilityUploadRequest
            {
                BucketName = _Bucket,
                Key = key,
                InputStream = stream,
                AutoCloseStream = false,
                PartSize = 8 * 1024 * 1024
            };

            ApplyMetadata(request.Metadata, metadata);
            ApplyServerSideEncryption(request);

            using (TransferUtility transfer = new TransferUtility(_Client))
            {
                try
                {
                    await transfer.UploadAsync(request, token).ConfigureAwait(false);
                }
                catch
                {
                    await DeleteObjectIfExistsAsync(key, token).ConfigureAwait(false);
                    throw;
                }
            }
        }

        private async Task DeleteObjectIfExistsAsync(string key, CancellationToken token)
        {
            try
            {
                await _Client.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = _Bucket,
                    Key = key
                }, token).ConfigureAwait(false);
            }
            catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
            }
        }

        private static AWSCredentials? BuildCredentials(ArchiveStoragePoolSettings settings)
        {
            if (String.IsNullOrWhiteSpace(settings.AccessKey) || String.IsNullOrWhiteSpace(settings.SecretKey)) return null;
            if (!String.IsNullOrWhiteSpace(settings.SessionToken))
            {
                return new SessionAWSCredentials(settings.AccessKey, settings.SecretKey, settings.SessionToken);
            }

            return new BasicAWSCredentials(settings.AccessKey, settings.SecretKey);
        }

        private void ApplyServerSideEncryption(TransferUtilityUploadRequest request)
        {
            ServerSideEncryptionMethod? method = ParseServerSideEncryption();
            if (method != null)
            {
                request.ServerSideEncryptionMethod = method;
            }
        }

        private void ApplyServerSideEncryption(CopyObjectRequest request)
        {
            ServerSideEncryptionMethod? method = ParseServerSideEncryption();
            if (method != null)
            {
                request.ServerSideEncryptionMethod = method;
            }
        }

        private ServerSideEncryptionMethod? ParseServerSideEncryption()
        {
            if (String.IsNullOrWhiteSpace(_ServerSideEncryption)) return null;
            if (String.Equals(_ServerSideEncryption, "AES256", StringComparison.OrdinalIgnoreCase)) return ServerSideEncryptionMethod.AES256;
            if (String.Equals(_ServerSideEncryption, "aws:kms", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(_ServerSideEncryption, "KMS", StringComparison.OrdinalIgnoreCase))
            {
                return ServerSideEncryptionMethod.AWSKMS;
            }

            throw new InvalidOperationException("Unsupported S3 server-side encryption setting '" + _ServerSideEncryption + "'.");
        }

        private static void ApplyMetadata(MetadataCollection target, Dictionary<string, string>? metadata)
        {
            if (metadata == null) return;
            foreach (KeyValuePair<string, string> property in metadata)
            {
                if (String.IsNullOrWhiteSpace(property.Key) || property.Value == null) continue;
                target[NormalizeMetadataKey(property.Key)] = property.Value;
            }
        }

        private static Dictionary<string, string> UserMetadataFromObjectMetadata(ArchiveObjectMetadata metadata)
        {
            Dictionary<string, string> ret = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> property in metadata.Properties)
            {
                if (String.Equals(property.Key, "Provider", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(property.Key, "Bucket", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(property.Key, "ETag", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ret[property.Key] = property.Value;
            }

            return ret;
        }

        private static string NormalizeKey(string relativePath)
        {
            if (String.IsNullOrWhiteSpace(relativePath)) throw new ArgumentNullException(nameof(relativePath));
            string key = relativePath.Replace('\\', '/').Trim('/');
            if (String.IsNullOrWhiteSpace(key)) throw new ArgumentException("S3 object key cannot be empty.", nameof(relativePath));
            if (key.Contains("../", StringComparison.Ordinal) || key.Equals("..", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Archive object key contains a parent path segment.");
            }

            return key;
        }

        private static string NormalizeMetadataKey(string key)
        {
            string normalized = key.Trim().ToLowerInvariant();
            return normalized.Replace('_', '-');
        }

        private static string StripMetadataPrefix(string key)
        {
            if (key.StartsWith("x-amz-meta-", StringComparison.OrdinalIgnoreCase))
            {
                return key.Substring("x-amz-meta-".Length);
            }

            return key;
        }
    }
}
