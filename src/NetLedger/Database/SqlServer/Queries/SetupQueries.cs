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
                @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'schemamigrations')
                CREATE TABLE [schemamigrations] (
                    [id] NVARCHAR(64) NOT NULL PRIMARY KEY,
                    [name] NVARCHAR(256) NOT NULL,
                    [appliedutc] DATETIME2 NOT NULL,
                    [checksum] NVARCHAR(128) NOT NULL,
                    [success] BIT NOT NULL DEFAULT 1
                );",

                @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'accountlocks')
                CREATE TABLE [accountlocks] (
                    [accountid] NVARCHAR(64) NOT NULL PRIMARY KEY,
                    [ownerid] NVARCHAR(64) NOT NULL,
                    [expiresutc] DATETIME2 NOT NULL,
                    [createdutc] DATETIME2 NOT NULL
                );",

                @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'accountarchivalsettings')
                CREATE TABLE [accountarchivalsettings] (
                    [id] NVARCHAR(64) NOT NULL PRIMARY KEY,
                    [tenantid] NVARCHAR(64) NOT NULL,
                    [accountid] NVARCHAR(64) NOT NULL,
                    [enabled] BIT NULL,
                    [maxretentiondays] BIGINT NULL,
                    [intervalseconds] INT NULL,
                    [maxbatchrows] INT NULL,
                    [deleteaftercommit] BIT NULL,
                    [storagepoolid] NVARCHAR(128) NULL,
                    [retrymaxattempts] INT NULL,
                    [retryinitialdelayseconds] INT NULL,
                    [retrymaxdelayseconds] INT NULL,
                    [lastattemptutc] DATETIME2 NULL,
                    [lastsuccessutc] DATETIME2 NULL,
                    [lastarchivedthroughutc] DATETIME2 NULL,
                    [lastfailureutc] DATETIME2 NULL,
                    [nextattemptutc] DATETIME2 NULL,
                    [failurecount] INT NOT NULL DEFAULT 0,
                    [lasterror] NVARCHAR(MAX) NULL,
                    [createdutc] DATETIME2 NOT NULL,
                    [lastupdateutc] DATETIME2 NOT NULL
                );",

                @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'accounts')
                CREATE TABLE [accounts] (
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
                );",

                @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'entries')
                CREATE TABLE [entries] (
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
                );",

                @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'apikeys')
                CREATE TABLE [apikeys] (
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
                );",

                @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tenants')
                CREATE TABLE [tenants] (
                    [id] NVARCHAR(64) NOT NULL PRIMARY KEY,
                    [parentid] NVARCHAR(64) NULL,
                    [name] NVARCHAR(256) NOT NULL,
                    [region] NVARCHAR(64) NULL,
                    [active] BIT NOT NULL DEFAULT 1,
                    [isprotected] BIT NOT NULL DEFAULT 0,
                    [createdutc] DATETIME2 NOT NULL,
                    [lastupdateutc] DATETIME2 NOT NULL
                );",

                @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'users')
                CREATE TABLE [users] (
                    [id] NVARCHAR(64) NOT NULL PRIMARY KEY,
                    [tenantid] NVARCHAR(64) NOT NULL,
                    [firstname] NVARCHAR(128) NULL,
                    [lastname] NVARCHAR(128) NULL,
                    [email] NVARCHAR(256) NOT NULL,
                    [passwordsha256] NVARCHAR(128) NULL,
                    [isadmin] BIT NOT NULL DEFAULT 0,
                    [istenantadmin] BIT NOT NULL DEFAULT 0,
                    [active] BIT NOT NULL DEFAULT 1,
                    [isprotected] BIT NOT NULL DEFAULT 0,
                    [createdutc] DATETIME2 NOT NULL,
                    [lastupdateutc] DATETIME2 NOT NULL
                );",

                @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'authsessions')
                CREATE TABLE [authsessions] (
                    [id] NVARCHAR(64) NOT NULL PRIMARY KEY,
                    [tenantid] NVARCHAR(64) NOT NULL,
                    [userid] NVARCHAR(64) NOT NULL,
                    [token] NVARCHAR(128) NOT NULL,
                    [active] BIT NOT NULL DEFAULT 1,
                    [expiresutc] DATETIME2 NOT NULL,
                    [revokedutc] DATETIME2 NULL,
                    [createdutc] DATETIME2 NOT NULL,
                    [lastupdateutc] DATETIME2 NOT NULL
                );",

                @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'accountusermaps')
                CREATE TABLE [accountusermaps] (
                    [id] NVARCHAR(64) NOT NULL,
                    [tenantid] NVARCHAR(64) NOT NULL,
                    [accountid] NVARCHAR(64) NOT NULL,
                    [userid] NVARCHAR(64) NOT NULL,
                    [createdutc] DATETIME2 NOT NULL,
                    CONSTRAINT [pk_accountusermaps] PRIMARY KEY ([tenantid], [accountid], [userid])
                );",

                @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'auditrecords')
                CREATE TABLE [auditrecords] (
                    [id] NVARCHAR(64) NOT NULL PRIMARY KEY,
                    [tenantid] NVARCHAR(64) NULL,
                    [principalid] NVARCHAR(64) NULL,
                    [principaltype] NVARCHAR(64) NULL,
                    [eventtype] NVARCHAR(64) NOT NULL,
                    [resourcetype] NVARCHAR(64) NULL,
                    [operationtype] NVARCHAR(64) NULL,
                    [resourceid] NVARCHAR(64) NULL,
                    [result] NVARCHAR(64) NOT NULL,
                    [reason] NVARCHAR(MAX) NULL,
                    [requestid] NVARCHAR(64) NULL,
                    [createdutc] DATETIME2 NOT NULL
                );",

                @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'requesthistory')
                CREATE TABLE [requesthistory] (
                    [id] NVARCHAR(64) NOT NULL PRIMARY KEY,
                    [tenantid] NVARCHAR(64) NULL,
                    [principalid] NVARCHAR(64) NULL,
                    [principaltype] NVARCHAR(64) NULL,
                    [method] NVARCHAR(16) NOT NULL,
                    [path] NVARCHAR(MAX) NOT NULL,
                    [url] NVARCHAR(MAX) NOT NULL,
                    [statuscode] INT NOT NULL,
                    [durationms] FLOAT NOT NULL,
                    [sourceip] NVARCHAR(128) NULL,
                    [requestheaders] NVARCHAR(MAX) NOT NULL,
                    [requestbody] NVARCHAR(MAX) NULL,
                    [requestbodybytes] BIGINT NOT NULL DEFAULT 0,
                    [requestbodytruncated] BIT NOT NULL DEFAULT 0,
                    [responseheaders] NVARCHAR(MAX) NOT NULL,
                    [responsebody] NVARCHAR(MAX) NULL,
                    [responsebodybytes] BIGINT NOT NULL DEFAULT 0,
                    [responsebodytruncated] BIT NOT NULL DEFAULT 0,
                    [createdutc] DATETIME2 NOT NULL,
                    [completedutc] DATETIME2 NULL
                );",

                @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'userroles')
                CREATE TABLE [userroles] (
                    [id] NVARCHAR(64) NOT NULL PRIMARY KEY,
                    [tenantid] NVARCHAR(64) NOT NULL DEFAULT '',
                    [name] NVARCHAR(128) NOT NULL,
                    [isbuiltin] BIT NOT NULL DEFAULT 0,
                    [active] BIT NOT NULL DEFAULT 1,
                    [isprotected] BIT NOT NULL DEFAULT 0,
                    [createdutc] DATETIME2 NOT NULL,
                    [lastupdateutc] DATETIME2 NOT NULL
                );",

                @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'permissions')
                CREATE TABLE [permissions] (
                    [id] NVARCHAR(64) NOT NULL PRIMARY KEY,
                    [tenantid] NVARCHAR(64) NOT NULL DEFAULT '',
                    [name] NVARCHAR(128) NOT NULL,
                    [resourcetypes] NVARCHAR(MAX) NOT NULL,
                    [operationtypes] NVARCHAR(MAX) NOT NULL,
                    [permissiontype] NVARCHAR(16) NOT NULL DEFAULT 'Permit',
                    [active] BIT NOT NULL DEFAULT 1,
                    [isprotected] BIT NOT NULL DEFAULT 0,
                    [createdutc] DATETIME2 NOT NULL,
                    [lastupdateutc] DATETIME2 NOT NULL
                );",

                @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'rolepermissionmaps')
                CREATE TABLE [rolepermissionmaps] (
                    [id] NVARCHAR(64) NOT NULL PRIMARY KEY,
                    [tenantid] NVARCHAR(64) NOT NULL DEFAULT '',
                    [roleid] NVARCHAR(64) NOT NULL,
                    [permissionid] NVARCHAR(64) NOT NULL,
                    [active] BIT NOT NULL DEFAULT 1,
                    [isprotected] BIT NOT NULL DEFAULT 0,
                    [createdutc] DATETIME2 NOT NULL
                );",

                @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'userroleassignments')
                CREATE TABLE [userroleassignments] (
                    [id] NVARCHAR(64) NOT NULL PRIMARY KEY,
                    [tenantid] NVARCHAR(64) NOT NULL,
                    [userid] NVARCHAR(64) NOT NULL,
                    [roleid] NVARCHAR(64) NULL,
                    [rolename] NVARCHAR(128) NULL,
                    [resourcescope] NVARCHAR(16) NOT NULL DEFAULT 'Tenant',
                    [resourceid] NVARCHAR(64) NULL,
                    [inheritstochildren] BIT NOT NULL DEFAULT 1,
                    [active] BIT NOT NULL DEFAULT 1,
                    [isprotected] BIT NOT NULL DEFAULT 0,
                    [createdutc] DATETIME2 NOT NULL,
                    [lastupdateutc] DATETIME2 NOT NULL
                );",

                @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'credentialscopeassignments')
                CREATE TABLE [credentialscopeassignments] (
                    [id] NVARCHAR(64) NOT NULL PRIMARY KEY,
                    [tenantid] NVARCHAR(64) NOT NULL,
                    [credentialid] NVARCHAR(64) NOT NULL,
                    [roleid] NVARCHAR(64) NULL,
                    [rolename] NVARCHAR(128) NULL,
                    [resourcescope] NVARCHAR(16) NOT NULL DEFAULT 'Tenant',
                    [resourceid] NVARCHAR(64) NULL,
                    [operationtypes] NVARCHAR(MAX) NOT NULL,
                    [resourcetypes] NVARCHAR(MAX) NOT NULL,
                    [active] BIT NOT NULL DEFAULT 1,
                    [isprotected] BIT NOT NULL DEFAULT 0,
                    [createdutc] DATETIME2 NOT NULL,
                    [lastupdateutc] DATETIME2 NOT NULL
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
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_accounts_tenantid_id') CREATE INDEX [idx_accounts_tenantid_id] ON [accounts] ([tenantid], [id]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_accounts_name') CREATE INDEX [idx_accounts_name] ON [accounts] ([name]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_accounts_createdutc') CREATE INDEX [idx_accounts_createdutc] ON [accounts] ([createdutc]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_accountarchivalsettings_tenantid_accountid') CREATE UNIQUE INDEX [idx_accountarchivalsettings_tenantid_accountid] ON [accountarchivalsettings] ([tenantid], [accountid]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_accountarchivalsettings_nextattemptutc') CREATE INDEX [idx_accountarchivalsettings_nextattemptutc] ON [accountarchivalsettings] ([nextattemptutc]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_accountarchivalsettings_lastattemptutc') CREATE INDEX [idx_accountarchivalsettings_lastattemptutc] ON [accountarchivalsettings] ([lastattemptutc]);",

                // Entries indices
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_entries_tenantid_id') CREATE INDEX [idx_entries_tenantid_id] ON [entries] ([tenantid], [id]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_entries_accountguid') CREATE INDEX [idx_entries_accountguid] ON [entries] ([accountguid]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_entries_tenantid_accountguid') CREATE INDEX [idx_entries_tenantid_accountguid] ON [entries] ([tenantid], [accountguid]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_entries_type') CREATE INDEX [idx_entries_type] ON [entries] ([type]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_entries_iscommitted') CREATE INDEX [idx_entries_iscommitted] ON [entries] ([iscommitted]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_entries_createdutc') CREATE INDEX [idx_entries_createdutc] ON [entries] ([createdutc]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_entries_accountguid_createdutc') CREATE INDEX [idx_entries_accountguid_createdutc] ON [entries] ([accountguid], [createdutc]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_entries_accountguid_id') CREATE INDEX [idx_entries_accountguid_id] ON [entries] ([accountguid], [id]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_entries_accountguid_type') CREATE INDEX [idx_entries_accountguid_type] ON [entries] ([accountguid], [type]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_entries_accountguid_iscommitted') CREATE INDEX [idx_entries_accountguid_iscommitted] ON [entries] ([accountguid], [iscommitted]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_entries_accountguid_type_iscommitted_createdutc') CREATE INDEX [idx_entries_accountguid_type_iscommitted_createdutc] ON [entries] ([accountguid], [type], [iscommitted], [createdutc]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_entries_tenantid_accountguid_createdutc') CREATE INDEX [idx_entries_tenantid_accountguid_createdutc] ON [entries] ([tenantid], [accountguid], [createdutc]);",

                // API keys indices
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_apikeys_apikey') CREATE INDEX [idx_apikeys_apikey] ON [apikeys] ([apikey]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_apikeys_active') CREATE INDEX [idx_apikeys_active] ON [apikeys] ([active]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_apikeys_createdutc') CREATE INDEX [idx_apikeys_createdutc] ON [apikeys] ([createdutc]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_tenants_name') CREATE INDEX [idx_tenants_name] ON [tenants] ([name]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_users_tenantid_email') CREATE UNIQUE INDEX [idx_users_tenantid_email] ON [users] ([tenantid], [email]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_authsessions_token') CREATE INDEX [idx_authsessions_token] ON [authsessions] ([token]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_accountusermaps_userid') CREATE INDEX [idx_accountusermaps_userid] ON [accountusermaps] ([userid]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_auditrecords_tenantid_createdutc') CREATE INDEX [idx_auditrecords_tenantid_createdutc] ON [auditrecords] ([tenantid], [createdutc]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_requesthistory_tenantid_createdutc') CREATE INDEX [idx_requesthistory_tenantid_createdutc] ON [requesthistory] ([tenantid], [createdutc]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_requesthistory_principalid_createdutc') CREATE INDEX [idx_requesthistory_principalid_createdutc] ON [requesthistory] ([principalid], [createdutc]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_requesthistory_method_createdutc') CREATE INDEX [idx_requesthistory_method_createdutc] ON [requesthistory] ([method], [createdutc]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_requesthistory_statuscode_createdutc') CREATE INDEX [idx_requesthistory_statuscode_createdutc] ON [requesthistory] ([statuscode], [createdutc]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_userroles_tenantid_name') CREATE UNIQUE INDEX [idx_userroles_tenantid_name] ON [userroles] ([tenantid], [name]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_permissions_tenantid_name') CREATE INDEX [idx_permissions_tenantid_name] ON [permissions] ([tenantid], [name]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_rolepermissionmaps_roleid') CREATE INDEX [idx_rolepermissionmaps_roleid] ON [rolepermissionmaps] ([roleid]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_userroleassignments_tenantid_userid') CREATE INDEX [idx_userroleassignments_tenantid_userid] ON [userroleassignments] ([tenantid], [userid]);",
                "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_credentialscopeassignments_tenantid_credentialid') CREATE INDEX [idx_credentialscopeassignments_tenantid_credentialid] ON [credentialscopeassignments] ([tenantid], [credentialid]);"
            };
        }
    }
}



