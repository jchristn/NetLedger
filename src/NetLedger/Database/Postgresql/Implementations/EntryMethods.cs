namespace NetLedger.Database.Postgresql.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Database.Interfaces;
    using NetLedger.Database.Postgresql.Queries;

    /// <summary>
    /// PostgreSQL implementation of entry methods.
    /// </summary>
    internal class EntryMethods : IEntryMethods
    {
        #region Private-Members

        private readonly PostgresqlDatabaseDriver _Driver;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the entry methods.
        /// </summary>
        /// <param name="driver">PostgreSQL database driver.</param>
        internal EntryMethods(PostgresqlDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<Entry> CreateAsync(Entry entry, CancellationToken token = default)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            string query = BuildInsertQuery(entry) + " RETURNING id;";

            DataTable result = await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (result != null && result.Rows.Count > 0)
            {
                entry.RowId = Convert.ToInt32(result.Rows[0][0]);
            }

            return entry;
        }

        /// <inheritdoc />
        public async Task<List<Entry>> CreateManyAsync(List<Entry> entries, CancellationToken token = default)
        {
            if (entries == null || entries.Count == 0) return new List<Entry>();

            await _Driver.ExecuteQueriesAsync(entries.Select(BuildInsertQuery), true, token).ConfigureAwait(false);

            return entries;
        }

        /// <inheritdoc />
        public async Task<Entry> ReadByIdAsync(string id, CancellationToken token = default)
        {
            string query = "SELECT * FROM entries WHERE guid = '" + Sanitize(id.ToString()) + "' LIMIT 1;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return null;

            return DataRowToEntry(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<List<Entry>> ReadByIdsAsync(List<string> ids, CancellationToken token = default)
        {
            if (ids == null || ids.Count == 0) return new List<Entry>();

            string idList = String.Join(",", ids.Select(g => "'" + Sanitize(g.ToString()) + "'"));
            string query = "SELECT * FROM entries WHERE guid IN (" + idList + ");";
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
                "AND iscommitted = FALSE " +
                "AND type != 'Balance'");

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
            string query = "SELECT * FROM entries WHERE accountguid = '" + accountId.ToString() + "' AND type = 'Balance' ORDER BY createdutc DESC LIMIT 1;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return null;

            return DataRowToEntry(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<Entry> ReadBalanceAsOfAsync(string accountId, DateTime asOfUtc, CancellationToken token = default)
        {
            string query = "SELECT * FROM entries WHERE accountguid = '" + accountId.ToString() + "' AND type = 'Balance' AND createdutc <= '" + asOfUtc.ToString(SetupQueries.TimestampFormat) + "' ORDER BY createdutc DESC LIMIT 1;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return null;

            return DataRowToEntry(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<List<Entry>> ReadWithFilterAsync(string accountId, FilterBuilder filter, CancellationToken token = default)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));

            StringBuilder query = new StringBuilder("SELECT * FROM entries WHERE accountguid = '" + accountId.ToString() + "'");

            string conditions = filter.BuildEntryConditions(DatabaseTypeEnum.Postgresql);
            if (!String.IsNullOrEmpty(conditions))
            {
                query.Append(" AND " + conditions);
            }

            query.Append(" " + filter.GetOrderByClause(DatabaseTypeEnum.Postgresql));
            query.Append(" " + filter.GetLimitOffsetClause(DatabaseTypeEnum.Postgresql));
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
            string conditions = filter.BuildEntryConditions(DatabaseTypeEnum.Postgresql);

            // Handle continuation token - get the entry's id for filtering
            string continuationCondition = "";
            int continuationId = 0;
            if (!String.IsNullOrEmpty(query.ContinuationToken))
            {
                Entry? continuationEntry = await ReadByIdAsync(query.ContinuationToken, token).ConfigureAwait(false);
                if (continuationEntry != null)
                {
                    continuationId = continuationEntry.RowId;
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

            // Get total count with filter (without pagination)
            StringBuilder countQuery = new StringBuilder("SELECT COUNT(*) FROM entries WHERE accountguid = '" + accountId.ToString() + "'");
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
            StringBuilder mainQuery = new StringBuilder("SELECT * FROM entries WHERE accountguid = '" + accountId.ToString() + "'");
            if (!String.IsNullOrEmpty(conditions))
            {
                mainQuery.Append(" AND " + conditions);
            }
            if (!String.IsNullOrEmpty(continuationCondition))
            {
                mainQuery.Append(" AND " + continuationCondition);
            }

            // Use ORDER BY id for stable ordering when ordering by created
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
                mainQuery.Append(" " + filter.GetOrderByClause(DatabaseTypeEnum.Postgresql));
            }

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
                    StringBuilder remainingQuery = new StringBuilder("SELECT COUNT(*) FROM entries WHERE accountguid = '" + accountId.ToString() + "'");
                    if (!String.IsNullOrEmpty(conditions))
                    {
                        remainingQuery.Append(" AND " + conditions);
                    }
                    if (query.Ordering == EnumerationOrderEnum.CreatedDescending)
                    {
                        remainingQuery.Append(" AND id < " + lastEntry.RowId);
                    }
                    else if (query.Ordering == EnumerationOrderEnum.CreatedAscending)
                    {
                        remainingQuery.Append(" AND id > " + lastEntry.RowId);
                    }
                    else if (query.Ordering == EnumerationOrderEnum.AmountDescending)
                    {
                        remainingQuery.Append(" AND (amount < " + lastEntry.Amount.ToString() + " OR (amount = " + lastEntry.Amount.ToString() + " AND id < " + lastEntry.RowId + "))");
                    }
                    else if (query.Ordering == EnumerationOrderEnum.AmountAscending)
                    {
                        remainingQuery.Append(" AND (amount > " + lastEntry.Amount.ToString() + " OR (amount = " + lastEntry.Amount.ToString() + " AND id > " + lastEntry.RowId + "))");
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

            await _Driver.ExecuteQueryAsync(BuildUpdateQuery(entry), true, token).ConfigureAwait(false);

            return entry;
        }

        /// <inheritdoc />
        public async Task UpdateManyAsync(List<Entry> entries, CancellationToken token = default)
        {
            if (entries == null || entries.Count == 0) return;

            await _Driver.ExecuteQueriesAsync(entries.Select(BuildUpdateQuery), true, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task ApplyCommitAsync(List<Entry> committedEntries, Entry balanceEntry, CancellationToken token = default)
        {
            if (committedEntries == null) throw new ArgumentNullException(nameof(committedEntries));
            if (balanceEntry == null) throw new ArgumentNullException(nameof(balanceEntry));

            List<string> queries = new List<string> { BuildInsertQuery(balanceEntry) };
            foreach (Entry entry in committedEntries)
            {
                queries.Add(BuildUpdateQuery(entry));
            }

            await _Driver.ExecuteQueriesAsync(queries, true, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task DeleteByIdAsync(string id, CancellationToken token = default)
        {
            string query = "DELETE FROM entries WHERE guid = '" + Sanitize(id.ToString()) + "';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task DeleteByAccountIdAsync(string accountId, CancellationToken token = default)
        {
            string query = "DELETE FROM entries WHERE accountguid = '" + accountId.ToString() + "';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> ExistsByIdAsync(string id, CancellationToken token = default)
        {
            string query = "SELECT COUNT(*) FROM entries WHERE guid = '" + Sanitize(id.ToString()) + "';";
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
        public async Task<decimal> SumPendingCreditsAsync(string accountId, CancellationToken token = default)
        {
            string query = "SELECT COALESCE(SUM(amount), 0) FROM entries WHERE accountguid = '" + accountId.ToString() + "' AND type = 'Credit' AND iscommitted = FALSE;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result != null && result.Rows.Count > 0 && result.Rows[0][0] != DBNull.Value)
            {
                return Convert.ToDecimal(result.Rows[0][0]);
            }

            return 0m;
        }

        /// <inheritdoc />
        public async Task<decimal> SumPendingDebitsAsync(string accountId, CancellationToken token = default)
        {
            string query = "SELECT COALESCE(SUM(amount), 0) FROM entries WHERE accountguid = '" + accountId.ToString() + "' AND type = 'Debit' AND iscommitted = FALSE;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result != null && result.Rows.Count > 0 && result.Rows[0][0] != DBNull.Value)
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
                "INSERT INTO entries (guid, tenantid, accountguid, type, amount, description, replaces, iscommitted, committedbyguid, committedutc, labels, tags, createdutc, lastupdateutc) VALUES (" +
                "'" + entry.Id.ToString() + "', " +
                "'" + Sanitize(entry.TenantId) + "', " +
                "'" + entry.AccountId.ToString() + "', " +
                "'" + entry.Type.ToString() + "', " +
                entry.Amount.ToString() + ", " +
                (entry.Description != null ? "'" + Sanitize(entry.Description) + "'" : "NULL") + ", " +
                (!String.IsNullOrEmpty(entry.Replaces) ? "'" + Sanitize(entry.Replaces) + "'" : "NULL") + ", " +
                (entry.IsCommitted ? "TRUE" : "FALSE") + ", " +
                (!String.IsNullOrEmpty(entry.CommittedById) ? "'" + Sanitize(entry.CommittedById) + "'" : "NULL") + ", " +
                (entry.CommittedUtc.HasValue ? "'" + entry.CommittedUtc.Value.ToString(SetupQueries.TimestampFormat) + "'" : "NULL") + ", " +
                "'" + Sanitize(global::NetLedger.MetadataSerializer.SerializeLabels(entry.Labels)) + "', " +
                "'" + Sanitize(global::NetLedger.MetadataSerializer.SerializeTags(entry.Tags)) + "', " +
                "'" + entry.CreatedUtc.ToString(SetupQueries.TimestampFormat) + "', " +
                "'" + (entry.LastUpdateUtc == default ? entry.CreatedUtc : entry.LastUpdateUtc).ToString(SetupQueries.TimestampFormat) + "'" +
                ")";
        }

        private string BuildUpdateQuery(Entry entry)
        {
            return
                "UPDATE entries SET " +
                "type = '" + entry.Type.ToString() + "', " +
                "amount = " + entry.Amount.ToString() + ", " +
                "description = " + (entry.Description != null ? "'" + Sanitize(entry.Description) + "'" : "NULL") + ", " +
                "replaces = " + (!String.IsNullOrEmpty(entry.Replaces) ? "'" + Sanitize(entry.Replaces) + "'" : "NULL") + ", " +
                "iscommitted = " + (entry.IsCommitted ? "TRUE" : "FALSE") + ", " +
                "committedbyguid = " + (!String.IsNullOrEmpty(entry.CommittedById) ? "'" + Sanitize(entry.CommittedById) + "'" : "NULL") + ", " +
                "committedutc = " + (entry.CommittedUtc.HasValue ? "'" + entry.CommittedUtc.Value.ToString(SetupQueries.TimestampFormat) + "'" : "NULL") + ", " +
                "tenantid = '" + Sanitize(entry.TenantId) + "', " +
                "labels = '" + Sanitize(global::NetLedger.MetadataSerializer.SerializeLabels(entry.Labels)) + "', " +
                "tags = '" + Sanitize(global::NetLedger.MetadataSerializer.SerializeTags(entry.Tags)) + "', " +
                "lastupdateutc = '" + DateTime.UtcNow.ToString(SetupQueries.TimestampFormat) + "' " +
                "WHERE guid = '" + entry.Id.ToString() + "';";
        }

        private Entry DataRowToEntry(DataRow row)
        {
            Entry entry = new Entry();
            entry.RowId = Convert.ToInt32(row["id"]);
            entry.Id = row["guid"].ToString()!;
            entry.AccountId = row["accountguid"].ToString()!;
            entry.Type = Enum.Parse<EntryType>(row["type"].ToString()!);
            entry.Amount = Convert.ToDecimal(row["amount"]);
            entry.Description = row["description"] != DBNull.Value ? row["description"]?.ToString() : null;
            entry.Replaces = row["replaces"] != DBNull.Value && !String.IsNullOrEmpty(row["replaces"]?.ToString()) ? row["replaces"].ToString()! : null;

            // PostgreSQL returns boolean as True/False strings
            string isCommittedStr = row["iscommitted"]?.ToString() ?? "False";
            entry.IsCommitted = isCommittedStr.Equals("True", StringComparison.OrdinalIgnoreCase) || isCommittedStr == "1";

            entry.CommittedById = row["committedbyguid"] != DBNull.Value && !String.IsNullOrEmpty(row["committedbyguid"]?.ToString()) ? row["committedbyguid"].ToString()! : null;
            entry.CommittedUtc = row["committedutc"] != DBNull.Value && !String.IsNullOrEmpty(row["committedutc"]?.ToString()) ? DateTime.Parse(row["committedutc"].ToString()!) : null;
            entry.CreatedUtc = DateTime.Parse(row["createdutc"].ToString()!);
            return entry;
        }

        #endregion
    }
}



