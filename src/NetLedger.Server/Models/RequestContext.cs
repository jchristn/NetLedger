namespace NetLedger.Server.Models
{
    using System;
    using System.Collections.Specialized;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
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
        public Guid RequestGuid { get; set; } = Guid.NewGuid();

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
        /// Query string parameters.
        /// </summary>
        public NameValueCollection QueryString { get; set; } = new NameValueCollection();

        /// <summary>
        /// URL parameters.
        /// </summary>
        public NameValueCollection UrlParameters { get; set; } = new NameValueCollection();

        /// <summary>
        /// Account GUID from URL.
        /// </summary>
        public Guid? AccountGuid { get; set; }

        /// <summary>
        /// Entry GUID from URL.
        /// </summary>
        public Guid? EntryGuid { get; set; }

        /// <summary>
        /// API key GUID from URL.
        /// </summary>
        public Guid? ApiKeyGuid { get; set; }

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
        /// Minimum amount filter.
        /// </summary>
        public decimal? AmountMin { get; set; }

        /// <summary>
        /// Maximum amount filter.
        /// </summary>
        public decimal? AmountMax { get; set; }

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
        public Guid? ContinuationToken { get; set; }

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

            // Extract account GUID from URL
            string? accountGuidStr = req.UrlParameters["accountGuid"];
            if (!string.IsNullOrEmpty(accountGuidStr) && Guid.TryParse(accountGuidStr, out Guid accountGuid))
            {
                req.AccountGuid = accountGuid;
            }

            // Extract entry GUID from URL
            string? entryGuidStr = req.UrlParameters["entryGuid"];
            if (!string.IsNullOrEmpty(entryGuidStr) && Guid.TryParse(entryGuidStr, out Guid entryGuid))
            {
                req.EntryGuid = entryGuid;
            }

            // Extract API key GUID from URL
            string? apiKeyGuidStr = req.UrlParameters["apiKeyGuid"];
            if (!string.IsNullOrEmpty(apiKeyGuidStr) && Guid.TryParse(apiKeyGuidStr, out Guid apiKeyGuid))
            {
                req.ApiKeyGuid = apiKeyGuid;
            }

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
            if (!string.IsNullOrEmpty(continuationTokenStr) && Guid.TryParse(continuationTokenStr, out Guid continuationToken))
            {
                req.ContinuationToken = continuationToken;
            }

            // Ordering
            string? orderingStr = req.QueryString["ordering"];
            if (!string.IsNullOrEmpty(orderingStr) && Enum.TryParse<EnumerationOrderEnum>(orderingStr, true, out EnumerationOrderEnum ordering))
            {
                req.Ordering = ordering;
            }
        }

        #endregion
    }
}
