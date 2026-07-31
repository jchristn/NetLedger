namespace NetLedger.Sdk.Methods
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Sdk.Interfaces;

    /// <summary>
    /// Implementation of Archive Server operations.
    /// </summary>
    internal class ArchiveMethods : IArchiveMethods
    {
        private readonly NetLedgerClient _Client;

        /// <summary>
        /// Instantiate archive methods.
        /// </summary>
        /// <param name="client">NetLedger client.</param>
        internal ArchiveMethods(NetLedgerClient client)
        {
            _Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        /// <inheritdoc />
        public async Task<ArchiveHealth> HealthAsync(CancellationToken cancellationToken = default)
        {
            ApiResponse<ArchiveHealth> response = await _Client.SendAsync<ArchiveHealth>(
                HttpMethod.Get,
                "/v1/health",
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new ArchiveHealth();
        }

        /// <inheritdoc />
        public async Task<List<ArchiveRangeInfo>> RangesAsync(ArchiveQuery? query = null, CancellationToken cancellationToken = default)
        {
            ApiResponse<List<ArchiveRangeInfo>> response = await _Client.SendAsync<List<ArchiveRangeInfo>>(
                HttpMethod.Get,
                "/v1/archive/ranges" + BuildQueryString(query),
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new List<ArchiveRangeInfo>();
        }

        /// <inheritdoc />
        public async Task<List<ArchiveManifestInfo>> ManifestsAsync(ArchiveQuery? query = null, CancellationToken cancellationToken = default)
        {
            ApiResponse<List<ArchiveManifestInfo>> response = await _Client.SendAsync<List<ArchiveManifestInfo>>(
                HttpMethod.Get,
                "/v1/archive/manifests" + BuildQueryString(query),
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new List<ArchiveManifestInfo>();
        }

        /// <inheritdoc />
        public async Task<ArchiveManifestInfo> ManifestAsync(string manifestId, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(manifestId)) throw new ArgumentNullException(nameof(manifestId));

            ApiResponse<ArchiveManifestInfo> response = await _Client.SendAsync<ArchiveManifestInfo>(
                HttpMethod.Get,
                "/v1/archive/manifests/" + Uri.EscapeDataString(manifestId),
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? throw new NetLedgerApiException(response.StatusCode, "No data returned from server.");
        }

        /// <inheritdoc />
        public async Task<List<ArchiveObjectInfo>> ManifestObjectsAsync(string manifestId, ArchiveQuery? query = null, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(manifestId)) throw new ArgumentNullException(nameof(manifestId));

            ApiResponse<List<ArchiveObjectInfo>> response = await _Client.SendAsync<List<ArchiveObjectInfo>>(
                HttpMethod.Get,
                "/v1/archive/manifests/" + Uri.EscapeDataString(manifestId) + "/objects" + BuildQueryString(query),
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new List<ArchiveObjectInfo>();
        }

        /// <inheritdoc />
        public async Task<List<ArchiveBalanceCheckpointInfo>> ManifestCheckpointsAsync(string manifestId, ArchiveQuery? query = null, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(manifestId)) throw new ArgumentNullException(nameof(manifestId));

            ApiResponse<List<ArchiveBalanceCheckpointInfo>> response = await _Client.SendAsync<List<ArchiveBalanceCheckpointInfo>>(
                HttpMethod.Get,
                "/v1/archive/manifests/" + Uri.EscapeDataString(manifestId) + "/checkpoints" + BuildQueryString(query),
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new List<ArchiveBalanceCheckpointInfo>();
        }

        /// <inheritdoc />
        public async Task VerifyManifestAsync(string manifestId, CancellationToken cancellationToken = default)
        {
            await ManifestActionAsync(manifestId, "verify", cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task QuarantineManifestAsync(string manifestId, CancellationToken cancellationToken = default)
        {
            await ManifestActionAsync(manifestId, "quarantine", cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task SupersedeManifestAsync(string manifestId, CancellationToken cancellationToken = default)
        {
            await ManifestActionAsync(manifestId, "supersede", cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<ArchiveStoragePoolInfo>> StoragePoolsAsync(ArchiveQuery? query = null, CancellationToken cancellationToken = default)
        {
            ApiResponse<List<ArchiveStoragePoolInfo>> response = await _Client.SendAsync<List<ArchiveStoragePoolInfo>>(
                HttpMethod.Get,
                "/v1/archive/storage-pools" + BuildQueryString(query),
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new List<ArchiveStoragePoolInfo>();
        }

        /// <inheritdoc />
        public async Task<ArchiveStoragePoolHealthInfo> StoragePoolHealthAsync(string storagePoolId, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(storagePoolId)) throw new ArgumentNullException(nameof(storagePoolId));

            ApiResponse<ArchiveStoragePoolHealthInfo> response = await _Client.SendAsync<ArchiveStoragePoolHealthInfo>(
                HttpMethod.Get,
                "/v1/archive/storage-pools/" + Uri.EscapeDataString(storagePoolId) + "/health",
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new ArchiveStoragePoolHealthInfo();
        }

        /// <inheritdoc />
        public async Task<List<ArchiveMigrationInfo>> MigrationsAsync(ArchiveQuery? query = null, CancellationToken cancellationToken = default)
        {
            ApiResponse<List<ArchiveMigrationInfo>> response = await _Client.SendAsync<List<ArchiveMigrationInfo>>(
                HttpMethod.Get,
                "/v1/archive/migrations" + BuildQueryString(query),
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new List<ArchiveMigrationInfo>();
        }

        /// <inheritdoc />
        public async Task<ArchiveMigrationInfo> MigrationAsync(string migrationId, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(migrationId)) throw new ArgumentNullException(nameof(migrationId));

            ApiResponse<ArchiveMigrationInfo> response = await _Client.SendAsync<ArchiveMigrationInfo>(
                HttpMethod.Get,
                "/v1/archive/migrations/" + Uri.EscapeDataString(migrationId),
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? throw new NetLedgerApiException(response.StatusCode, "No data returned from server.");
        }

        /// <inheritdoc />
        public async Task<List<ArchiveMigrationBatchInfo>> MigrationBatchesAsync(string migrationId, ArchiveQuery? query = null, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(migrationId)) throw new ArgumentNullException(nameof(migrationId));

            ApiResponse<List<ArchiveMigrationBatchInfo>> response = await _Client.SendAsync<List<ArchiveMigrationBatchInfo>>(
                HttpMethod.Get,
                "/v1/archive/migrations/" + Uri.EscapeDataString(migrationId) + "/batches" + BuildQueryString(query),
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new List<ArchiveMigrationBatchInfo>();
        }

        /// <inheritdoc />
        public async Task<ArchiveMigrationInfo> CreateMigrationAsync(ArchiveMigrationRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            ApiResponse<ArchiveMigrationInfo> response = await _Client.SendAsync<ArchiveMigrationInfo>(
                HttpMethod.Post,
                "/v1/archive/migrations",
                request,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? throw new NetLedgerApiException(response.StatusCode, "No data returned from server.");
        }

        /// <inheritdoc />
        public async Task<ArchiveMigrationBatchInfo> CreateMigrationBatchAsync(string migrationId, ArchiveMigrationBatchRequest request, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(migrationId)) throw new ArgumentNullException(nameof(migrationId));
            if (request == null) throw new ArgumentNullException(nameof(request));

            ApiResponse<ArchiveMigrationBatchInfo> response = await _Client.SendAsync<ArchiveMigrationBatchInfo>(
                HttpMethod.Post,
                "/v1/archive/migrations/" + Uri.EscapeDataString(migrationId) + "/batches",
                request,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? throw new NetLedgerApiException(response.StatusCode, "No data returned from server.");
        }

        /// <inheritdoc />
        public async Task<ArchiveMigrationBatchInfo> UploadMigrationBatchContentAsync(string migrationId, string batchId, Stream content, string? contentHashSha256 = null, string contentType = "application/gzip", CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(migrationId)) throw new ArgumentNullException(nameof(migrationId));
            if (String.IsNullOrWhiteSpace(batchId)) throw new ArgumentNullException(nameof(batchId));
            if (content == null) throw new ArgumentNullException(nameof(content));

            StreamContent streamContent = new StreamContent(content);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(String.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);

            Dictionary<string, string>? headers = null;
            if (!String.IsNullOrWhiteSpace(contentHashSha256))
            {
                headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["x-content-sha256"] = contentHashSha256!
                };
            }

            ApiResponse<ArchiveMigrationBatchInfo> response = await _Client.SendContentAsync<ArchiveMigrationBatchInfo>(
                HttpMethod.Put,
                "/v1/archive/migrations/" + Uri.EscapeDataString(migrationId) + "/batches/" + Uri.EscapeDataString(batchId) + "/content",
                streamContent,
                headers,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? throw new NetLedgerApiException(response.StatusCode, "No data returned from server.");
        }

        /// <inheritdoc />
        public async Task<ArchiveMigrationInfo> SealMigrationAsync(string migrationId, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(migrationId)) throw new ArgumentNullException(nameof(migrationId));

            ApiResponse<ArchiveMigrationInfo> response = await _Client.SendAsync<ArchiveMigrationInfo>(
                HttpMethod.Post,
                "/v1/archive/migrations/" + Uri.EscapeDataString(migrationId) + "/seal",
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? throw new NetLedgerApiException(response.StatusCode, "No data returned from server.");
        }

        /// <inheritdoc />
        public async Task<ArchiveManifestInfo> CommitMigrationAsync(string migrationId, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(migrationId)) throw new ArgumentNullException(nameof(migrationId));

            ApiResponse<ArchiveManifestInfo> response = await _Client.SendAsync<ArchiveManifestInfo>(
                HttpMethod.Post,
                "/v1/archive/migrations/" + Uri.EscapeDataString(migrationId) + "/commit",
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? throw new NetLedgerApiException(response.StatusCode, "No data returned from server.");
        }

        /// <inheritdoc />
        public async Task<ArchiveMigrationInfo> AbortMigrationAsync(string migrationId, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(migrationId)) throw new ArgumentNullException(nameof(migrationId));

            ApiResponse<ArchiveMigrationInfo> response = await _Client.SendAsync<ArchiveMigrationInfo>(
                HttpMethod.Post,
                "/v1/archive/migrations/" + Uri.EscapeDataString(migrationId) + "/abort",
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? throw new NetLedgerApiException(response.StatusCode, "No data returned from server.");
        }

        /// <inheritdoc />
        public async Task<ArchiveExportResponse> ExportEntriesAsync(ArchiveExportRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            ApiResponse<ArchiveExportResponse> response = await _Client.SendAsync<ArchiveExportResponse>(
                HttpMethod.Post,
                "/v1/archive/exports/entries",
                request,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new ArchiveExportResponse();
        }

        /// <inheritdoc />
        public async Task<ArchiveExportResponse> ExportRequestHistoryAsync(ArchiveExportRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            ApiResponse<ArchiveExportResponse> response = await _Client.SendAsync<ArchiveExportResponse>(
                HttpMethod.Post,
                "/v1/archive/exports/request-history",
                request,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new ArchiveExportResponse();
        }

        /// <inheritdoc />
        public async Task<ArchiveExportResponse> ExportTenantAccountEntriesAsync(string tenantId, string accountId, ArchiveExportRequest? request = null, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(accountId)) throw new ArgumentNullException(nameof(accountId));

            ApiResponse<ArchiveExportResponse> response = await _Client.SendAsync<ArchiveExportResponse>(
                HttpMethod.Post,
                "/v1/tenants/" + Uri.EscapeDataString(tenantId) + "/accounts/" + Uri.EscapeDataString(accountId) + "/archive/export",
                request ?? new ArchiveExportRequest(),
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new ArchiveExportResponse();
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<Entry>> EntriesAsync(string accountId, ArchiveQuery? query = null, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(accountId)) throw new ArgumentNullException(nameof(accountId));

            ApiResponse<EnumerationResult<Entry>> response = await _Client.SendAsync<EnumerationResult<Entry>>(
                HttpMethod.Get,
                "/v1/archive/accounts/" + Uri.EscapeDataString(accountId) + "/entries" + BuildQueryString(query),
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new EnumerationResult<Entry>();
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<Entry>> TenantEntriesAsync(string tenantId, string accountId, ArchiveQuery? query = null, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(accountId)) throw new ArgumentNullException(nameof(accountId));

            ApiResponse<EnumerationResult<Entry>> response = await _Client.SendAsync<EnumerationResult<Entry>>(
                HttpMethod.Get,
                "/v1/tenants/" + Uri.EscapeDataString(tenantId) + "/accounts/" + Uri.EscapeDataString(accountId) + "/entries" + BuildQueryString(query),
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new EnumerationResult<Entry>();
        }

        /// <inheritdoc />
        public async Task<ArchiveBalanceInfo> BalanceAsOfAsync(string accountId, DateTime asOfUtc, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(accountId)) throw new ArgumentNullException(nameof(accountId));

            ApiResponse<ArchiveBalanceInfo> response = await _Client.SendAsync<ArchiveBalanceInfo>(
                HttpMethod.Get,
                "/v1/archive/accounts/" + Uri.EscapeDataString(accountId) + "/balance/asof?asOf=" + Uri.EscapeDataString(asOfUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? throw new NetLedgerApiException(response.StatusCode, "No data returned from server.");
        }

        /// <inheritdoc />
        public async Task<ArchiveBalanceInfo> TenantBalanceAsOfAsync(string tenantId, string accountId, DateTime asOfUtc, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(accountId)) throw new ArgumentNullException(nameof(accountId));

            ApiResponse<ArchiveBalanceInfo> response = await _Client.SendAsync<ArchiveBalanceInfo>(
                HttpMethod.Get,
                "/v1/tenants/" + Uri.EscapeDataString(tenantId) + "/accounts/" + Uri.EscapeDataString(accountId) + "/balance/asof?asOf=" + Uri.EscapeDataString(asOfUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? throw new NetLedgerApiException(response.StatusCode, "No data returned from server.");
        }

        /// <inheritdoc />
        public async Task<ArchiveVerificationResult> VerifyAccountAsync(string accountId, ArchiveQuery? query = null, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(accountId)) throw new ArgumentNullException(nameof(accountId));

            ApiResponse<ArchiveVerificationResult> response = await _Client.SendAsync<ArchiveVerificationResult>(
                HttpMethod.Get,
                "/v1/archive/accounts/" + Uri.EscapeDataString(accountId) + "/verify" + BuildQueryString(query),
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new ArchiveVerificationResult();
        }

        /// <inheritdoc />
        public async Task<ArchiveVerificationResult> VerifyTenantAccountAsync(string tenantId, string accountId, ArchiveQuery? query = null, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(accountId)) throw new ArgumentNullException(nameof(accountId));

            ApiResponse<ArchiveVerificationResult> response = await _Client.SendAsync<ArchiveVerificationResult>(
                HttpMethod.Get,
                "/v1/tenants/" + Uri.EscapeDataString(tenantId) + "/accounts/" + Uri.EscapeDataString(accountId) + "/verify" + BuildQueryString(query),
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new ArchiveVerificationResult();
        }

        /// <inheritdoc />
        public async Task<ArchiveObjectMetadataInfo> ObjectMetadataAsync(string objectId, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(objectId)) throw new ArgumentNullException(nameof(objectId));

            ApiResponse<ArchiveObjectMetadataInfo> response = await _Client.SendAsync<ArchiveObjectMetadataInfo>(
                HttpMethod.Get,
                "/v1/archive/objects/" + Uri.EscapeDataString(objectId) + "/metadata",
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new ArchiveObjectMetadataInfo();
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<RequestHistoryEntry>> RequestHistoryAsync(RequestHistoryQuery? query = null, CancellationToken cancellationToken = default)
        {
            ApiResponse<EnumerationResult<RequestHistoryEntry>> response = await _Client.SendAsync<EnumerationResult<RequestHistoryEntry>>(
                HttpMethod.Get,
                "/v1/request-history" + BuildRequestHistoryQueryString(query),
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new EnumerationResult<RequestHistoryEntry>();
        }

        /// <inheritdoc />
        public async Task<RequestHistorySummary> RequestHistorySummaryAsync(RequestHistoryQuery? query = null, CancellationToken cancellationToken = default)
        {
            ApiResponse<RequestHistorySummary> response = await _Client.SendAsync<RequestHistorySummary>(
                HttpMethod.Get,
                "/v1/request-history/summary" + BuildRequestHistoryQueryString(query),
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new RequestHistorySummary();
        }

        /// <inheritdoc />
        public async Task<RequestHistoryEntry> RequestHistoryEntryAsync(string id, RequestHistoryQuery? query = null, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));

            ApiResponse<RequestHistoryEntry> response = await _Client.SendAsync<RequestHistoryEntry>(
                HttpMethod.Get,
                "/v1/request-history/" + Uri.EscapeDataString(id) + BuildRequestHistoryQueryString(query),
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? throw new NetLedgerApiException(response.StatusCode, "No data returned from server.");
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<RequestHistoryEntry>> ArchiveServerRequestHistoryAsync(RequestHistoryQuery? query = null, CancellationToken cancellationToken = default)
        {
            ApiResponse<EnumerationResult<RequestHistoryEntry>> response = await _Client.SendAsync<EnumerationResult<RequestHistoryEntry>>(
                HttpMethod.Get,
                "/v1/archive-server/request-history" + BuildRequestHistoryQueryString(query),
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new EnumerationResult<RequestHistoryEntry>();
        }

        /// <inheritdoc />
        public async Task<RequestHistorySummary> ArchiveServerRequestHistorySummaryAsync(RequestHistoryQuery? query = null, CancellationToken cancellationToken = default)
        {
            ApiResponse<RequestHistorySummary> response = await _Client.SendAsync<RequestHistorySummary>(
                HttpMethod.Get,
                "/v1/archive-server/request-history/summary" + BuildRequestHistoryQueryString(query),
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new RequestHistorySummary();
        }

        /// <inheritdoc />
        public async Task<RequestHistoryEntry> ArchiveServerRequestHistoryEntryAsync(string id, RequestHistoryQuery? query = null, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));

            ApiResponse<RequestHistoryEntry> response = await _Client.SendAsync<RequestHistoryEntry>(
                HttpMethod.Get,
                "/v1/archive-server/request-history/" + Uri.EscapeDataString(id) + BuildRequestHistoryQueryString(query),
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? throw new NetLedgerApiException(response.StatusCode, "No data returned from server.");
        }

        private async Task ManifestActionAsync(string manifestId, string action, CancellationToken cancellationToken)
        {
            if (String.IsNullOrWhiteSpace(manifestId)) throw new ArgumentNullException(nameof(manifestId));

            await _Client.SendAsync<object>(
                HttpMethod.Post,
                "/v1/archive/manifests/" + Uri.EscapeDataString(manifestId) + "/" + action,
                null,
                cancellationToken).ConfigureAwait(false);
        }

        private static string BuildQueryString(ArchiveQuery? query)
        {
            if (query == null) return String.Empty;

            List<string> parameters = new List<string>();
            Add(parameters, "maxResults", query.MaxResults.ToString(CultureInfo.InvariantCulture));
            Add(parameters, "skip", query.Skip.ToString(CultureInfo.InvariantCulture));
            Add(parameters, "continuationToken", query.ContinuationToken);
            Add(parameters, "search", query.Search);
            Add(parameters, "tenantId", query.TenantId);
            Add(parameters, "accountId", query.AccountId);
            Add(parameters, "entityType", query.EntityType);
            Add(parameters, "storagePoolId", query.StoragePoolId);
            Add(parameters, "migrationId", query.MigrationId);
            Add(parameters, "manifestStatus", query.ManifestStatus);
            Add(parameters, "migrationStatus", query.MigrationStatus);
            Add(parameters, "fromUtc", query.FromUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            Add(parameters, "toUtc", query.ToUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            Add(parameters, "startTime", query.StartTimeUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            Add(parameters, "endTime", query.EndTimeUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            Add(parameters, "ordering", query.Ordering);
            Add(parameters, "amountMinimum", query.AmountMinimum?.ToString(CultureInfo.InvariantCulture));
            Add(parameters, "amountMaximum", query.AmountMaximum?.ToString(CultureInfo.InvariantCulture));
            Add(parameters, "creditMinimum", query.CreditMinimum?.ToString(CultureInfo.InvariantCulture));
            Add(parameters, "creditMaximum", query.CreditMaximum?.ToString(CultureInfo.InvariantCulture));
            Add(parameters, "debitMinimum", query.DebitMinimum?.ToString(CultureInfo.InvariantCulture));
            Add(parameters, "debitMaximum", query.DebitMaximum?.ToString(CultureInfo.InvariantCulture));
            if (query.Labels != null && query.Labels.Count > 0)
            {
                Add(parameters, "labels", String.Join(",", query.Labels));
            }

            if (query.Tags != null && query.Tags.Count > 0)
            {
                List<string> tags = new List<string>();
                foreach (KeyValuePair<string, string> tag in query.Tags)
                {
                    tags.Add(tag.Key + "=" + tag.Value);
                }

                Add(parameters, "tags", String.Join(",", tags));
            }

            Add(parameters, "allowPartial", query.AllowPartial?.ToString());
            return parameters.Count == 0 ? String.Empty : "?" + String.Join("&", parameters);
        }

        private static string BuildRequestHistoryQueryString(RequestHistoryQuery? query)
        {
            if (query == null) return String.Empty;

            List<string> parameters = new List<string>();
            Add(parameters, "maxResults", query.MaxResults.ToString(CultureInfo.InvariantCulture));
            Add(parameters, "skip", query.Skip.ToString(CultureInfo.InvariantCulture));
            Add(parameters, "continuationToken", query.ContinuationToken);
            Add(parameters, "tenantId", query.TenantId);
            Add(parameters, "principalId", query.PrincipalId);
            Add(parameters, "method", query.Method);
            Add(parameters, "statusCode", query.StatusCode?.ToString(CultureInfo.InvariantCulture));
            Add(parameters, "pathContains", query.PathContains);
            Add(parameters, "fromUtc", query.FromUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            Add(parameters, "toUtc", query.ToUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            Add(parameters, "bucketMinutes", query.BucketMinutes.ToString(CultureInfo.InvariantCulture));
            Add(parameters, "allowPartial", query.AllowPartial?.ToString());
            return parameters.Count == 0 ? String.Empty : "?" + String.Join("&", parameters);
        }

        private static void Add(List<string> parameters, string key, string? value)
        {
            if (String.IsNullOrEmpty(value)) return;
            parameters.Add(Uri.EscapeDataString(key) + "=" + Uri.EscapeDataString(value));
        }
    }
}
