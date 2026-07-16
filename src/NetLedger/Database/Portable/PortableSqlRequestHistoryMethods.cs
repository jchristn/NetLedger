namespace NetLedger.Database.Portable
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Database.Interfaces;

    /// <summary>
    /// Portable SQL request history methods.
    /// </summary>
    internal sealed class PortableSqlRequestHistoryMethods : IRequestHistoryMethods
    {
        private readonly PortableSqlDialect _Sql;
        private readonly DatabaseTypeEnum _DatabaseType;

        /// <summary>
        /// Instantiate request history methods.
        /// </summary>
        /// <param name="driver">Database driver.</param>
        /// <param name="databaseType">Database type.</param>
        internal PortableSqlRequestHistoryMethods(DatabaseDriverBase driver, DatabaseTypeEnum databaseType)
        {
            _Sql = new PortableSqlDialect(driver, databaseType);
            _DatabaseType = databaseType;
        }

        /// <inheritdoc />
        public async Task<RequestHistoryEntry> CreateAsync(RequestHistoryEntry entry, CancellationToken token = default)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (String.IsNullOrEmpty(entry.Id)) entry.Id = NetLedgerId.Generate(IdentifierPrefixes.RequestHistory);
            if (entry.CreatedUtc == DateTime.MinValue) entry.CreatedUtc = DateTime.UtcNow;

            string query =
                "INSERT INTO " + _Sql.Table("requesthistory") + " (" +
                _Sql.Columns(
                    "id",
                    "tenantid",
                    "principalid",
                    "principaltype",
                    "method",
                    "path",
                    "url",
                    "statuscode",
                    "durationms",
                    "sourceip",
                    "requestheaders",
                    "requestbody",
                    "requestbodybytes",
                    "requestbodytruncated",
                    "responseheaders",
                    "responsebody",
                    "responsebodybytes",
                    "responsebodytruncated",
                    "createdutc",
                    "completedutc") +
                ") VALUES (" +
                "'" + _Sql.Sanitize(entry.Id) + "', " +
                _Sql.Nullable(entry.TenantId) + ", " +
                _Sql.Nullable(entry.PrincipalId) + ", " +
                _Sql.Nullable(entry.PrincipalType) + ", " +
                "'" + _Sql.Sanitize(entry.Method) + "', " +
                "'" + _Sql.Sanitize(entry.Path) + "', " +
                "'" + _Sql.Sanitize(entry.Url) + "', " +
                entry.StatusCode.ToString(CultureInfo.InvariantCulture) + ", " +
                entry.DurationMs.ToString(CultureInfo.InvariantCulture) + ", " +
                _Sql.Nullable(entry.SourceIp) + ", " +
                "'" + _Sql.Sanitize(SerializeHeaders(entry.RequestHeaders)) + "', " +
                _Sql.Nullable(entry.RequestBody) + ", " +
                entry.RequestBodyBytes.ToString(CultureInfo.InvariantCulture) + ", " +
                _Sql.Bool(entry.RequestBodyTruncated) + ", " +
                "'" + _Sql.Sanitize(SerializeHeaders(entry.ResponseHeaders)) + "', " +
                _Sql.Nullable(entry.ResponseBody) + ", " +
                entry.ResponseBodyBytes.ToString(CultureInfo.InvariantCulture) + ", " +
                _Sql.Bool(entry.ResponseBodyTruncated) + ", " +
                "'" + _Sql.Timestamp(entry.CreatedUtc) + "', " +
                NullableTimestamp(entry.CompletedUtc) + ");";

            await _Sql.ExecuteAsync(query, true, token).ConfigureAwait(false);
            return entry;
        }

        /// <inheritdoc />
        public async Task<RequestHistoryEntry?> ReadAsync(string? tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string where = _Sql.Column("id") + " = '" + _Sql.Sanitize(id) + "'";
            if (!String.IsNullOrEmpty(tenantId))
            {
                where += " AND " + _Sql.Column("tenantid") + " = '" + _Sql.Sanitize(tenantId) + "'";
            }

            DataTable data = await _Sql.QueryOneAsync("requesthistory", where, token).ConfigureAwait(false);
            return data.Rows.Count == 0 ? null : ToEntry(data.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<RequestHistoryResult> EnumerateAsync(RequestHistoryFilter filter, CancellationToken token = default)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));

            string where = BuildWhereClause(filter);
            RequestHistoryResult result = new RequestHistoryResult
            {
                MaxResults = filter.MaxResults,
                Skip = filter.Skip
            };

            DataTable count = await _Sql.ExecuteAsync("SELECT COUNT(*) FROM " + _Sql.Table("requesthistory") + where + ";", false, token).ConfigureAwait(false);
            if (count.Rows.Count > 0)
            {
                result.TotalRecords = Convert.ToInt64(count.Rows[0][0], CultureInfo.InvariantCulture);
            }

            DataTable rows = await _Sql.ExecuteAsync(
                "SELECT * FROM " + _Sql.Table("requesthistory") + where +
                " ORDER BY " + _Sql.Column("createdutc") + " DESC " + LimitOffset(filter) + ";",
                false,
                token).ConfigureAwait(false);

            foreach (DataRow row in rows.Rows)
            {
                result.Objects.Add(ToEntry(row));
            }

            result.RecordsRemaining = Math.Max(0, result.TotalRecords - filter.Skip - result.Objects.Count);
            result.EndOfResults = result.RecordsRemaining == 0;
            return result;
        }

        /// <inheritdoc />
        public async Task<RequestHistorySummary> SummarizeAsync(RequestHistoryFilter filter, CancellationToken token = default)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));

            RequestHistorySummary summary = new RequestHistorySummary();
            DateTime fromUtc = (filter.FromUtc ?? DateTime.UtcNow.AddHours(-24)).ToUniversalTime();
            DateTime toUtc = (filter.ToUtc ?? DateTime.UtcNow).ToUniversalTime();
            if (toUtc < fromUtc)
            {
                DateTime swap = fromUtc;
                fromUtc = toUtc;
                toUtc = swap;
            }

            filter.FromUtc = fromUtc;
            filter.ToUtc = toUtc;

            Dictionary<DateTime, BucketAccumulator> accumulators = BuildEmptyBuckets(fromUtc, toUtc, filter.BucketMinutes);
            string bucketExpression = BuildBucketEpochExpression(filter.BucketMinutes);

            DataTable rows = await _Sql.ExecuteAsync(
                "SELECT " + bucketExpression + " AS bucketepoch, " +
                "COUNT(*) AS totalcount, " +
                "SUM(CASE WHEN " + _Sql.Column("statuscode") + " >= 200 AND " + _Sql.Column("statuscode") + " < 400 THEN 1 ELSE 0 END) AS successcount, " +
                "SUM(CASE WHEN " + _Sql.Column("statuscode") + " >= 200 AND " + _Sql.Column("statuscode") + " < 400 THEN 0 ELSE 1 END) AS failurecount, " +
                "SUM(" + _Sql.Column("durationms") + ") AS durationtotalms " +
                "FROM " + _Sql.Table("requesthistory") + BuildWhereClause(filter) +
                " GROUP BY " + bucketExpression +
                " ORDER BY bucketepoch ASC;",
                false,
                token).ConfigureAwait(false);

            foreach (DataRow row in rows.Rows)
            {
                long bucketEpoch = Convert.ToInt64(Convert.ToDouble(row["bucketepoch"], CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                DateTime bucketStart = DateTimeOffset.FromUnixTimeSeconds(bucketEpoch).UtcDateTime;

                if (!accumulators.ContainsKey(bucketStart))
                {
                    continue;
                }

                BucketAccumulator accumulator = accumulators[bucketStart];
                accumulator.Count = Convert.ToInt32(row["totalcount"], CultureInfo.InvariantCulture);
                accumulator.SuccessCount = Convert.ToInt32(row["successcount"], CultureInfo.InvariantCulture);
                accumulator.FailureCount = Convert.ToInt32(row["failurecount"], CultureInfo.InvariantCulture);
                accumulator.DurationTotalMs = Convert.ToDouble(row["durationtotalms"], CultureInfo.InvariantCulture);
            }

            foreach (BucketAccumulator accumulator in accumulators.Values)
            {
                summary.TotalCount += accumulator.Count;
                summary.TotalSuccess += accumulator.SuccessCount;
                summary.TotalFailure += accumulator.FailureCount;
                summary.AverageDurationMs += accumulator.DurationTotalMs;
                summary.Buckets.Add(new RequestHistorySummaryBucket
                {
                    BucketStartUtc = accumulator.BucketStartUtc,
                    BucketEndUtc = accumulator.BucketEndUtc,
                    SuccessCount = accumulator.SuccessCount,
                    FailureCount = accumulator.FailureCount,
                    AverageDurationMs = accumulator.Count == 0 ? 0 : accumulator.DurationTotalMs / accumulator.Count
                });
            }

            if (summary.TotalCount > 0)
            {
                summary.AverageDurationMs /= summary.TotalCount;
            }

            return summary;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string? tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            RequestHistoryFilter filter = new RequestHistoryFilter
            {
                TenantId = tenantId,
                MaxResults = 1
            };
            string where = _Sql.Column("id") + " = '" + _Sql.Sanitize(id) + "'";
            if (!String.IsNullOrEmpty(filter.TenantId))
            {
                where += " AND " + _Sql.Column("tenantid") + " = '" + _Sql.Sanitize(filter.TenantId) + "'";
            }

            DataTable count = await _Sql.ExecuteAsync("SELECT COUNT(*) FROM " + _Sql.Table("requesthistory") + " WHERE " + where + ";", false, token).ConfigureAwait(false);
            long existing = count.Rows.Count > 0 ? Convert.ToInt64(count.Rows[0][0], CultureInfo.InvariantCulture) : 0;
            if (existing == 0) return false;

            await _Sql.ExecuteAsync("DELETE FROM " + _Sql.Table("requesthistory") + " WHERE " + where + ";", true, token).ConfigureAwait(false);
            return true;
        }

        /// <inheritdoc />
        public async Task<long> DeleteManyAsync(RequestHistoryFilter filter, CancellationToken token = default)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            string where = BuildWhereClause(filter);
            DataTable count = await _Sql.ExecuteAsync("SELECT COUNT(*) FROM " + _Sql.Table("requesthistory") + where + ";", false, token).ConfigureAwait(false);
            long existing = count.Rows.Count > 0 ? Convert.ToInt64(count.Rows[0][0], CultureInfo.InvariantCulture) : 0;
            if (existing == 0) return 0;

            await _Sql.ExecuteAsync("DELETE FROM " + _Sql.Table("requesthistory") + where + ";", true, token).ConfigureAwait(false);
            return existing;
        }

        /// <inheritdoc />
        public async Task<long> PruneAsync(DateTime olderThanUtc, CancellationToken token = default)
        {
            RequestHistoryFilter filter = new RequestHistoryFilter
            {
                ToUtc = olderThanUtc.ToUniversalTime()
            };
            return await DeleteManyAsync(filter, token).ConfigureAwait(false);
        }

        private string BuildWhereClause(RequestHistoryFilter filter)
        {
            List<string> conditions = new List<string>();

            if (!String.IsNullOrEmpty(filter.TenantId)) conditions.Add(_Sql.Column("tenantid") + " = '" + _Sql.Sanitize(filter.TenantId) + "'");
            if (!String.IsNullOrEmpty(filter.PrincipalId)) conditions.Add(_Sql.Column("principalid") + " = '" + _Sql.Sanitize(filter.PrincipalId) + "'");
            if (!String.IsNullOrEmpty(filter.Method)) conditions.Add(_Sql.Column("method") + " = '" + _Sql.Sanitize(filter.Method.ToUpperInvariant()) + "'");
            if (filter.StatusCode.HasValue) conditions.Add(_Sql.Column("statuscode") + " = " + filter.StatusCode.Value.ToString(CultureInfo.InvariantCulture));
            if (!String.IsNullOrEmpty(filter.PathContains)) conditions.Add(_Sql.Column("path") + " LIKE '%" + _Sql.Sanitize(filter.PathContains) + "%'");
            if (filter.FromUtc.HasValue) conditions.Add(_Sql.Column("createdutc") + " >= '" + _Sql.Timestamp(filter.FromUtc.Value) + "'");
            if (filter.ToUtc.HasValue) conditions.Add(_Sql.Column("createdutc") + " <= '" + _Sql.Timestamp(filter.ToUtc.Value) + "'");

            return conditions.Count == 0 ? String.Empty : " WHERE " + String.Join(" AND ", conditions);
        }

        private string LimitOffset(RequestHistoryFilter filter)
        {
            if (_DatabaseType == DatabaseTypeEnum.SqlServer)
            {
                return "OFFSET " + filter.Skip + " ROWS FETCH NEXT " + filter.MaxResults + " ROWS ONLY";
            }

            return "LIMIT " + filter.MaxResults + " OFFSET " + filter.Skip;
        }

        private string BuildBucketEpochExpression(int bucketMinutes)
        {
            int bucketSeconds = Math.Max(1, bucketMinutes) * 60;
            string created = _Sql.Column("createdutc");

            return _DatabaseType switch
            {
                DatabaseTypeEnum.Mysql => "FLOOR(UNIX_TIMESTAMP(" + created + ") / " + bucketSeconds + ") * " + bucketSeconds,
                DatabaseTypeEnum.Postgresql => "FLOOR(EXTRACT(EPOCH FROM " + created + "::timestamp) / " + bucketSeconds + ") * " + bucketSeconds,
                DatabaseTypeEnum.SqlServer => "FLOOR(DATEDIFF_BIG(SECOND, '1970-01-01', " + created + ") / " + bucketSeconds + ".0) * " + bucketSeconds,
                _ => "FLOOR(strftime('%s', " + created + ") / " + bucketSeconds + ") * " + bucketSeconds
            };
        }

        private string NullableTimestamp(DateTime? value)
        {
            return value.HasValue ? "'" + _Sql.Timestamp(value.Value) + "'" : "NULL";
        }

        private RequestHistoryEntry ToEntry(DataRow row)
        {
            return new RequestHistoryEntry
            {
                Id = _Sql.Get(row, "id"),
                TenantId = _Sql.GetNull(row, "tenantid"),
                PrincipalId = _Sql.GetNull(row, "principalid"),
                PrincipalType = _Sql.GetNull(row, "principaltype"),
                Method = _Sql.Get(row, "method"),
                Path = _Sql.Get(row, "path"),
                Url = _Sql.Get(row, "url"),
                StatusCode = Convert.ToInt32(row["statuscode"], CultureInfo.InvariantCulture),
                DurationMs = Convert.ToDouble(row["durationms"], CultureInfo.InvariantCulture),
                SourceIp = _Sql.GetNull(row, "sourceip"),
                RequestHeaders = DeserializeHeaders(_Sql.Get(row, "requestheaders")),
                RequestBody = _Sql.GetNull(row, "requestbody"),
                RequestBodyBytes = Convert.ToInt64(row["requestbodybytes"], CultureInfo.InvariantCulture),
                RequestBodyTruncated = _Sql.GetBool(row, "requestbodytruncated"),
                ResponseHeaders = DeserializeHeaders(_Sql.Get(row, "responseheaders")),
                ResponseBody = _Sql.GetNull(row, "responsebody"),
                ResponseBodyBytes = Convert.ToInt64(row["responsebodybytes"], CultureInfo.InvariantCulture),
                ResponseBodyTruncated = _Sql.GetBool(row, "responsebodytruncated"),
                CreatedUtc = ParseDate(_Sql.Get(row, "createdutc")),
                CompletedUtc = String.IsNullOrEmpty(_Sql.Get(row, "completedutc")) ? null : ParseDate(_Sql.Get(row, "completedutc"))
            };
        }

        private static string SerializeHeaders(Dictionary<string, string> headers)
        {
            return JsonSerializer.Serialize(headers ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }

        private static Dictionary<string, string> DeserializeHeaders(string json)
        {
            if (String.IsNullOrEmpty(json)) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string>? headers = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return headers ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        private static DateTime ParseDate(string value)
        {
            return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        }

        private static DateTime FloorToBucket(DateTime value, int bucketMinutes)
        {
            DateTime utc = value.ToUniversalTime();
            long ticks = TimeSpan.FromMinutes(bucketMinutes).Ticks;
            return new DateTime((utc.Ticks / ticks) * ticks, DateTimeKind.Utc);
        }

        private static Dictionary<DateTime, BucketAccumulator> BuildEmptyBuckets(DateTime fromUtc, DateTime toUtc, int bucketMinutes)
        {
            Dictionary<DateTime, BucketAccumulator> buckets = new Dictionary<DateTime, BucketAccumulator>();
            DateTime current = FloorToBucket(fromUtc, bucketMinutes);
            DateTime last = FloorToBucket(toUtc, bucketMinutes);

            while (current <= last)
            {
                DateTime end = current.AddMinutes(bucketMinutes);
                buckets[current] = new BucketAccumulator
                {
                    BucketStartUtc = current,
                    BucketEndUtc = end
                };
                current = end;
            }

            return buckets;
        }
    }
}
