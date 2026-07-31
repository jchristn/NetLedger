namespace NetLedger.Database.Sqlite.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Database.Interfaces;
    using NetLedger.Database.Sqlite.Queries;

    /// <summary>
    /// SQLite implementation of entry methods.
    /// </summary>
    internal class EntryMethods : IEntryMethods
    {
        #region Private-Members

        private readonly SqliteDatabaseDriver _Driver;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the entry methods.
        /// </summary>
        /// <param name="driver">SQLite database driver.</param>
        internal EntryMethods(SqliteDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<Entry> CreateAsync(Entry entry, CancellationToken token = default)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            await _Driver.ExecuteQueryAsync(BuildInsertQuery(entry), true, token).ConfigureAwait(false);

            return entry;
        }

        /// <inheritdoc />
        public async Task<List<Entry>> CreateManyAsync(List<Entry> entries, CancellationToken token = default)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            if (entries.Count == 0) return entries;

            List<string> queries = new List<string>();
            foreach (Entry entry in entries)
            {
                queries.Add(BuildInsertQuery(entry));
            }

            await _Driver.ExecuteQueriesAsync(queries, true, token).ConfigureAwait(false);

            return entries;
        }

        /// <inheritdoc />
        public async Task<Entry> ReadByIdAsync(string id, CancellationToken token = default)
        {
            string query = "SELECT * FROM entries WHERE id = '" + Sanitize(id.ToString()) + "' LIMIT 1;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return null;

            return DataRowToEntry(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<List<Entry>> ReadByIdsAsync(List<string> ids, CancellationToken token = default)
        {
            if (ids == null || ids.Count == 0) return new List<Entry>();

            string idList = String.Join(", ", ids.Select(g => "'" + Sanitize(g.ToString()) + "'"));
            string query = "SELECT * FROM entries WHERE id IN (" + idList + ");";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            List<Entry> entries = new List<Entry>();

            if (result != null && result.Rows.Count > 0)
            {
                foreach (DataRow row in result.Rows)
                {
                    entries.Add(DataRowToEntry(row));
                }
            }

            return entries;
        }

        /// <inheritdoc />
        public async Task<List<Entry>> ReadByAccountIdAsync(string accountId, CancellationToken token = default)
        {
            string query = "SELECT * FROM entries WHERE accountguid = '" + accountId.ToString() + "' ORDER BY createdutc DESC;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            List<Entry> entries = new List<Entry>();

            if (result != null && result.Rows.Count > 0)
            {
                foreach (DataRow row in result.Rows)
                {
                    entries.Add(DataRowToEntry(row));
                }
            }

            return entries;
        }

        /// <inheritdoc />
        public async Task<List<Entry>> ReadPendingByAccountIdAsync(string accountId, EntryType? entryType = null, CancellationToken token = default)
        {
            StringBuilder query = new StringBuilder(
                "SELECT * FROM entries WHERE accountguid = '" + accountId.ToString() + "' " +
                "AND committed = 0 " +
                "AND type != '" + EntryType.Balance.ToString() + "'");

            if (entryType.HasValue)
            {
                query.Append(" AND type = '" + entryType.Value.ToString() + "'");
            }

            query.Append(" ORDER BY createdutc ASC;");

            DataTable result = await _Driver.ExecuteQueryAsync(query.ToString(), false, token).ConfigureAwait(false);

            List<Entry> entries = new List<Entry>();

            if (result != null && result.Rows.Count > 0)
            {
                foreach (DataRow row in result.Rows)
                {
                    entries.Add(DataRowToEntry(row));
                }
            }

            return entries;
        }

        /// <inheritdoc />
        public async Task<Entry> ReadLatestBalanceAsync(string accountId, CancellationToken token = default)
        {
            string query =
                "SELECT * FROM entries " +
                "WHERE accountguid = '" + accountId.ToString() + "' " +
                "AND type = '" + EntryType.Balance.ToString() + "' " +
                "ORDER BY createdutc DESC LIMIT 1;";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return null;

            return DataRowToEntry(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<Entry> ReadBalanceAsOfAsync(string accountId, DateTime asOfUtc, CancellationToken token = default)
        {
            string query =
                "SELECT * FROM entries " +
                "WHERE accountguid = '" + accountId.ToString() + "' " +
                "AND type = '" + EntryType.Balance.ToString() + "' " +
                "AND createdutc <= '" + asOfUtc.ToString(SetupQueries.TimestampFormat) + "' " +
                "ORDER BY createdutc DESC LIMIT 1;";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return null;

            return DataRowToEntry(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<Entry> ReadFirstBalanceAfterAsync(string accountId, DateTime afterUtc, CancellationToken token = default)
        {
            string query =
                "SELECT * FROM entries " +
                "WHERE accountguid = '" + Sanitize(accountId) + "' " +
                "AND type = '" + EntryType.Balance.ToString() + "' " +
                "AND createdutc > '" + afterUtc.ToUniversalTime().ToString(SetupQueries.TimestampFormat) + "' " +
                "ORDER BY createdutc ASC, id ASC LIMIT 1;";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return null;

            return DataRowToEntry(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<List<Entry>> ReadWithFilterAsync(string accountId, FilterBuilder filter, CancellationToken token = default)
        {
            StringBuilder query = new StringBuilder(
                "SELECT * FROM entries WHERE accountguid = '" + accountId.ToString() + "'");

            if (filter != null)
            {
                string conditions = filter.BuildEntryConditions(DatabaseTypeEnum.Sqlite);
                if (!String.IsNullOrEmpty(conditions))
                {
                    query.Append(" AND " + conditions);
                }
                query.Append(" " + filter.GetOrderByClause(DatabaseTypeEnum.Sqlite));
                query.Append(" " + filter.GetLimitOffsetClause(DatabaseTypeEnum.Sqlite));
            }
            else
            {
                query.Append(" ORDER BY createdutc DESC");
            }

            query.Append(";");

            DataTable result = await _Driver.ExecuteQueryAsync(query.ToString(), false, token).ConfigureAwait(false);

            List<Entry> entries = new List<Entry>();

            if (result != null && result.Rows.Count > 0)
            {
                foreach (DataRow row in result.Rows)
                {
                    entries.Add(DataRowToEntry(row));
                }
            }

            return entries;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<Entry>> EnumerateAsync(string accountId, EnumerationQuery query, CancellationToken token = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));

            EnumerationResult<Entry> result = new EnumerationResult<Entry>();
            result.MaxResults = query.MaxResults;
            result.Skip = query.Skip;

            // Build filter
            FilterBuilder filter = FilterBuilder.FromEnumerationQuery(query);
            string conditions = filter.BuildEntryConditions(DatabaseTypeEnum.Sqlite);

            // Handle continuation token - get the entry's timestamp and id for filtering
            string continuationCondition = "";
            if (!String.IsNullOrEmpty(query.ContinuationToken))
            {
                Entry continuationEntry = await ReadByIdAsync(query.ContinuationToken, token).ConfigureAwait(false);
                if (continuationEntry != null)
                {
                    continuationCondition = BuildContinuationCondition(query.Ordering, continuationEntry);
                }
            }

            // Get total count (without pagination but with filters)
            StringBuilder countQuery = new StringBuilder(
                "SELECT COUNT(*) FROM entries WHERE accountguid = '" + accountId.ToString() + "'");
            if (!String.IsNullOrEmpty(conditions))
            {
                countQuery.Append(" AND " + conditions);
            }
            countQuery.Append(";");

            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery.ToString(), false, token).ConfigureAwait(false);
            if (countResult != null && countResult.Rows.Count > 0)
            {
                result.TotalRecords = Convert.ToInt64(countResult.Rows[0][0]);
            }

            // Build main query with continuation token or skip
            StringBuilder mainQuery = new StringBuilder(
                "SELECT * FROM entries WHERE accountguid = '" + accountId.ToString() + "'");
            if (!String.IsNullOrEmpty(conditions))
            {
                mainQuery.Append(" AND " + conditions);
            }
            if (!String.IsNullOrEmpty(continuationCondition))
            {
                mainQuery.Append(" AND " + continuationCondition);
            }

            mainQuery.Append(" " + GetEntryOrderByClause(query.Ordering));

            mainQuery.Append(" LIMIT " + query.MaxResults);
            if (query.Skip > 0 && String.IsNullOrEmpty(query.ContinuationToken))
            {
                mainQuery.Append(" OFFSET " + query.Skip);
            }
            mainQuery.Append(";");

            DataTable dataResult = await _Driver.ExecuteQueryAsync(mainQuery.ToString(), false, token).ConfigureAwait(false);

            if (dataResult != null && dataResult.Rows.Count > 0)
            {
                foreach (DataRow row in dataResult.Rows)
                {
                    result.Objects.Add(DataRowToEntry(row));
                }
            }

            // Calculate records remaining based on skip/continuation
            if (!String.IsNullOrEmpty(query.ContinuationToken))
            {
                // For continuation token, count remaining after last entry
                if (result.Objects.Count > 0)
                {
                    Entry lastEntry = result.Objects[result.Objects.Count - 1];
                    StringBuilder remainingQuery = new StringBuilder(
                        "SELECT COUNT(*) FROM entries WHERE accountguid = '" + accountId.ToString() + "'");
                    if (!String.IsNullOrEmpty(conditions))
                    {
                        remainingQuery.Append(" AND " + conditions);
                    }
                    string remainingCondition = BuildContinuationCondition(query.Ordering, lastEntry);
                    if (!String.IsNullOrEmpty(remainingCondition))
                    {
                        remainingQuery.Append(" AND " + remainingCondition);
                    }
                    remainingQuery.Append(";");

                    DataTable remainingResult = await _Driver.ExecuteQueryAsync(remainingQuery.ToString(), false, token).ConfigureAwait(false);
                    if (remainingResult != null && remainingResult.Rows.Count > 0)
                    {
                        result.RecordsRemaining = Convert.ToInt64(remainingResult.Rows[0][0]);
                    }
                }
            }
            else
            {
                // For skip-based pagination
                result.RecordsRemaining = Math.Max(0, result.TotalRecords - query.Skip - result.Objects.Count);
            }

            result.EndOfResults = result.RecordsRemaining == 0;

            // Set continuation token if there are more records
            if (!result.EndOfResults && result.Objects.Count > 0)
            {
                result.ContinuationToken = result.Objects[result.Objects.Count - 1].Id;
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<Entry> UpdateAsync(Entry entry, CancellationToken token = default)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            string query =
                "UPDATE entries SET " +
                "type = '" + entry.Type.ToString() + "', " +
                "amount = " + entry.Amount.ToString() + ", " +
                "description = " + (entry.Description != null ? "'" + Sanitize(entry.Description) + "'" : "NULL") + ", " +
                "replaces = " + (!String.IsNullOrEmpty(entry.Replaces) ? "'" + Sanitize(entry.Replaces) + "'" : "NULL") + ", " +
                "committed = " + (entry.IsCommitted ? "1" : "0") + ", " +
                "committedbyguid = " + (!String.IsNullOrEmpty(entry.CommittedById) ? "'" + Sanitize(entry.CommittedById) + "'" : "NULL") + ", " +
                "committedutc = " + (entry.CommittedUtc.HasValue ? "'" + entry.CommittedUtc.Value.ToString(SetupQueries.TimestampFormat) + "'" : "NULL") + ", " +
                "tenantid = '" + Sanitize(entry.TenantId) + "', " +
                "labels = '" + Sanitize(MetadataSerializer.SerializeLabels(entry.Labels)) + "', " +
                "tags = '" + Sanitize(MetadataSerializer.SerializeTags(entry.Tags)) + "', " +
                "lastupdateutc = '" + DateTime.UtcNow.ToString(SetupQueries.TimestampFormat) + "' " +
                "WHERE id = '" + entry.Id.ToString() + "';";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            return entry;
        }

        /// <inheritdoc />
        public async Task UpdateManyAsync(List<Entry> entries, CancellationToken token = default)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            if (entries.Count == 0) return;

            List<string> queries = new List<string>();
            foreach (Entry entry in entries)
            {
                queries.Add(BuildUpdateQuery(entry));
            }

            await _Driver.ExecuteQueriesAsync(queries, true, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task ApplyCommitAsync(List<Entry> committedEntries, Entry balanceEntry, CancellationToken token = default)
        {
            if (committedEntries == null) throw new ArgumentNullException(nameof(committedEntries));
            if (balanceEntry == null) throw new ArgumentNullException(nameof(balanceEntry));

            List<string> queries = new List<string>();
            queries.Add(BuildInsertQuery(balanceEntry));
            foreach (Entry entry in committedEntries)
            {
                queries.Add(BuildUpdateQuery(entry));
            }

            await _Driver.ExecuteQueriesAsync(queries, true, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task DeleteByIdAsync(string id, CancellationToken token = default)
        {
            string query = "DELETE FROM entries WHERE id = '" + Sanitize(id.ToString()) + "';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task DeleteByAccountIdAsync(string accountId, CancellationToken token = default)
        {
            string query = "DELETE FROM entries WHERE accountguid = '" + accountId.ToString() + "';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<long> DeleteCommittedBeforeAsync(string tenantId, string accountId, DateTime beforeUtc, int maxRows, string? preserveEntryId = null, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(accountId)) throw new ArgumentNullException(nameof(accountId));
            if (maxRows < 1) throw new ArgumentOutOfRangeException(nameof(maxRows), "Max rows must be at least 1.");

            string where =
                "tenantid = '" + Sanitize(tenantId) + "' " +
                "AND accountguid = '" + Sanitize(accountId) + "' " +
                "AND committed = 1 " +
                "AND createdutc <= '" + beforeUtc.ToUniversalTime().ToString(SetupQueries.TimestampFormat) + "'";
            if (!String.IsNullOrWhiteSpace(preserveEntryId))
            {
                where += " AND id != '" + Sanitize(preserveEntryId) + "'";
            }

            string limitedIds = "SELECT id FROM entries WHERE " + where + " ORDER BY createdutc ASC, id ASC LIMIT " + maxRows.ToString(CultureInfo.InvariantCulture);
            DataTable countResult = await _Driver.ExecuteQueryAsync("SELECT COUNT(*) FROM (" + limitedIds + ") AS archivaldeleteids;", false, token).ConfigureAwait(false);
            long deleted = countResult != null && countResult.Rows.Count > 0 ? Convert.ToInt64(countResult.Rows[0][0], CultureInfo.InvariantCulture) : 0L;
            if (deleted == 0) return 0L;

            await _Driver.ExecuteQueryAsync("DELETE FROM entries WHERE id IN (" + limitedIds + ");", true, token).ConfigureAwait(false);
            return deleted;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsByIdAsync(string id, CancellationToken token = default)
        {
            string query = "SELECT COUNT(*) FROM entries WHERE id = '" + Sanitize(id.ToString()) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result != null && result.Rows.Count > 0)
            {
                return Convert.ToInt32(result.Rows[0][0]) > 0;
            }

            return false;
        }

        /// <inheritdoc />
        public async Task<int> GetCountByAccountIdAsync(string accountId, CancellationToken token = default)
        {
            string query = "SELECT COUNT(*) FROM entries WHERE accountguid = '" + accountId.ToString() + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result != null && result.Rows.Count > 0)
            {
                return Convert.ToInt32(result.Rows[0][0]);
            }

            return 0;
        }

        /// <inheritdoc />
        public async Task<long> CountPendingBeforeAsync(string accountId, DateTime beforeUtc, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(accountId)) throw new ArgumentNullException(nameof(accountId));

            string query =
                "SELECT COUNT(*) FROM entries " +
                "WHERE accountguid = '" + Sanitize(accountId) + "' " +
                "AND type != '" + EntryType.Balance.ToString() + "' " +
                "AND committed = 0 " +
                "AND createdutc <= '" + beforeUtc.ToUniversalTime().ToString(SetupQueries.TimestampFormat) + "';";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result != null && result.Rows.Count > 0)
            {
                return Convert.ToInt64(result.Rows[0][0], CultureInfo.InvariantCulture);
            }

            return 0L;
        }

        /// <inheritdoc />
        public async Task<decimal> SumPendingCreditsAsync(string accountId, CancellationToken token = default)
        {
            string query =
                "SELECT COALESCE(SUM(amount), 0) FROM entries " +
                "WHERE accountguid = '" + accountId.ToString() + "' " +
                "AND type = '" + EntryType.Credit.ToString() + "' " +
                "AND committed = 0;";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result != null && result.Rows.Count > 0)
            {
                return Convert.ToDecimal(result.Rows[0][0]);
            }

            return 0m;
        }

        /// <inheritdoc />
        public async Task<decimal> SumPendingDebitsAsync(string accountId, CancellationToken token = default)
        {
            string query =
                "SELECT COALESCE(SUM(amount), 0) FROM entries " +
                "WHERE accountguid = '" + accountId.ToString() + "' " +
                "AND type = '" + EntryType.Debit.ToString() + "' " +
                "AND committed = 0;";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result != null && result.Rows.Count > 0)
            {
                return Convert.ToDecimal(result.Rows[0][0]);
            }

            return 0m;
        }

        #endregion

        #region Private-Methods

        private string Sanitize(string input)
        {
            if (String.IsNullOrEmpty(input)) return String.Empty;
            return input.Replace("'", "''");
        }

        private string BuildInsertQuery(Entry entry)
        {
            return
                "INSERT INTO entries (id, tenantid, accountguid, type, amount, description, replaces, committed, committedbyguid, committedutc, labels, tags, createdutc, lastupdateutc) VALUES (" +
                "'" + entry.Id.ToString() + "', " +
                "'" + Sanitize(entry.TenantId) + "', " +
                "'" + entry.AccountId.ToString() + "', " +
                "'" + entry.Type.ToString() + "', " +
                entry.Amount.ToString() + ", " +
                (entry.Description != null ? "'" + Sanitize(entry.Description) + "'" : "NULL") + ", " +
                (!String.IsNullOrEmpty(entry.Replaces) ? "'" + Sanitize(entry.Replaces) + "'" : "NULL") + ", " +
                (entry.IsCommitted ? "1" : "0") + ", " +
                (!String.IsNullOrEmpty(entry.CommittedById) ? "'" + Sanitize(entry.CommittedById) + "'" : "NULL") + ", " +
                (entry.CommittedUtc.HasValue ? "'" + entry.CommittedUtc.Value.ToString(SetupQueries.TimestampFormat) + "'" : "NULL") + ", " +
                "'" + Sanitize(MetadataSerializer.SerializeLabels(entry.Labels)) + "', " +
                "'" + Sanitize(MetadataSerializer.SerializeTags(entry.Tags)) + "', " +
                "'" + entry.CreatedUtc.ToString(SetupQueries.TimestampFormat) + "', " +
                "'" + (entry.LastUpdateUtc == default ? entry.CreatedUtc : entry.LastUpdateUtc).ToString(SetupQueries.TimestampFormat) + "'" +
                ");";
        }

        private string BuildUpdateQuery(Entry entry)
        {
            return
                "UPDATE entries SET " +
                "type = '" + entry.Type.ToString() + "', " +
                "amount = " + entry.Amount.ToString() + ", " +
                "description = " + (entry.Description != null ? "'" + Sanitize(entry.Description) + "'" : "NULL") + ", " +
                "replaces = " + (!String.IsNullOrEmpty(entry.Replaces) ? "'" + Sanitize(entry.Replaces) + "'" : "NULL") + ", " +
                "committed = " + (entry.IsCommitted ? "1" : "0") + ", " +
                "committedbyguid = " + (!String.IsNullOrEmpty(entry.CommittedById) ? "'" + Sanitize(entry.CommittedById) + "'" : "NULL") + ", " +
                "committedutc = " + (entry.CommittedUtc.HasValue ? "'" + entry.CommittedUtc.Value.ToString(SetupQueries.TimestampFormat) + "'" : "NULL") + ", " +
                "tenantid = '" + Sanitize(entry.TenantId) + "', " +
                "labels = '" + Sanitize(MetadataSerializer.SerializeLabels(entry.Labels)) + "', " +
                "tags = '" + Sanitize(MetadataSerializer.SerializeTags(entry.Tags)) + "', " +
                "lastupdateutc = '" + DateTime.UtcNow.ToString(SetupQueries.TimestampFormat) + "' " +
                "WHERE id = '" + entry.Id.ToString() + "';";
        }

        private string BuildContinuationCondition(EnumerationOrderEnum ordering, Entry continuation)
        {
            string continuationId = "'" + Sanitize(continuation.Id) + "'";
            string continuationCreated = "'" + continuation.CreatedUtc.ToString(SetupQueries.TimestampFormat) + "'";
            string continuationAmount = continuation.Amount.ToString(CultureInfo.InvariantCulture);

            if (ordering == EnumerationOrderEnum.CreatedDescending) return "(createdutc < " + continuationCreated + " OR (createdutc = " + continuationCreated + " AND id < " + continuationId + "))";
            if (ordering == EnumerationOrderEnum.CreatedAscending) return "(createdutc > " + continuationCreated + " OR (createdutc = " + continuationCreated + " AND id > " + continuationId + "))";
            if (ordering == EnumerationOrderEnum.AmountDescending) return "(amount < " + continuationAmount + " OR (amount = " + continuationAmount + " AND id < " + continuationId + "))";
            if (ordering == EnumerationOrderEnum.AmountAscending) return "(amount > " + continuationAmount + " OR (amount = " + continuationAmount + " AND id > " + continuationId + "))";
            return String.Empty;
        }

        private string GetEntryOrderByClause(EnumerationOrderEnum ordering)
        {
            if (ordering == EnumerationOrderEnum.CreatedAscending) return "ORDER BY createdutc ASC, id ASC";
            if (ordering == EnumerationOrderEnum.AmountDescending) return "ORDER BY amount DESC, id DESC";
            if (ordering == EnumerationOrderEnum.AmountAscending) return "ORDER BY amount ASC, id ASC";
            return "ORDER BY createdutc DESC, id DESC";
        }

        private Entry DataRowToEntry(DataRow row)
        {
            Entry entry = new Entry();
            entry.Id = row["id"].ToString();
            entry.TenantId = GetString(row, "tenantid");
            entry.AccountId = row["accountguid"].ToString();
            entry.Type = (EntryType)Enum.Parse(typeof(EntryType), row["type"].ToString());
            entry.Amount = Convert.ToDecimal(row["amount"]);
            entry.Description = row["description"]?.ToString();

            string replacesStr = row["replaces"]?.ToString();
            if (!String.IsNullOrEmpty(replacesStr))
            {
                entry.Replaces = replacesStr;
            }

            entry.IsCommitted = Convert.ToInt32(row["committed"]) == 1;
            entry.Labels = MetadataSerializer.DeserializeLabels(GetString(row, "labels"));
            entry.Tags = MetadataSerializer.DeserializeTags(GetString(row, "tags"));

            string committedByStr = row["committedbyguid"]?.ToString();
            if (!String.IsNullOrEmpty(committedByStr))
            {
                entry.CommittedById = committedByStr;
            }

            string committedUtcStr = row["committedutc"]?.ToString();
            if (!String.IsNullOrEmpty(committedUtcStr))
            {
                entry.CommittedUtc = DateTime.Parse(
                    committedUtcStr,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            }

            entry.CreatedUtc = DateTime.Parse(
                row["createdutc"].ToString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

            string lastUpdateUtc = GetString(row, "lastupdateutc");
            entry.LastUpdateUtc = !String.IsNullOrEmpty(lastUpdateUtc)
                ? DateTime.Parse(lastUpdateUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal)
                : entry.CreatedUtc;

            return entry;
        }

        private string GetString(DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName)) return String.Empty;
            return row[columnName]?.ToString() ?? String.Empty;
        }

        #endregion
    }
}



