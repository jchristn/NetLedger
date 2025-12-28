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
    /// SQL Server implementation of API key methods.
    /// </summary>
    internal class ApiKeyMethods : IApiKeyMethods
    {
        #region Private-Members

        private readonly SqlServerDatabaseDriver _Driver;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the API key methods.
        /// </summary>
        /// <param name="driver">SQL Server database driver.</param>
        internal ApiKeyMethods(SqlServerDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<ApiKey> CreateAsync(ApiKey apiKey, CancellationToken token = default)
        {
            if (apiKey == null) throw new ArgumentNullException(nameof(apiKey));

            string query =
                "INSERT INTO [apikeys] ([guid], [name], [apikey], [active], [isadmin], [createdutc]) " +
                "OUTPUT INSERTED.[id] " +
                "VALUES (" +
                "'" + apiKey.GUID.ToString() + "', " +
                "'" + Sanitize(apiKey.Name) + "', " +
                "'" + Sanitize(apiKey.Key) + "', " +
                (apiKey.Active ? "1" : "0") + ", " +
                (apiKey.IsAdmin ? "1" : "0") + ", " +
                "'" + apiKey.CreatedUtc.ToString(SetupQueries.TimestampFormat) + "'" +
                ");";

            DataTable result = await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (result != null && result.Rows.Count > 0)
            {
                apiKey.Id = Convert.ToInt32(result.Rows[0][0]);
            }

            return apiKey;
        }

        /// <inheritdoc />
        public async Task<ApiKey> ReadByGuidAsync(Guid guid, CancellationToken token = default)
        {
            string query = "SELECT TOP 1 * FROM [apikeys] WHERE [guid] = '" + guid.ToString() + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return null;

            return DataRowToApiKey(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<ApiKey> ReadByKeyAsync(string key, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            string query = "SELECT TOP 1 * FROM [apikeys] WHERE [apikey] = '" + Sanitize(key) + "';";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return null;

            return DataRowToApiKey(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<List<ApiKey>> ReadAllAsync(CancellationToken token = default)
        {
            string query = "SELECT * FROM [apikeys] ORDER BY [createdutc] DESC;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            List<ApiKey> apiKeys = new List<ApiKey>();

            if (result != null && result.Rows.Count > 0)
            {
                foreach (DataRow row in result.Rows)
                {
                    apiKeys.Add(DataRowToApiKey(row));
                }
            }

            return apiKeys;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<ApiKey>> EnumerateAsync(EnumerationQuery query, CancellationToken token = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));

            EnumerationResult<ApiKey> result = new EnumerationResult<ApiKey>();
            result.MaxResults = query.MaxResults;
            result.Skip = query.Skip;

            // Build filter
            FilterBuilder filter = FilterBuilder.FromEnumerationQuery(query);

            // Get total count
            string countQuery = "SELECT COUNT(*) FROM [apikeys];";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            if (countResult != null && countResult.Rows.Count > 0)
            {
                result.TotalRecords = Convert.ToInt64(countResult.Rows[0][0]);
            }

            // Build main query
            StringBuilder mainQuery = new StringBuilder("SELECT * FROM [apikeys]");
            mainQuery.Append(" " + filter.GetOrderByClause(DatabaseTypeEnum.SqlServer));
            mainQuery.Append(" " + filter.GetLimitOffsetClause(DatabaseTypeEnum.SqlServer));
            mainQuery.Append(";");

            DataTable dataResult = await _Driver.ExecuteQueryAsync(mainQuery.ToString(), false, token).ConfigureAwait(false);

            if (dataResult != null && dataResult.Rows.Count > 0)
            {
                foreach (DataRow row in dataResult.Rows)
                {
                    result.Objects.Add(DataRowToApiKey(row));
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
        public async Task<ApiKey> UpdateAsync(ApiKey apiKey, CancellationToken token = default)
        {
            if (apiKey == null) throw new ArgumentNullException(nameof(apiKey));

            string query =
                "UPDATE [apikeys] SET " +
                "[name] = '" + Sanitize(apiKey.Name) + "', " +
                "[apikey] = '" + Sanitize(apiKey.Key) + "', " +
                "[active] = " + (apiKey.Active ? "1" : "0") + ", " +
                "[isadmin] = " + (apiKey.IsAdmin ? "1" : "0") + " " +
                "WHERE [guid] = '" + apiKey.GUID.ToString() + "';";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            return apiKey;
        }

        /// <inheritdoc />
        public async Task DeleteByGuidAsync(Guid guid, CancellationToken token = default)
        {
            string query = "DELETE FROM [apikeys] WHERE [guid] = '" + guid.ToString() + "';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> ExistsActiveKeyAsync(string key, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) return false;

            string query = "SELECT COUNT(*) FROM [apikeys] WHERE [apikey] = '" + Sanitize(key) + "' AND [active] = 1;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result != null && result.Rows.Count > 0)
            {
                return Convert.ToInt32(result.Rows[0][0]) > 0;
            }

            return false;
        }

        /// <inheritdoc />
        public async Task<ApiKey> AuthenticateAsync(string key, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) return null;

            string query = "SELECT TOP 1 * FROM [apikeys] WHERE [apikey] = '" + Sanitize(key) + "' AND [active] = 1;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return null;

            return DataRowToApiKey(result.Rows[0]);
        }

        #endregion

        #region Private-Methods

        private string Sanitize(string input)
        {
            if (String.IsNullOrEmpty(input)) return String.Empty;
            return input.Replace("'", "''");
        }

        private ApiKey DataRowToApiKey(DataRow row)
        {
            ApiKey apiKey = new ApiKey();
            apiKey.Id = Convert.ToInt32(row["id"]);
            apiKey.GUID = Guid.Parse(row["guid"].ToString()!);
            apiKey.Name = row["name"]?.ToString() ?? String.Empty;
            apiKey.Key = row["apikey"]?.ToString() ?? String.Empty;

            // SQL Server returns bit as True/False strings
            string activeStr = row["active"]?.ToString() ?? "False";
            apiKey.Active = activeStr.Equals("True", StringComparison.OrdinalIgnoreCase) || activeStr == "1";

            string isAdminStr = row["isadmin"]?.ToString() ?? "False";
            apiKey.IsAdmin = isAdminStr.Equals("True", StringComparison.OrdinalIgnoreCase) || isAdminStr == "1";

            apiKey.CreatedUtc = DateTime.Parse(row["createdutc"].ToString()!);
            return apiKey;
        }

        #endregion
    }
}
