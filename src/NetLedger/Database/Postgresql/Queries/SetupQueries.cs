namespace NetLedger.Database.Postgresql.Queries
{
    using System;

    /// <summary>
    /// PostgreSQL setup queries for table and index creation.
    /// </summary>
    internal static class SetupQueries
    {
        /// <summary>
        /// Timestamp format for PostgreSQL TIMESTAMP columns.
        /// </summary>
        internal const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";

        /// <summary>
        /// Get the SQL statements to create all required tables.
        /// </summary>
        /// <returns>Array of SQL statements.</returns>
        internal static string[] CreateTables()
        {
            return new string[]
            {
                @"CREATE TABLE IF NOT EXISTS accounts (
                    id SERIAL PRIMARY KEY,
                    guid VARCHAR(36) NOT NULL,
                    name VARCHAR(256) NOT NULL,
                    notes TEXT NULL,
                    createdutc TIMESTAMP NOT NULL
                );",

                @"CREATE TABLE IF NOT EXISTS entries (
                    id SERIAL PRIMARY KEY,
                    guid VARCHAR(36) NOT NULL,
                    accountguid VARCHAR(36) NOT NULL,
                    type VARCHAR(16) NOT NULL,
                    amount NUMERIC(18, 8) NOT NULL,
                    description TEXT NULL,
                    replaces VARCHAR(36) NULL,
                    iscommitted BOOLEAN NOT NULL DEFAULT FALSE,
                    committedbyguid VARCHAR(36) NULL,
                    committedutc TIMESTAMP NULL,
                    createdutc TIMESTAMP NOT NULL
                );",

                @"CREATE TABLE IF NOT EXISTS apikeys (
                    id SERIAL PRIMARY KEY,
                    guid VARCHAR(36) NOT NULL,
                    name VARCHAR(256) NOT NULL,
                    apikey VARCHAR(256) NOT NULL,
                    active BOOLEAN NOT NULL DEFAULT TRUE,
                    isadmin BOOLEAN NOT NULL DEFAULT FALSE,
                    createdutc TIMESTAMP NOT NULL
                );"
            };
        }

        /// <summary>
        /// Get the SQL statements to create all required indices.
        /// </summary>
        /// <returns>Array of SQL statements.</returns>
        internal static string[] CreateIndices()
        {
            return new string[]
            {
                // Accounts indices
                "CREATE INDEX IF NOT EXISTS idx_accounts_guid ON accounts (guid);",
                "CREATE INDEX IF NOT EXISTS idx_accounts_name ON accounts (name);",
                "CREATE INDEX IF NOT EXISTS idx_accounts_createdutc ON accounts (createdutc);",

                // Entries indices
                "CREATE INDEX IF NOT EXISTS idx_entries_guid ON entries (guid);",
                "CREATE INDEX IF NOT EXISTS idx_entries_accountguid ON entries (accountguid);",
                "CREATE INDEX IF NOT EXISTS idx_entries_type ON entries (type);",
                "CREATE INDEX IF NOT EXISTS idx_entries_iscommitted ON entries (iscommitted);",
                "CREATE INDEX IF NOT EXISTS idx_entries_createdutc ON entries (createdutc);",
                "CREATE INDEX IF NOT EXISTS idx_entries_accountguid_type ON entries (accountguid, type);",
                "CREATE INDEX IF NOT EXISTS idx_entries_accountguid_iscommitted ON entries (accountguid, iscommitted);",

                // API keys indices
                "CREATE INDEX IF NOT EXISTS idx_apikeys_guid ON apikeys (guid);",
                "CREATE INDEX IF NOT EXISTS idx_apikeys_apikey ON apikeys (apikey);",
                "CREATE INDEX IF NOT EXISTS idx_apikeys_active ON apikeys (active);",
                "CREATE INDEX IF NOT EXISTS idx_apikeys_createdutc ON apikeys (createdutc);"
            };
        }
    }
}
