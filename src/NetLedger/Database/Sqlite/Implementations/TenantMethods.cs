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
    /// SQLite tenant methods.
    /// </summary>
    internal class TenantMethods : ITenantMethods
    {
        private readonly SqliteDatabaseDriver _Driver;

        /// <summary>
        /// Instantiate the tenant methods.
        /// </summary>
        /// <param name="driver">SQLite database driver.</param>
        public TenantMethods(SqliteDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <inheritdoc />
        public async Task<Tenant> CreateAsync(Tenant tenant, CancellationToken token = default)
        {
            if (tenant == null) throw new ArgumentNullException(nameof(tenant));
            if (String.IsNullOrEmpty(tenant.Id)) tenant.Id = NetLedgerId.Generate(IdentifierPrefixes.Tenant);
            tenant.CreatedUtc = tenant.CreatedUtc == DateTime.MinValue ? DateTime.UtcNow : tenant.CreatedUtc;
            tenant.LastUpdateUtc = DateTime.UtcNow;

            string query =
                "INSERT INTO tenants (id, parentid, name, region, active, isprotected, createdutc, lastupdateutc) VALUES (" +
                "'" + Sanitize(tenant.Id) + "', " +
                ToSql(tenant.ParentId) + ", " +
                "'" + Sanitize(tenant.Name) + "', " +
                ToSql(tenant.Region) + ", " +
                (tenant.Active ? "1" : "0") + ", " +
                (tenant.IsProtected ? "1" : "0") + ", " +
                "'" + tenant.CreatedUtc.ToString(SetupQueries.TimestampFormat) + "', " +
                "'" + tenant.LastUpdateUtc.ToString(SetupQueries.TimestampFormat) + "');";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return tenant;
        }

        /// <inheritdoc />
        public async Task<Tenant?> ReadAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            DataTable result = await _Driver.ExecuteQueryAsync("SELECT * FROM tenants WHERE id = '" + Sanitize(id) + "' LIMIT 1;", false, token).ConfigureAwait(false);
            if (result == null || result.Rows.Count == 0) return null;
            return DataRowToTenant(result.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<Tenant>> EnumerateAsync(EnumerationQuery query, CancellationToken token = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));

            EnumerationResult<Tenant> result = new EnumerationResult<Tenant>();
            result.MaxResults = query.MaxResults;
            result.Skip = query.Skip;

            string where = String.IsNullOrEmpty(query.SearchTerm) ? String.Empty : " WHERE name LIKE '%" + Sanitize(query.SearchTerm) + "%'";
            DataTable count = await _Driver.ExecuteQueryAsync("SELECT COUNT(*) FROM tenants" + where + ";", false, token).ConfigureAwait(false);
            if (count != null && count.Rows.Count > 0) result.TotalRecords = Convert.ToInt64(count.Rows[0][0]);

            StringBuilder sql = new StringBuilder("SELECT * FROM tenants");
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
                    result.Objects.Add(DataRowToTenant(row));
                }
            }

            result.RecordsRemaining = Math.Max(0, result.TotalRecords - query.Skip - result.Objects.Count);
            result.EndOfResults = result.RecordsRemaining == 0;
            if (!result.EndOfResults && result.Objects.Count > 0) result.ContinuationToken = result.Objects[result.Objects.Count - 1].Id;
            return result;
        }

        /// <inheritdoc />
        public async Task<Tenant> UpdateAsync(Tenant tenant, CancellationToken token = default)
        {
            if (tenant == null) throw new ArgumentNullException(nameof(tenant));
            tenant.LastUpdateUtc = DateTime.UtcNow;
            string query =
                "UPDATE tenants SET " +
                "parentid = " + ToSql(tenant.ParentId) + ", " +
                "name = '" + Sanitize(tenant.Name) + "', " +
                "region = " + ToSql(tenant.Region) + ", " +
                "active = " + (tenant.Active ? "1" : "0") + ", " +
                "isprotected = " + (tenant.IsProtected ? "1" : "0") + ", " +
                "lastupdateutc = '" + tenant.LastUpdateUtc.ToString(SetupQueries.TimestampFormat) + "' " +
                "WHERE id = '" + Sanitize(tenant.Id) + "';";
            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return tenant;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            Tenant? tenant = await ReadAsync(id, token).ConfigureAwait(false);
            if (tenant == null) return false;
            if (tenant.IsProtected) throw new InvalidOperationException("Tenant '" + id + "' is protected and cannot be deleted.");
            await _Driver.ExecuteQueryAsync("DELETE FROM tenants WHERE id = '" + Sanitize(id) + "';", true, token).ConfigureAwait(false);
            return true;
        }

        private Tenant DataRowToTenant(DataRow row)
        {
            Tenant tenant = new Tenant();
            tenant.Id = row["id"]?.ToString() ?? String.Empty;
            tenant.ParentId = NullIfEmpty(row["parentid"]?.ToString());
            tenant.Name = row["name"]?.ToString() ?? String.Empty;
            tenant.Region = NullIfEmpty(row["region"]?.ToString());
            tenant.Active = Convert.ToInt32(row["active"]) == 1;
            tenant.IsProtected = Convert.ToInt32(row["isprotected"]) == 1;
            tenant.CreatedUtc = ParseTimestamp(row["createdutc"]?.ToString());
            tenant.LastUpdateUtc = ParseTimestamp(row["lastupdateutc"]?.ToString());
            return tenant;
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
