namespace NetLedger.Database.SqlServer.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Database.Interfaces;
    using NetLedger.Database.SqlServer.Queries;

    /// <summary>
    /// SQL Server implementation of account methods.
    /// </summary>
    internal class AccountMethods : IAccountMethods
    {
        #region Private-Members

        private readonly SqlServerDatabaseDriver _Driver;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the account methods.
        /// </summary>
        /// <param name="driver">SQL Server database driver.</param>
        internal AccountMethods(SqlServerDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<Account> CreateAsync(Account account, CancellationToken token = default)
        {
            if (account == null) throw new ArgumentNullException(nameof(account));

            string query =
                "INSERT INTO [accounts] ([guid], [name], [notes], [createdutc]) " +
                "OUTPUT INSERTED.[id] " +
                "VALUES (" +
                "'" + account.GUID.ToString() + "', " +
                "'" + Sanitize(account.Name) + "', " +
                (account.Notes != null ? "'" + Sanitize(account.Notes) + "'" : "NULL") + ", " +
                "'" + account.CreatedUtc.ToString(SetupQueries.TimestampFormat) + "'" +
                ");";

            DataTable result = await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (result != null && result.Rows.Count > 0)
            {
                account.Id = Convert.ToInt32(result.Rows[0][0]);
            }

            return account;
        }

        /// <inheritdoc />
        public async Task<Account> ReadByGuidAsync(Guid guid, CancellationToken token = default)
        {
            string query = "SELECT TOP 1 * FROM [accounts] WHERE [guid] = '" + guid.ToString() + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return null;

            return DataRowToAccount(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<Account> ReadByNameAsync(string name, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

            string query = "SELECT TOP 1 * FROM [accounts] WHERE [name] = '" + Sanitize(name) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return null;

            return DataRowToAccount(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<List<Account>> ReadAllAsync(CancellationToken token = default)
        {
            string query = "SELECT * FROM [accounts] ORDER BY [createdutc] DESC;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            List<Account> accounts = new List<Account>();

            if (result != null && result.Rows.Count > 0)
            {
                foreach (DataRow row in result.Rows)
                {
                    accounts.Add(DataRowToAccount(row));
                }
            }

            return accounts;
        }

        /// <inheritdoc />
        public async Task<List<Account>> SearchByNameAsync(string searchTerm, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(searchTerm)) throw new ArgumentNullException(nameof(searchTerm));

            string query = "SELECT * FROM [accounts] WHERE [name] LIKE '%" + Sanitize(searchTerm) + "%' ORDER BY [createdutc] DESC;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            List<Account> accounts = new List<Account>();

            if (result != null && result.Rows.Count > 0)
            {
                foreach (DataRow row in result.Rows)
                {
                    accounts.Add(DataRowToAccount(row));
                }
            }

            return accounts;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<Account>> EnumerateAsync(EnumerationQuery query, CancellationToken token = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));

            EnumerationResult<Account> result = new EnumerationResult<Account>();
            result.MaxResults = query.MaxResults;
            result.Skip = query.Skip;

            // Build filter
            FilterBuilder filter = FilterBuilder.FromEnumerationQuery(query);

            // Get total count
            string countQuery = "SELECT COUNT(*) FROM [accounts];";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            if (countResult != null && countResult.Rows.Count > 0)
            {
                result.TotalRecords = Convert.ToInt64(countResult.Rows[0][0]);
            }

            // Build main query
            StringBuilder mainQuery = new StringBuilder("SELECT * FROM [accounts]");
            mainQuery.Append(" " + filter.GetOrderByClause(DatabaseTypeEnum.SqlServer));
            mainQuery.Append(" " + filter.GetLimitOffsetClause(DatabaseTypeEnum.SqlServer));
            mainQuery.Append(";");

            DataTable dataResult = await _Driver.ExecuteQueryAsync(mainQuery.ToString(), false, token).ConfigureAwait(false);

            if (dataResult != null && dataResult.Rows.Count > 0)
            {
                foreach (DataRow row in dataResult.Rows)
                {
                    result.Objects.Add(DataRowToAccount(row));
                }
            }

            // Calculate records remaining
            result.RecordsRemaining = Math.Max(0, result.TotalRecords - query.Skip - result.Objects.Count);
            result.EndOfResults = result.RecordsRemaining == 0;

            // Set continuation token if there are more records
            if (!result.EndOfResults && result.Objects.Count > 0)
            {
                result.ContinuationToken = result.Objects[result.Objects.Count - 1].GUID;
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<Account> UpdateAsync(Account account, CancellationToken token = default)
        {
            if (account == null) throw new ArgumentNullException(nameof(account));

            string query =
                "UPDATE [accounts] SET " +
                "[name] = '" + Sanitize(account.Name) + "', " +
                "[notes] = " + (account.Notes != null ? "'" + Sanitize(account.Notes) + "'" : "NULL") + " " +
                "WHERE [guid] = '" + account.GUID.ToString() + "';";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            return account;
        }

        /// <inheritdoc />
        public async Task DeleteByGuidAsync(Guid guid, CancellationToken token = default)
        {
            string query = "DELETE FROM [accounts] WHERE [guid] = '" + guid.ToString() + "';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> ExistsByGuidAsync(Guid guid, CancellationToken token = default)
        {
            string query = "SELECT COUNT(*) FROM [accounts] WHERE [guid] = '" + guid.ToString() + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result != null && result.Rows.Count > 0)
            {
                return Convert.ToInt32(result.Rows[0][0]) > 0;
            }

            return false;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsByNameAsync(string name, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

            string query = "SELECT COUNT(*) FROM [accounts] WHERE [name] = '" + Sanitize(name) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result != null && result.Rows.Count > 0)
            {
                return Convert.ToInt32(result.Rows[0][0]) > 0;
            }

            return false;
        }

        /// <inheritdoc />
        public async Task<int> GetCountAsync(CancellationToken token = default)
        {
            string query = "SELECT COUNT(*) FROM [accounts];";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result != null && result.Rows.Count > 0)
            {
                return Convert.ToInt32(result.Rows[0][0]);
            }

            return 0;
        }

        #endregion

        #region Private-Methods

        private string Sanitize(string input)
        {
            if (String.IsNullOrEmpty(input)) return String.Empty;
            return input.Replace("'", "''");
        }

        private Account DataRowToAccount(DataRow row)
        {
            Account account = new Account();
            account.Id = Convert.ToInt32(row["id"]);
            account.GUID = Guid.Parse(row["guid"].ToString()!);
            account.Name = row["name"]?.ToString() ?? String.Empty;
            account.Notes = row["notes"] != DBNull.Value ? row["notes"]?.ToString() : null;
            account.CreatedUtc = DateTime.Parse(row["createdutc"].ToString()!);
            return account;
        }

        #endregion
    }
}
