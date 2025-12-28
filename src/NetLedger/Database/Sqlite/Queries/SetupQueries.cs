namespace NetLedger.Database.Sqlite.Queries
{
    /// <summary>
    /// SQLite setup queries for creating tables and indices.
    /// </summary>
    internal static class SetupQueries
    {
        /// <summary>
        /// Timestamp format for SQLite.
        /// </summary>
        internal static readonly string TimestampFormat = "yyyy-MM-dd HH:mm:ss.ffffffZ";

        /// <summary>
        /// Create all required tables.
        /// </summary>
        /// <returns>SQL statements to create tables.</returns>
        internal static string CreateTables()
        {
            return @"
CREATE TABLE IF NOT EXISTS accounts (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    guid TEXT NOT NULL,
    name TEXT NOT NULL,
    notes TEXT,
    createdutc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS entries (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    guid TEXT NOT NULL,
    accountguid TEXT NOT NULL,
    type TEXT NOT NULL,
    amount REAL NOT NULL,
    description TEXT,
    replaces TEXT,
    committed INTEGER NOT NULL DEFAULT 0,
    committedbyguid TEXT,
    committedutc TEXT,
    createdutc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS apikeys (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    guid TEXT NOT NULL,
    name TEXT NOT NULL,
    apikey TEXT NOT NULL,
    active INTEGER NOT NULL DEFAULT 1,
    isadmin INTEGER NOT NULL DEFAULT 0,
    createdutc TEXT NOT NULL
);
";
        }

        /// <summary>
        /// Create indices for better query performance.
        /// </summary>
        /// <returns>SQL statements to create indices.</returns>
        internal static string CreateIndices()
        {
            return @"
CREATE INDEX IF NOT EXISTS idx_accounts_guid ON accounts(guid);
CREATE INDEX IF NOT EXISTS idx_accounts_name ON accounts(name);
CREATE INDEX IF NOT EXISTS idx_accounts_createdutc ON accounts(createdutc);

CREATE INDEX IF NOT EXISTS idx_entries_guid ON entries(guid);
CREATE INDEX IF NOT EXISTS idx_entries_accountguid ON entries(accountguid);
CREATE INDEX IF NOT EXISTS idx_entries_type ON entries(type);
CREATE INDEX IF NOT EXISTS idx_entries_committed ON entries(committed);
CREATE INDEX IF NOT EXISTS idx_entries_createdutc ON entries(createdutc);
CREATE INDEX IF NOT EXISTS idx_entries_committedbyguid ON entries(committedbyguid);
CREATE INDEX IF NOT EXISTS idx_entries_accountguid_createdutc ON entries(accountguid, createdutc);
CREATE INDEX IF NOT EXISTS idx_entries_accountguid_type ON entries(accountguid, type);
CREATE INDEX IF NOT EXISTS idx_entries_accountguid_committed ON entries(accountguid, committed);
CREATE INDEX IF NOT EXISTS idx_entries_accountguid_type_committed ON entries(accountguid, type, committed);

CREATE INDEX IF NOT EXISTS idx_apikeys_guid ON apikeys(guid);
CREATE INDEX IF NOT EXISTS idx_apikeys_apikey ON apikeys(apikey);
CREATE INDEX IF NOT EXISTS idx_apikeys_active ON apikeys(active);
CREATE INDEX IF NOT EXISTS idx_apikeys_apikey_active ON apikeys(apikey, active);
";
        }
    }
}
