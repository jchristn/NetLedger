namespace NetLedger.Database
{
    using System;
    using System.Data;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;

    // Adds the nullable accounts.units column introduced for per-account unit/currency labeling.
    // Fresh databases receive the column from the provider setup queries; this migration backfills the
    // column on databases created before the feature existed. It is idempotent and safe to run on every startup.
    internal static class AccountUnitsMigration
    {
        #region Private-Members

        private static readonly string _MigrationName = "account-units-v1";
        private static readonly string _MigrationChecksum = "account-units-column-20260804";
        private static readonly string _MigrationLockId = "__schema_migration_account_units";

        #endregion

        #region Internal-Methods

        internal static async Task ApplyAsync(DatabaseDriverBase driver, CancellationToken token = default)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            token.ThrowIfCancellationRequested();

            if (await ColumnExistsAsync(driver, "accounts", "units", token).ConfigureAwait(false))
            {
                await RecordIfMissingAsync(driver, token).ConfigureAwait(false);
                return;
            }

            await using IAsyncDisposable migrationLock = await driver.AcquireAccountLockAsync(_MigrationLockId, token).ConfigureAwait(false);

            // Re-check inside the lock; another instance may have added the column concurrently.
            if (!await ColumnExistsAsync(driver, "accounts", "units", token).ConfigureAwait(false))
            {
                await driver.ExecuteQueryAsync(BuildAddColumnQuery(driver.Settings.Type), true, token).ConfigureAwait(false);
            }

            await RecordIfMissingAsync(driver, token).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        private static string BuildAddColumnQuery(DatabaseTypeEnum databaseType)
        {
            if (databaseType == DatabaseTypeEnum.Mysql) return "ALTER TABLE `accounts` ADD COLUMN `units` VARCHAR(64) NULL;";
            if (databaseType == DatabaseTypeEnum.SqlServer) return "ALTER TABLE [accounts] ADD [units] NVARCHAR(64) NULL;";
            if (databaseType == DatabaseTypeEnum.Sqlite) return "ALTER TABLE accounts ADD COLUMN units TEXT;";
            return "ALTER TABLE accounts ADD COLUMN units VARCHAR(64) NULL;";
        }

        private static async Task<bool> ColumnExistsAsync(DatabaseDriverBase driver, string tableName, string columnName, CancellationToken token)
        {
            string query = BuildColumnExistsQuery(driver.Settings.Type, tableName, columnName);
            DataTable result = await driver.ExecuteQueryAsync(query, false, token).ConfigureAwait(false);
            return result != null && result.Rows.Count > 0;
        }

        private static string BuildColumnExistsQuery(DatabaseTypeEnum databaseType, string tableName, string columnName)
        {
            if (databaseType == DatabaseTypeEnum.Sqlite)
            {
                return "SELECT name FROM pragma_table_info('" + Sanitize(tableName) + "') WHERE name = '" + Sanitize(columnName) + "';";
            }

            if (databaseType == DatabaseTypeEnum.Mysql)
            {
                return "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '" + Sanitize(tableName) + "' AND COLUMN_NAME = '" + Sanitize(columnName) + "';";
            }

            if (databaseType == DatabaseTypeEnum.SqlServer)
            {
                return "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '" + Sanitize(tableName) + "' AND COLUMN_NAME = '" + Sanitize(columnName) + "';";
            }

            return "SELECT column_name FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = '" + Sanitize(tableName) + "' AND column_name = '" + Sanitize(columnName) + "';";
        }

        private static async Task RecordIfMissingAsync(DatabaseDriverBase driver, CancellationToken token)
        {
            string where = Column(driver.Settings.Type, "name") + " = '" + Sanitize(_MigrationName) + "' AND " + Column(driver.Settings.Type, "success") + " = " + Bool(driver.Settings.Type, true);
            DataTable result = await driver.ExecuteQueryAsync("SELECT COUNT(*) FROM " + Table(driver.Settings.Type, "schemamigrations") + " WHERE " + where + ";", false, token).ConfigureAwait(false);
            if (GetCount(result) > 0) return;

            string query =
                "INSERT INTO " + Table(driver.Settings.Type, "schemamigrations") + " (" + Columns(driver.Settings.Type, "id", "name", "appliedutc", "checksum", "success") + ") VALUES (" +
                "'" + NetLedgerId.Generate(IdentifierPrefixes.Migration) + "', " +
                "'" + Sanitize(_MigrationName) + "', " +
                "'" + Timestamp(DateTime.UtcNow) + "', " +
                "'" + Sanitize(_MigrationChecksum) + "', " +
                Bool(driver.Settings.Type, true) +
                ");";

            await driver.ExecuteQueryAsync(query, true, token).ConfigureAwait(false);
        }

        private static long GetCount(DataTable result)
        {
            if (result == null || result.Rows.Count == 0) return 0L;
            return Convert.ToInt64(result.Rows[0][0], CultureInfo.InvariantCulture);
        }

        private static string Columns(DatabaseTypeEnum databaseType, params string[] columnNames)
        {
            string[] columns = new string[columnNames.Length];
            for (int i = 0; i < columnNames.Length; i++)
            {
                columns[i] = Column(databaseType, columnNames[i]);
            }

            return String.Join(", ", columns);
        }

        private static string Table(DatabaseTypeEnum databaseType, string tableName)
        {
            if (databaseType == DatabaseTypeEnum.Mysql) return "`" + Sanitize(tableName) + "`";
            if (databaseType == DatabaseTypeEnum.SqlServer) return "[" + Sanitize(tableName) + "]";
            return Sanitize(tableName);
        }

        private static string Column(DatabaseTypeEnum databaseType, string columnName)
        {
            if (databaseType == DatabaseTypeEnum.Mysql) return "`" + Sanitize(columnName) + "`";
            if (databaseType == DatabaseTypeEnum.SqlServer) return "[" + Sanitize(columnName) + "]";
            return Sanitize(columnName);
        }

        private static string Bool(DatabaseTypeEnum databaseType, bool value)
        {
            if (databaseType == DatabaseTypeEnum.Postgresql) return value ? "TRUE" : "FALSE";
            return value ? "1" : "0";
        }

        private static string Timestamp(DateTime value)
        {
            return value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture);
        }

        private static string Sanitize(string value)
        {
            if (String.IsNullOrEmpty(value)) return String.Empty;
            return value.Replace("'", "''");
        }

        #endregion
    }
}
