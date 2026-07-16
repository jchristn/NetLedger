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
    /// SQLite audit record methods.
    /// </summary>
    internal class AuditRecordMethods : IAuditRecordMethods
    {
        private readonly SqliteDatabaseDriver _Driver;

        /// <summary>
        /// Instantiate the audit record methods.
        /// </summary>
        /// <param name="driver">SQLite database driver.</param>
        public AuditRecordMethods(SqliteDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <inheritdoc />
        public async Task<AuditRecord> CreateAsync(AuditRecord record, CancellationToken token = default)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (String.IsNullOrEmpty(record.Id)) record.Id = NetLedgerId.Generate(IdentifierPrefixes.Audit);
            record.CreatedUtc = record.CreatedUtc == DateTime.MinValue ? DateTime.UtcNow : record.CreatedUtc;

            string query =
                "INSERT INTO auditrecords (id, tenantid, principalid, principaltype, eventtype, resourcetype, operationtype, resourceid, result, reason, requestid, createdutc) VALUES (" +
                "'" + Sanitize(record.Id) + "', " +
                ToSql(record.TenantId) + ", " +
                ToSql(record.PrincipalId) + ", " +
                ToSql(record.PrincipalType) + ", " +
                "'" + Sanitize(record.EventType) + "', " +
                ToSql(record.ResourceType) + ", " +
                ToSql(record.OperationType) + ", " +
                ToSql(record.ResourceId) + ", " +
                "'" + Sanitize(record.Result) + "', " +
                ToSql(record.Reason) + ", " +
                ToSql(record.RequestId) + ", " +
                "'" + record.CreatedUtc.ToString(SetupQueries.TimestampFormat) + "');";

            await _Driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
            return record;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<AuditRecord>> EnumerateAsync(EnumerationQuery query, CancellationToken token = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            EnumerationResult<AuditRecord> result = new EnumerationResult<AuditRecord>();
            result.MaxResults = query.MaxResults;
            result.Skip = query.Skip;

            string where = String.IsNullOrEmpty(query.TenantId) ? String.Empty : " WHERE tenantid = '" + Sanitize(query.TenantId) + "'";
            DataTable count = await _Driver.ExecuteQueryAsync("SELECT COUNT(*) FROM auditrecords" + where + ";", false, token).ConfigureAwait(false);
            if (count != null && count.Rows.Count > 0) result.TotalRecords = Convert.ToInt64(count.Rows[0][0]);

            StringBuilder sql = new StringBuilder("SELECT * FROM auditrecords");
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
                    result.Objects.Add(DataRowToRecord(row));
                }
            }

            result.RecordsRemaining = Math.Max(0, result.TotalRecords - query.Skip - result.Objects.Count);
            result.EndOfResults = result.RecordsRemaining == 0;
            return result;
        }

        private AuditRecord DataRowToRecord(DataRow row)
        {
            AuditRecord record = new AuditRecord();
            record.Id = row["id"]?.ToString() ?? String.Empty;
            record.TenantId = row["tenantid"]?.ToString() ?? String.Empty;
            record.PrincipalId = NullIfEmpty(row["principalid"]?.ToString());
            record.PrincipalType = NullIfEmpty(row["principaltype"]?.ToString());
            record.EventType = row["eventtype"]?.ToString() ?? String.Empty;
            record.ResourceType = NullIfEmpty(row["resourcetype"]?.ToString());
            record.OperationType = NullIfEmpty(row["operationtype"]?.ToString());
            record.ResourceId = NullIfEmpty(row["resourceid"]?.ToString());
            record.Result = row["result"]?.ToString() ?? String.Empty;
            record.Reason = NullIfEmpty(row["reason"]?.ToString());
            record.RequestId = NullIfEmpty(row["requestid"]?.ToString());
            record.CreatedUtc = ParseTimestamp(row["createdutc"]?.ToString());
            return record;
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
