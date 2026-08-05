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
        internal const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.ffffff";

        /// <summary>
        /// Get the SQL statements to create all required tables.
        /// </summary>
        /// <returns>Array of SQL statements.</returns>
        internal static string[] CreateTables()
        {
            return new string[]
            {
                @"CREATE TABLE IF NOT EXISTS schemamigrations (
                    id VARCHAR(64) PRIMARY KEY,
                    name VARCHAR(256) NOT NULL,
                    appliedutc TIMESTAMP NOT NULL,
                    checksum VARCHAR(128) NOT NULL,
                    success BOOLEAN NOT NULL DEFAULT TRUE
                );",

                @"CREATE TABLE IF NOT EXISTS accountlocks (
                    accountid VARCHAR(64) PRIMARY KEY,
                    ownerid VARCHAR(64) NOT NULL,
                    expiresutc TIMESTAMP NOT NULL,
                    createdutc TIMESTAMP NOT NULL
                );",

                @"CREATE TABLE IF NOT EXISTS accountarchivalsettings (
                    id VARCHAR(64) PRIMARY KEY,
                    tenantid VARCHAR(64) NOT NULL,
                    accountid VARCHAR(64) NOT NULL,
                    enabled BOOLEAN NULL,
                    maxretentiondays BIGINT NULL,
                    intervalseconds INTEGER NULL,
                    maxbatchrows INTEGER NULL,
                    deleteaftercommit BOOLEAN NULL,
                    storagepoolid VARCHAR(128) NULL,
                    retrymaxattempts INTEGER NULL,
                    retryinitialdelayseconds INTEGER NULL,
                    retrymaxdelayseconds INTEGER NULL,
                    lastattemptutc TIMESTAMP NULL,
                    lastsuccessutc TIMESTAMP NULL,
                    lastarchivedthroughutc TIMESTAMP NULL,
                    lastfailureutc TIMESTAMP NULL,
                    nextattemptutc TIMESTAMP NULL,
                    failurecount INTEGER NOT NULL DEFAULT 0,
                    lasterror TEXT NULL,
                    createdutc TIMESTAMP NOT NULL,
                    lastupdateutc TIMESTAMP NOT NULL
                );",

                @"CREATE TABLE IF NOT EXISTS accounts (
                    id VARCHAR(64) PRIMARY KEY,
                    tenantid VARCHAR(64) NOT NULL DEFAULT '',
                    owneruserid VARCHAR(64) NULL,
                    name VARCHAR(256) NOT NULL,
                    notes TEXT NULL,
                    units VARCHAR(64) NULL,
                    labels TEXT NOT NULL DEFAULT '[]',
                    tags TEXT NOT NULL DEFAULT '{}',
                    active BOOLEAN NOT NULL DEFAULT TRUE,
                    createdutc TIMESTAMP NOT NULL,
                    lastupdateutc TIMESTAMP NOT NULL
                );",

                @"CREATE TABLE IF NOT EXISTS entries (
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
                );",

                @"CREATE TABLE IF NOT EXISTS apikeys (
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
                );",

                @"CREATE TABLE IF NOT EXISTS tenants (
                    id VARCHAR(64) PRIMARY KEY,
                    parentid VARCHAR(64) NULL,
                    name VARCHAR(256) NOT NULL,
                    region VARCHAR(64) NULL,
                    active BOOLEAN NOT NULL DEFAULT TRUE,
                    isprotected BOOLEAN NOT NULL DEFAULT FALSE,
                    createdutc TIMESTAMP NOT NULL,
                    lastupdateutc TIMESTAMP NOT NULL
                );",

                @"CREATE TABLE IF NOT EXISTS users (
                    id VARCHAR(64) PRIMARY KEY,
                    tenantid VARCHAR(64) NOT NULL,
                    firstname VARCHAR(128) NULL,
                    lastname VARCHAR(128) NULL,
                    email VARCHAR(256) NOT NULL,
                    passwordsha256 VARCHAR(128) NULL,
                    isadmin BOOLEAN NOT NULL DEFAULT FALSE,
                    istenantadmin BOOLEAN NOT NULL DEFAULT FALSE,
                    active BOOLEAN NOT NULL DEFAULT TRUE,
                    isprotected BOOLEAN NOT NULL DEFAULT FALSE,
                    createdutc TIMESTAMP NOT NULL,
                    lastupdateutc TIMESTAMP NOT NULL
                );",

                @"CREATE TABLE IF NOT EXISTS authsessions (
                    id VARCHAR(64) PRIMARY KEY,
                    tenantid VARCHAR(64) NOT NULL,
                    userid VARCHAR(64) NOT NULL,
                    token VARCHAR(128) NOT NULL,
                    active BOOLEAN NOT NULL DEFAULT TRUE,
                    expiresutc TIMESTAMP NOT NULL,
                    revokedutc TIMESTAMP NULL,
                    createdutc TIMESTAMP NOT NULL,
                    lastupdateutc TIMESTAMP NOT NULL
                );",

                @"CREATE TABLE IF NOT EXISTS accountusermaps (
                    id VARCHAR(64) NOT NULL,
                    tenantid VARCHAR(64) NOT NULL,
                    accountid VARCHAR(64) NOT NULL,
                    userid VARCHAR(64) NOT NULL,
                    createdutc TIMESTAMP NOT NULL,
                    PRIMARY KEY (tenantid, accountid, userid)
                );",

                @"CREATE TABLE IF NOT EXISTS auditrecords (
                    id VARCHAR(64) PRIMARY KEY,
                    tenantid VARCHAR(64) NULL,
                    principalid VARCHAR(64) NULL,
                    principaltype VARCHAR(64) NULL,
                    eventtype VARCHAR(64) NOT NULL,
                    resourcetype VARCHAR(64) NULL,
                    operationtype VARCHAR(64) NULL,
                    resourceid VARCHAR(64) NULL,
                    result VARCHAR(64) NOT NULL,
                    reason TEXT NULL,
                    requestid VARCHAR(64) NULL,
                    createdutc TIMESTAMP NOT NULL
                );",

                @"CREATE TABLE IF NOT EXISTS requesthistory (
                    id VARCHAR(64) PRIMARY KEY,
                    tenantid VARCHAR(64) NULL,
                    principalid VARCHAR(64) NULL,
                    principaltype VARCHAR(64) NULL,
                    method VARCHAR(16) NOT NULL,
                    path TEXT NOT NULL,
                    url TEXT NOT NULL,
                    statuscode INTEGER NOT NULL,
                    durationms DOUBLE PRECISION NOT NULL,
                    sourceip VARCHAR(128) NULL,
                    requestheaders TEXT NOT NULL DEFAULT '{}',
                    requestbody TEXT NULL,
                    requestbodybytes BIGINT NOT NULL DEFAULT 0,
                    requestbodytruncated BOOLEAN NOT NULL DEFAULT FALSE,
                    responseheaders TEXT NOT NULL DEFAULT '{}',
                    responsebody TEXT NULL,
                    responsebodybytes BIGINT NOT NULL DEFAULT 0,
                    responsebodytruncated BOOLEAN NOT NULL DEFAULT FALSE,
                    createdutc TIMESTAMP NOT NULL,
                    completedutc TIMESTAMP NULL
                );",

                @"CREATE TABLE IF NOT EXISTS userroles (
                    id VARCHAR(64) PRIMARY KEY,
                    tenantid VARCHAR(64) NOT NULL DEFAULT '',
                    name VARCHAR(128) NOT NULL,
                    isbuiltin BOOLEAN NOT NULL DEFAULT FALSE,
                    active BOOLEAN NOT NULL DEFAULT TRUE,
                    isprotected BOOLEAN NOT NULL DEFAULT FALSE,
                    createdutc TIMESTAMP NOT NULL,
                    lastupdateutc TIMESTAMP NOT NULL
                );",

                @"CREATE TABLE IF NOT EXISTS permissions (
                    id VARCHAR(64) PRIMARY KEY,
                    tenantid VARCHAR(64) NOT NULL DEFAULT '',
                    name VARCHAR(128) NOT NULL,
                    resourcetypes TEXT NOT NULL,
                    operationtypes TEXT NOT NULL,
                    permissiontype VARCHAR(16) NOT NULL DEFAULT 'Permit',
                    active BOOLEAN NOT NULL DEFAULT TRUE,
                    isprotected BOOLEAN NOT NULL DEFAULT FALSE,
                    createdutc TIMESTAMP NOT NULL,
                    lastupdateutc TIMESTAMP NOT NULL
                );",

                @"CREATE TABLE IF NOT EXISTS rolepermissionmaps (
                    id VARCHAR(64) PRIMARY KEY,
                    tenantid VARCHAR(64) NOT NULL DEFAULT '',
                    roleid VARCHAR(64) NOT NULL,
                    permissionid VARCHAR(64) NOT NULL,
                    active BOOLEAN NOT NULL DEFAULT TRUE,
                    isprotected BOOLEAN NOT NULL DEFAULT FALSE,
                    createdutc TIMESTAMP NOT NULL
                );",

                @"CREATE TABLE IF NOT EXISTS userroleassignments (
                    id VARCHAR(64) PRIMARY KEY,
                    tenantid VARCHAR(64) NOT NULL,
                    userid VARCHAR(64) NOT NULL,
                    roleid VARCHAR(64) NULL,
                    rolename VARCHAR(128) NULL,
                    resourcescope VARCHAR(16) NOT NULL DEFAULT 'Tenant',
                    resourceid VARCHAR(64) NULL,
                    inheritstochildren BOOLEAN NOT NULL DEFAULT TRUE,
                    active BOOLEAN NOT NULL DEFAULT TRUE,
                    isprotected BOOLEAN NOT NULL DEFAULT FALSE,
                    createdutc TIMESTAMP NOT NULL,
                    lastupdateutc TIMESTAMP NOT NULL
                );",

                @"CREATE TABLE IF NOT EXISTS credentialscopeassignments (
                    id VARCHAR(64) PRIMARY KEY,
                    tenantid VARCHAR(64) NOT NULL,
                    credentialid VARCHAR(64) NOT NULL,
                    roleid VARCHAR(64) NULL,
                    rolename VARCHAR(128) NULL,
                    resourcescope VARCHAR(16) NOT NULL DEFAULT 'Tenant',
                    resourceid VARCHAR(64) NULL,
                    operationtypes TEXT NOT NULL,
                    resourcetypes TEXT NOT NULL,
                    active BOOLEAN NOT NULL DEFAULT TRUE,
                    isprotected BOOLEAN NOT NULL DEFAULT FALSE,
                    createdutc TIMESTAMP NOT NULL,
                    lastupdateutc TIMESTAMP NOT NULL
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
                "CREATE INDEX IF NOT EXISTS idx_accounts_tenantid_id ON accounts (tenantid, id);",
                "CREATE INDEX IF NOT EXISTS idx_accounts_name ON accounts (name);",
                "CREATE INDEX IF NOT EXISTS idx_accounts_createdutc ON accounts (createdutc);",
                "CREATE UNIQUE INDEX IF NOT EXISTS idx_accountarchivalsettings_tenantid_accountid ON accountarchivalsettings (tenantid, accountid);",
                "CREATE INDEX IF NOT EXISTS idx_accountarchivalsettings_nextattemptutc ON accountarchivalsettings (nextattemptutc);",
                "CREATE INDEX IF NOT EXISTS idx_accountarchivalsettings_lastattemptutc ON accountarchivalsettings (lastattemptutc);",

                // Entries indices
                "CREATE INDEX IF NOT EXISTS idx_entries_tenantid_id ON entries (tenantid, id);",
                "CREATE INDEX IF NOT EXISTS idx_entries_accountguid ON entries (accountguid);",
                "CREATE INDEX IF NOT EXISTS idx_entries_tenantid_accountguid ON entries (tenantid, accountguid);",
                "CREATE INDEX IF NOT EXISTS idx_entries_type ON entries (type);",
                "CREATE INDEX IF NOT EXISTS idx_entries_iscommitted ON entries (iscommitted);",
                "CREATE INDEX IF NOT EXISTS idx_entries_createdutc ON entries (createdutc);",
                "CREATE INDEX IF NOT EXISTS idx_entries_accountguid_createdutc ON entries (accountguid, createdutc);",
                "CREATE INDEX IF NOT EXISTS idx_entries_accountguid_id ON entries (accountguid, id);",
                "CREATE INDEX IF NOT EXISTS idx_entries_accountguid_type ON entries (accountguid, type);",
                "CREATE INDEX IF NOT EXISTS idx_entries_accountguid_iscommitted ON entries (accountguid, iscommitted);",
                "CREATE INDEX IF NOT EXISTS idx_entries_accountguid_type_iscommitted_createdutc ON entries (accountguid, type, iscommitted, createdutc);",
                "CREATE INDEX IF NOT EXISTS idx_entries_tenantid_accountguid_createdutc ON entries (tenantid, accountguid, createdutc);",

                // API keys indices
                "CREATE INDEX IF NOT EXISTS idx_apikeys_apikey ON apikeys (apikey);",
                "CREATE INDEX IF NOT EXISTS idx_apikeys_active ON apikeys (active);",
                "CREATE INDEX IF NOT EXISTS idx_apikeys_createdutc ON apikeys (createdutc);",
                "CREATE INDEX IF NOT EXISTS idx_tenants_name ON tenants (name);",
                "CREATE UNIQUE INDEX IF NOT EXISTS idx_users_tenantid_email ON users (tenantid, email);",
                "CREATE INDEX IF NOT EXISTS idx_authsessions_token ON authsessions (token);",
                "CREATE INDEX IF NOT EXISTS idx_accountusermaps_userid ON accountusermaps (userid);",
                "CREATE INDEX IF NOT EXISTS idx_auditrecords_tenantid_createdutc ON auditrecords (tenantid, createdutc);",
                "CREATE INDEX IF NOT EXISTS idx_requesthistory_tenantid_createdutc ON requesthistory (tenantid, createdutc);",
                "CREATE INDEX IF NOT EXISTS idx_requesthistory_principalid_createdutc ON requesthistory (principalid, createdutc);",
                "CREATE INDEX IF NOT EXISTS idx_requesthistory_method_createdutc ON requesthistory (method, createdutc);",
                "CREATE INDEX IF NOT EXISTS idx_requesthistory_statuscode_createdutc ON requesthistory (statuscode, createdutc);",
                "CREATE UNIQUE INDEX IF NOT EXISTS idx_userroles_tenantid_name ON userroles (tenantid, name);",
                "CREATE INDEX IF NOT EXISTS idx_permissions_tenantid_name ON permissions (tenantid, name);",
                "CREATE INDEX IF NOT EXISTS idx_rolepermissionmaps_roleid ON rolepermissionmaps (roleid);",
                "CREATE INDEX IF NOT EXISTS idx_userroleassignments_tenantid_userid ON userroleassignments (tenantid, userid);",
                "CREATE INDEX IF NOT EXISTS idx_credentialscopeassignments_tenantid_credentialid ON credentialscopeassignments (tenantid, credentialid);"
            };
        }
    }
}



