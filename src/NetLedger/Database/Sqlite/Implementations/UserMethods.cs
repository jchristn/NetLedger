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
    /// SQLite user methods.
    /// </summary>
    internal class UserMethods : IUserMethods
    {
        private readonly SqliteDatabaseDriver _Driver;

        /// <summary>
        /// Instantiate the user methods.
        /// </summary>
        /// <param name="driver">SQLite database driver.</param>
        public UserMethods(SqliteDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <inheritdoc />
        public async Task<User> CreateAsync(User user, CancellationToken token = default)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (String.IsNullOrEmpty(user.Id)) user.Id = NetLedgerId.Generate(IdentifierPrefixes.User);
            user.Email = user.Email.Trim().ToLowerInvariant();
            user.CreatedUtc = user.CreatedUtc == DateTime.MinValue ? DateTime.UtcNow : user.CreatedUtc;
            user.LastUpdateUtc = DateTime.UtcNow;

            string query =
                "INSERT INTO users (id, tenantid, firstname, lastname, email, passwordsha256, isadmin, istenantadmin, active, isprotected, createdutc, lastupdateutc) VALUES (" +
                "'" + Sanitize(user.Id) + "', " +
                "'" + Sanitize(user.TenantId) + "', " +
                ToSql(user.FirstName) + ", " +
                ToSql(user.LastName) + ", " +
                "'" + Sanitize(user.Email) + "', " +
                "'" + Sanitize(user.PasswordSha256) + "', " +
                (user.IsAdmin ? "1" : "0") + ", " +
                (user.IsTenantAdmin ? "1" : "0") + ", " +
                (user.Active ? "1" : "0") + ", " +
                (user.IsProtected ? "1" : "0") + ", " +
                "'" + user.CreatedUtc.ToString(SetupQueries.TimestampFormat) + "', " +
                "'" + user.LastUpdateUtc.ToString(SetupQueries.TimestampFormat) + "');";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return user;
        }

        /// <inheritdoc />
        public async Task<User?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            DataTable result = await _Driver.ExecuteQueryAsync("SELECT * FROM users WHERE tenantid = '" + Sanitize(tenantId) + "' AND id = '" + Sanitize(id) + "' LIMIT 1;", false, token).ConfigureAwait(false);
            if (result == null || result.Rows.Count == 0) return null;
            return DataRowToUser(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<User?> ReadByEmailAsync(string tenantId, string email, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(email)) throw new ArgumentNullException(nameof(email));
            DataTable result = await _Driver.ExecuteQueryAsync("SELECT * FROM users WHERE tenantid = '" + Sanitize(tenantId) + "' AND email = '" + Sanitize(email.Trim().ToLowerInvariant()) + "' LIMIT 1;", false, token).ConfigureAwait(false);
            if (result == null || result.Rows.Count == 0) return null;
            return DataRowToUser(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<User>> EnumerateAsync(EnumerationQuery query, CancellationToken token = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));

            EnumerationResult<User> result = new EnumerationResult<User>();
            result.MaxResults = query.MaxResults;
            result.Skip = query.Skip;

            string where = BuildWhere(query);
            DataTable count = await _Driver.ExecuteQueryAsync("SELECT COUNT(*) FROM users" + where + ";", false, token).ConfigureAwait(false);
            if (count != null && count.Rows.Count > 0) result.TotalRecords = Convert.ToInt64(count.Rows[0][0]);

            StringBuilder sql = new StringBuilder("SELECT * FROM users");
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
                    result.Objects.Add(DataRowToUser(row));
                }
            }

            result.RecordsRemaining = Math.Max(0, result.TotalRecords - query.Skip - result.Objects.Count);
            result.EndOfResults = result.RecordsRemaining == 0;
            if (!result.EndOfResults && result.Objects.Count > 0) result.ContinuationToken = result.Objects[result.Objects.Count - 1].Id;
            return result;
        }

        /// <inheritdoc />
        public async Task<User> UpdateAsync(User user, CancellationToken token = default)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            user.Email = user.Email.Trim().ToLowerInvariant();
            user.LastUpdateUtc = DateTime.UtcNow;
            string query =
                "UPDATE users SET " +
                "firstname = " + ToSql(user.FirstName) + ", " +
                "lastname = " + ToSql(user.LastName) + ", " +
                "email = '" + Sanitize(user.Email) + "', " +
                "passwordsha256 = '" + Sanitize(user.PasswordSha256) + "', " +
                "isadmin = " + (user.IsAdmin ? "1" : "0") + ", " +
                "istenantadmin = " + (user.IsTenantAdmin ? "1" : "0") + ", " +
                "active = " + (user.Active ? "1" : "0") + ", " +
                "isprotected = " + (user.IsProtected ? "1" : "0") + ", " +
                "lastupdateutc = '" + user.LastUpdateUtc.ToString(SetupQueries.TimestampFormat) + "' " +
                "WHERE tenantid = '" + Sanitize(user.TenantId) + "' AND id = '" + Sanitize(user.Id) + "';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return user;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            User? user = await ReadAsync(tenantId, id, token).ConfigureAwait(false);
            if (user == null) return false;
            if (user.IsProtected) throw new InvalidOperationException("User '" + id + "' is protected and cannot be deleted.");
            await _Driver.ExecuteQueryAsync("DELETE FROM users WHERE tenantid = '" + Sanitize(tenantId) + "' AND id = '" + Sanitize(id) + "';", true, token).ConfigureAwait(false);
            return true;
        }

        private string BuildWhere(EnumerationQuery query)
        {
            StringBuilder where = new StringBuilder();
            string prefix = " WHERE ";

            if (!String.IsNullOrEmpty(query.TenantId))
            {
                where.Append(prefix);
                where.Append("tenantid = '" + Sanitize(query.TenantId) + "'");
                prefix = " AND ";
            }

            if (!String.IsNullOrEmpty(query.SearchTerm))
            {
                where.Append(prefix);
                where.Append("(email LIKE '%" + Sanitize(query.SearchTerm) + "%' OR firstname LIKE '%" + Sanitize(query.SearchTerm) + "%' OR lastname LIKE '%" + Sanitize(query.SearchTerm) + "%')");
            }

            return where.ToString();
        }

        private User DataRowToUser(DataRow row)
        {
            User user = new User();
            user.Id = row["id"]?.ToString() ?? String.Empty;
            user.TenantId = row["tenantid"]?.ToString() ?? String.Empty;
            user.FirstName = NullIfEmpty(row["firstname"]?.ToString());
            user.LastName = NullIfEmpty(row["lastname"]?.ToString());
            user.Email = row["email"]?.ToString() ?? String.Empty;
            user.PasswordSha256 = row["passwordsha256"]?.ToString() ?? String.Empty;
            user.IsAdmin = Convert.ToInt32(row["isadmin"]) == 1;
            user.IsTenantAdmin = Convert.ToInt32(row["istenantadmin"]) == 1;
            user.Active = Convert.ToInt32(row["active"]) == 1;
            user.IsProtected = Convert.ToInt32(row["isprotected"]) == 1;
            user.CreatedUtc = ParseTimestamp(row["createdutc"]?.ToString());
            user.LastUpdateUtc = ParseTimestamp(row["lastupdateutc"]?.ToString());
            return user;
        }

        private DateTime ParseTimestamp(string? value)
        {
            if (String.IsNullOrEmpty(value)) return DateTime.MinValue;
            return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        }

        private string? NullIfEmpty(string? value)
        {
            return String.IsNullOrEmpty(value) ? null : value;
        }

        private string ToSql(string? value)
        {
            return String.IsNullOrEmpty(value) ? "NULL" : "'" + Sanitize(value) + "'";
        }

        private string Sanitize(string? input)
        {
            return String.IsNullOrEmpty(input) ? String.Empty : input.Replace("'", "''");
        }
    }
}
