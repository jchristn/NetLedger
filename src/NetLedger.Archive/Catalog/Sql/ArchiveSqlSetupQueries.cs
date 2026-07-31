namespace NetLedger.Archive.Catalog.Sql
{
    using System.Collections.Generic;
    using NetLedger.Database;

    /// <summary>
    /// Archive catalog setup queries.
    /// </summary>
    internal static class ArchiveSqlSetupQueries
    {
        /// <summary>
        /// Build table creation queries.
        /// </summary>
        /// <param name="databaseType">Database type.</param>
        /// <returns>Queries.</returns>
        internal static List<string> CreateTables(DatabaseTypeEnum databaseType)
        {
            List<string> queries = new List<string>();

            queries.Add(CreateTable(databaseType, ArchiveCatalogTables.SchemaMigrations,
                "id " + Text(databaseType, 128) + " NOT NULL PRIMARY KEY",
                "version " + Text(databaseType, 64) + " NOT NULL",
                "createdutc " + Timestamp(databaseType) + " NOT NULL"));

            queries.Add(CreateTable(databaseType, ArchiveCatalogTables.StoragePools,
                "id " + Text(databaseType, 128) + " NOT NULL PRIMARY KEY",
                "name " + Text(databaseType, 256) + " NOT NULL",
                "type " + Text(databaseType, 64) + " NOT NULL",
                "basepath " + LongText(databaseType) + " NULL",
                "bucket " + Text(databaseType, 512) + " NULL",
                "prefix " + Text(databaseType, 512) + " NOT NULL",
                "format " + Text(databaseType, 64) + " NOT NULL",
                "compression " + Text(databaseType, 64) + " NOT NULL",
                "createdutc " + Timestamp(databaseType) + " NOT NULL",
                "lastupdateutc " + Timestamp(databaseType) + " NOT NULL"));

            queries.Add(CreateTable(databaseType, ArchiveCatalogTables.Migrations,
                "id " + Text(databaseType, 128) + " NOT NULL PRIMARY KEY",
                "tenantid " + Text(databaseType, 128) + " NOT NULL",
                "accountid " + Text(databaseType, 128) + " NULL",
                "entitytype " + Text(databaseType, 64) + " NOT NULL",
                "storagepoolid " + Text(databaseType, 128) + " NOT NULL",
                "format " + Text(databaseType, 64) + " NOT NULL",
                "compression " + Text(databaseType, 64) + " NOT NULL",
                "fromutc " + Timestamp(databaseType) + " NOT NULL",
                "toutc " + Timestamp(databaseType) + " NOT NULL",
                "status " + Text(databaseType, 64) + " NOT NULL",
                "idempotencykey " + Text(databaseType, 256) + " NOT NULL",
                "createdutc " + Timestamp(databaseType) + " NOT NULL",
                "lastupdateutc " + Timestamp(databaseType) + " NOT NULL"));

            queries.Add(CreateTable(databaseType, ArchiveCatalogTables.MigrationBatches,
                "id " + Text(databaseType, 128) + " NOT NULL PRIMARY KEY",
                "migrationid " + Text(databaseType, 128) + " NOT NULL",
                "tenantid " + Text(databaseType, 128) + " NOT NULL",
                "accountid " + Text(databaseType, 128) + " NULL",
                "storagepoolid " + Text(databaseType, 128) + " NOT NULL",
                "sequencenumber " + BigInt(databaseType) + " NOT NULL",
                "rowcount " + BigInt(databaseType) + " NOT NULL",
                "bytecount " + BigInt(databaseType) + " NOT NULL",
                "contenthashsha256 " + Text(databaseType, 128) + " NOT NULL",
                "temporaryrelativepath " + LongText(databaseType) + " NOT NULL",
                "committedrelativepath " + LongText(databaseType) + " NOT NULL",
                "status " + Text(databaseType, 64) + " NOT NULL",
                "createdutc " + Timestamp(databaseType) + " NOT NULL",
                "lastupdateutc " + Timestamp(databaseType) + " NOT NULL"));

            queries.Add(CreateTable(databaseType, ArchiveCatalogTables.Manifests,
                "id " + Text(databaseType, 128) + " NOT NULL PRIMARY KEY",
                "tenantid " + Text(databaseType, 128) + " NOT NULL",
                "accountid " + Text(databaseType, 128) + " NULL",
                "migrationid " + Text(databaseType, 128) + " NULL",
                "entitytype " + Text(databaseType, 64) + " NOT NULL",
                "storagepoolid " + Text(databaseType, 128) + " NOT NULL",
                "fromutc " + Timestamp(databaseType) + " NOT NULL",
                "toutc " + Timestamp(databaseType) + " NOT NULL",
                "rowcount " + BigInt(databaseType) + " NOT NULL",
                "credittotal " + Decimal(databaseType) + " NOT NULL",
                "debittotal " + Decimal(databaseType) + " NOT NULL",
                "contenthashsha256 " + Text(databaseType, 128) + " NOT NULL",
                "manifesthashsha256 " + Text(databaseType, 128) + " NOT NULL",
                "status " + Text(databaseType, 64) + " NOT NULL",
                "createdutc " + Timestamp(databaseType) + " NOT NULL",
                "lastupdateutc " + Timestamp(databaseType) + " NOT NULL"));

            queries.Add(CreateTable(databaseType, ArchiveCatalogTables.Objects,
                "id " + Text(databaseType, 128) + " NOT NULL PRIMARY KEY",
                "manifestid " + Text(databaseType, 128) + " NOT NULL",
                "storagepoolid " + Text(databaseType, 128) + " NOT NULL",
                "relativepath " + LongText(databaseType) + " NOT NULL",
                "rowcount " + BigInt(databaseType) + " NOT NULL",
                "bytecount " + BigInt(databaseType) + " NOT NULL",
                "contenthashsha256 " + Text(databaseType, 128) + " NOT NULL",
                "createdutc " + Timestamp(databaseType) + " NOT NULL"));

            queries.Add(CreateTable(databaseType, ArchiveCatalogTables.AccountRanges,
                "id " + Text(databaseType, 128) + " NOT NULL PRIMARY KEY",
                "tenantid " + Text(databaseType, 128) + " NOT NULL",
                "accountid " + Text(databaseType, 128) + " NULL",
                "manifestid " + Text(databaseType, 128) + " NULL",
                "entitytype " + Text(databaseType, 64) + " NOT NULL",
                "fromutc " + Timestamp(databaseType) + " NOT NULL",
                "toutc " + Timestamp(databaseType) + " NOT NULL",
                "rowcount " + BigInt(databaseType) + " NOT NULL"));

            queries.Add(CreateTable(databaseType, ArchiveCatalogTables.BalanceCheckpoints,
                "id " + Text(databaseType, 128) + " NOT NULL PRIMARY KEY",
                "tenantid " + Text(databaseType, 128) + " NOT NULL",
                "accountid " + Text(databaseType, 128) + " NOT NULL",
                "manifestid " + Text(databaseType, 128) + " NOT NULL",
                "asofutc " + Timestamp(databaseType) + " NOT NULL",
                "balance " + Decimal(databaseType) + " NOT NULL",
                "createdutc " + Timestamp(databaseType) + " NOT NULL"));

            queries.Add(CreateTable(databaseType, ArchiveCatalogTables.RequestHistoryRanges,
                "id " + Text(databaseType, 128) + " NOT NULL PRIMARY KEY",
                "tenantid " + Text(databaseType, 128) + " NULL",
                "manifestid " + Text(databaseType, 128) + " NOT NULL",
                "fromutc " + Timestamp(databaseType) + " NOT NULL",
                "toutc " + Timestamp(databaseType) + " NOT NULL",
                "rowcount " + BigInt(databaseType) + " NOT NULL",
                "methodcountsjson " + LongText(databaseType) + " NULL",
                "statuscodecountsjson " + LongText(databaseType) + " NULL",
                "createdutc " + Timestamp(databaseType) + " NOT NULL"));

            queries.Add(CreateTable(databaseType, ArchiveCatalogTables.AuditRecords,
                "id " + Text(databaseType, 128) + " NOT NULL PRIMARY KEY",
                "tenantid " + Text(databaseType, 128) + " NULL",
                "principalid " + Text(databaseType, 128) + " NULL",
                "action " + Text(databaseType, 256) + " NOT NULL",
                "targettype " + Text(databaseType, 128) + " NOT NULL",
                "targetid " + Text(databaseType, 128) + " NULL",
                "metadata " + LongText(databaseType) + " NULL",
                "createdutc " + Timestamp(databaseType) + " NOT NULL"));

            queries.Add(CreateTable(databaseType, ArchiveCatalogTables.ServerRequestHistory,
                "id " + Text(databaseType, 128) + " NOT NULL PRIMARY KEY",
                "tenantid " + Text(databaseType, 128) + " NULL",
                "principalid " + Text(databaseType, 128) + " NULL",
                "method " + Text(databaseType, 16) + " NOT NULL",
                "path " + LongText(databaseType) + " NOT NULL",
                "statuscode " + Integer(databaseType) + " NOT NULL",
                "durationms " + Decimal(databaseType) + " NOT NULL",
                "createdutc " + Timestamp(databaseType) + " NOT NULL"));

            queries.Add(CreateTable(databaseType, ArchiveCatalogTables.ObjectLocks,
                "id " + Text(databaseType, 128) + " NOT NULL PRIMARY KEY",
                "targetid " + Text(databaseType, 128) + " NOT NULL",
                "ownerid " + Text(databaseType, 128) + " NOT NULL",
                "expiresutc " + Timestamp(databaseType) + " NOT NULL",
                "createdutc " + Timestamp(databaseType) + " NOT NULL"));

            queries.Add(CreateTable(databaseType, ArchiveCatalogTables.NonceReplay,
                "id " + Text(databaseType, 128) + " NOT NULL PRIMARY KEY",
                "nonce " + Text(databaseType, 256) + " NOT NULL",
                "expiresutc " + Timestamp(databaseType) + " NOT NULL",
                "createdutc " + Timestamp(databaseType) + " NOT NULL"));

            return queries;
        }

        /// <summary>
        /// Build index creation queries.
        /// </summary>
        /// <param name="databaseType">Database type.</param>
        /// <returns>Queries.</returns>
        internal static List<string> CreateIndexes(DatabaseTypeEnum databaseType)
        {
            return new List<string>
            {
                CreateIndex(databaseType, "idx_archivemanifests_tenant_account_time", ArchiveCatalogTables.Manifests, "tenantid", "accountid", "fromutc", "toutc"),
                CreateIndex(databaseType, "idx_archivemanifests_status", ArchiveCatalogTables.Manifests, "status"),
                CreateIndex(databaseType, "idx_archiveobjects_manifest", ArchiveCatalogTables.Objects, "manifestid"),
                CreateIndex(databaseType, "idx_archiveranges_tenant_account_time", ArchiveCatalogTables.AccountRanges, "tenantid", "accountid", "fromutc", "toutc"),
                CreateIndex(databaseType, "idx_archivebatches_migration", ArchiveCatalogTables.MigrationBatches, "migrationid", "sequencenumber"),
                CreateIndex(databaseType, "idx_archivemigrations_status", ArchiveCatalogTables.Migrations, "status"),
                CreateIndex(databaseType, "idx_archivemigrations_idempotency", ArchiveCatalogTables.Migrations, "idempotencykey"),
                CreateIndex(databaseType, "idx_archivecheckpoints_manifest", ArchiveCatalogTables.BalanceCheckpoints, "manifestid"),
                CreateIndex(databaseType, "idx_archivecheckpoints_account_time", ArchiveCatalogTables.BalanceCheckpoints, "tenantid", "accountid", "asofutc"),
                CreateIndex(databaseType, "idx_archiverequesthistoryranges_tenant_time", ArchiveCatalogTables.RequestHistoryRanges, "tenantid", "fromutc", "toutc"),
                CreateIndex(databaseType, "idx_archiverequesthistoryranges_manifest", ArchiveCatalogTables.RequestHistoryRanges, "manifestid"),
                CreateIndex(databaseType, "idx_archiveserverrequesthistory_tenant_time", ArchiveCatalogTables.ServerRequestHistory, "tenantid", "createdutc"),
                CreateIndex(databaseType, "idx_archiveserverrequesthistory_status", ArchiveCatalogTables.ServerRequestHistory, "statuscode"),
                CreateIndex(databaseType, "idx_archivenonce_nonce", ArchiveCatalogTables.NonceReplay, "nonce")
            };
        }

        private static string CreateTable(DatabaseTypeEnum databaseType, string table, params string[] columns)
        {
            ArchiveCatalogTables.ValidateApproved(table);
            string body = System.String.Join(", ", System.Array.ConvertAll(columns, column => ColumnDefinition(databaseType, column)));
            if (databaseType == DatabaseTypeEnum.SqlServer)
            {
                return "IF OBJECT_ID(N'" + table + "', N'U') IS NULL CREATE TABLE " +
                    ArchiveSqlDialect.Identifier(databaseType, table) + " (" + body + ");";
            }

            return "CREATE TABLE IF NOT EXISTS " + ArchiveSqlDialect.Identifier(databaseType, table) + " (" + body + ");";
        }

        private static string ColumnDefinition(DatabaseTypeEnum databaseType, string definition)
        {
            int split = definition.IndexOf(' ');
            if (split <= 0) return ArchiveSqlDialect.Identifier(databaseType, definition);
            string name = definition.Substring(0, split);
            string rest = definition.Substring(split);
            return ArchiveSqlDialect.Identifier(databaseType, name) + rest;
        }

        private static string CreateIndex(DatabaseTypeEnum databaseType, string index, string table, params string[] columns)
        {
            string columnList = System.String.Join(", ", System.Array.ConvertAll(columns, column => ArchiveSqlDialect.Identifier(databaseType, column)));
            if (databaseType == DatabaseTypeEnum.SqlServer)
            {
                return "IF NOT EXISTS (SELECT name FROM sys.indexes WHERE name = N'" + index + "') CREATE INDEX " +
                    ArchiveSqlDialect.Identifier(databaseType, index) + " ON " + ArchiveSqlDialect.Identifier(databaseType, table) + " (" + columnList + ");";
            }

            if (databaseType == DatabaseTypeEnum.Mysql)
            {
                return "CREATE INDEX " + ArchiveSqlDialect.Identifier(databaseType, index) + " ON " +
                    ArchiveSqlDialect.Identifier(databaseType, table) + " (" + columnList + ");";
            }

            return "CREATE INDEX IF NOT EXISTS " + ArchiveSqlDialect.Identifier(databaseType, index) + " ON " +
                ArchiveSqlDialect.Identifier(databaseType, table) + " (" + columnList + ");";
        }

        private static string Text(DatabaseTypeEnum databaseType, int length)
        {
            return databaseType == DatabaseTypeEnum.SqlServer ? "NVARCHAR(" + length + ")" : "VARCHAR(" + length + ")";
        }

        private static string LongText(DatabaseTypeEnum databaseType)
        {
            return databaseType == DatabaseTypeEnum.SqlServer ? "NVARCHAR(MAX)" : databaseType == DatabaseTypeEnum.Postgresql ? "TEXT" : "TEXT";
        }

        private static string Timestamp(DatabaseTypeEnum databaseType)
        {
            return databaseType == DatabaseTypeEnum.SqlServer ? "DATETIME2" : databaseType == DatabaseTypeEnum.Postgresql ? "TIMESTAMP" : "DATETIME";
        }

        private static string BigInt(DatabaseTypeEnum databaseType)
        {
            return "BIGINT";
        }

        private static string Integer(DatabaseTypeEnum databaseType)
        {
            return "INTEGER";
        }

        private static string Decimal(DatabaseTypeEnum databaseType)
        {
            return databaseType == DatabaseTypeEnum.SqlServer ? "DECIMAL(38, 12)" : "DECIMAL(38, 12)";
        }
    }
}
