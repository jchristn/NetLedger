namespace NetLedger.Database.Sqlite.Implementations
{
    using System;
    using System.Data;
    using System.Globalization;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Database.Interfaces;
    using NetLedger.Database.Sqlite.Queries;

    /// <summary>
    /// SQLite account-user map methods.
    /// </summary>
    internal class AccountUserMapMethods : IAccountUserMapMethods
    {
        private readonly SqliteDatabaseDriver _Driver;

        /// <summary>
        /// Instantiate the account-user map methods.
        /// </summary>
        /// <param name="driver">SQLite database driver.</param>
        public AccountUserMapMethods(SqliteDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <inheritdoc />
        public async Task<AccountUserMap> CreateAsync(AccountUserMap map, CancellationToken token = default)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (String.IsNullOrEmpty(map.Id)) map.Id = NetLedgerId.Generate(IdentifierPrefixes.Assignment);
            map.CreatedUtc = map.CreatedUtc == DateTime.MinValue ? DateTime.UtcNow : map.CreatedUtc;

            string query =
                "INSERT OR REPLACE INTO accountusermaps (id, tenantid, accountid, userid, createdutc) VALUES (" +
                "'" + Sanitize(map.Id) + "', " +
                "'" + Sanitize(map.TenantId) + "', " +
                "'" + Sanitize(map.AccountId) + "', " +
                "'" + Sanitize(map.UserId) + "', " +
                "'" + map.CreatedUtc.ToString(SetupQueries.TimestampFormat) + "');";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return map;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string accountId, string userId, CancellationToken token = default)
        {
            string query =
                "SELECT COUNT(*) FROM accountusermaps WHERE tenantid = '" + Sanitize(tenantId) + "' " +
                "AND accountid = '" + Sanitize(accountId) + "' AND userid = '" + Sanitize(userId) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            if (result == null || result.Rows.Count == 0) return false;
            return Convert.ToInt32(result.Rows[0][0]) > 0;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<AccountUserMap>> EnumerateAsync(EnumerationQuery query, CancellationToken token = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            EnumerationResult<AccountUserMap> result = new EnumerationResult<AccountUserMap>();
            result.MaxResults = query.MaxResults;
            result.Skip = query.Skip;

            string where = BuildWhereClause(query);
            DataTable count = await _Driver.ExecuteQueryAsync("SELECT COUNT(*) FROM accountusermaps" + where + ";", false, token).ConfigureAwait(false);
            if (count != null && count.Rows.Count > 0) result.TotalRecords = Convert.ToInt64(count.Rows[0][0]);

            StringBuilder sql = new StringBuilder("SELECT * FROM accountusermaps");
            sql.Append(where);
            sql.Append(" ORDER BY createdutc DESC LIMIT ");
            sql.Append(query.MaxResults);
            sql.Append(" OFFSET ");
            sql.Append(query.Skip);
            sql.Append(";");

            DataTable rows = await _Driver.ExecuteQueryAsync(sql.ToString(), false, token).ConfigureAwait(false);
            if (rows != null)
            {
                foreach (DataRow row in rows.Rows)
                {
                    result.Objects.Add(DataRowToMap(row));
                }
            }

            result.RecordsRemaining = Math.Max(0, result.TotalRecords - query.Skip - result.Objects.Count);
            result.EndOfResults = result.RecordsRemaining == 0;
            return result;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string accountId, string userId, CancellationToken token = default)
        {
            string query =
                "DELETE FROM accountusermaps WHERE tenantid = '" + Sanitize(tenantId) + "' " +
                "AND accountid = '" + Sanitize(accountId) + "' AND userid = '" + Sanitize(userId) + "';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return true;
        }

        private string BuildWhereClause(EnumerationQuery query)
        {
            StringBuilder where = new StringBuilder();
            if (!String.IsNullOrEmpty(query.TenantId))
            {
                AppendCondition(where, "tenantid = '" + Sanitize(query.TenantId) + "'");
            }

            if (!String.IsNullOrEmpty(query.UserId))
            {
                AppendCondition(where, "userid = '" + Sanitize(query.UserId) + "'");
            }

            return where.ToString();
        }

        private static void AppendCondition(StringBuilder where, string condition)
        {
            where.Append(where.Length == 0 ? " WHERE " : " AND ");
            where.Append(condition);
        }

        private AccountUserMap DataRowToMap(DataRow row)
        {
            AccountUserMap map = new AccountUserMap();
            map.Id = row.Table.Columns.Contains("id") ? row["id"]?.ToString() ?? String.Empty : NetLedgerId.Generate(IdentifierPrefixes.Assignment);
            map.TenantId = row["tenantid"]?.ToString() ?? String.Empty;
            map.AccountId = row["accountid"]?.ToString() ?? String.Empty;
            map.UserId = row["userid"]?.ToString() ?? String.Empty;
            map.CreatedUtc = ParseTimestamp(row["createdutc"]?.ToString());
            return map;
        }

        private DateTime ParseTimestamp(string? value)
        {
            if (String.IsNullOrEmpty(value)) return DateTime.MinValue;
            return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        }

        private string Sanitize(string? input)
        {
            return String.IsNullOrEmpty(input) ? String.Empty : input.Replace("'", "''");
        }
    }
}
