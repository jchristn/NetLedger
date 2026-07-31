namespace NetLedger.Sdk.Methods
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Sdk.Interfaces;

    /// <summary>
    /// Implementation of request history operations.
    /// </summary>
    internal class RequestHistoryMethods : IRequestHistoryMethods
    {
        private readonly NetLedgerClient _Client;

        /// <summary>
        /// Instantiate request history methods.
        /// </summary>
        /// <param name="client">NetLedger client.</param>
        internal RequestHistoryMethods(NetLedgerClient client)
        {
            _Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<RequestHistoryEntry>> EnumerateAsync(RequestHistoryQuery? query = null, CancellationToken cancellationToken = default)
        {
            ApiResponse<EnumerationResult<RequestHistoryEntry>> response = await _Client.SendAsync<EnumerationResult<RequestHistoryEntry>>(
                HttpMethod.Get,
                "/v1.0/api/request-history" + BuildQueryString(query, false),
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new EnumerationResult<RequestHistoryEntry>();
        }

        /// <inheritdoc />
        public async Task<RequestHistorySummary> SummarizeAsync(RequestHistoryQuery? query = null, CancellationToken cancellationToken = default)
        {
            ApiResponse<RequestHistorySummary> response = await _Client.SendAsync<RequestHistorySummary>(
                HttpMethod.Get,
                "/v1.0/api/request-history/summary" + BuildQueryString(query, true),
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? throw new NetLedgerApiException(response.StatusCode, "No data returned from server.");
        }

        /// <inheritdoc />
        public async Task<RequestHistoryEntry> ReadAsync(string id, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));

            ApiResponse<RequestHistoryEntry> response = await _Client.SendAsync<RequestHistoryEntry>(
                HttpMethod.Get,
                "/v1.0/api/request-history/" + Uri.EscapeDataString(id),
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? throw new NetLedgerApiException(response.StatusCode, "No data returned from server.");
        }

        /// <inheritdoc />
        public async Task<RequestHistoryDeleteResult> DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));

            ApiResponse<RequestHistoryDeleteResult> response = await _Client.SendAsync<RequestHistoryDeleteResult>(
                HttpMethod.Delete,
                "/v1.0/api/request-history/" + Uri.EscapeDataString(id),
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new RequestHistoryDeleteResult();
        }

        /// <inheritdoc />
        public async Task<RequestHistoryDeleteResult> DeleteManyAsync(RequestHistoryQuery? query = null, CancellationToken cancellationToken = default)
        {
            ApiResponse<RequestHistoryDeleteResult> response = await _Client.SendAsync<RequestHistoryDeleteResult>(
                HttpMethod.Delete,
                "/v1.0/api/request-history" + BuildQueryString(query, false),
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? new RequestHistoryDeleteResult();
        }

        private static string BuildQueryString(RequestHistoryQuery? query, bool includeBucketMinutes)
        {
            if (query == null) query = new RequestHistoryQuery();

            List<string> parameters = new List<string>();
            Add(parameters, "tenantId", query.TenantId);
            Add(parameters, "principalId", query.PrincipalId);
            Add(parameters, "method", query.Method);
            Add(parameters, "statusCode", query.StatusCode?.ToString(CultureInfo.InvariantCulture));
            Add(parameters, "pathContains", query.PathContains);
            Add(parameters, "fromUtc", query.FromUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            Add(parameters, "toUtc", query.ToUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            Add(parameters, "maxResults", query.MaxResults.ToString(CultureInfo.InvariantCulture));
            Add(parameters, "skip", query.Skip.ToString(CultureInfo.InvariantCulture));
            Add(parameters, "continuationToken", query.ContinuationToken);
            if (includeBucketMinutes)
            {
                Add(parameters, "bucketMinutes", query.BucketMinutes.ToString(CultureInfo.InvariantCulture));
            }

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
