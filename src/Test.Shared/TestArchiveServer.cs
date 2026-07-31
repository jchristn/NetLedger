namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.IO.Compression;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Archive;
    using NetLedger.Archive.Models;
    using NetLedger.Archive.Requests;

    internal sealed class TestArchiveServer : IDisposable
    {
        private readonly object _Sync = new object();
        private readonly Dictionary<string, long> _ExpectedRowsByBatchId = new Dictionary<string, long>();
        private readonly TcpListener _Listener;
        private readonly CancellationTokenSource _TokenSource = new CancellationTokenSource();
        private readonly Task _AcceptTask;
        private int _FailuresBeforeSuccessfulMigrationCreate;
        private int _MigrationCreateAttempts = 0;
        private int _BatchCreateAttempts = 0;
        private int _UploadAttempts = 0;
        private int _SealAttempts = 0;
        private int _CommitAttempts = 0;
        private long _UploadedBytes = 0;
        private long _ExpectedBatchRows = 0;
        private long _ValidatedJsonlRows = 0;
        private DateTime? _LastMigrationFromUtc = null;
        private DateTime? _LastMigrationToUtc = null;
        private bool _Disposed = false;

        internal TestArchiveServer(int failuresBeforeSuccessfulMigrationCreate = 0)
        {
            _FailuresBeforeSuccessfulMigrationCreate = Math.Max(0, failuresBeforeSuccessfulMigrationCreate);
            _Listener = new TcpListener(IPAddress.Loopback, 0);
            _Listener.Start();
            Port = ((IPEndPoint)_Listener.LocalEndpoint).Port;
            Endpoint = "http://127.0.0.1:" + Port.ToString(CultureInfo.InvariantCulture);
            _AcceptTask = Task.Run(() => AcceptLoopAsync(_TokenSource.Token));
        }

        internal string Endpoint { get; private set; }

        internal int Port { get; private set; }

        internal int MigrationCreateAttempts
        {
            get { return Volatile.Read(ref _MigrationCreateAttempts); }
        }

        internal int BatchCreateAttempts
        {
            get { return Volatile.Read(ref _BatchCreateAttempts); }
        }

        internal int UploadAttempts
        {
            get { return Volatile.Read(ref _UploadAttempts); }
        }

        internal int SealAttempts
        {
            get { return Volatile.Read(ref _SealAttempts); }
        }

        internal int CommitAttempts
        {
            get { return Volatile.Read(ref _CommitAttempts); }
        }

        internal long UploadedBytes
        {
            get { return Interlocked.Read(ref _UploadedBytes); }
        }

        internal long ExpectedBatchRows
        {
            get { return Interlocked.Read(ref _ExpectedBatchRows); }
        }

        internal long ValidatedJsonlRows
        {
            get { return Interlocked.Read(ref _ValidatedJsonlRows); }
        }

        internal DateTime? LastMigrationFromUtc
        {
            get
            {
                lock (_Sync)
                {
                    return _LastMigrationFromUtc;
                }
            }
        }

        internal DateTime? LastMigrationToUtc
        {
            get
            {
                lock (_Sync)
                {
                    return _LastMigrationToUtc;
                }
            }
        }

        /// <summary>
        /// Stop the test archive server and release listener resources.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed)
            {
                return;
            }

            _Disposed = true;
            _TokenSource.Cancel();
            _Listener.Stop();
            try
            {
                _AcceptTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
            }

            _TokenSource.Dispose();
        }

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient? client = null;
                try
                {
                    client = await _Listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                    TcpClient acceptedClient = client;
                    client = null;
                    _ = Task.Run(() => HandleClientAsync(acceptedClient, token), token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                finally
                {
                    client?.Dispose();
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] requestBytes = await ReadRequestAsync(stream, token).ConfigureAwait(false);
                    if (requestBytes.Length == 0)
                    {
                        return;
                    }

                    int headerEnd = FindHeaderEnd(requestBytes);
                    if (headerEnd < 0)
                    {
                        await WriteTextAsync(stream, 400, "Bad Request", "Missing header terminator.", token).ConfigureAwait(false);
                        return;
                    }

                    string headerText = Encoding.ASCII.GetString(requestBytes, 0, headerEnd);
                    string[] headerLines = headerText.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                    if (headerLines.Length == 0)
                    {
                        await WriteTextAsync(stream, 400, "Bad Request", "Missing request line.", token).ConfigureAwait(false);
                        return;
                    }

                    string[] requestLine = headerLines[0].Split(' ');
                    if (requestLine.Length < 2)
                    {
                        await WriteTextAsync(stream, 400, "Bad Request", "Invalid request line.", token).ConfigureAwait(false);
                        return;
                    }

                    string method = requestLine[0];
                    string path = requestLine[1].Split('?')[0];
                    int bodyStart = headerEnd + 4;
                    byte[] body = new byte[Math.Max(0, requestBytes.Length - bodyStart)];
                    Buffer.BlockCopy(requestBytes, bodyStart, body, 0, body.Length);

                    await HandleRequestAsync(stream, method, path, body, token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (IOException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private async Task HandleRequestAsync(NetworkStream stream, string method, string path, byte[] body, CancellationToken token)
        {
            string[] segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (String.Equals(method, "POST", StringComparison.OrdinalIgnoreCase) &&
                String.Equals(path, "/v1/archive/migrations", StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref _MigrationCreateAttempts);
                if (ShouldFailMigrationCreate())
                {
                    await WriteTextAsync(stream, 503, "Service Unavailable", "Injected migration failure.", token).ConfigureAwait(false);
                    return;
                }

                CreateArchiveMigrationRequest? request = Deserialize<CreateArchiveMigrationRequest>(body);
                ArchiveMigration migration = new ArchiveMigration
                {
                    TenantId = request?.TenantId ?? String.Empty,
                    AccountId = request?.AccountId,
                    EntityType = request?.EntityType ?? ArchiveEntityType.Entries,
                    StoragePoolId = request?.StoragePoolId ?? String.Empty,
                    Format = request?.Format ?? ArchiveFormat.JsonlGzip,
                    Compression = request?.Compression ?? ArchiveCompression.Gzip,
                    FromUtc = request?.FromUtc ?? DateTime.UtcNow,
                    ToUtc = request?.ToUtc ?? DateTime.UtcNow,
                    IdempotencyKey = request?.IdempotencyKey ?? String.Empty,
                    Status = ArchiveMigrationStatus.Receiving
                };

                lock (_Sync)
                {
                    _LastMigrationFromUtc = migration.FromUtc;
                    _LastMigrationToUtc = migration.ToUtc;
                }

                await WriteJsonAsync(stream, 200, "OK", migration, token).ConfigureAwait(false);
                return;
            }

            if (segments.Length == 5 &&
                String.Equals(method, "POST", StringComparison.OrdinalIgnoreCase) &&
                IsMigrationPath(segments) &&
                String.Equals(segments[4], "batches", StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref _BatchCreateAttempts);
                CreateArchiveMigrationBatchRequest? request = Deserialize<CreateArchiveMigrationBatchRequest>(body);
                Interlocked.Add(ref _ExpectedBatchRows, request?.RowCount ?? 0);

                ArchiveMigrationBatch batch = new ArchiveMigrationBatch
                {
                    MigrationId = segments[3],
                    StoragePoolId = "test",
                    TenantId = "test",
                    SequenceNumber = request?.SequenceNumber ?? 0,
                    RowCount = request?.RowCount ?? 0,
                    ByteCount = request?.ByteCount ?? 0,
                    ContentHashSha256 = request?.ContentHashSha256 ?? String.Empty,
                    Status = ArchiveMigrationBatchStatus.Pending
                };

                lock (_Sync)
                {
                    _ExpectedRowsByBatchId[batch.Id] = batch.RowCount;
                }

                await WriteJsonAsync(stream, 200, "OK", batch, token).ConfigureAwait(false);
                return;
            }

            if (segments.Length == 7 &&
                String.Equals(method, "PUT", StringComparison.OrdinalIgnoreCase) &&
                IsMigrationPath(segments) &&
                String.Equals(segments[4], "batches", StringComparison.OrdinalIgnoreCase) &&
                String.Equals(segments[6], "content", StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref _UploadAttempts);
                Interlocked.Add(ref _UploadedBytes, body.Length);
                if (!TryValidateJsonlGzipUpload(segments[5], body, out long validatedRows, out string validationError))
                {
                    await WriteTextAsync(stream, 400, "Bad Request", validationError, token).ConfigureAwait(false);
                    return;
                }

                Interlocked.Add(ref _ValidatedJsonlRows, validatedRows);
                await WriteTextAsync(stream, 200, "OK", "{}", token).ConfigureAwait(false);
                return;
            }

            if (segments.Length == 5 &&
                String.Equals(method, "POST", StringComparison.OrdinalIgnoreCase) &&
                IsMigrationPath(segments) &&
                String.Equals(segments[4], "seal", StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref _SealAttempts);
                ArchiveMigration migration = new ArchiveMigration
                {
                    Id = segments[3],
                    Status = ArchiveMigrationStatus.Sealing
                };
                await WriteJsonAsync(stream, 200, "OK", migration, token).ConfigureAwait(false);
                return;
            }

            if (segments.Length == 5 &&
                String.Equals(method, "POST", StringComparison.OrdinalIgnoreCase) &&
                IsMigrationPath(segments) &&
                String.Equals(segments[4], "commit", StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref _CommitAttempts);
                ArchiveManifest manifest = new ArchiveManifest
                {
                    MigrationId = segments[3],
                    Status = ArchiveManifestStatus.Committed
                };
                await WriteJsonAsync(stream, 200, "OK", manifest, token).ConfigureAwait(false);
                return;
            }

            await WriteTextAsync(stream, 404, "Not Found", "Not found.", token).ConfigureAwait(false);
        }

        private async Task<byte[]> ReadRequestAsync(NetworkStream stream, CancellationToken token)
        {
            byte[] buffer = new byte[8192];
            using (MemoryStream memory = new MemoryStream())
            {
                int headerEnd = -1;
                int contentLength = 0;
                while (headerEnd < 0)
                {
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                    if (read == 0)
                    {
                        return memory.ToArray();
                    }

                    memory.Write(buffer, 0, read);
                    byte[] current = memory.ToArray();
                    headerEnd = FindHeaderEnd(current);
                    if (headerEnd >= 0)
                    {
                        contentLength = ReadContentLength(current, headerEnd);
                    }
                }

                int expectedLength = headerEnd + 4 + contentLength;
                while (memory.Length < expectedLength)
                {
                    int read = await stream.ReadAsync(buffer, 0, Math.Min(buffer.Length, expectedLength - Convert.ToInt32(memory.Length)), token).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    memory.Write(buffer, 0, read);
                }

                return memory.ToArray();
            }
        }

        private int ReadContentLength(byte[] requestBytes, int headerEnd)
        {
            string headerText = Encoding.ASCII.GetString(requestBytes, 0, headerEnd);
            string[] lines = headerText.Split(new string[] { "\r\n" }, StringSplitOptions.None);
            foreach (string line in lines)
            {
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                {
                    string value = line.Substring("Content-Length:".Length).Trim();
                    if (Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                    {
                        return Math.Max(0, parsed);
                    }
                }
            }

            return 0;
        }

        private int FindHeaderEnd(byte[] bytes)
        {
            for (int i = 0; i < bytes.Length - 3; i++)
            {
                if (bytes[i] == 13 && bytes[i + 1] == 10 && bytes[i + 2] == 13 && bytes[i + 3] == 10)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool IsMigrationPath(string[] segments)
        {
            return segments.Length >= 4 &&
                String.Equals(segments[0], "v1", StringComparison.OrdinalIgnoreCase) &&
                String.Equals(segments[1], "archive", StringComparison.OrdinalIgnoreCase) &&
                String.Equals(segments[2], "migrations", StringComparison.OrdinalIgnoreCase);
        }

        private bool ShouldFailMigrationCreate()
        {
            while (true)
            {
                int current = Volatile.Read(ref _FailuresBeforeSuccessfulMigrationCreate);
                if (current <= 0)
                {
                    return false;
                }

                if (Interlocked.CompareExchange(ref _FailuresBeforeSuccessfulMigrationCreate, current - 1, current) == current)
                {
                    return true;
                }
            }
        }

        private bool TryValidateJsonlGzipUpload(string batchId, byte[] body, out long rows, out string error)
        {
            rows = 0;
            error = String.Empty;
            long expectedRows = -1;
            lock (_Sync)
            {
                if (_ExpectedRowsByBatchId.TryGetValue(batchId, out long storedRows))
                {
                    expectedRows = storedRows;
                }
            }

            try
            {
                using (MemoryStream memory = new MemoryStream(body))
                using (GZipStream gzip = new GZipStream(memory, CompressionMode.Decompress))
                using (StreamReader reader = new StreamReader(gzip, Encoding.UTF8, false))
                {
                    string? line = reader.ReadLine();
                    while (line != null)
                    {
                        if (String.IsNullOrWhiteSpace(line))
                        {
                            error = "Archive batch upload contained an empty JSONL row.";
                            return false;
                        }

                        using (JsonDocument document = JsonDocument.Parse(line))
                        {
                            if (document.RootElement.ValueKind != JsonValueKind.Object)
                            {
                                error = "Archive batch upload JSONL row was not a JSON object.";
                                return false;
                            }
                        }

                        rows++;
                        line = reader.ReadLine();
                    }
                }
            }
            catch (Exception e) when (e is InvalidDataException || e is JsonException || e is IOException)
            {
                error = "Archive batch upload was not valid JSONL.Gzip: " + e.Message;
                return false;
            }

            if (expectedRows >= 0 && rows != expectedRows)
            {
                error = "Archive batch upload row count mismatch. Expected " + expectedRows.ToString(CultureInfo.InvariantCulture) + " row(s) but received " + rows.ToString(CultureInfo.InvariantCulture) + ".";
                return false;
            }

            return true;
        }

        private T? Deserialize<T>(byte[] body)
            where T : class
        {
            if (body == null || body.Length == 0)
            {
                return null;
            }

            return JsonSerializer.Deserialize<T>(body, NetLedger.Server.Constants.JsonOptions);
        }

        private async Task WriteJsonAsync(NetworkStream stream, int statusCode, string statusText, object body, CancellationToken token)
        {
            string json = JsonSerializer.Serialize(body, NetLedger.Server.Constants.JsonOptions);
            await WriteTextAsync(stream, statusCode, statusText, json, token).ConfigureAwait(false);
        }

        private async Task WriteTextAsync(NetworkStream stream, int statusCode, string statusText, string body, CancellationToken token)
        {
            byte[] payload = Encoding.UTF8.GetBytes(body ?? String.Empty);
            string headers =
                "HTTP/1.1 " + statusCode.ToString(CultureInfo.InvariantCulture) + " " + statusText + "\r\n" +
                "Content-Type: application/json\r\n" +
                "Content-Length: " + payload.Length.ToString(CultureInfo.InvariantCulture) + "\r\n" +
                "Connection: close\r\n" +
                "\r\n";
            byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
            await stream.WriteAsync(headerBytes, 0, headerBytes.Length, token).ConfigureAwait(false);
            if (payload.Length > 0)
            {
                await stream.WriteAsync(payload, 0, payload.Length, token).ConfigureAwait(false);
            }
        }
    }
}
