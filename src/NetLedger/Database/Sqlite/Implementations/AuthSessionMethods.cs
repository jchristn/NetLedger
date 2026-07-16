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
    /// SQLite authentication session methods.
    /// </summary>
    internal class AuthSessionMethods : IAuthSessionMethods
    {
        private readonly SqliteDatabaseDriver _Driver;

        /// <summary>
        /// Instantiate the authentication session methods.
        /// </summary>
        /// <param name="driver">SQLite database driver.</param>
        public AuthSessionMethods(SqliteDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <inheritdoc />
        public async Task<AuthSession> CreateAsync(AuthSession session, CancellationToken token = default)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (String.IsNullOrEmpty(session.Id)) session.Id = NetLedgerId.Generate(IdentifierPrefixes.Session);
            if (String.IsNullOrEmpty(session.Token)) session.Token = NetLedgerId.Generate("tok_");
            session.CreatedUtc = session.CreatedUtc == DateTime.MinValue ? DateTime.UtcNow : session.CreatedUtc;

            string query =
                "INSERT INTO authsessions (id, tenantid, userid, token, active, expiresutc, revokedutc, createdutc, lastupdateutc) VALUES (" +
                "'" + Sanitize(session.Id) + "', " +
                "'" + Sanitize(session.TenantId) + "', " +
                "'" + Sanitize(session.UserId) + "', " +
                "'" + Sanitize(session.Token) + "', " +
                (session.Active ? "1" : "0") + ", " +
                "'" + session.ExpiresUtc.ToString(SetupQueries.TimestampFormat) + "', " +
                "NULL, " +
                "'" + session.CreatedUtc.ToString(SetupQueries.TimestampFormat) + "', " +
                "'" + DateTime.UtcNow.ToString(SetupQueries.TimestampFormat) + "');";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return session;
        }

        /// <inheritdoc />
        public async Task<AuthSession?> ReadByTokenAsync(string tokenValue, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tokenValue)) throw new ArgumentNullException(nameof(tokenValue));
            DataTable result = await _Driver.ExecuteQueryAsync("SELECT * FROM authsessions WHERE token = '" + Sanitize(tokenValue) + "' LIMIT 1;", false, token).ConfigureAwait(false);
            if (result == null || result.Rows.Count == 0) return null;
            return DataRowToSession(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<bool> RevokeAsync(string tenantId, string id, string reason, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string now = DateTime.UtcNow.ToString(SetupQueries.TimestampFormat);
            string query =
                "UPDATE authsessions SET active = 0, revokedutc = '" + now + "', lastupdateutc = '" + now + "' " +
                "WHERE tenantid = '" + Sanitize(tenantId) + "' AND id = '" + Sanitize(id) + "';";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return true;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<AuthSession>> EnumerateAsync(EnumerationQuery query, CancellationToken token = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            EnumerationResult<AuthSession> result = new EnumerationResult<AuthSession>();
            result.MaxResults = query.MaxResults;
            result.Skip = query.Skip;

            string where = String.IsNullOrEmpty(query.TenantId) ? String.Empty : " WHERE tenantid = '" + Sanitize(query.TenantId) + "'";
            DataTable count = await _Driver.ExecuteQueryAsync("SELECT COUNT(*) FROM authsessions" + where + ";", false, token).ConfigureAwait(false);
            if (count != null && count.Rows.Count > 0) result.TotalRecords = Convert.ToInt64(count.Rows[0][0]);

            StringBuilder sql = new StringBuilder("SELECT * FROM authsessions");
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
                    result.Objects.Add(DataRowToSession(row));
                }
            }

            result.RecordsRemaining = Math.Max(0, result.TotalRecords - query.Skip - result.Objects.Count);
            result.EndOfResults = result.RecordsRemaining == 0;
            return result;
        }

        private AuthSession DataRowToSession(DataRow row)
        {
            AuthSession session = new AuthSession();
            session.Id = row["id"]?.ToString() ?? String.Empty;
            session.TenantId = row["tenantid"]?.ToString() ?? String.Empty;
            session.UserId = row["userid"]?.ToString();
            session.Token = row["token"]?.ToString() ?? String.Empty;
            session.Active = Convert.ToInt32(row["active"]) == 1;
            session.ExpiresUtc = ParseTimestamp(row["expiresutc"]?.ToString());
            session.CreatedUtc = ParseTimestamp(row["createdutc"]?.ToString());
            return session;
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
