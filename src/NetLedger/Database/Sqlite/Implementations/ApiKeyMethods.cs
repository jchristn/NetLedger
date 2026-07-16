namespace NetLedger.Database.Sqlite.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Database.Interfaces;
    using NetLedger.Database.Sqlite.Queries;

    /// <summary>
    /// SQLite implementation of API key methods.
    /// </summary>
    internal class ApiKeyMethods : IApiKeyMethods
    {
        #region Private-Members

        private readonly SqliteDatabaseDriver _Driver;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the API key methods.
        /// </summary>
        /// <param name="driver">SQLite database driver.</param>
        internal ApiKeyMethods(SqliteDatabaseDriver driver)
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
                "INSERT INTO apikeys (guid, tenantid, userid, name, apikey, secretkeysha256, secretkeylast4, active, isadmin, createdutc) VALUES (" +
                "'" + apiKey.GUID.ToString() + "', " +
                "'" + Sanitize(apiKey.TenantId) + "', " +
                "'" + Sanitize(apiKey.UserId) + "', " +
                "'" + Sanitize(apiKey.Name) + "', " +
                "'" + Sanitize(apiKey.Key) + "', " +
                "'" + Sanitize(apiKey.SecretKeySha256) + "', " +
                "'" + Sanitize(apiKey.SecretKeyLast4) + "', " +
                (apiKey.Active ? "1" : "0") + ", " +
                (apiKey.IsAdmin ? "1" : "0") + ", " +
                "'" + apiKey.CreatedUtc.ToString(SetupQueries.TimestampFormat) + "'" +
                "); SELECT last_insert_rowid();";

            DataTable result = await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            if (result != null && result.Rows.Count > 0)
            {
                apiKey.RowId = Convert.ToInt32(result.Rows[0][0]);
            }

            return apiKey;
        }

        /// <inheritdoc />
        public async Task<ApiKey> ReadByGuidAsync(string guid, CancellationToken token = default)
        {
            string query = "SELECT * FROM apikeys WHERE guid = '" + Sanitize(guid) + "' LIMIT 1;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return null;

            return DataRowToApiKey(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<ApiKey> ReadByKeyAsync(string key, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            string query = "SELECT * FROM apikeys WHERE apikey = '" + Sanitize(key) + "' LIMIT 1;";
            DataTable result = await _Driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);

            if (result == null || result.Rows.Count == 0) return null;

            return DataRowToApiKey(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<List<ApiKey>> ReadAllAsync(CancellationToken token = default)
        {
            string query = "SELECT * FROM apikeys ORDER BY createdutc DESC;";
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

            FilterBuilder filter = FilterBuilder.FromEnumerationQuery(query);
            string where = filter.BuildCredentialConditions(DatabaseTypeEnum.Sqlite);
            string whereClause = String.IsNullOrEmpty(where) ? String.Empty : " WHERE " + where;

            // Get total count
            string countQuery = "SELECT COUNT(*) FROM apikeys" + whereClause + ";";
            DataTable countResult = await _Driver.ExecuteQueryAsync(countQuery, false, token).ConfigureAwait(false);
            if (countResult != null && countResult.Rows.Count > 0)
            {
                result.TotalRecords = Convert.ToInt64(countResult.Rows[0][0]);
            }

            // Build main query
            StringBuilder mainQuery = new StringBuilder("SELECT * FROM apikeys");
            mainQuery.Append(whereClause);
            mainQuery.Append(" " + filter.GetOrderByClause(DatabaseTypeEnum.Sqlite));
            mainQuery.Append(" " + filter.GetLimitOffsetClause(DatabaseTypeEnum.Sqlite));
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
                "UPDATE apikeys SET " +
                "tenantid = '" + Sanitize(apiKey.TenantId) + "', " +
                "userid = '" + Sanitize(apiKey.UserId) + "', " +
                "name = '" + Sanitize(apiKey.Name) + "', " +
                "apikey = '" + Sanitize(apiKey.Key) + "', " +
                "secretkeysha256 = '" + Sanitize(apiKey.SecretKeySha256) + "', " +
                "secretkeylast4 = '" + Sanitize(apiKey.SecretKeyLast4) + "', " +
                "active = " + (apiKey.Active ? "1" : "0") + ", " +
                "isadmin = " + (apiKey.IsAdmin ? "1" : "0") + " " +
                "WHERE guid = '" + Sanitize(apiKey.GUID) + "';";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);

            return apiKey;
        }

        /// <inheritdoc />
        public async Task DeleteByGuidAsync(string guid, CancellationToken token = default)
        {
            string query = "DELETE FROM apikeys WHERE guid = '" + Sanitize(guid) + "';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> ExistsActiveKeyAsync(string key, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) return false;

            string query = "SELECT COUNT(*) FROM apikeys WHERE apikey = '" + Sanitize(key) + "' AND active = 1;";
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

            string query = "SELECT * FROM apikeys WHERE apikey = '" + Sanitize(key) + "' AND active = 1 LIMIT 1;";
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
            apiKey.RowId = Convert.ToInt32(row["id"]);
            apiKey.GUID = row["guid"].ToString();
            apiKey.TenantId = HasColumn(row, "tenantid") ? row["tenantid"]?.ToString() ?? String.Empty : String.Empty;
            apiKey.UserId = HasColumn(row, "userid") ? row["userid"]?.ToString() ?? String.Empty : String.Empty;
            apiKey.Name = row["name"]?.ToString() ?? String.Empty;
            apiKey.Key = row["apikey"]?.ToString() ?? String.Empty;
            apiKey.SecretKeySha256 = HasColumn(row, "secretkeysha256") ? row["secretkeysha256"]?.ToString() ?? String.Empty : String.Empty;
            apiKey.SecretKeyLast4 = HasColumn(row, "secretkeylast4") ? row["secretkeylast4"]?.ToString() ?? String.Empty : String.Empty;
            apiKey.Active = Convert.ToInt32(row["active"]) == 1;
            apiKey.IsAdmin = Convert.ToInt32(row["isadmin"]) == 1;
            apiKey.CreatedUtc = DateTime.Parse(
                row["createdutc"].ToString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            return apiKey;
        }

        private bool HasColumn(DataRow row, string columnName)
        {
            return row.Table.Columns.Contains(columnName);
        }

        #endregion
    }
}



