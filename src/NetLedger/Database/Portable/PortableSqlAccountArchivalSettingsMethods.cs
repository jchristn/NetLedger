namespace NetLedger.Database.Portable
{
    using System;
    using System.Data;
    using System.Globalization;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Database.Interfaces;

    internal sealed class PortableSqlAccountArchivalSettingsMethods : IAccountArchivalSettingsMethods
    {
        private readonly PortableSqlDialect _Sql;

        internal PortableSqlAccountArchivalSettingsMethods(DatabaseDriverBase driver, DatabaseTypeEnum databaseType)
        {
            _Sql = new PortableSqlDialect(driver, databaseType);
        }

        public async Task<AccountArchivalSettings> UpsertAsync(AccountArchivalSettings settings, CancellationToken token = default)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (String.IsNullOrWhiteSpace(settings.TenantId)) throw new ArgumentNullException(nameof(settings.TenantId));
            if (String.IsNullOrWhiteSpace(settings.AccountId)) throw new ArgumentNullException(nameof(settings.AccountId));

            DateTime now = DateTime.UtcNow;
            AccountArchivalSettings? existing = await ReadByAccountAsync(settings.TenantId, settings.AccountId, token).ConfigureAwait(false);
            if (existing == null)
            {
                if (String.IsNullOrWhiteSpace(settings.Id))
                {
                    settings.Id = NetLedgerId.Generate(IdentifierPrefixes.AccountArchivalSettings);
                }

                if (settings.CreatedUtc == DateTime.MinValue)
                {
                    settings.CreatedUtc = now;
                }

                settings.LastUpdateUtc = now;
                await _Sql.ExecuteAsync(BuildInsert(settings), true, token).ConfigureAwait(false);
                return settings;
            }

            settings.Id = existing.Id;
            if (settings.CreatedUtc == DateTime.MinValue)
            {
                settings.CreatedUtc = existing.CreatedUtc;
            }

            settings.LastUpdateUtc = now;
            await _Sql.ExecuteAsync(BuildUpdate(settings), true, token).ConfigureAwait(false);
            return settings;
        }

        public async Task<AccountArchivalSettings?> ReadByAccountAsync(string tenantId, string accountId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(accountId)) throw new ArgumentNullException(nameof(accountId));

            string where = _Sql.Column("tenantid") + " = '" + _Sql.Sanitize(tenantId) + "' AND " +
                _Sql.Column("accountid") + " = '" + _Sql.Sanitize(accountId) + "'";
            DataTable result = await _Sql.QueryOneAsync("accountarchivalsettings", where, token).ConfigureAwait(false);
            if (result == null || result.Rows.Count == 0)
            {
                return null;
            }

            return ToSettings(result.Rows[0]);
        }

        public async Task<EnumerationResult<AccountArchivalSettings>> EnumerateAsync(EnumerationQuery query, CancellationToken token = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));

            string whereClause = BuildWhereClause(query);
            string orderBy = _Sql.Column("tenantid") + " ASC, " + _Sql.Column("accountid") + " ASC";
            return await _Sql.EnumerateAsync(
                "accountarchivalsettings",
                whereClause,
                query,
                ToSettings,
                orderBy,
                token).ConfigureAwait(false);
        }

        public async Task<bool> DeleteByAccountAsync(string tenantId, string accountId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(accountId)) throw new ArgumentNullException(nameof(accountId));

            AccountArchivalSettings? existing = await ReadByAccountAsync(tenantId, accountId, token).ConfigureAwait(false);
            if (existing == null)
            {
                return false;
            }

            string where = _Sql.Column("tenantid") + " = '" + _Sql.Sanitize(tenantId) + "' AND " +
                _Sql.Column("accountid") + " = '" + _Sql.Sanitize(accountId) + "'";
            await _Sql.ExecuteAsync("DELETE FROM " + _Sql.Table("accountarchivalsettings") + " WHERE " + where + ";", true, token).ConfigureAwait(false);
            return true;
        }

        private string BuildInsert(AccountArchivalSettings settings)
        {
            return "INSERT INTO " + _Sql.Table("accountarchivalsettings") + " (" +
                ColumnList() + ") VALUES (" +
                ValueList(settings) + ");";
        }

        private string BuildUpdate(AccountArchivalSettings settings)
        {
            return "UPDATE " + _Sql.Table("accountarchivalsettings") + " SET " +
                _Sql.Column("tenantid") + " = '" + _Sql.Sanitize(settings.TenantId) + "', " +
                _Sql.Column("accountid") + " = '" + _Sql.Sanitize(settings.AccountId) + "', " +
                _Sql.Column("enabled") + " = " + NullableBool(settings.Enabled) + ", " +
                _Sql.Column("maxretentiondays") + " = " + NullableLong(settings.MaxRetentionDays) + ", " +
                _Sql.Column("intervalseconds") + " = " + NullableInt(settings.IntervalSeconds) + ", " +
                _Sql.Column("maxbatchrows") + " = " + NullableInt(settings.MaxBatchRows) + ", " +
                _Sql.Column("deleteaftercommit") + " = " + NullableBool(settings.DeleteAfterCommit) + ", " +
                _Sql.Column("storagepoolid") + " = " + _Sql.Nullable(settings.StoragePoolId) + ", " +
                _Sql.Column("retrymaxattempts") + " = " + NullableInt(settings.RetryMaxAttempts) + ", " +
                _Sql.Column("retryinitialdelayseconds") + " = " + NullableInt(settings.RetryInitialDelaySeconds) + ", " +
                _Sql.Column("retrymaxdelayseconds") + " = " + NullableInt(settings.RetryMaxDelaySeconds) + ", " +
                _Sql.Column("lastattemptutc") + " = " + NullableTimestamp(settings.LastAttemptUtc) + ", " +
                _Sql.Column("lastsuccessutc") + " = " + NullableTimestamp(settings.LastSuccessUtc) + ", " +
                _Sql.Column("lastarchivedthroughutc") + " = " + NullableTimestamp(settings.LastArchivedThroughUtc) + ", " +
                _Sql.Column("lastfailureutc") + " = " + NullableTimestamp(settings.LastFailureUtc) + ", " +
                _Sql.Column("nextattemptutc") + " = " + NullableTimestamp(settings.NextAttemptUtc) + ", " +
                _Sql.Column("failurecount") + " = " + settings.FailureCount.ToString(CultureInfo.InvariantCulture) + ", " +
                _Sql.Column("lasterror") + " = " + _Sql.Nullable(settings.LastError) + ", " +
                _Sql.Column("createdutc") + " = '" + _Sql.Timestamp(settings.CreatedUtc) + "', " +
                _Sql.Column("lastupdateutc") + " = '" + _Sql.Timestamp(settings.LastUpdateUtc) + "' " +
                "WHERE " + _Sql.Column("id") + " = '" + _Sql.Sanitize(settings.Id) + "';";
        }

        private string ColumnList()
        {
            return _Sql.Columns(
                "id",
                "tenantid",
                "accountid",
                "enabled",
                "maxretentiondays",
                "intervalseconds",
                "maxbatchrows",
                "deleteaftercommit",
                "storagepoolid",
                "retrymaxattempts",
                "retryinitialdelayseconds",
                "retrymaxdelayseconds",
                "lastattemptutc",
                "lastsuccessutc",
                "lastarchivedthroughutc",
                "lastfailureutc",
                "nextattemptutc",
                "failurecount",
                "lasterror",
                "createdutc",
                "lastupdateutc");
        }

        private string ValueList(AccountArchivalSettings settings)
        {
            return "'" + _Sql.Sanitize(settings.Id) + "', " +
                "'" + _Sql.Sanitize(settings.TenantId) + "', " +
                "'" + _Sql.Sanitize(settings.AccountId) + "', " +
                NullableBool(settings.Enabled) + ", " +
                NullableLong(settings.MaxRetentionDays) + ", " +
                NullableInt(settings.IntervalSeconds) + ", " +
                NullableInt(settings.MaxBatchRows) + ", " +
                NullableBool(settings.DeleteAfterCommit) + ", " +
                _Sql.Nullable(settings.StoragePoolId) + ", " +
                NullableInt(settings.RetryMaxAttempts) + ", " +
                NullableInt(settings.RetryInitialDelaySeconds) + ", " +
                NullableInt(settings.RetryMaxDelaySeconds) + ", " +
                NullableTimestamp(settings.LastAttemptUtc) + ", " +
                NullableTimestamp(settings.LastSuccessUtc) + ", " +
                NullableTimestamp(settings.LastArchivedThroughUtc) + ", " +
                NullableTimestamp(settings.LastFailureUtc) + ", " +
                NullableTimestamp(settings.NextAttemptUtc) + ", " +
                settings.FailureCount.ToString(CultureInfo.InvariantCulture) + ", " +
                _Sql.Nullable(settings.LastError) + ", " +
                "'" + _Sql.Timestamp(settings.CreatedUtc) + "', " +
                "'" + _Sql.Timestamp(settings.LastUpdateUtc) + "'";
        }

        private string BuildWhereClause(EnumerationQuery query)
        {
            StringBuilder builder = new StringBuilder();
            if (!String.IsNullOrWhiteSpace(query.TenantId))
            {
                AppendWhere(builder, _Sql.Column("tenantid") + " = '" + _Sql.Sanitize(query.TenantId) + "'");
            }

            if (!String.IsNullOrWhiteSpace(query.AccountId))
            {
                AppendWhere(builder, _Sql.Column("accountid") + " = '" + _Sql.Sanitize(query.AccountId) + "'");
            }

            return builder.ToString();
        }

        private void AppendWhere(StringBuilder builder, string condition)
        {
            if (builder.Length == 0)
            {
                builder.Append(" WHERE ");
            }
            else
            {
                builder.Append(" AND ");
            }

            builder.Append(condition);
        }

        private AccountArchivalSettings ToSettings(DataRow row)
        {
            AccountArchivalSettings settings = new AccountArchivalSettings
            {
                Id = _Sql.Get(row, "id"),
                TenantId = _Sql.Get(row, "tenantid"),
                AccountId = _Sql.Get(row, "accountid"),
                Enabled = GetNullableBool(row, "enabled"),
                MaxRetentionDays = GetNullableLong(row, "maxretentiondays"),
                IntervalSeconds = GetNullableInt(row, "intervalseconds"),
                MaxBatchRows = GetNullableInt(row, "maxbatchrows"),
                DeleteAfterCommit = GetNullableBool(row, "deleteaftercommit"),
                StoragePoolId = _Sql.GetNull(row, "storagepoolid"),
                RetryMaxAttempts = GetNullableInt(row, "retrymaxattempts"),
                RetryInitialDelaySeconds = GetNullableInt(row, "retryinitialdelayseconds"),
                RetryMaxDelaySeconds = GetNullableInt(row, "retrymaxdelayseconds"),
                LastAttemptUtc = GetNullableDate(row, "lastattemptutc"),
                LastSuccessUtc = GetNullableDate(row, "lastsuccessutc"),
                LastArchivedThroughUtc = GetNullableDate(row, "lastarchivedthroughutc"),
                LastFailureUtc = GetNullableDate(row, "lastfailureutc"),
                NextAttemptUtc = GetNullableDate(row, "nextattemptutc"),
                FailureCount = GetRequiredInt(row, "failurecount"),
                LastError = _Sql.GetNull(row, "lasterror"),
                CreatedUtc = _Sql.GetDate(row, "createdutc"),
                LastUpdateUtc = _Sql.GetDate(row, "lastupdateutc")
            };

            return settings;
        }

        private string NullableBool(bool? value)
        {
            return value.HasValue ? _Sql.Bool(value.Value) : "NULL";
        }

        private string NullableInt(int? value)
        {
            return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "NULL";
        }

        private string NullableLong(long? value)
        {
            return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "NULL";
        }

        private string NullableTimestamp(DateTime? value)
        {
            return value.HasValue ? "'" + _Sql.Timestamp(value.Value) + "'" : "NULL";
        }

        private bool? GetNullableBool(DataRow row, string columnName)
        {
            string value = _Sql.Get(row, columnName);
            if (String.IsNullOrEmpty(value))
            {
                return null;
            }

            return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private int? GetNullableInt(DataRow row, string columnName)
        {
            string value = _Sql.Get(row, columnName);
            if (String.IsNullOrEmpty(value))
            {
                return null;
            }

            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private long? GetNullableLong(DataRow row, string columnName)
        {
            string value = _Sql.Get(row, columnName);
            if (String.IsNullOrEmpty(value))
            {
                return null;
            }

            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        private DateTime? GetNullableDate(DataRow row, string columnName)
        {
            string value = _Sql.Get(row, columnName);
            if (String.IsNullOrEmpty(value))
            {
                return null;
            }

            return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        }

        private int GetRequiredInt(DataRow row, string columnName)
        {
            string value = _Sql.Get(row, columnName);
            if (String.IsNullOrEmpty(value))
            {
                return 0;
            }

            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
    }
}
