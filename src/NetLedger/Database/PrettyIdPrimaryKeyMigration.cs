namespace NetLedger.Database
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;

    // Handles the one-time legacy schema rebuild from integer row IDs plus guid to PrettyId primary keys.
    internal static class PrettyIdPrimaryKeyMigration
    {
        #region Private-Members

        private static readonly string _MigrationName = "prettyid-primary-keys-v1";
        private static readonly string _MigrationChecksum = "prettyid-id-primary-key-20260720";
        private static readonly string _MigrationLockId = "__schema_migration_prettyid_primary_keys";

        #endregion

        #region Public-Methods

        internal static async Task ApplyAsync(DatabaseDriverBase driver, CancellationToken token = default)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            token.ThrowIfCancellationRequested();

            bool migrateAccounts = await ColumnExistsAsync(driver, "accounts", "guid", token).ConfigureAwait(false);
            bool migrateEntries = await ColumnExistsAsync(driver, "entries", "guid", token).ConfigureAwait(false);
            bool migrateApiKeys = await ColumnExistsAsync(driver, "apikeys", "guid", token).ConfigureAwait(false);

            if (!migrateAccounts && !migrateEntries && !migrateApiKeys)
            {
                await RecordIfMissingAsync(driver, token).ConfigureAwait(false);
                return;
            }

            await using IAsyncDisposable migrationLock = await driver.AcquireAccountLockAsync(_MigrationLockId, token).ConfigureAwait(false);

            migrateAccounts = await ColumnExistsAsync(driver, "accounts", "guid", token).ConfigureAwait(false);
            migrateEntries = await ColumnExistsAsync(driver, "entries", "guid", token).ConfigureAwait(false);
            migrateApiKeys = await ColumnExistsAsync(driver, "apikeys", "guid", token).ConfigureAwait(false);

            if (!migrateAccounts && !migrateEntries && !migrateApiKeys)
            {
                await RecordIfMissingAsync(driver, token).ConfigureAwait(false);
                return;
            }

            if (migrateAccounts) await ValidateLegacyIdsAsync(driver, "accounts", token).ConfigureAwait(false);
            if (migrateEntries) await ValidateLegacyIdsAsync(driver, "entries", token).ConfigureAwait(false);
            if (migrateApiKeys) await ValidateLegacyIdsAsync(driver, "apikeys", token).ConfigureAwait(false);

            List<string> migrationQueries = BuildMigrationQueries(driver.Settings.Type, migrateAccounts, migrateEntries, migrateApiKeys);
            await driver.ExecuteQueriesAsync(migrationQueries, true, token).ConfigureAwait(false);
            await RecordIfMissingAsync(driver, token).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

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

        private static async Task ValidateLegacyIdsAsync(DatabaseDriverBase driver, string tableName, CancellationToken token)
        {
            string table = Table(driver.Settings.Type, tableName);
            string guid = Column(driver.Settings.Type, "guid");

            DataTable emptyResult = await driver.ExecuteQueryAsync(
                "SELECT COUNT(*) FROM " + table + " WHERE " + guid + " IS NULL OR " + guid + " = '';",
                false,
                token).ConfigureAwait(false);

            long emptyCount = GetCount(emptyResult);
            if (emptyCount > 0)
            {
                throw new InvalidOperationException(
                    "Unable to migrate table '" + tableName + "' to PrettyId primary keys because " + emptyCount.ToString(CultureInfo.InvariantCulture) + " rows have a null or empty legacy guid value.");
            }

            DataTable duplicateResult = await driver.ExecuteQueryAsync(
                "SELECT COUNT(*) FROM (SELECT " + guid + " FROM " + table + " GROUP BY " + guid + " HAVING COUNT(*) > 1) duplicateids;",
                false,
                token).ConfigureAwait(false);

            long duplicateCount = GetCount(duplicateResult);
            if (duplicateCount > 0)
            {
                throw new InvalidOperationException(
                    "Unable to migrate table '" + tableName + "' to PrettyId primary keys because " + duplicateCount.ToString(CultureInfo.InvariantCulture) + " duplicate legacy guid values were found.");
            }
        }

        private static long GetCount(DataTable result)
        {
            if (result == null || result.Rows.Count == 0) return 0L;
            return Convert.ToInt64(result.Rows[0][0], CultureInfo.InvariantCulture);
        }

        private static List<string> BuildMigrationQueries(DatabaseTypeEnum databaseType, bool migrateAccounts, bool migrateEntries, bool migrateApiKeys)
        {
            List<string> queries = new List<string>();

            if (migrateAccounts)
            {
                queries.AddRange(BuildDropAccountsIndexes(databaseType));
                queries.AddRange(BuildAccountsMigration(databaseType));
            }

            if (migrateEntries)
            {
                queries.AddRange(BuildDropEntriesIndexes(databaseType));
                queries.AddRange(BuildEntriesMigration(databaseType));
            }

            if (migrateApiKeys)
            {
                queries.AddRange(BuildDropApiKeysIndexes(databaseType));
                queries.AddRange(BuildApiKeysMigration(databaseType));
            }

            return queries;
        }

        private static IEnumerable<string> BuildDropAccountsIndexes(DatabaseTypeEnum databaseType)
        {
            List<string> queries = new List<string>
            {
                DropIndex(databaseType, "accounts", "idx_accounts_guid"),
                DropIndex(databaseType, "accounts", "idx_accounts_tenantid_guid"),
                DropIndex(databaseType, "accounts", "idx_accounts_name"),
                DropIndex(databaseType, "accounts", "idx_accounts_createdutc")
            };

            if (databaseType == DatabaseTypeEnum.Sqlite)
            {
                queries.Add(DropIndex(databaseType, "accounts", "idx_accounts_tenantid_name"));
            }

            return queries;
        }

        private static IEnumerable<string> BuildDropEntriesIndexes(DatabaseTypeEnum databaseType)
        {
            List<string> queries = new List<string>
            {
                DropIndex(databaseType, "entries", "idx_entries_guid"),
                DropIndex(databaseType, "entries", "idx_entries_tenantid_guid"),
                DropIndex(databaseType, "entries", "idx_entries_accountguid"),
                DropIndex(databaseType, "entries", "idx_entries_tenantid_accountguid"),
                DropIndex(databaseType, "entries", "idx_entries_type"),
                DropIndex(databaseType, "entries", "idx_entries_createdutc"),
                DropIndex(databaseType, "entries", "idx_entries_accountguid_createdutc"),
                DropIndex(databaseType, "entries", "idx_entries_accountguid_id"),
                DropIndex(databaseType, "entries", "idx_entries_accountguid_type"),
                DropIndex(databaseType, "entries", "idx_entries_tenantid_accountguid_createdutc")
            };

            if (databaseType == DatabaseTypeEnum.Sqlite)
            {
                queries.Add(DropIndex(databaseType, "entries", "idx_entries_committed"));
                queries.Add(DropIndex(databaseType, "entries", "idx_entries_committedbyguid"));
                queries.Add(DropIndex(databaseType, "entries", "idx_entries_accountguid_committed"));
                queries.Add(DropIndex(databaseType, "entries", "idx_entries_accountguid_type_committed"));
                queries.Add(DropIndex(databaseType, "entries", "idx_entries_accountguid_type_committed_createdutc"));
            }
            else
            {
                queries.Add(DropIndex(databaseType, "entries", "idx_entries_iscommitted"));
                queries.Add(DropIndex(databaseType, "entries", "idx_entries_accountguid_iscommitted"));
                queries.Add(DropIndex(databaseType, "entries", "idx_entries_accountguid_type_iscommitted_createdutc"));
            }

            return queries;
        }

        private static IEnumerable<string> BuildDropApiKeysIndexes(DatabaseTypeEnum databaseType)
        {
            List<string> queries = new List<string>
            {
                DropIndex(databaseType, "apikeys", "idx_apikeys_guid"),
                DropIndex(databaseType, "apikeys", "idx_apikeys_apikey"),
                DropIndex(databaseType, "apikeys", "idx_apikeys_active")
            };

            if (databaseType == DatabaseTypeEnum.Sqlite)
            {
                queries.Add(DropIndex(databaseType, "apikeys", "idx_apikeys_apikey_active"));
            }
            else
            {
                queries.Add(DropIndex(databaseType, "apikeys", "idx_apikeys_createdutc"));
            }

            return queries;
        }

        private static List<string> BuildAccountsMigration(DatabaseTypeEnum databaseType)
        {
            string legacy = Table(databaseType, "accounts_prettyid_pk_legacy");
            string current = Table(databaseType, "accounts");

            return new List<string>
            {
                RenameTable(databaseType, "accounts", "accounts_prettyid_pk_legacy"),
                CreateAccountsTable(databaseType),
                "INSERT INTO " + current + " (" + Columns(databaseType, "id", "tenantid", "owneruserid", "name", "notes", "labels", "tags", "active", "createdutc", "lastupdateutc") + ") SELECT " + Columns(databaseType, "guid", "tenantid", "owneruserid", "name", "notes", "labels", "tags", "active", "createdutc", "lastupdateutc") + " FROM " + legacy + ";",
                "DROP TABLE " + legacy + ";"
            };
        }

        private static List<string> BuildEntriesMigration(DatabaseTypeEnum databaseType)
        {
            string legacy = Table(databaseType, "entries_prettyid_pk_legacy");
            string current = Table(databaseType, "entries");

            return new List<string>
            {
                RenameTable(databaseType, "entries", "entries_prettyid_pk_legacy"),
                CreateEntriesTable(databaseType),
                "INSERT INTO " + current + " (" + Columns(databaseType, "id", "tenantid", "accountguid", "type", "amount", "description", "replaces", CommittedColumn(databaseType), "committedbyguid", "committedutc", "labels", "tags", "createdutc", "lastupdateutc") + ") SELECT " + Columns(databaseType, "guid", "tenantid", "accountguid", "type", "amount", "description", "replaces", CommittedColumn(databaseType), "committedbyguid", "committedutc", "labels", "tags", "createdutc", "lastupdateutc") + " FROM " + legacy + ";",
                "DROP TABLE " + legacy + ";"
            };
        }

        private static List<string> BuildApiKeysMigration(DatabaseTypeEnum databaseType)
        {
            string legacy = Table(databaseType, "apikeys_prettyid_pk_legacy");
            string current = Table(databaseType, "apikeys");

            return new List<string>
            {
                RenameTable(databaseType, "apikeys", "apikeys_prettyid_pk_legacy"),
                CreateApiKeysTable(databaseType),
                "INSERT INTO " + current + " (" + Columns(databaseType, "id", "tenantid", "userid", "name", "apikey", "secretkeysha256", "secretkeylast4", "active", "isadmin", "createdutc") + ") SELECT " + Columns(databaseType, "guid", "tenantid", "userid", "name", "apikey", "secretkeysha256", "secretkeylast4", "active", "isadmin", "createdutc") + " FROM " + legacy + ";",
                "DROP TABLE " + legacy + ";"
            };
        }

        private static string CreateAccountsTable(DatabaseTypeEnum databaseType)
        {
            if (databaseType == DatabaseTypeEnum.Mysql)
            {
                return @"CREATE TABLE `accounts` (
                    `id` VARCHAR(64) NOT NULL,
                    `tenantid` VARCHAR(64) NOT NULL DEFAULT '',
                    `owneruserid` VARCHAR(64) NULL,
                    `name` VARCHAR(256) NOT NULL,
                    `notes` TEXT NULL,
                    `labels` TEXT NOT NULL,
                    `tags` TEXT NOT NULL,
                    `active` TINYINT(1) NOT NULL DEFAULT 1,
                    `createdutc` DATETIME(6) NOT NULL,
                    `lastupdateutc` DATETIME(6) NOT NULL,
                    PRIMARY KEY (`id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";
            }

            if (databaseType == DatabaseTypeEnum.SqlServer)
            {
                return @"CREATE TABLE [accounts] (
                    [id] NVARCHAR(64) NOT NULL PRIMARY KEY,
                    [tenantid] NVARCHAR(64) NOT NULL DEFAULT '',
                    [owneruserid] NVARCHAR(64) NULL,
                    [name] NVARCHAR(256) NOT NULL,
                    [notes] NVARCHAR(MAX) NULL,
                    [labels] NVARCHAR(MAX) NOT NULL DEFAULT '[]',
                    [tags] NVARCHAR(MAX) NOT NULL DEFAULT '{}',
                    [active] BIT NOT NULL DEFAULT 1,
                    [createdutc] DATETIME2 NOT NULL,
                    [lastupdateutc] DATETIME2 NOT NULL
                );";
            }

            if (databaseType == DatabaseTypeEnum.Sqlite)
            {
                return @"CREATE TABLE accounts (
                    id TEXT PRIMARY KEY,
                    tenantid TEXT NOT NULL DEFAULT '',
                    owneruserid TEXT,
                    name TEXT NOT NULL,
                    notes TEXT,
                    labels TEXT NOT NULL DEFAULT '[]',
                    tags TEXT NOT NULL DEFAULT '{}',
                    active INTEGER NOT NULL DEFAULT 1,
                    createdutc TEXT NOT NULL,
                    lastupdateutc TEXT NOT NULL DEFAULT ''
                );";
            }

            return @"CREATE TABLE accounts (
                id VARCHAR(64) PRIMARY KEY,
                tenantid VARCHAR(64) NOT NULL DEFAULT '',
                owneruserid VARCHAR(64) NULL,
                name VARCHAR(256) NOT NULL,
                notes TEXT NULL,
                labels TEXT NOT NULL DEFAULT '[]',
                tags TEXT NOT NULL DEFAULT '{}',
                active BOOLEAN NOT NULL DEFAULT TRUE,
                createdutc TIMESTAMP NOT NULL,
                lastupdateutc TIMESTAMP NOT NULL
            );";
        }

        private static string CreateEntriesTable(DatabaseTypeEnum databaseType)
        {
            if (databaseType == DatabaseTypeEnum.Mysql)
            {
                return @"CREATE TABLE `entries` (
                    `id` VARCHAR(64) NOT NULL,
                    `tenantid` VARCHAR(64) NOT NULL DEFAULT '',
                    `accountguid` VARCHAR(64) NOT NULL,
                    `type` VARCHAR(16) NOT NULL,
                    `amount` DECIMAL(18, 8) NOT NULL,
                    `description` TEXT NULL,
                    `replaces` VARCHAR(64) NULL,
                    `iscommitted` TINYINT(1) NOT NULL DEFAULT 0,
                    `committedbyguid` VARCHAR(64) NULL,
                    `committedutc` DATETIME(6) NULL,
                    `labels` TEXT NOT NULL,
                    `tags` TEXT NOT NULL,
                    `createdutc` DATETIME(6) NOT NULL,
                    `lastupdateutc` DATETIME(6) NOT NULL,
                    PRIMARY KEY (`id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";
            }

            if (databaseType == DatabaseTypeEnum.SqlServer)
            {
                return @"CREATE TABLE [entries] (
                    [id] NVARCHAR(64) NOT NULL PRIMARY KEY,
                    [tenantid] NVARCHAR(64) NOT NULL DEFAULT '',
                    [accountguid] NVARCHAR(64) NOT NULL,
                    [type] NVARCHAR(16) NOT NULL,
                    [amount] DECIMAL(18, 8) NOT NULL,
                    [description] NVARCHAR(MAX) NULL,
                    [replaces] NVARCHAR(64) NULL,
                    [iscommitted] BIT NOT NULL DEFAULT 0,
                    [committedbyguid] NVARCHAR(64) NULL,
                    [committedutc] DATETIME2 NULL,
                    [labels] NVARCHAR(MAX) NOT NULL DEFAULT '[]',
                    [tags] NVARCHAR(MAX) NOT NULL DEFAULT '{}',
                    [createdutc] DATETIME2 NOT NULL,
                    [lastupdateutc] DATETIME2 NOT NULL
                );";
            }

            if (databaseType == DatabaseTypeEnum.Sqlite)
            {
                return @"CREATE TABLE entries (
                    id TEXT PRIMARY KEY,
                    tenantid TEXT NOT NULL DEFAULT '',
                    accountguid TEXT NOT NULL,
                    type TEXT NOT NULL,
                    amount REAL NOT NULL,
                    description TEXT,
                    replaces TEXT,
                    committed INTEGER NOT NULL DEFAULT 0,
                    committedbyguid TEXT,
                    committedutc TEXT,
                    labels TEXT NOT NULL DEFAULT '[]',
                    tags TEXT NOT NULL DEFAULT '{}',
                    createdutc TEXT NOT NULL,
                    lastupdateutc TEXT NOT NULL DEFAULT ''
                );";
            }

            return @"CREATE TABLE entries (
                id VARCHAR(64) PRIMARY KEY,
                tenantid VARCHAR(64) NOT NULL DEFAULT '',
                accountguid VARCHAR(64) NOT NULL,
                type VARCHAR(16) NOT NULL,
                amount NUMERIC(18, 8) NOT NULL,
                description TEXT NULL,
                replaces VARCHAR(64) NULL,
                iscommitted BOOLEAN NOT NULL DEFAULT FALSE,
                committedbyguid VARCHAR(64) NULL,
                committedutc TIMESTAMP NULL,
                labels TEXT NOT NULL DEFAULT '[]',
                tags TEXT NOT NULL DEFAULT '{}',
                createdutc TIMESTAMP NOT NULL,
                lastupdateutc TIMESTAMP NOT NULL
            );";
        }

        private static string CreateApiKeysTable(DatabaseTypeEnum databaseType)
        {
            if (databaseType == DatabaseTypeEnum.Mysql)
            {
                return @"CREATE TABLE `apikeys` (
                    `id` VARCHAR(64) NOT NULL,
                    `tenantid` VARCHAR(64) NOT NULL DEFAULT '',
                    `userid` VARCHAR(64) NOT NULL DEFAULT '',
                    `name` VARCHAR(256) NOT NULL,
                    `apikey` VARCHAR(256) NOT NULL,
                    `secretkeysha256` VARCHAR(128) NULL,
                    `secretkeylast4` VARCHAR(16) NULL,
                    `active` TINYINT(1) NOT NULL DEFAULT 1,
                    `isadmin` TINYINT(1) NOT NULL DEFAULT 0,
                    `createdutc` DATETIME(6) NOT NULL,
                    PRIMARY KEY (`id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";
            }

            if (databaseType == DatabaseTypeEnum.SqlServer)
            {
                return @"CREATE TABLE [apikeys] (
                    [id] NVARCHAR(64) NOT NULL PRIMARY KEY,
                    [tenantid] NVARCHAR(64) NOT NULL DEFAULT '',
                    [userid] NVARCHAR(64) NOT NULL DEFAULT '',
                    [name] NVARCHAR(256) NOT NULL,
                    [apikey] NVARCHAR(256) NOT NULL,
                    [secretkeysha256] NVARCHAR(128) NULL,
                    [secretkeylast4] NVARCHAR(16) NULL,
                    [active] BIT NOT NULL DEFAULT 1,
                    [isadmin] BIT NOT NULL DEFAULT 0,
                    [createdutc] DATETIME2 NOT NULL
                );";
            }

            if (databaseType == DatabaseTypeEnum.Sqlite)
            {
                return @"CREATE TABLE apikeys (
                    id TEXT PRIMARY KEY,
                    tenantid TEXT NOT NULL DEFAULT '',
                    userid TEXT NOT NULL DEFAULT '',
                    name TEXT NOT NULL,
                    apikey TEXT NOT NULL,
                    secretkeysha256 TEXT,
                    secretkeylast4 TEXT,
                    active INTEGER NOT NULL DEFAULT 1,
                    isadmin INTEGER NOT NULL DEFAULT 0,
                    createdutc TEXT NOT NULL
                );";
            }

            return @"CREATE TABLE apikeys (
                id VARCHAR(64) PRIMARY KEY,
                tenantid VARCHAR(64) NOT NULL DEFAULT '',
                userid VARCHAR(64) NOT NULL DEFAULT '',
                name VARCHAR(256) NOT NULL,
                apikey VARCHAR(256) NOT NULL,
                secretkeysha256 VARCHAR(128) NULL,
                secretkeylast4 VARCHAR(16) NULL,
                active BOOLEAN NOT NULL DEFAULT TRUE,
                isadmin BOOLEAN NOT NULL DEFAULT FALSE,
                createdutc TIMESTAMP NOT NULL
            );";
        }

        private static string RenameTable(DatabaseTypeEnum databaseType, string oldName, string newName)
        {
            if (databaseType == DatabaseTypeEnum.Mysql) return "RENAME TABLE " + Table(databaseType, oldName) + " TO " + Table(databaseType, newName) + ";";
            if (databaseType == DatabaseTypeEnum.SqlServer) return "EXEC sp_rename '" + Sanitize(oldName) + "', '" + Sanitize(newName) + "';";
            return "ALTER TABLE " + Table(databaseType, oldName) + " RENAME TO " + Table(databaseType, newName) + ";";
        }

        private static string DropIndex(DatabaseTypeEnum databaseType, string tableName, string indexName)
        {
            if (databaseType == DatabaseTypeEnum.Mysql) return "DROP INDEX `" + Sanitize(indexName) + "` ON `" + Sanitize(tableName) + "`;";
            if (databaseType == DatabaseTypeEnum.SqlServer) return "IF EXISTS (SELECT * FROM sys.indexes WHERE name = '" + Sanitize(indexName) + "') DROP INDEX [" + Sanitize(indexName) + "] ON [" + Sanitize(tableName) + "];";
            if (databaseType == DatabaseTypeEnum.Sqlite) return "DROP INDEX IF EXISTS " + Sanitize(indexName) + ";";
            return "DROP INDEX IF EXISTS " + Sanitize(indexName) + ";";
        }

        private static string CommittedColumn(DatabaseTypeEnum databaseType)
        {
            return databaseType == DatabaseTypeEnum.Sqlite ? "committed" : "iscommitted";
        }

        private static string Columns(DatabaseTypeEnum databaseType, params string[] columnNames)
        {
            List<string> columns = new List<string>();
            foreach (string columnName in columnNames)
            {
                columns.Add(Column(databaseType, columnName));
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
