namespace NetLedger.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.IO;
    using System.IO.Compression;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Archive;
    using NetLedger.Archive.Models;
    using NetLedger.Archive.Requests;
    using NetLedger.Server.Models;
    using NetLedger.Server.Settings;
    using SyslogLogging;

    /// <summary>
    /// Exports active NetLedger rows to NetLedger Archive Server.
    /// </summary>
    internal sealed class ArchiveExportService : IDisposable
    {
        private readonly ServerSettings _Settings;
        private readonly Ledger _Ledger;
        private readonly LoggingModule _Logging;
        private readonly HttpClient _HttpClient;
        private readonly ActiveArchiveBoundaryService _BoundaryService;
        private const int ActiveCleanupBatchRows = 1000;
        private static readonly JsonSerializerOptions _JsonlOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };
        private bool _Disposed = false;

        /// <summary>
        /// Instantiate archive export service.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="ledger">Ledger instance.</param>
        /// <param name="logging">Logging module.</param>
        public ArchiveExportService(ServerSettings settings, Ledger ledger, LoggingModule logging)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _HttpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(10)
            };
            _BoundaryService = new ActiveArchiveBoundaryService(settings);
        }

        /// <summary>
        /// Export committed active ledger entries for one tenant account.
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <param name="exportRequest">Archive export request.</param>
        /// <param name="headers">Inbound request headers to forward for Archive Server authorization.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Archive export response.</returns>
        public async Task<ArchiveExportResponse> ExportEntriesAsync(
            RequestContext req,
            ArchiveExportRequest exportRequest,
            NameValueCollection headers,
            CancellationToken token = default)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));
            if (exportRequest == null) throw new ArgumentNullException(nameof(exportRequest));
            if (headers == null) throw new ArgumentNullException(nameof(headers));

            if (_Settings.Archive == null || !_Settings.Archive.Enabled)
            {
                throw new InvalidOperationException("Archive integration is disabled.");
            }

            string tenantId = FirstNonEmpty(exportRequest.TenantId, req.TenantId, req.Auth?.TenantId);
            string accountId = FirstNonEmpty(exportRequest.AccountId, req.AccountId);
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant ID is required.", nameof(exportRequest));
            if (String.IsNullOrWhiteSpace(accountId)) throw new ArgumentException("Account ID is required.", nameof(exportRequest));
            await WriteActiveArchiveAuditAsync(req, tenantId, "ArchiveExportAttempted", "ArchiveMigration", "Create", accountId, "Attempt", "Entry export requested.", token).ConfigureAwait(false);

            IAsyncDisposable? accountLock = null;
            try
            {
                if (exportRequest.DeleteAfterCommit)
                {
                    accountLock = await _Ledger.Driver.AcquireAccountLockAsync(accountId, token).ConfigureAwait(false);
                }

                Account account = await _Ledger.GetAccountByIdAsync(accountId, token).ConfigureAwait(false);
                if (account == null) throw new KeyNotFoundException("Account was not found.");
                if (!String.Equals(account.TenantId, tenantId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Account does not belong to the requested tenant.");
                }

                DateTime boundaryUtc = ResolveBoundaryUtc(req, exportRequest);
                DateTime fromUtc = (exportRequest.FromUtc ?? DateTime.UnixEpoch).ToUniversalTime();
                DateTime toUtc = (exportRequest.ToUtc ?? boundaryUtc).ToUniversalTime();
                if (toUtc > boundaryUtc)
                {
                    throw new InvalidOperationException("Archive export range must end at or before the active retention boundary.");
                }

                if (toUtc < fromUtc)
                {
                    throw new InvalidOperationException("Archive export ToUtc must be greater than or equal to FromUtc.");
                }

                if (exportRequest.DeleteAfterCommit)
                {
                    await ValidateEntryCleanupPreconditionsAsync(tenantId, accountId, toUtc, token).ConfigureAwait(false);
                }

                ArchiveExportResponse response = new ArchiveExportResponse
                {
                    TenantId = tenantId,
                    AccountId = accountId,
                    ActiveCleanupExecuted = false
                };

                int activePageSize = Math.Clamp(exportRequest.MaxBatchRows, 1, 1000);
                string idempotencyKey = FirstNonEmpty(
                    exportRequest.IdempotencyKey,
                    "netledger-entry-export:" + tenantId + ":" + accountId + ":" + fromUtc.ToString("O", CultureInfo.InvariantCulture) + ":" + toUtc.ToString("O", CultureInfo.InvariantCulture));

                ArchiveMigration? migration = null;
                int skip = 0;
                long sequence = 0;
                bool done = false;
                while (!done)
                {
                    token.ThrowIfCancellationRequested();

                    EnumerationResult<Entry> page = await _Ledger.EnumerateTransactionsAsync(new EnumerationQuery
                    {
                        TenantId = tenantId,
                        AccountId = accountId,
                        CreatedAfterUtc = fromUtc,
                        CreatedBeforeUtc = toUtc,
                        MaxResults = activePageSize,
                        Skip = skip,
                        Ordering = EnumerationOrderEnum.CreatedAscending
                    }, token).ConfigureAwait(false);

                    if (page.Objects == null || page.Objects.Count < 1)
                    {
                        break;
                    }

                    skip += page.Objects.Count;
                    List<Entry> committedEntries = FilterCommittedEntries(page.Objects, tenantId, accountId);
                    if (committedEntries.Count > 0)
                    {
                        if (migration == null)
                        {
                            migration = await CreateMigrationAsync(tenantId, accountId, ArchiveEntityType.Entries, fromUtc, toUtc, exportRequest.StoragePoolId, idempotencyKey, headers, token).ConfigureAwait(false);
                            response.MigrationId = migration.Id;
                        }

                        ArchiveExportBatchResult batchResult = await UploadBatchAsync(migration.Id, committedEntries, sequence, headers, token).ConfigureAwait(false);
                        response.Batches.Add(batchResult);
                        response.RowsExported += batchResult.RowCount;
                        response.BytesUploaded += batchResult.ByteCount;
                        sequence++;
                    }

                    done = page.EndOfResults || page.Objects.Count < activePageSize;
                }

                if (migration == null)
                {
                    await WriteActiveArchiveAuditAsync(req, tenantId, "ArchiveExportCompleted", "ArchiveMigration", "Create", accountId, "NoRows", "No committed entry rows matched the requested range.", token).ConfigureAwait(false);
                    return response;
                }

                await PostArchiveAsync<ArchiveMigration>(BuildArchiveUrl("v1", "archive", "migrations", migration.Id, "seal"), null, headers, null, token).ConfigureAwait(false);
                ArchiveManifest manifest = await PostArchiveAsync<ArchiveManifest>(BuildArchiveUrl("v1", "archive", "migrations", migration.Id, "commit"), null, headers, null, token).ConfigureAwait(false);
                response.ManifestId = manifest.Id;
                if (exportRequest.DeleteAfterCommit)
                {
                    response.ActiveCleanupRowsDeleted = await CleanupArchivedEntriesAsync(req, tenantId, accountId, toUtc, token).ConfigureAwait(false);
                    response.ActiveCleanupExecuted = true;
                }

                await WriteActiveArchiveAuditAsync(req, tenantId, "ArchiveExportCompleted", "ArchiveMigration", "Create", migration.Id, "Permit", "Entry export committed to manifest " + response.ManifestId + ".", token).ConfigureAwait(false);
                _Logging.Info("[ArchiveExportService] exported " + response.RowsExported.ToString(CultureInfo.InvariantCulture) + " entries to archive manifest " + response.ManifestId + ".");
                return response;
            }
            catch (Exception e)
            {
                await WriteActiveArchiveAuditAsync(req, tenantId, "ArchiveExportFailed", "ArchiveMigration", "Create", accountId, "Denied", e.Message, token).ConfigureAwait(false);
                throw;
            }
            finally
            {
                if (accountLock != null)
                {
                    await accountLock.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Export active request history rows for one tenant.
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <param name="exportRequest">Archive export request.</param>
        /// <param name="headers">Inbound request headers to forward for Archive Server authorization.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Archive export response.</returns>
        public async Task<ArchiveExportResponse> ExportRequestHistoryAsync(
            RequestContext req,
            ArchiveExportRequest exportRequest,
            NameValueCollection headers,
            CancellationToken token = default)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));
            if (exportRequest == null) throw new ArgumentNullException(nameof(exportRequest));
            if (headers == null) throw new ArgumentNullException(nameof(headers));

            if (_Settings.Archive == null || !_Settings.Archive.Enabled)
            {
                throw new InvalidOperationException("Archive integration is disabled.");
            }

            string tenantId = FirstNonEmpty(exportRequest.TenantId, req.TenantId, req.Auth?.TenantId);
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant ID is required.", nameof(exportRequest));
            await WriteActiveArchiveAuditAsync(req, tenantId, "ArchiveExportAttempted", "ArchiveMigration", "Create", null, "Attempt", "Request-history export requested.", token).ConfigureAwait(false);

            try
            {
                DateTime boundaryUtc = ResolveBoundaryUtc(req, exportRequest);
                DateTime fromUtc = (exportRequest.FromUtc ?? DateTime.UnixEpoch).ToUniversalTime();
                DateTime toUtc = (exportRequest.ToUtc ?? boundaryUtc).ToUniversalTime();
                if (toUtc > boundaryUtc)
                {
                    throw new InvalidOperationException("Archive export range must end at or before the active retention boundary.");
                }

                if (toUtc < fromUtc)
                {
                    throw new InvalidOperationException("Archive export ToUtc must be greater than or equal to FromUtc.");
                }

                ArchiveExportResponse response = new ArchiveExportResponse
                {
                    TenantId = tenantId,
                    ActiveCleanupExecuted = false
                };

                string idempotencyKey = FirstNonEmpty(
                    exportRequest.IdempotencyKey,
                    "netledger-request-history-export:" + tenantId + ":" + fromUtc.ToString("O", CultureInfo.InvariantCulture) + ":" + toUtc.ToString("O", CultureInfo.InvariantCulture));

                ArchiveMigration? migration = null;
                int skip = 0;
                long sequence = 0;
                bool done = false;
                while (!done)
                {
                    token.ThrowIfCancellationRequested();

                    RequestHistoryResult page = await _Ledger.Driver.RequestHistory.EnumerateAsync(new RequestHistoryFilter
                    {
                        TenantId = tenantId,
                        FromUtc = fromUtc,
                        ToUtc = toUtc,
                        MaxResults = exportRequest.MaxBatchRows,
                        Skip = skip
                    }, token).ConfigureAwait(false);

                    if (page.Objects == null || page.Objects.Count < 1)
                    {
                        break;
                    }

                    skip += page.Objects.Count;
                    if (migration == null)
                    {
                        migration = await CreateMigrationAsync(tenantId, null, ArchiveEntityType.RequestHistory, fromUtc, toUtc, exportRequest.StoragePoolId, idempotencyKey, headers, token).ConfigureAwait(false);
                        response.MigrationId = migration.Id;
                    }

                    ArchiveExportBatchResult batchResult = await UploadBatchAsync(migration.Id, page.Objects, sequence, headers, token).ConfigureAwait(false);
                    response.Batches.Add(batchResult);
                    response.RowsExported += batchResult.RowCount;
                    response.BytesUploaded += batchResult.ByteCount;
                    sequence++;
                    done = page.EndOfResults || page.Objects.Count < exportRequest.MaxBatchRows;
                }

                if (migration == null)
                {
                    await WriteActiveArchiveAuditAsync(req, tenantId, "ArchiveExportCompleted", "ArchiveMigration", "Create", null, "NoRows", "No request-history rows matched the requested range.", token).ConfigureAwait(false);
                    return response;
                }

                await PostArchiveAsync<ArchiveMigration>(BuildArchiveUrl("v1", "archive", "migrations", migration.Id, "seal"), null, headers, null, token).ConfigureAwait(false);
                ArchiveManifest manifest = await PostArchiveAsync<ArchiveManifest>(BuildArchiveUrl("v1", "archive", "migrations", migration.Id, "commit"), null, headers, null, token).ConfigureAwait(false);
                response.ManifestId = manifest.Id;
                if (exportRequest.DeleteAfterCommit)
                {
                    response.ActiveCleanupRowsDeleted = await CleanupArchivedRequestHistoryAsync(tenantId, fromUtc, toUtc, token).ConfigureAwait(false);
                    response.ActiveCleanupExecuted = true;
                }

                await WriteActiveArchiveAuditAsync(req, tenantId, "ArchiveExportCompleted", "ArchiveMigration", "Create", migration.Id, "Permit", "Request-history export committed to manifest " + response.ManifestId + ".", token).ConfigureAwait(false);
                _Logging.Info("[ArchiveExportService] exported " + response.RowsExported.ToString(CultureInfo.InvariantCulture) + " request history rows to archive manifest " + response.ManifestId + ".");
                return response;
            }
            catch (Exception e)
            {
                await WriteActiveArchiveAuditAsync(req, tenantId, "ArchiveExportFailed", "ArchiveMigration", "Create", null, "Denied", e.Message, token).ConfigureAwait(false);
                throw;
            }
        }

        /// <summary>
        /// Dispose managed resources.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            _HttpClient.Dispose();
        }

        private async Task<ArchiveMigration> CreateMigrationAsync(
            string tenantId,
            string? accountId,
            ArchiveEntityType entityType,
            DateTime fromUtc,
            DateTime toUtc,
            string? storagePoolId,
            string idempotencyKey,
            NameValueCollection headers,
            CancellationToken token)
        {
            CreateArchiveMigrationRequest request = new CreateArchiveMigrationRequest
            {
                TenantId = tenantId,
                AccountId = accountId,
                EntityType = entityType,
                StoragePoolId = storagePoolId,
                Format = ArchiveFormat.JsonlGzip,
                Compression = ArchiveCompression.Gzip,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                IdempotencyKey = idempotencyKey
            };

            return await PostArchiveAsync<ArchiveMigration>(BuildArchiveUrl("v1", "archive", "migrations"), request, headers, idempotencyKey, token).ConfigureAwait(false);
        }

        private async Task<ArchiveExportBatchResult> UploadBatchAsync<T>(
            string migrationId,
            List<T> rows,
            long sequence,
            NameValueCollection headers,
            CancellationToken token)
        {
            byte[] payload = BuildJsonlGzip(rows);
            string hash = ToHex(SHA256.HashData(payload));

            CreateArchiveMigrationBatchRequest batchRequest = new CreateArchiveMigrationBatchRequest
            {
                SequenceNumber = sequence,
                RowCount = rows.Count,
                ByteCount = payload.Length,
                ContentHashSha256 = hash
            };

            ArchiveMigrationBatch batch = await PostArchiveAsync<ArchiveMigrationBatch>(
                BuildArchiveUrl("v1", "archive", "migrations", migrationId, "batches"),
                batchRequest,
                headers,
                null,
                token).ConfigureAwait(false);

            using (ByteArrayContent content = new ByteArrayContent(payload))
            {
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/gzip");
                using (HttpRequestMessage upload = new HttpRequestMessage(System.Net.Http.HttpMethod.Put, BuildArchiveUrl("v1", "archive", "migrations", migrationId, "batches", batch.Id, "content")))
                {
                    upload.Content = content;
                    AddForwardedHeaders(upload, headers);
                    upload.Headers.TryAddWithoutValidation("x-content-sha256", hash);
                    using (HttpResponseMessage uploadResponse = await _HttpClient.SendAsync(upload, token).ConfigureAwait(false))
                    {
                        string body = await uploadResponse.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                        if (!uploadResponse.IsSuccessStatusCode)
                        {
                            throw new InvalidOperationException("Archive batch upload failed with HTTP " + ((int)uploadResponse.StatusCode).ToString(CultureInfo.InvariantCulture) + ": " + body);
                        }
                    }
                }
            }

            return new ArchiveExportBatchResult
            {
                BatchId = batch.Id,
                SequenceNumber = sequence,
                RowCount = rows.Count,
                ByteCount = payload.Length,
                ContentHashSha256 = hash
            };
        }

        private async Task ValidateEntryCleanupPreconditionsAsync(string tenantId, string accountId, DateTime toUtc, CancellationToken token)
        {
            bool validBalanceChain = await _Ledger.VerifyBalanceChainAsync(accountId, token).ConfigureAwait(false);
            if (!validBalanceChain)
            {
                throw new InvalidOperationException("Active cleanup requires a valid active balance chain before export.");
            }

            long pendingRows = await _Ledger.Driver.Entries.CountPendingBeforeAsync(accountId, toUtc, token).ConfigureAwait(false);
            if (pendingRows > 0)
            {
                throw new InvalidOperationException("Active cleanup cannot remove an archived range while pending entries remain at or before the archive cutoff.");
            }
        }

        internal async Task<long> CleanupArchivedEntriesAsync(RequestContext req, string tenantId, string accountId, DateTime toUtc, CancellationToken token)
        {
            await ValidateEntryCleanupPreconditionsAsync(tenantId, accountId, toUtc, token).ConfigureAwait(false);

            Entry anchor = await EnsureActiveBalanceAnchorAsync(tenantId, accountId, toUtc, token).ConfigureAwait(false);
            Entry nextBalance = await _Ledger.Driver.Entries.ReadFirstBalanceAfterAsync(accountId, toUtc, token).ConfigureAwait(false);
            if (nextBalance != null && !String.Equals(nextBalance.Id, anchor.Id, StringComparison.Ordinal) &&
                !String.Equals(nextBalance.Replaces, anchor.Id, StringComparison.Ordinal))
            {
                nextBalance.Replaces = anchor.Id;
                await _Ledger.Driver.Entries.UpdateAsync(nextBalance, token).ConfigureAwait(false);
            }

            bool validAnchoredChain = await _Ledger.VerifyBalanceChainAsync(accountId, token).ConfigureAwait(false);
            if (!validAnchoredChain)
            {
                throw new InvalidOperationException("Active cleanup could not establish a valid retained balance anchor.");
            }

            long rowsDeleted = 0L;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                long deleted = await _Ledger.Driver.Entries.DeleteCommittedBeforeAsync(
                    tenantId,
                    accountId,
                    toUtc,
                    ActiveCleanupBatchRows,
                    anchor.Id,
                    token).ConfigureAwait(false);
                rowsDeleted += deleted;
                if (deleted < ActiveCleanupBatchRows)
                {
                    break;
                }
            }

            bool validAfterCleanup = await _Ledger.VerifyBalanceChainAsync(accountId, token).ConfigureAwait(false);
            if (!validAfterCleanup)
            {
                throw new InvalidOperationException("Active balance chain verification failed after archive cleanup.");
            }

            await WriteActiveArchiveAuditAsync(req, tenantId, "ArchiveActiveCleanupCompleted", "Entry", "Delete", accountId, "Permit", "Deleted " + rowsDeleted.ToString(CultureInfo.InvariantCulture) + " committed active rows after archive commit.", token).ConfigureAwait(false);
            return rowsDeleted;
        }

        private async Task<Entry> EnsureActiveBalanceAnchorAsync(string tenantId, string accountId, DateTime toUtc, CancellationToken token)
        {
            DateTime cutoffUtc = toUtc.ToUniversalTime();
            Entry existing = await _Ledger.Driver.Entries.ReadBalanceAsOfAsync(accountId, cutoffUtc, token).ConfigureAwait(false);
            if (IsArchiveBalanceAnchor(existing, tenantId, accountId, cutoffUtc))
            {
                return existing;
            }

            decimal balance = existing?.Amount ?? 0m;
            Entry anchor = new Entry
            {
                Id = NetLedgerId.Generate(IdentifierPrefixes.Entry),
                TenantId = tenantId,
                AccountId = accountId,
                Type = EntryType.Balance,
                Amount = balance,
                Description = BuildArchiveBalanceAnchorDescription(cutoffUtc),
                IsCommitted = true,
                CommittedUtc = cutoffUtc,
                CreatedUtc = cutoffUtc,
                LastUpdateUtc = DateTime.UtcNow
            };

            return await _Ledger.Driver.Entries.CreateAsync(anchor, token).ConfigureAwait(false);
        }

        private async Task<long> CleanupArchivedRequestHistoryAsync(string tenantId, DateTime fromUtc, DateTime toUtc, CancellationToken token)
        {
            RequestHistoryFilter filter = new RequestHistoryFilter
            {
                TenantId = tenantId,
                FromUtc = fromUtc,
                ToUtc = toUtc
            };

            return await _Ledger.Driver.RequestHistory.DeleteManyAsync(filter, token).ConfigureAwait(false);
        }

        private static bool IsArchiveBalanceAnchor(Entry? entry, string tenantId, string accountId, DateTime cutoffUtc)
        {
            if (entry == null) return false;
            if (entry.Type != EntryType.Balance) return false;
            if (!entry.IsCommitted) return false;
            if (!String.Equals(entry.TenantId, tenantId, StringComparison.Ordinal)) return false;
            if (!String.Equals(entry.AccountId, accountId, StringComparison.Ordinal)) return false;
            if (!String.Equals(entry.Description, BuildArchiveBalanceAnchorDescription(cutoffUtc), StringComparison.Ordinal)) return false;
            return entry.CreatedUtc.ToUniversalTime() == cutoffUtc.ToUniversalTime();
        }

        private static string BuildArchiveBalanceAnchorDescription(DateTime cutoffUtc)
        {
            return "Archive balance anchor through " + cutoffUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        }

        private async Task<T> PostArchiveAsync<T>(Uri uri, object? body, NameValueCollection headers, string? idempotencyKey, CancellationToken token)
        {
            using (HttpRequestMessage request = new HttpRequestMessage(System.Net.Http.HttpMethod.Post, uri))
            {
                AddForwardedHeaders(request, headers);
                if (!String.IsNullOrWhiteSpace(idempotencyKey))
                {
                    request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
                }

                if (body != null)
                {
                    string json = JsonSerializer.Serialize(body, NetLedger.Server.Constants.JsonOptions);
                    request.Content = new StringContent(json, Encoding.UTF8, NetLedger.Server.Constants.JsonContentType);
                }

                using (HttpResponseMessage response = await _HttpClient.SendAsync(request, token).ConfigureAwait(false))
                {
                    string responseBody = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Archive Server returned HTTP " + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture) + ": " + responseBody);
                    }

                    T? deserialized = JsonSerializer.Deserialize<T>(responseBody, NetLedger.Server.Constants.JsonOptions);
                    if (deserialized == null)
                    {
                        throw new InvalidOperationException("Archive Server returned an empty response.");
                    }

                    return deserialized;
                }
            }
        }

        private Uri BuildArchiveUrl(params string[] segments)
        {
            string baseUrl = _Settings.Archive.ArchiveServerEndpoint.TrimEnd('/') + "/";
            StringBuilder path = new StringBuilder();
            foreach (string segment in segments)
            {
                if (String.IsNullOrWhiteSpace(segment)) continue;
                if (path.Length > 0) path.Append('/');
                path.Append(Uri.EscapeDataString(segment));
            }

            return new Uri(new Uri(baseUrl, UriKind.Absolute), path.ToString());
        }

        private DateTime ResolveBoundaryUtc(RequestContext req, ArchiveExportRequest exportRequest)
        {
            if (exportRequest.ActiveDataRetentionDaysOverride.HasValue)
            {
                return DateTime.UtcNow.AddDays(-exportRequest.ActiveDataRetentionDaysOverride.Value);
            }

            return _BoundaryService.GetBoundaryUtc(req);
        }

        private static List<Entry> FilterCommittedEntries(List<Entry> entries, string tenantId, string accountId)
        {
            List<Entry> results = new List<Entry>();
            foreach (Entry entry in entries)
            {
                if (!entry.IsCommitted) continue;
                if (!String.Equals(entry.TenantId, tenantId, StringComparison.Ordinal)) continue;
                if (!String.Equals(entry.AccountId, accountId, StringComparison.Ordinal)) continue;
                results.Add(entry);
            }

            return results;
        }

        private static byte[] BuildJsonlGzip<T>(List<T> rows)
        {
            using (MemoryStream output = new MemoryStream())
            {
                using (GZipStream gzip = new GZipStream(output, CompressionLevel.Optimal, true))
                {
                    using (StreamWriter writer = new StreamWriter(gzip, new UTF8Encoding(false)))
                    {
                        foreach (T row in rows)
                        {
                            writer.WriteLine(JsonSerializer.Serialize(row, _JsonlOptions));
                        }
                    }
                }

                return output.ToArray();
            }
        }

        private static void AddForwardedHeaders(HttpRequestMessage request, NameValueCollection headers)
        {
            CopyHeader(headers, request, "authorization");
            CopyHeader(headers, request, "x-token");
            CopyHeader(headers, request, "x-access-key");
            CopyHeader(headers, request, "x-secret-key");
            CopyHeader(headers, request, "x-tenant-id");
        }

        private static void CopyHeader(NameValueCollection headers, HttpRequestMessage request, string name)
        {
            string value = GetHeader(headers, name);
            if (String.IsNullOrWhiteSpace(value)) return;
            request.Headers.TryAddWithoutValidation(name, value);
        }

        private static string GetHeader(NameValueCollection headers, string name)
        {
            if (headers == null) return String.Empty;
            for (int i = 0; i < headers.Count; i++)
            {
                string? key = headers.GetKey(i);
                if (String.IsNullOrEmpty(key)) continue;
                if (String.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return headers.Get(i) ?? String.Empty;
                }
            }

            return String.Empty;
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            foreach (string? value in values)
            {
                if (!String.IsNullOrWhiteSpace(value)) return value;
            }

            return String.Empty;
        }

        private static string ToHex(byte[] bytes)
        {
            StringBuilder builder = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private async Task WriteActiveArchiveAuditAsync(
            RequestContext req,
            string tenantId,
            string eventType,
            string resourceType,
            string operationType,
            string? resourceId,
            string result,
            string? reason,
            CancellationToken token)
        {
            try
            {
                AuditRecord record = new AuditRecord
                {
                    TenantId = tenantId,
                    PrincipalId = req.Auth?.PrincipalId,
                    PrincipalType = req.Auth?.PrincipalType,
                    EventType = eventType,
                    ResourceType = resourceType,
                    OperationType = operationType,
                    ResourceId = resourceId,
                    Result = result,
                    Reason = reason,
                    RequestId = req.RequestId.ToString()
                };

                await _Ledger.Driver.AuditRecords.CreateAsync(record, token).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _Logging.Warn("[ArchiveExportService] failed to write active archive audit record: " + e.Message);
            }
        }
    }
}
