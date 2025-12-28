namespace NetLedger.Database.SqlServer.Queries
{
    using System;

    /// <summary>
    /// SQL Server setup queries for table and index creation.
    /// </summary>
    internal static class SetupQueries
    {
        /// <summary>
        /// Timestamp format for SQL Server DATETIME2 columns.
        /// </summary>
        internal const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.ffffff";

        /// <summary>
        /// Get the SQL statements to create all required tables.
        /// </summary>
        /// <returns>Array of SQL statements.</returns>
        internal static string[] CreateTables()
        {
            return new string[]
            {
                @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'accounts')
                CREATE TABLE [accounts] (
                    [id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [guid] NVARCHAR(36) NOT NULL,
                    [name] NVARCHAR(256) NOT NULL,
                    [notes] NVARCHAR(MAX) NULL,
                    [createdutc] DATETIME2 NOT NULL
                );",

                @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'entries')
                CREATE TABLE [entries] (
                    [id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [guid] NVARCHAR(36) NOT NULL,
                    [accountguid] NVARCHAR(36) NOT NULL,
                    [type] NVARCHAR(16) NOT NULL,
                    [amount] DECIMAL(18, 8) NOT NULL,
                    [description] NVARCHAR(MAX) NULL,
                    [replaces] NVARCHAR(36) NULL,
                    [iscommitted] BIT NOT NULL DEFAULT 0,
                    [committedbyguid] NVARCHAR(36) NULL,
                    [committedutc] DATETIME2 NULL,
                    [createdutc] DATETIME2 NOT NULL
                );",

                @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'apikeys')
                CREATE TABLE [apikeys] (
                    [id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [guid] NVARCHAR(36) NOT NULL,
                    [name] NVARCHAR(256) NOT NULL,
                    [apikey] NVARCHAR(256) NOT NULL,
                    [active] BIT NOT NULL DEFAULT 1,
                    [isadmin] BIT NOT NULL DEFAULT 0,
                    [createdutc] DATETIME2 NOT NULL
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
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_accounts_guid') CREATE INDEX [idx_accounts_guid] ON [accounts] ([guid]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_accounts_name') CREATE INDEX [idx_accounts_name] ON [accounts] ([name]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_accounts_createdutc') CREATE INDEX [idx_accounts_createdutc] ON [accounts] ([createdutc]);",

                // Entries indices
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_entries_guid') CREATE INDEX [idx_entries_guid] ON [entries] ([guid]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_entries_accountguid') CREATE INDEX [idx_entries_accountguid] ON [entries] ([accountguid]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_entries_type') CREATE INDEX [idx_entries_type] ON [entries] ([type]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_entries_iscommitted') CREATE INDEX [idx_entries_iscommitted] ON [entries] ([iscommitted]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_entries_createdutc') CREATE INDEX [idx_entries_createdutc] ON [entries] ([createdutc]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_entries_accountguid_type') CREATE INDEX [idx_entries_accountguid_type] ON [entries] ([accountguid], [type]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_entries_accountguid_iscommitted') CREATE INDEX [idx_entries_accountguid_iscommitted] ON [entries] ([accountguid], [iscommitted]);",

                // API keys indices
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_apikeys_guid') CREATE INDEX [idx_apikeys_guid] ON [apikeys] ([guid]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_apikeys_apikey') CREATE INDEX [idx_apikeys_apikey] ON [apikeys] ([apikey]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_apikeys_active') CREATE INDEX [idx_apikeys_active] ON [apikeys] ([active]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_apikeys_createdutc') CREATE INDEX [idx_apikeys_createdutc] ON [apikeys] ([createdutc]);"
            };
        }
    }
}
