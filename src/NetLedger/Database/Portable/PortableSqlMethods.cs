namespace NetLedger.Database.Portable
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

    internal sealed class PortableSqlAccountMethods : IAccountMethods
    {
        private readonly DatabaseDriverBase _Driver;
        private readonly DatabaseTypeEnum _DatabaseType;

        internal PortableSqlAccountMethods(DatabaseDriverBase driver, DatabaseTypeEnum databaseType)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _DatabaseType = databaseType;
        }

        public async Task<Account> CreateAsync(Account account, CancellationToken token = default)
        {
            if (account == null) throw new ArgumentNullException(nameof(account));
            if (account.LastUpdateUtc == default) account.LastUpdateUtc = account.CreatedUtc;

            string query = _DatabaseType switch
            {
                DatabaseTypeEnum.Mysql =>
                    "INSERT INTO `accounts` (`guid`, `tenantid`, `name`, `notes`, `labels`, `tags`, `active`, `createdutc`, `lastupdateutc`) VALUES (" +
                    Values(account) + "); SELECT LAST_INSERT_ID();",
                DatabaseTypeEnum.SqlServer =>
                    "INSERT INTO [accounts] ([guid], [tenantid], [name], [notes], [labels], [tags], [active], [createdutc], [lastupdateutc]) OUTPUT INSERTED.[id] VALUES (" +
                    Values(account) + ");",
                _ =>
                    "INSERT INTO accounts (guid, tenantid, name, notes, labels, tags, active, createdutc, lastupdateutc) VALUES (" +
                    Values(account) + ") RETURNING id;"
            };

            DataTable result = await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            if (result != null && result.Rows.Count > 0)
            {
                account.RowId = Convert.ToInt32(result.Rows[0][0], CultureInfo.InvariantCulture);
            }

            return account;
        }

        public async Task<Account> ReadByGuidAsync(string guid, CancellationToken token = default)
        {
            string query = SelectOne("accounts", "guid = '" + Sanitize(guid) + "'");
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return result == null || result.Rows.Count == 0 ? null : DataRowToAccount(result.Rows[0]);
        }

        public async Task<Account> ReadByNameAsync(string name, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            string query = SelectOne("accounts", "name = '" + Sanitize(name) + "'");
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return result == null || result.Rows.Count == 0 ? null : DataRowToAccount(result.Rows[0]);
        }

        public async Task<List<Account>> ReadAllAsync(CancellationToken token = default)
        {
            DataTable result = await _Driver.ExecuteQueryAsync("SELECT * FROM " + Table("accounts") + " ORDER BY " + Column("createdutc") + " DESC;", false, token).ConfigureAwait(false);
            return RowsToAccounts(result);
        }

        public async Task<List<Account>> SearchByNameAsync(string searchTerm, CancellationToken token = default)
        {
            StringBuilder query = new StringBuilder("SELECT * FROM " + Table("accounts"));
            if (!String.IsNullOrEmpty(searchTerm))
            {
                query.Append(" WHERE " + Column("name") + " LIKE '%" + Sanitize(searchTerm) + "%'");
            }

            query.Append(" ORDER BY " + Column("createdutc") + " DESC;");
            DataTable result = await _Driver.ExecuteQueryAsync(query.ToString(), false, token).ConfigureAwait(false);
            return RowsToAccounts(result);
        }

        public async Task<EnumerationResult<Account>> EnumerateAsync(EnumerationQuery query, CancellationToken token = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));

            EnumerationResult<Account> result = new EnumerationResult<Account>
            {
                MaxResults = query.MaxResults,
                Skip = query.Skip
            };

            FilterBuilder filter = FilterBuilder.FromEnumerationQuery(query);
            string conditions = filter.BuildAccountConditions(_DatabaseType);
            bool hasBalanceFilter = query.BalanceMinimum.HasValue || query.BalanceMaximum.HasValue;

            if (hasBalanceFilter)
            {
                StringBuilder allAccountsQuery = new StringBuilder("SELECT * FROM " + Table("accounts"));
                if (!String.IsNullOrEmpty(conditions))
                {
                    allAccountsQuery.Append(" WHERE " + conditions);
                }

                allAccountsQuery.Append(" " + filter.GetOrderByClause(_DatabaseType));
                allAccountsQuery.Append(";");

                List<Account> allAccounts = RowsToAccounts(await _Driver.ExecuteQueryAsync(allAccountsQuery.ToString(), false, token).ConfigureAwait(false));
                List<Account> filteredAccounts = new List<Account>();
                foreach (Account account in allAccounts)
                {
                    decimal balance = await GetAccountCommittedBalanceAsync(account.GUID, token).ConfigureAwait(false);
                    if ((!query.BalanceMinimum.HasValue || balance >= query.BalanceMinimum.Value)
                        && (!query.BalanceMaximum.HasValue || balance <= query.BalanceMaximum.Value))
                    {
                        filteredAccounts.Add(account);
                    }
                }

                result.TotalRecords = filteredAccounts.Count;
                result.Objects = filteredAccounts.Skip(query.Skip).Take(query.MaxResults).ToList();
                result.RecordsRemaining = Math.Max(0, filteredAccounts.Count - query.Skip - result.Objects.Count);
            }
            else
            {
                StringBuilder countQuery = new StringBuilder("SELECT COUNT(*) FROM " + Table("accounts"));
                if (!String.IsNullOrEmpty(conditions))
                {
                    countQuery.Append(" WHERE " + conditions);
                }

                countQuery.Append(";");
                DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery.ToString(), false, token).ConfigureAwait(false);
                if (countResult != null && countResult.Rows.Count > 0)
                {
                    result.TotalRecords = Convert.ToInt64(countResult.Rows[0][0], CultureInfo.InvariantCulture);
                }

                StringBuilder mainQuery = new StringBuilder("SELECT * FROM " + Table("accounts"));
                if (!String.IsNullOrEmpty(conditions))
                {
                    mainQuery.Append(" WHERE " + conditions);
                }

                mainQuery.Append(" " + filter.GetOrderByClause(_DatabaseType));
                mainQuery.Append(" " + filter.GetLimitOffsetClause(_DatabaseType));
                mainQuery.Append(";");
                result.Objects = RowsToAccounts(await _Driver.ExecuteQueryAsync(mainQuery.ToString(), false, token).ConfigureAwait(false));
                result.RecordsRemaining = Math.Max(0, result.TotalRecords - query.Skip - result.Objects.Count);
            }

            result.EndOfResults = result.RecordsRemaining == 0;
            if (!result.EndOfResults && result.Objects.Count > 0)
            {
                result.ContinuationToken = result.Objects[result.Objects.Count - 1].GUID;
            }

            return result;
        }

        public async Task<Account> UpdateAsync(Account account, CancellationToken token = default)
        {
            if (account == null) throw new ArgumentNullException(nameof(account));

            string query =
                "UPDATE " + Table("accounts") + " SET " +
                Column("name") + " = '" + Sanitize(account.Name) + "', " +
                Column("tenantid") + " = '" + Sanitize(account.TenantId) + "', " +
                Column("notes") + " = " + Nullable(account.Notes) + ", " +
                Column("labels") + " = '" + Sanitize(MetadataSerializer.SerializeLabels(account.Labels)) + "', " +
                Column("tags") + " = '" + Sanitize(MetadataSerializer.SerializeTags(account.Tags)) + "', " +
                Column("active") + " = " + Bool(account.Active) + ", " +
                Column("lastupdateutc") + " = '" + FormatTimestamp(DateTime.UtcNow) + "' " +
                "WHERE " + Column("guid") + " = '" + Sanitize(account.GUID) + "';";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return account;
        }

        public Task DeleteByGuidAsync(string guid, CancellationToken token = default)
        {
            return _Driver.ExecuteQueryAsync("DELETE FROM " + Table("accounts") + " WHERE " + Column("guid") + " = '" + Sanitize(guid) + "';", true, token);
        }

        public async Task<bool> ExistsByGuidAsync(string guid, CancellationToken token = default)
        {
            return await CountAsync("accounts", "guid = '" + Sanitize(guid) + "'", token).ConfigureAwait(false) > 0;
        }

        public async Task<bool> ExistsByNameAsync(string name, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            return await CountAsync("accounts", "name = '" + Sanitize(name) + "'", token).ConfigureAwait(false) > 0;
        }

        public async Task<int> GetCountAsync(CancellationToken token = default)
        {
            return (int)await CountAsync("accounts", null, token).ConfigureAwait(false);
        }

        private string Values(Account account)
        {
            return
                "'" + Sanitize(account.GUID) + "', " +
                "'" + Sanitize(account.TenantId) + "', " +
                "'" + Sanitize(account.Name) + "', " +
                Nullable(account.Notes) + ", " +
                "'" + Sanitize(MetadataSerializer.SerializeLabels(account.Labels)) + "', " +
                "'" + Sanitize(MetadataSerializer.SerializeTags(account.Tags)) + "', " +
                Bool(account.Active) + ", " +
                "'" + FormatTimestamp(account.CreatedUtc) + "', " +
                "'" + FormatTimestamp(account.LastUpdateUtc == default ? account.CreatedUtc : account.LastUpdateUtc) + "'";
        }

        private async Task<decimal> GetAccountCommittedBalanceAsync(string accountGuid, CancellationToken token)
        {
            string query = SelectOne("entries", "accountguid = '" + Sanitize(accountGuid) + "' AND type = '" + EntryType.Balance + "'", "createdutc DESC");
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return result != null && result.Rows.Count > 0 ? Convert.ToDecimal(result.Rows[0]["amount"], CultureInfo.InvariantCulture) : 0m;
        }

        private List<Account> RowsToAccounts(DataTable result)
        {
            List<Account> accounts = new List<Account>();
            if (result == null) return accounts;
            foreach (DataRow row in result.Rows) accounts.Add(DataRowToAccount(row));
            return accounts;
        }

        private Account DataRowToAccount(DataRow row)
        {
            Account account = new Account
            {
                RowId = Convert.ToInt32(row["id"], CultureInfo.InvariantCulture),
                GUID = GetString(row, "guid"),
                TenantId = GetString(row, "tenantid"),
                Name = GetString(row, "name"),
                Notes = GetNullableString(row, "notes"),
                Labels = MetadataSerializer.DeserializeLabels(GetString(row, "labels")),
                Tags = MetadataSerializer.DeserializeTags(GetString(row, "tags")),
                Active = ToBool(GetString(row, "active")),
                CreatedUtc = ParseDate(GetString(row, "createdutc"))
            };
            string lastUpdateUtc = GetString(row, "lastupdateutc");
            account.LastUpdateUtc = !String.IsNullOrEmpty(lastUpdateUtc) ? ParseDate(lastUpdateUtc) : account.CreatedUtc;
            return account;
        }

        private string SelectOne(string table, string where, string orderBy = null)
        {
            string order = String.IsNullOrEmpty(orderBy) ? String.Empty : " ORDER BY " + orderBy;
            return _DatabaseType == DatabaseTypeEnum.SqlServer
                ? "SELECT TOP 1 * FROM " + Table(table) + " WHERE " + where + order + ";"
                : "SELECT * FROM " + Table(table) + " WHERE " + where + order + " LIMIT 1;";
        }

        private async Task<long> CountAsync(string table, string where, CancellationToken token)
        {
            string query = "SELECT COUNT(*) FROM " + Table(table) + (String.IsNullOrEmpty(where) ? String.Empty : " WHERE " + where) + ";";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return result != null && result.Rows.Count > 0 ? Convert.ToInt64(result.Rows[0][0], CultureInfo.InvariantCulture) : 0L;
        }

        private string Table(string name) => _DatabaseType == DatabaseTypeEnum.SqlServer ? "[" + name + "]" : _DatabaseType == DatabaseTypeEnum.Mysql ? "`" + name + "`" : name;
        private string Column(string name) => _DatabaseType == DatabaseTypeEnum.SqlServer ? "[" + name + "]" : _DatabaseType == DatabaseTypeEnum.Mysql ? "`" + name + "`" : name;
        private string Bool(bool value) => _DatabaseType == DatabaseTypeEnum.Postgresql ? (value ? "TRUE" : "FALSE") : (value ? "1" : "0");
        private string FormatTimestamp(DateTime value) => value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture);
        private string Nullable(string value) => String.IsNullOrEmpty(value) ? "NULL" : "'" + Sanitize(value) + "'";
        private string Sanitize(string value) => String.IsNullOrEmpty(value) ? String.Empty : value.Replace("'", "''");
        private string GetString(DataRow row, string columnName) => row.Table.Columns.Contains(columnName) && row[columnName] != DBNull.Value ? row[columnName]?.ToString() ?? String.Empty : String.Empty;
        private string GetNullableString(DataRow row, string columnName) => row.Table.Columns.Contains(columnName) && row[columnName] != DBNull.Value ? row[columnName]?.ToString() : null;
        private bool ToBool(string value) => value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
        private DateTime ParseDate(string value) => DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }

    internal sealed class PortableSqlEntryMethods : IEntryMethods
    {
        private readonly DatabaseDriverBase _Driver;
        private readonly DatabaseTypeEnum _DatabaseType;

        internal PortableSqlEntryMethods(DatabaseDriverBase driver, DatabaseTypeEnum databaseType)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _DatabaseType = databaseType;
        }

        public async Task<Entry> CreateAsync(Entry entry, CancellationToken token = default)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            string query = _DatabaseType switch
            {
                DatabaseTypeEnum.Mysql => BuildInsertQuery(entry, false) + " SELECT LAST_INSERT_ID();",
                DatabaseTypeEnum.SqlServer => BuildInsertQuery(entry, true),
                _ => BuildInsertQuery(entry, true)
            };

            DataTable result = await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            if (result != null && result.Rows.Count > 0)
            {
                entry.RowId = Convert.ToInt32(result.Rows[0][0], CultureInfo.InvariantCulture);
            }

            return entry;
        }

        public async Task<List<Entry>> CreateManyAsync(List<Entry> entries, CancellationToken token = default)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            if (entries.Count == 0) return entries;

            await _Driver.ExecuteQueriesAsync(entries.Select(entry => BuildInsertQuery(entry, false)), true, token).ConfigureAwait(false);
            return entries;
        }

        public async Task<Entry> ReadByGuidAsync(string guid, CancellationToken token = default)
        {
            DataTable result = await _Driver.ExecuteQueryAsync(SelectOne("entries", "guid = '" + Sanitize(guid) + "'"), false, token).ConfigureAwait(false);
            return result == null || result.Rows.Count == 0 ? null : DataRowToEntry(result.Rows[0]);
        }

        public async Task<List<Entry>> ReadByGuidsAsync(List<string> guids, CancellationToken token = default)
        {
            if (guids == null || guids.Count == 0) return new List<Entry>();
            string guidList = String.Join(", ", guids.Select(guid => "'" + Sanitize(guid) + "'"));
            DataTable result = await _Driver.ExecuteQueryAsync("SELECT * FROM " + Table("entries") + " WHERE " + Column("guid") + " IN (" + guidList + ");", false, token).ConfigureAwait(false);
            return RowsToEntries(result);
        }

        public async Task<List<Entry>> ReadByAccountGuidAsync(string accountGuid, CancellationToken token = default)
        {
            DataTable result = await _Driver.ExecuteQueryAsync("SELECT * FROM " + Table("entries") + " WHERE " + Column("accountguid") + " = '" + Sanitize(accountGuid) + "' ORDER BY " + Column("createdutc") + " DESC;", false, token).ConfigureAwait(false);
            return RowsToEntries(result);
        }

        public async Task<List<Entry>> ReadPendingByAccountGuidAsync(string accountGuid, EntryType? entryType = null, CancellationToken token = default)
        {
            StringBuilder query = new StringBuilder("SELECT * FROM " + Table("entries") + " WHERE " + Column("accountguid") + " = '" + Sanitize(accountGuid) + "' AND " + Column("iscommitted") + " = " + Bool(false) + " AND " + Column("type") + " != '" + EntryType.Balance + "'");
            if (entryType.HasValue)
            {
                query.Append(" AND " + Column("type") + " = '" + entryType.Value + "'");
            }

            query.Append(" ORDER BY " + Column("createdutc") + " ASC;");
            DataTable result = await _Driver.ExecuteQueryAsync(query.ToString(), false, token).ConfigureAwait(false);
            return RowsToEntries(result);
        }

        public async Task<Entry> ReadLatestBalanceAsync(string accountGuid, CancellationToken token = default)
        {
            DataTable result = await _Driver.ExecuteQueryAsync(SelectOne("entries", "accountguid = '" + Sanitize(accountGuid) + "' AND type = '" + EntryType.Balance + "'", "createdutc DESC"), false, token).ConfigureAwait(false);
            return result == null || result.Rows.Count == 0 ? null : DataRowToEntry(result.Rows[0]);
        }

        public async Task<Entry> ReadBalanceAsOfAsync(string accountGuid, DateTime asOfUtc, CancellationToken token = default)
        {
            string where = "accountguid = '" + Sanitize(accountGuid) + "' AND type = '" + EntryType.Balance + "' AND createdutc <= '" + FormatTimestamp(asOfUtc) + "'";
            DataTable result = await _Driver.ExecuteQueryAsync(SelectOne("entries", where, "createdutc DESC"), false, token).ConfigureAwait(false);
            return result == null || result.Rows.Count == 0 ? null : DataRowToEntry(result.Rows[0]);
        }

        public async Task<List<Entry>> ReadWithFilterAsync(string accountGuid, FilterBuilder filter, CancellationToken token = default)
        {
            StringBuilder query = new StringBuilder("SELECT * FROM " + Table("entries") + " WHERE " + Column("accountguid") + " = '" + Sanitize(accountGuid) + "'");
            if (filter != null)
            {
                string conditions = filter.BuildEntryConditions(_DatabaseType);
                if (!String.IsNullOrEmpty(conditions))
                {
                    query.Append(" AND " + conditions);
                }

                query.Append(" " + filter.GetOrderByClause(_DatabaseType));
                query.Append(" " + filter.GetLimitOffsetClause(_DatabaseType));
            }
            else
            {
                query.Append(" ORDER BY " + Column("createdutc") + " DESC");
            }

            query.Append(";");
            DataTable result = await _Driver.ExecuteQueryAsync(query.ToString(), false, token).ConfigureAwait(false);
            return RowsToEntries(result);
        }

        public async Task<EnumerationResult<Entry>> EnumerateAsync(string accountGuid, EnumerationQuery query, CancellationToken token = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));

            EnumerationResult<Entry> result = new EnumerationResult<Entry>
            {
                MaxResults = query.MaxResults,
                Skip = query.Skip
            };

            FilterBuilder filter = FilterBuilder.FromEnumerationQuery(query);
            string conditions = filter.BuildEntryConditions(_DatabaseType);
            string continuationCondition = await BuildContinuationConditionAsync(query, token).ConfigureAwait(false);

            StringBuilder countQuery = new StringBuilder("SELECT COUNT(*) FROM " + Table("entries") + " WHERE " + Column("accountguid") + " = '" + Sanitize(accountGuid) + "'");
            if (!String.IsNullOrEmpty(conditions)) countQuery.Append(" AND " + conditions);
            countQuery.Append(";");
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery.ToString(), false, token).ConfigureAwait(false);
            if (countResult != null && countResult.Rows.Count > 0)
            {
                result.TotalRecords = Convert.ToInt64(countResult.Rows[0][0], CultureInfo.InvariantCulture);
            }

            StringBuilder mainQuery = new StringBuilder("SELECT * FROM " + Table("entries") + " WHERE " + Column("accountguid") + " = '" + Sanitize(accountGuid) + "'");
            if (!String.IsNullOrEmpty(conditions)) mainQuery.Append(" AND " + conditions);
            if (!String.IsNullOrEmpty(continuationCondition)) mainQuery.Append(" AND " + continuationCondition);

            if (query.Ordering == EnumerationOrderEnum.CreatedDescending) mainQuery.Append(" ORDER BY " + Column("id") + " DESC");
            else if (query.Ordering == EnumerationOrderEnum.CreatedAscending) mainQuery.Append(" ORDER BY " + Column("id") + " ASC");
            else mainQuery.Append(" " + filter.GetOrderByClause(_DatabaseType));

            mainQuery.Append(_DatabaseType == DatabaseTypeEnum.SqlServer
                ? " OFFSET " + (String.IsNullOrEmpty(query.ContinuationToken) ? query.Skip : 0) + " ROWS FETCH NEXT " + query.MaxResults + " ROWS ONLY"
                : " LIMIT " + query.MaxResults + (query.Skip > 0 && String.IsNullOrEmpty(query.ContinuationToken) ? " OFFSET " + query.Skip : String.Empty));
            mainQuery.Append(";");

            result.Objects = RowsToEntries(await _Driver.ExecuteQueryAsync(mainQuery.ToString(), false, token).ConfigureAwait(false));
            result.RecordsRemaining = Math.Max(0, result.TotalRecords - query.Skip - result.Objects.Count);
            result.EndOfResults = result.RecordsRemaining == 0;
            if (!result.EndOfResults && result.Objects.Count > 0)
            {
                result.ContinuationToken = result.Objects[result.Objects.Count - 1].GUID;
            }

            return result;
        }

        public async Task<Entry> UpdateAsync(Entry entry, CancellationToken token = default)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            string query =
                "UPDATE " + Table("entries") + " SET " +
                Column("tenantid") + " = '" + Sanitize(entry.TenantId) + "', " +
                Column("type") + " = '" + entry.Type + "', " +
                Column("amount") + " = " + entry.Amount.ToString(CultureInfo.InvariantCulture) + ", " +
                Column("description") + " = " + Nullable(entry.Description) + ", " +
                Column("replaces") + " = " + Nullable(entry.Replaces) + ", " +
                Column("iscommitted") + " = " + Bool(entry.IsCommitted) + ", " +
                Column("committedbyguid") + " = " + Nullable(entry.CommittedByGUID) + ", " +
                Column("committedutc") + " = " + (entry.CommittedUtc.HasValue ? "'" + FormatTimestamp(entry.CommittedUtc.Value) + "'" : "NULL") + ", " +
                Column("labels") + " = '" + Sanitize(MetadataSerializer.SerializeLabels(entry.Labels)) + "', " +
                Column("tags") + " = '" + Sanitize(MetadataSerializer.SerializeTags(entry.Tags)) + "', " +
                Column("lastupdateutc") + " = '" + FormatTimestamp(DateTime.UtcNow) + "' " +
                "WHERE " + Column("guid") + " = '" + Sanitize(entry.GUID) + "';";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return entry;
        }

        public async Task UpdateManyAsync(List<Entry> entries, CancellationToken token = default)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            foreach (Entry entry in entries) await UpdateAsync(entry, token).ConfigureAwait(false);
        }

        public Task DeleteByGuidAsync(string guid, CancellationToken token = default)
        {
            return _Driver.ExecuteQueryAsync("DELETE FROM " + Table("entries") + " WHERE " + Column("guid") + " = '" + Sanitize(guid) + "';", true, token);
        }

        public Task DeleteByAccountGuidAsync(string accountGuid, CancellationToken token = default)
        {
            return _Driver.ExecuteQueryAsync("DELETE FROM " + Table("entries") + " WHERE " + Column("accountguid") + " = '" + Sanitize(accountGuid) + "';", true, token);
        }

        public async Task<bool> ExistsByGuidAsync(string guid, CancellationToken token = default)
        {
            return await CountAsync("guid = '" + Sanitize(guid) + "'", token).ConfigureAwait(false) > 0;
        }

        public async Task<int> GetCountByAccountGuidAsync(string accountGuid, CancellationToken token = default)
        {
            return (int)await CountAsync("accountguid = '" + Sanitize(accountGuid) + "'", token).ConfigureAwait(false);
        }

        public async Task<decimal> SumPendingCreditsAsync(string accountGuid, CancellationToken token = default)
        {
            return await SumPendingAsync(accountGuid, EntryType.Credit, token).ConfigureAwait(false);
        }

        public async Task<decimal> SumPendingDebitsAsync(string accountGuid, CancellationToken token = default)
        {
            return await SumPendingAsync(accountGuid, EntryType.Debit, token).ConfigureAwait(false);
        }

        private string BuildInsertQuery(Entry entry, bool returnId)
        {
            string columnList = _DatabaseType switch
            {
                DatabaseTypeEnum.Mysql => "(`guid`, `tenantid`, `accountguid`, `type`, `amount`, `description`, `replaces`, `iscommitted`, `committedbyguid`, `committedutc`, `labels`, `tags`, `createdutc`, `lastupdateutc`)",
                DatabaseTypeEnum.SqlServer => "([guid], [tenantid], [accountguid], [type], [amount], [description], [replaces], [iscommitted], [committedbyguid], [committedutc], [labels], [tags], [createdutc], [lastupdateutc])",
                _ => "(guid, tenantid, accountguid, type, amount, description, replaces, iscommitted, committedbyguid, committedutc, labels, tags, createdutc, lastupdateutc)"
            };

            string output = returnId && _DatabaseType == DatabaseTypeEnum.SqlServer ? " OUTPUT INSERTED.[id]" : String.Empty;
            string returning = returnId && _DatabaseType == DatabaseTypeEnum.Postgresql ? " RETURNING id" : String.Empty;
            return
                "INSERT INTO " + Table("entries") + " " + columnList + output + " VALUES (" +
                "'" + Sanitize(entry.GUID) + "', " +
                "'" + Sanitize(entry.TenantId) + "', " +
                "'" + Sanitize(entry.AccountGUID) + "', " +
                "'" + entry.Type + "', " +
                entry.Amount.ToString(CultureInfo.InvariantCulture) + ", " +
                Nullable(entry.Description) + ", " +
                Nullable(entry.Replaces) + ", " +
                Bool(entry.IsCommitted) + ", " +
                Nullable(entry.CommittedByGUID) + ", " +
                (entry.CommittedUtc.HasValue ? "'" + FormatTimestamp(entry.CommittedUtc.Value) + "'" : "NULL") + ", " +
                "'" + Sanitize(MetadataSerializer.SerializeLabels(entry.Labels)) + "', " +
                "'" + Sanitize(MetadataSerializer.SerializeTags(entry.Tags)) + "', " +
                "'" + FormatTimestamp(entry.CreatedUtc) + "', " +
                "'" + FormatTimestamp(entry.LastUpdateUtc == default ? entry.CreatedUtc : entry.LastUpdateUtc) + "'" +
                ")" + returning + ";";
        }

        private async Task<string> BuildContinuationConditionAsync(EnumerationQuery query, CancellationToken token)
        {
            if (String.IsNullOrEmpty(query.ContinuationToken)) return String.Empty;
            Entry continuation = await ReadByGuidAsync(query.ContinuationToken, token).ConfigureAwait(false);
            if (continuation == null) return String.Empty;

            if (query.Ordering == EnumerationOrderEnum.CreatedDescending) return Column("id") + " < " + continuation.RowId;
            if (query.Ordering == EnumerationOrderEnum.CreatedAscending) return Column("id") + " > " + continuation.RowId;
            if (query.Ordering == EnumerationOrderEnum.AmountDescending) return "(" + Column("amount") + " < " + continuation.Amount.ToString(CultureInfo.InvariantCulture) + " OR (" + Column("amount") + " = " + continuation.Amount.ToString(CultureInfo.InvariantCulture) + " AND " + Column("id") + " < " + continuation.RowId + "))";
            if (query.Ordering == EnumerationOrderEnum.AmountAscending) return "(" + Column("amount") + " > " + continuation.Amount.ToString(CultureInfo.InvariantCulture) + " OR (" + Column("amount") + " = " + continuation.Amount.ToString(CultureInfo.InvariantCulture) + " AND " + Column("id") + " > " + continuation.RowId + "))";
            return String.Empty;
        }

        private async Task<long> CountAsync(string where, CancellationToken token)
        {
            DataTable result = await _Driver.ExecuteQueryAsync("SELECT COUNT(*) FROM " + Table("entries") + " WHERE " + where + ";", false, token).ConfigureAwait(false);
            return result != null && result.Rows.Count > 0 ? Convert.ToInt64(result.Rows[0][0], CultureInfo.InvariantCulture) : 0L;
        }

        private async Task<decimal> SumPendingAsync(string accountGuid, EntryType type, CancellationToken token)
        {
            string query = "SELECT COALESCE(SUM(" + Column("amount") + "), 0) FROM " + Table("entries") +
                " WHERE " + Column("accountguid") + " = '" + Sanitize(accountGuid) + "' AND " + Column("type") + " = '" + type + "' AND " + Column("iscommitted") + " = " + Bool(false) + ";";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return result != null && result.Rows.Count > 0 ? Convert.ToDecimal(result.Rows[0][0], CultureInfo.InvariantCulture) : 0m;
        }

        private List<Entry> RowsToEntries(DataTable result)
        {
            List<Entry> entries = new List<Entry>();
            if (result == null) return entries;
            foreach (DataRow row in result.Rows) entries.Add(DataRowToEntry(row));
            return entries;
        }

        private Entry DataRowToEntry(DataRow row)
        {
            Entry entry = new Entry
            {
                RowId = Convert.ToInt32(row["id"], CultureInfo.InvariantCulture),
                GUID = GetString(row, "guid"),
                TenantId = GetString(row, "tenantid"),
                AccountGUID = GetString(row, "accountguid"),
                Type = Enum.Parse<EntryType>(GetString(row, "type")),
                Amount = Convert.ToDecimal(row["amount"], CultureInfo.InvariantCulture),
                Description = GetNullableString(row, "description"),
                Replaces = GetNullableString(row, "replaces"),
                IsCommitted = ToBool(GetString(row, "iscommitted")),
                CommittedByGUID = GetNullableString(row, "committedbyguid"),
                Labels = MetadataSerializer.DeserializeLabels(GetString(row, "labels")),
                Tags = MetadataSerializer.DeserializeTags(GetString(row, "tags")),
                CreatedUtc = ParseDate(GetString(row, "createdutc"))
            };

            string committedUtc = GetString(row, "committedutc");
            if (!String.IsNullOrEmpty(committedUtc)) entry.CommittedUtc = ParseDate(committedUtc);
            string lastUpdateUtc = GetString(row, "lastupdateutc");
            entry.LastUpdateUtc = !String.IsNullOrEmpty(lastUpdateUtc) ? ParseDate(lastUpdateUtc) : entry.CreatedUtc;
            return entry;
        }

        private string SelectOne(string table, string where, string orderBy = null)
        {
            string order = String.IsNullOrEmpty(orderBy) ? String.Empty : " ORDER BY " + orderBy;
            return _DatabaseType == DatabaseTypeEnum.SqlServer
                ? "SELECT TOP 1 * FROM " + Table(table) + " WHERE " + where + order + ";"
                : "SELECT * FROM " + Table(table) + " WHERE " + where + order + " LIMIT 1;";
        }

        private string Table(string name) => _DatabaseType == DatabaseTypeEnum.SqlServer ? "[" + name + "]" : _DatabaseType == DatabaseTypeEnum.Mysql ? "`" + name + "`" : name;
        private string Column(string name) => _DatabaseType == DatabaseTypeEnum.SqlServer ? "[" + name + "]" : _DatabaseType == DatabaseTypeEnum.Mysql ? "`" + name + "`" : name;
        private string Bool(bool value) => _DatabaseType == DatabaseTypeEnum.Postgresql ? (value ? "TRUE" : "FALSE") : (value ? "1" : "0");
        private string FormatTimestamp(DateTime value) => value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture);
        private string Nullable(string value) => String.IsNullOrEmpty(value) ? "NULL" : "'" + Sanitize(value) + "'";
        private string Sanitize(string value) => String.IsNullOrEmpty(value) ? String.Empty : value.Replace("'", "''");
        private string GetString(DataRow row, string columnName) => row.Table.Columns.Contains(columnName) && row[columnName] != DBNull.Value ? row[columnName]?.ToString() ?? String.Empty : String.Empty;
        private string GetNullableString(DataRow row, string columnName) => row.Table.Columns.Contains(columnName) && row[columnName] != DBNull.Value ? row[columnName]?.ToString() : null;
        private bool ToBool(string value) => value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
        private DateTime ParseDate(string value) => DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }

    internal sealed class PortableSqlApiKeyMethods : IApiKeyMethods
    {
        private readonly DatabaseDriverBase _Driver;
        private readonly DatabaseTypeEnum _DatabaseType;

        internal PortableSqlApiKeyMethods(DatabaseDriverBase driver, DatabaseTypeEnum databaseType)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _DatabaseType = databaseType;
        }

        public async Task<ApiKey> CreateAsync(ApiKey apiKey, CancellationToken token = default)
        {
            if (apiKey == null) throw new ArgumentNullException(nameof(apiKey));

            string query = _DatabaseType switch
            {
                DatabaseTypeEnum.Mysql => BuildInsertQuery(apiKey, false) + " SELECT LAST_INSERT_ID();",
                DatabaseTypeEnum.SqlServer => BuildInsertQuery(apiKey, true),
                _ => BuildInsertQuery(apiKey, true)
            };

            DataTable result = await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            if (result != null && result.Rows.Count > 0)
            {
                apiKey.RowId = Convert.ToInt32(result.Rows[0][0], CultureInfo.InvariantCulture);
            }

            return apiKey;
        }

        public async Task<ApiKey> ReadByGuidAsync(string guid, CancellationToken token = default)
        {
            DataTable result = await _Driver.ExecuteQueryAsync(SelectOne("guid = '" + Sanitize(guid) + "'"), false, token).ConfigureAwait(false);
            return result == null || result.Rows.Count == 0 ? null : DataRowToApiKey(result.Rows[0]);
        }

        public async Task<ApiKey> ReadByKeyAsync(string key, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            DataTable result = await _Driver.ExecuteQueryAsync(SelectOne("apikey = '" + Sanitize(key) + "'"), false, token).ConfigureAwait(false);
            return result == null || result.Rows.Count == 0 ? null : DataRowToApiKey(result.Rows[0]);
        }

        public async Task<List<ApiKey>> ReadAllAsync(CancellationToken token = default)
        {
            DataTable result = await _Driver.ExecuteQueryAsync("SELECT * FROM " + Table("apikeys") + " ORDER BY " + Column("createdutc") + " DESC;", false, token).ConfigureAwait(false);
            return RowsToApiKeys(result);
        }

        public async Task<EnumerationResult<ApiKey>> EnumerateAsync(EnumerationQuery query, CancellationToken token = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            EnumerationResult<ApiKey> result = new EnumerationResult<ApiKey> { MaxResults = query.MaxResults, Skip = query.Skip };
            FilterBuilder filter = FilterBuilder.FromEnumerationQuery(query);
            string where = filter.BuildCredentialConditions(_DatabaseType);
            string whereClause = String.IsNullOrEmpty(where) ? String.Empty : " WHERE " + where;

            DataTable countResult = await _Driver.ExecuteQueryAsync("SELECT COUNT(*) FROM " + Table("apikeys") + whereClause + ";", false, token).ConfigureAwait(false);
            if (countResult != null && countResult.Rows.Count > 0)
            {
                result.TotalRecords = Convert.ToInt64(countResult.Rows[0][0], CultureInfo.InvariantCulture);
            }

            DataTable dataResult = await _Driver.ExecuteQueryAsync("SELECT * FROM " + Table("apikeys") + whereClause + " " + filter.GetOrderByClause(_DatabaseType) + " " + filter.GetLimitOffsetClause(_DatabaseType) + ";", false, token).ConfigureAwait(false);
            result.Objects = RowsToApiKeys(dataResult);
            result.RecordsRemaining = Math.Max(0, result.TotalRecords - query.Skip - result.Objects.Count);
            result.EndOfResults = result.RecordsRemaining == 0;
            if (!result.EndOfResults && result.Objects.Count > 0)
            {
                result.ContinuationToken = result.Objects[result.Objects.Count - 1].GUID;
            }

            return result;
        }

        public async Task<ApiKey> UpdateAsync(ApiKey apiKey, CancellationToken token = default)
        {
            if (apiKey == null) throw new ArgumentNullException(nameof(apiKey));

            string query =
                "UPDATE " + Table("apikeys") + " SET " +
                Column("tenantid") + " = '" + Sanitize(apiKey.TenantId) + "', " +
                Column("userid") + " = '" + Sanitize(apiKey.UserId) + "', " +
                Column("name") + " = '" + Sanitize(apiKey.Name) + "', " +
                Column("apikey") + " = '" + Sanitize(apiKey.Key) + "', " +
                Column("secretkeysha256") + " = '" + Sanitize(apiKey.SecretKeySha256) + "', " +
                Column("secretkeylast4") + " = '" + Sanitize(apiKey.SecretKeyLast4) + "', " +
                Column("active") + " = " + Bool(apiKey.Active) + ", " +
                Column("isadmin") + " = " + Bool(apiKey.IsAdmin) + " " +
                "WHERE " + Column("guid") + " = '" + Sanitize(apiKey.GUID) + "';";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return apiKey;
        }

        public Task DeleteByGuidAsync(string guid, CancellationToken token = default)
        {
            return _Driver.ExecuteQueryAsync("DELETE FROM " + Table("apikeys") + " WHERE " + Column("guid") + " = '" + Sanitize(guid) + "';", true, token);
        }

        public async Task<bool> ExistsActiveKeyAsync(string key, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) return false;
            DataTable result = await _Driver.ExecuteQueryAsync("SELECT COUNT(*) FROM " + Table("apikeys") + " WHERE " + Column("apikey") + " = '" + Sanitize(key) + "' AND " + Column("active") + " = " + Bool(true) + ";", false, token).ConfigureAwait(false);
            return result != null && result.Rows.Count > 0 && Convert.ToInt32(result.Rows[0][0], CultureInfo.InvariantCulture) > 0;
        }

        public async Task<ApiKey> AuthenticateAsync(string key, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) return null;
            DataTable result = await _Driver.ExecuteQueryAsync(SelectOne("apikey = '" + Sanitize(key) + "' AND active = " + Bool(true)), false, token).ConfigureAwait(false);
            return result == null || result.Rows.Count == 0 ? null : DataRowToApiKey(result.Rows[0]);
        }

        private string BuildInsertQuery(ApiKey apiKey, bool returnId)
        {
            string columnList = _DatabaseType switch
            {
                DatabaseTypeEnum.Mysql => "(`guid`, `tenantid`, `userid`, `name`, `apikey`, `secretkeysha256`, `secretkeylast4`, `active`, `isadmin`, `createdutc`)",
                DatabaseTypeEnum.SqlServer => "([guid], [tenantid], [userid], [name], [apikey], [secretkeysha256], [secretkeylast4], [active], [isadmin], [createdutc])",
                _ => "(guid, tenantid, userid, name, apikey, secretkeysha256, secretkeylast4, active, isadmin, createdutc)"
            };
            string output = returnId && _DatabaseType == DatabaseTypeEnum.SqlServer ? " OUTPUT INSERTED.[id]" : String.Empty;
            string returning = returnId && _DatabaseType == DatabaseTypeEnum.Postgresql ? " RETURNING id" : String.Empty;
            return
                "INSERT INTO " + Table("apikeys") + " " + columnList + output + " VALUES (" +
                "'" + Sanitize(apiKey.GUID) + "', " +
                "'" + Sanitize(apiKey.TenantId) + "', " +
                "'" + Sanitize(apiKey.UserId) + "', " +
                "'" + Sanitize(apiKey.Name) + "', " +
                "'" + Sanitize(apiKey.Key) + "', " +
                "'" + Sanitize(apiKey.SecretKeySha256) + "', " +
                "'" + Sanitize(apiKey.SecretKeyLast4) + "', " +
                Bool(apiKey.Active) + ", " +
                Bool(apiKey.IsAdmin) + ", " +
                "'" + FormatTimestamp(apiKey.CreatedUtc) + "'" +
                ")" + returning + ";";
        }

        private List<ApiKey> RowsToApiKeys(DataTable result)
        {
            List<ApiKey> apiKeys = new List<ApiKey>();
            if (result == null) return apiKeys;
            foreach (DataRow row in result.Rows) apiKeys.Add(DataRowToApiKey(row));
            return apiKeys;
        }

        private ApiKey DataRowToApiKey(DataRow row)
        {
            return new ApiKey
            {
                RowId = Convert.ToInt32(row["id"], CultureInfo.InvariantCulture),
                GUID = GetString(row, "guid"),
                TenantId = GetString(row, "tenantid"),
                UserId = GetString(row, "userid"),
                Name = GetString(row, "name"),
                Key = GetString(row, "apikey"),
                SecretKeySha256 = GetString(row, "secretkeysha256"),
                SecretKeyLast4 = GetString(row, "secretkeylast4"),
                Active = ToBool(GetString(row, "active")),
                IsAdmin = ToBool(GetString(row, "isadmin")),
                CreatedUtc = ParseDate(GetString(row, "createdutc"))
            };
        }

        private string SelectOne(string where)
        {
            return _DatabaseType == DatabaseTypeEnum.SqlServer
                ? "SELECT TOP 1 * FROM " + Table("apikeys") + " WHERE " + where + ";"
                : "SELECT * FROM " + Table("apikeys") + " WHERE " + where + " LIMIT 1;";
        }

        private string Table(string name) => _DatabaseType == DatabaseTypeEnum.SqlServer ? "[" + name + "]" : _DatabaseType == DatabaseTypeEnum.Mysql ? "`" + name + "`" : name;
        private string Column(string name) => _DatabaseType == DatabaseTypeEnum.SqlServer ? "[" + name + "]" : _DatabaseType == DatabaseTypeEnum.Mysql ? "`" + name + "`" : name;
        private string Bool(bool value) => _DatabaseType == DatabaseTypeEnum.Postgresql ? (value ? "TRUE" : "FALSE") : (value ? "1" : "0");
        private string FormatTimestamp(DateTime value) => value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture);
        private string Sanitize(string value) => String.IsNullOrEmpty(value) ? String.Empty : value.Replace("'", "''");
        private string GetString(DataRow row, string columnName) => row.Table.Columns.Contains(columnName) && row[columnName] != DBNull.Value ? row[columnName]?.ToString() ?? String.Empty : String.Empty;
        private bool ToBool(string value) => value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
        private DateTime ParseDate(string value) => DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }
}
