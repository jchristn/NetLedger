namespace NetLedger.Server.Models
{
    using System;
    using System.Collections.Specialized;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using NetLedger;
    using NetLedger.Server.Authentication;
    using WatsonWebserver.Core;

    /// <summary>
    /// Request context containing parsed HTTP request data.
    /// </summary>
    public class RequestContext
    {
        #region Public-Members

        /// <summary>
        /// Unique request identifier.
        /// </summary>
        public string RequestId { get; set; } = NetLedgerId.Generate("req_");

        /// <summary>
        /// Timestamp when the request was received.
        /// </summary>
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// HTTP method.
        /// </summary>
        public HttpMethod Method { get; set; } = HttpMethod.GET;

        /// <summary>
        /// Full URL path.
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Raw URL with query string.
        /// </summary>
        public string RawUrlWithQuery { get; set; } = string.Empty;

        /// <summary>
        /// Source IP address.
        /// </summary>
        public string SourceIp { get; set; } = string.Empty;

        /// <summary>
        /// Content length.
        /// </summary>
        public long ContentLength { get; set; }

        /// <summary>
        /// Request body as bytes.
        /// </summary>
        public byte[]? Data { get; set; }

        /// <summary>
        /// Authentication context.
        /// </summary>
        public AuthContext? Auth { get; set; }

        /// <summary>
        /// Tenant identifier from route or x-tenant-id.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Query string parameters.
        /// </summary>
        public NameValueCollection QueryString { get; set; } = new NameValueCollection();

        /// <summary>
        /// URL parameters.
        /// </summary>
        public NameValueCollection UrlParameters { get; set; } = new NameValueCollection();

        /// <summary>
        /// Account identifier from URL.
        /// </summary>
        public string? AccountId { get; set; }

        /// <summary>
        /// Entry identifier from URL.
        /// </summary>
        public string? EntryId { get; set; }

        /// <summary>
        /// API key identifier from URL.
        /// </summary>
        public string? CredentialId { get; set; }

        /// <summary>
        /// User identifier from URL.
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// Session identifier from URL.
        /// </summary>
        public string? SessionId { get; set; }

        /// <summary>
        /// Account name from URL.
        /// </summary>
        public string? AccountName { get; set; }

        /// <summary>
        /// Max results for pagination. Default 1000, range 1-1000.
        /// </summary>
        public int MaxResults { get; set; } = 1000;

        /// <summary>
        /// Skip count for pagination. Default 0.
        /// </summary>
        public int Skip { get; set; } = 0;

        /// <summary>
        /// Search term for filtering.
        /// </summary>
        public string? SearchTerm { get; set; }

        /// <summary>
        /// Start time filter (UTC).
        /// </summary>
        public DateTime? StartTimeUtc { get; set; }

        /// <summary>
        /// End time filter (UTC).
        /// </summary>
        public DateTime? EndTimeUtc { get; set; }

        /// <summary>
        /// As-of timestamp for historical balance queries (UTC).
        /// </summary>
        public DateTime? AsOfUtc { get; set; }

        /// <summary>
        /// Whether the caller accepts an explicitly partial active-only response when the requested range crosses the active/archive boundary.
        /// </summary>
        public bool AllowPartial { get; set; } = false;

        /// <summary>
        /// Minimum amount filter.
        /// </summary>
        public decimal? AmountMin { get; set; }

        /// <summary>
        /// Maximum amount filter.
        /// </summary>
        public decimal? AmountMax { get; set; }

        /// <summary>
        /// Minimum credit amount filter.
        /// </summary>
        public decimal? CreditMinimum { get; set; }

        /// <summary>
        /// Maximum credit amount filter.
        /// </summary>
        public decimal? CreditMaximum { get; set; }

        /// <summary>
        /// Minimum debit amount filter.
        /// </summary>
        public decimal? DebitMinimum { get; set; }

        /// <summary>
        /// Maximum debit amount filter.
        /// </summary>
        public decimal? DebitMaximum { get; set; }

        /// <summary>
        /// Labels that must all match.
        /// </summary>
        public List<string> Labels { get; set; } = new List<string>();

        /// <summary>
        /// Tags that must all match.
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Minimum balance filter for account enumeration.
        /// </summary>
        public decimal? BalanceMinimum { get; set; }

        /// <summary>
        /// Maximum balance filter for account enumeration.
        /// </summary>
        public decimal? BalanceMaximum { get; set; }

        /// <summary>
        /// Continuation token for pagination.
        /// </summary>
        public string? ContinuationToken { get; set; }

        /// <summary>
        /// Ordering for enumeration results.
        /// </summary>
        public EnumerationOrderEnum Ordering { get; set; } = EnumerationOrderEnum.CreatedDescending;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public RequestContext()
        {
        }

        /// <summary>
        /// Instantiate from HTTP context.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Request context.</returns>
        public static async Task<RequestContext> FromHttpContextAsync(HttpContextBase ctx, CancellationToken token = default)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            RequestContext req = new RequestContext
            {
                Method = ctx.Request.Method,
                Url = ctx.Request.Url.RawWithoutQuery,
                RawUrlWithQuery = ctx.Request.Url.RawWithQuery,
                SourceIp = ctx.Request.Source.IpAddress,
                ContentLength = ctx.Request.ContentLength
            };

            // Parse query string
            if (ctx.Request.Query != null && ctx.Request.Query.Elements != null)
            {
                req.QueryString = ctx.Request.Query.Elements;
            }

            // Parse URL parameters
            if (ctx.Request.Url.Parameters != null)
            {
                req.UrlParameters = ctx.Request.Url.Parameters;
            }

            string? tenantHeader = ctx.Request.Headers.Get("x-tenant-id");
            string? tenantRoute = req.UrlParameters["tenantId"];
            string? tenantQuery = req.QueryString["tenantId"];
            string? tenantCandidate = !String.IsNullOrEmpty(tenantRoute) ? tenantRoute : tenantHeader;
            if (!String.IsNullOrEmpty(tenantCandidate) && !String.IsNullOrEmpty(tenantQuery) && !String.Equals(tenantCandidate, tenantQuery, StringComparison.Ordinal))
            {
                throw new ArgumentException("Tenant query value and route/header tenant value disagree.");
            }
            if (!String.IsNullOrEmpty(tenantHeader) && !String.IsNullOrEmpty(tenantRoute) && !String.Equals(tenantHeader, tenantRoute, StringComparison.Ordinal))
            {
                throw new ArgumentException("Tenant route value and x-tenant-id header disagree.");
            }
            req.TenantId = !String.IsNullOrEmpty(tenantCandidate) ? tenantCandidate : tenantQuery;

            // Extract account identifier from URL
            string? accountIdStr = req.UrlParameters["accountId"];
            if (!string.IsNullOrEmpty(accountIdStr))
            {
                req.AccountId = accountIdStr;
            }

            // Extract entry identifier from URL
            string? entryIdStr = req.UrlParameters["entryId"];
            if (!string.IsNullOrEmpty(entryIdStr))
            {
                req.EntryId = entryIdStr;
            }

            // Extract API key identifier from URL
            string? credentialIdStr = req.UrlParameters["credentialId"];
            if (String.IsNullOrEmpty(credentialIdStr))
            {
                credentialIdStr = req.UrlParameters["credentialId"];
            }
            if (!string.IsNullOrEmpty(credentialIdStr))
            {
                req.CredentialId = credentialIdStr;
            }

            req.UserId = req.UrlParameters["userId"];
            req.SessionId = req.UrlParameters["sessionId"];

            // Extract account name from URL
            req.AccountName = req.UrlParameters["accountName"];

            // Parse query parameters
            ParseQueryParameters(req);

            // Read body data
            if (ctx.Request.ContentLength > 0 && ctx.Request.Data != null)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    await ctx.Request.Data.CopyToAsync(ms, token).ConfigureAwait(false);
                    req.Data = ms.ToArray();
                }
            }

            return req;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Deserialize request body as JSON.
        /// </summary>
        /// <typeparam name="T">Type to deserialize to.</typeparam>
        /// <returns>Deserialized object or default.</returns>
        public T? DeserializeBody<T>() where T : class
        {
            if (Data == null || Data.Length == 0) return null;

            string json = Encoding.UTF8.GetString(Data);
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        #endregion

        #region Private-Methods

        private static void ParseQueryParameters(RequestContext req)
        {
            // MaxResults
            string? maxResultsStr = req.QueryString["maxResults"];
            if (!string.IsNullOrEmpty(maxResultsStr) && int.TryParse(maxResultsStr, out int maxResults))
            {
                req.MaxResults = Math.Clamp(maxResults, 1, 1000);
            }

            // Skip
            string? skipStr = req.QueryString["skip"];
            if (!string.IsNullOrEmpty(skipStr) && int.TryParse(skipStr, out int skip))
            {
                req.Skip = Math.Max(0, skip);
            }

            // SearchTerm
            req.SearchTerm = req.QueryString["search"];

            // StartTimeUtc
            string? startTimeStr = req.QueryString["startTime"];
            if (!string.IsNullOrEmpty(startTimeStr) && DateTime.TryParse(startTimeStr, out DateTime startTime))
            {
                req.StartTimeUtc = startTime.ToUniversalTime();
            }

            // EndTimeUtc
            string? endTimeStr = req.QueryString["endTime"];
            if (!string.IsNullOrEmpty(endTimeStr) && DateTime.TryParse(endTimeStr, out DateTime endTime))
            {
                req.EndTimeUtc = endTime.ToUniversalTime();
            }

            string? allowPartialStr = req.QueryString["allowPartial"];
            if (!String.IsNullOrEmpty(allowPartialStr) && Boolean.TryParse(allowPartialStr, out bool allowPartial))
            {
                req.AllowPartial = allowPartial;
            }

            // AsOfUtc
            string? asOfStr = req.QueryString["asOf"];
            if (!string.IsNullOrEmpty(asOfStr) && DateTime.TryParse(asOfStr, out DateTime asOf))
            {
                req.AsOfUtc = asOf.ToUniversalTime();
            }

            // AmountMin
            string? amountMinStr = req.QueryString["amountMin"];
            if (!string.IsNullOrEmpty(amountMinStr) && decimal.TryParse(amountMinStr, out decimal amountMin))
            {
                req.AmountMin = amountMin;
            }

            // AmountMax
            string? amountMaxStr = req.QueryString["amountMax"];
            if (!string.IsNullOrEmpty(amountMaxStr) && decimal.TryParse(amountMaxStr, out decimal amountMax))
            {
                req.AmountMax = amountMax;
            }

            string? creditMinStr = req.QueryString["creditMin"];
            if (!String.IsNullOrEmpty(creditMinStr) && decimal.TryParse(creditMinStr, out decimal creditMin))
            {
                req.CreditMinimum = creditMin;
            }

            string? creditMaxStr = req.QueryString["creditMax"];
            if (!String.IsNullOrEmpty(creditMaxStr) && decimal.TryParse(creditMaxStr, out decimal creditMax))
            {
                req.CreditMaximum = creditMax;
            }

            string? debitMinStr = req.QueryString["debitMin"];
            if (!String.IsNullOrEmpty(debitMinStr) && decimal.TryParse(debitMinStr, out decimal debitMin))
            {
                req.DebitMinimum = debitMin;
            }

            string? debitMaxStr = req.QueryString["debitMax"];
            if (!String.IsNullOrEmpty(debitMaxStr) && decimal.TryParse(debitMaxStr, out decimal debitMax))
            {
                req.DebitMaximum = debitMax;
            }

            // BalanceMinimum
            string? balanceMinStr = req.QueryString["balanceMin"];
            if (!string.IsNullOrEmpty(balanceMinStr) && decimal.TryParse(balanceMinStr, out decimal balanceMin))
            {
                req.BalanceMinimum = balanceMin;
            }

            // BalanceMaximum
            string? balanceMaxStr = req.QueryString["balanceMax"];
            if (!string.IsNullOrEmpty(balanceMaxStr) && decimal.TryParse(balanceMaxStr, out decimal balanceMax))
            {
                req.BalanceMaximum = balanceMax;
            }

            // ContinuationToken
            string? continuationTokenStr = req.QueryString["continuationToken"];
            if (!string.IsNullOrEmpty(continuationTokenStr))
            {
                req.ContinuationToken = continuationTokenStr;
            }

            // Ordering
            string? orderingStr = req.QueryString["ordering"];
            if (!string.IsNullOrEmpty(orderingStr) && Enum.TryParse<EnumerationOrderEnum>(orderingStr, true, out EnumerationOrderEnum ordering))
            {
                req.Ordering = ordering;
            }

            string? labelsStr = req.QueryString["labels"];
            if (!String.IsNullOrEmpty(labelsStr))
            {
                req.Labels = MetadataValidator.NormalizeLabels(labelsStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }

            string? tagsStr = req.QueryString["tags"];
            if (!String.IsNullOrEmpty(tagsStr))
            {
                Dictionary<string, string> tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string[] pairs = tagsStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (string pair in pairs)
                {
                    string[] parts = pair.Split('=', 2, StringSplitOptions.TrimEntries);
                    if (parts.Length == 2 && !String.IsNullOrEmpty(parts[0]))
                    {
                        tags[parts[0]] = parts[1];
                    }
                }
                req.Tags = MetadataValidator.NormalizeTags(tags);
            }
        }

        #endregion
    }
}




