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

            string query = BuildInsertQuery(entry) + " SELECT last_insert_rowid();";
            DataTable result = await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (result != null && result.Rows.Count > 0)
            {
                entry.Id = Convert.ToInt32(result.Rows[0][0]);
            }

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
        public async Task<Entry> ReadByGuidAsync(Guid guid, CancellationToken token = default)
        {
            string query = "SELECT * FROM entries WHERE guid = '" + Sanitize(guid.ToString()) + "' LIMIT 1;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return null;

            return DataRowToEntry(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<List<Entry>> ReadByGuidsAsync(List<Guid> guids, CancellationToken token = default)
        {
            if (guids == null || guids.Count == 0) return new List<Entry>();

            string guidList = String.Join(", ", guids.Select(g => "'" + Sanitize(g.ToString()) + "'"));
            string query = "SELECT * FROM entries WHERE guid IN (" + guidList + ");";
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
        public async Task<List<Entry>> ReadByAccountGuidAsync(Guid accountGuid, CancellationToken token = default)
        {
            string query = "SELECT * FROM entries WHERE accountguid = '" + accountGuid.ToString() + "' ORDER BY createdutc DESC;";
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
        public async Task<List<Entry>> ReadPendingByAccountGuidAsync(Guid accountGuid, EntryType? entryType = null, CancellationToken token = default)
        {
            StringBuilder query = new StringBuilder(
                "SELECT * FROM entries WHERE accountguid = '" + accountGuid.ToString() + "' " +
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
        public async Task<Entry> ReadLatestBalanceAsync(Guid accountGuid, CancellationToken token = default)
        {
            string query =
                "SELECT * FROM entries " +
                "WHERE accountguid = '" + accountGuid.ToString() + "' " +
                "AND type = '" + EntryType.Balance.ToString() + "' " +
                "ORDER BY createdutc DESC LIMIT 1;";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return null;

            return DataRowToEntry(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<Entry> ReadBalanceAsOfAsync(Guid accountGuid, DateTime asOfUtc, CancellationToken token = default)
        {
            string query =
                "SELECT * FROM entries " +
                "WHERE accountguid = '" + accountGuid.ToString() + "' " +
                "AND type = '" + EntryType.Balance.ToString() + "' " +
                "AND createdutc <= '" + asOfUtc.ToString(SetupQueries.TimestampFormat) + "' " +
                "ORDER BY createdutc DESC LIMIT 1;";

            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return null;

            return DataRowToEntry(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<List<Entry>> ReadWithFilterAsync(Guid accountGuid, FilterBuilder filter, CancellationToken token = default)
        {
            StringBuilder query = new StringBuilder(
                "SELECT * FROM entries WHERE accountguid = '" + accountGuid.ToString() + "'");

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
        public async Task<EnumerationResult<Entry>> EnumerateAsync(Guid accountGuid, EnumerationQuery query, CancellationToken token = default)
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
            int continuationId = 0;
            if (query.ContinuationToken.HasValue)
            {
                Entry continuationEntry = await ReadByGuidAsync(query.ContinuationToken.Value, token).ConfigureAwait(false);
                if (continuationEntry != null)
                {
                    continuationId = continuationEntry.Id;
                    // Use Id for stable ordering since multiple entries might have same timestamp
                    if (query.Ordering == EnumerationOrderEnum.CreatedDescending)
                    {
                        continuationCondition = "id < " + continuationId;
                    }
                    else if (query.Ordering == EnumerationOrderEnum.CreatedAscending)
                    {
                        continuationCondition = "id > " + continuationId;
                    }
                    else if (query.Ordering == EnumerationOrderEnum.AmountDescending)
                    {
                        continuationCondition = "(amount < " + continuationEntry.Amount.ToString() + " OR (amount = " + continuationEntry.Amount.ToString() + " AND id < " + continuationId + "))";
                    }
                    else if (query.Ordering == EnumerationOrderEnum.AmountAscending)
                    {
                        continuationCondition = "(amount > " + continuationEntry.Amount.ToString() + " OR (amount = " + continuationEntry.Amount.ToString() + " AND id > " + continuationId + "))";
                    }
                }
            }

            // Get total count (without pagination but with filters)
            StringBuilder countQuery = new StringBuilder(
                "SELECT COUNT(*) FROM entries WHERE accountguid = '" + accountGuid.ToString() + "'");
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
                "SELECT * FROM entries WHERE accountguid = '" + accountGuid.ToString() + "'");
            if (!String.IsNullOrEmpty(conditions))
            {
                mainQuery.Append(" AND " + conditions);
            }
            if (!String.IsNullOrEmpty(continuationCondition))
            {
                mainQuery.Append(" AND " + continuationCondition);
            }

            // Use ORDER BY id for stable ordering
            if (query.Ordering == EnumerationOrderEnum.CreatedDescending)
            {
                mainQuery.Append(" ORDER BY id DESC");
            }
            else if (query.Ordering == EnumerationOrderEnum.CreatedAscending)
            {
                mainQuery.Append(" ORDER BY id ASC");
            }
            else
            {
                mainQuery.Append(" " + filter.GetOrderByClause(DatabaseTypeEnum.Sqlite));
            }

            mainQuery.Append(" LIMIT " + query.MaxResults);
            if (query.Skip > 0 && !query.ContinuationToken.HasValue)
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
            if (query.ContinuationToken.HasValue)
            {
                // For continuation token, count remaining after last entry
                if (result.Objects.Count > 0)
                {
                    Entry lastEntry = result.Objects[result.Objects.Count - 1];
                    StringBuilder remainingQuery = new StringBuilder(
                        "SELECT COUNT(*) FROM entries WHERE accountguid = '" + accountGuid.ToString() + "'");
                    if (!String.IsNullOrEmpty(conditions))
                    {
                        remainingQuery.Append(" AND " + conditions);
                    }
                    if (query.Ordering == EnumerationOrderEnum.CreatedDescending)
                    {
                        remainingQuery.Append(" AND id < " + lastEntry.Id);
                    }
                    else if (query.Ordering == EnumerationOrderEnum.CreatedAscending)
                    {
                        remainingQuery.Append(" AND id > " + lastEntry.Id);
                    }
                    else if (query.Ordering == EnumerationOrderEnum.AmountDescending)
                    {
                        remainingQuery.Append(" AND (amount < " + lastEntry.Amount.ToString() + " OR (amount = " + lastEntry.Amount.ToString() + " AND id < " + lastEntry.Id + "))");
                    }
                    else if (query.Ordering == EnumerationOrderEnum.AmountAscending)
                    {
                        remainingQuery.Append(" AND (amount > " + lastEntry.Amount.ToString() + " OR (amount = " + lastEntry.Amount.ToString() + " AND id > " + lastEntry.Id + "))");
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
                result.ContinuationToken = result.Objects[result.Objects.Count - 1].GUID;
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
                "replaces = " + (entry.Replaces.HasValue ? "'" + Sanitize(entry.Replaces.Value.ToString()) + "'" : "NULL") + ", " +
                "committed = " + (entry.IsCommitted ? "1" : "0") + ", " +
                "committedbyguid = " + (entry.CommittedByGUID.HasValue ? "'" + Sanitize(entry.CommittedByGUID.Value.ToString()) + "'" : "NULL") + ", " +
                "committedutc = " + (entry.CommittedUtc.HasValue ? "'" + entry.CommittedUtc.Value.ToString(SetupQueries.TimestampFormat) + "'" : "NULL") + " " +
                "WHERE guid = '" + entry.GUID.ToString() + "';";

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
                queries.Add(
                    "UPDATE entries SET " +
                    "type = '" + entry.Type.ToString() + "', " +
                    "amount = " + entry.Amount.ToString() + ", " +
                    "description = " + (entry.Description != null ? "'" + Sanitize(entry.Description) + "'" : "NULL") + ", " +
                    "replaces = " + (entry.Replaces.HasValue ? "'" + Sanitize(entry.Replaces.Value.ToString()) + "'" : "NULL") + ", " +
                    "committed = " + (entry.IsCommitted ? "1" : "0") + ", " +
                    "committedbyguid = " + (entry.CommittedByGUID.HasValue ? "'" + Sanitize(entry.CommittedByGUID.Value.ToString()) + "'" : "NULL") + ", " +
                    "committedutc = " + (entry.CommittedUtc.HasValue ? "'" + entry.CommittedUtc.Value.ToString(SetupQueries.TimestampFormat) + "'" : "NULL") + " " +
                    "WHERE guid = '" + entry.GUID.ToString() + "';"
                );
            }

            await _Driver.ExecuteQueriesAsync(queries, true, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task DeleteByGuidAsync(Guid guid, CancellationToken token = default)
        {
            string query = "DELETE FROM entries WHERE guid = '" + Sanitize(guid.ToString()) + "';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task DeleteByAccountGuidAsync(Guid accountGuid, CancellationToken token = default)
        {
            string query = "DELETE FROM entries WHERE accountguid = '" + accountGuid.ToString() + "';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> ExistsByGuidAsync(Guid guid, CancellationToken token = default)
        {
            string query = "SELECT COUNT(*) FROM entries WHERE guid = '" + Sanitize(guid.ToString()) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result != null && result.Rows.Count > 0)
            {
                return Convert.ToInt32(result.Rows[0][0]) > 0;
            }

            return false;
        }

        /// <inheritdoc />
        public async Task<int> GetCountByAccountGuidAsync(Guid accountGuid, CancellationToken token = default)
        {
            string query = "SELECT COUNT(*) FROM entries WHERE accountguid = '" + accountGuid.ToString() + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result != null && result.Rows.Count > 0)
            {
                return Convert.ToInt32(result.Rows[0][0]);
            }

            return 0;
        }

        /// <inheritdoc />
        public async Task<decimal> SumPendingCreditsAsync(Guid accountGuid, CancellationToken token = default)
        {
            string query =
                "SELECT COALESCE(SUM(amount), 0) FROM entries " +
                "WHERE accountguid = '" + accountGuid.ToString() + "' " +
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
        public async Task<decimal> SumPendingDebitsAsync(Guid accountGuid, CancellationToken token = default)
        {
            string query =
                "SELECT COALESCE(SUM(amount), 0) FROM entries " +
                "WHERE accountguid = '" + accountGuid.ToString() + "' " +
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
                "INSERT INTO entries (guid, accountguid, type, amount, description, replaces, committed, committedbyguid, committedutc, createdutc) VALUES (" +
                "'" + entry.GUID.ToString() + "', " +
                "'" + entry.AccountGUID.ToString() + "', " +
                "'" + entry.Type.ToString() + "', " +
                entry.Amount.ToString() + ", " +
                (entry.Description != null ? "'" + Sanitize(entry.Description) + "'" : "NULL") + ", " +
                (entry.Replaces.HasValue ? "'" + Sanitize(entry.Replaces.Value.ToString()) + "'" : "NULL") + ", " +
                (entry.IsCommitted ? "1" : "0") + ", " +
                (entry.CommittedByGUID.HasValue ? "'" + Sanitize(entry.CommittedByGUID.Value.ToString()) + "'" : "NULL") + ", " +
                (entry.CommittedUtc.HasValue ? "'" + entry.CommittedUtc.Value.ToString(SetupQueries.TimestampFormat) + "'" : "NULL") + ", " +
                "'" + entry.CreatedUtc.ToString(SetupQueries.TimestampFormat) + "'" +
                ");";
        }

        private Entry DataRowToEntry(DataRow row)
        {
            Entry entry = new Entry();
            entry.Id = Convert.ToInt32(row["id"]);
            entry.GUID = Guid.Parse(row["guid"].ToString());
            entry.AccountGUID = Guid.Parse(row["accountguid"].ToString());
            entry.Type = (EntryType)Enum.Parse(typeof(EntryType), row["type"].ToString());
            entry.Amount = Convert.ToDecimal(row["amount"]);
            entry.Description = row["description"]?.ToString();

            string replacesStr = row["replaces"]?.ToString();
            if (!String.IsNullOrEmpty(replacesStr))
            {
                entry.Replaces = Guid.Parse(replacesStr);
            }

            entry.IsCommitted = Convert.ToInt32(row["committed"]) == 1;

            string committedByStr = row["committedbyguid"]?.ToString();
            if (!String.IsNullOrEmpty(committedByStr))
            {
                entry.CommittedByGUID = Guid.Parse(committedByStr);
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

            return entry;
        }

        #endregion
    }
}
