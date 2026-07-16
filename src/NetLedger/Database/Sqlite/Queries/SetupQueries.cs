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
    tenantid TEXT NOT NULL DEFAULT '',
    owneruserid TEXT,
    name TEXT NOT NULL,
    notes TEXT,
    labels TEXT NOT NULL DEFAULT '[]',
    tags TEXT NOT NULL DEFAULT '{}',
    active INTEGER NOT NULL DEFAULT 1,
    createdutc TEXT NOT NULL,
    lastupdateutc TEXT NOT NULL DEFAULT ''
);

CREATE TABLE IF NOT EXISTS schemamigrations (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    appliedutc TEXT NOT NULL,
    checksum TEXT NOT NULL,
    success INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS entries (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    guid TEXT NOT NULL,
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
);

CREATE TABLE IF NOT EXISTS tenants (
    id TEXT PRIMARY KEY,
    parentid TEXT,
    name TEXT NOT NULL,
    region TEXT,
    active INTEGER NOT NULL DEFAULT 1,
    isprotected INTEGER NOT NULL DEFAULT 0,
    createdutc TEXT NOT NULL,
    lastupdateutc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS users (
    id TEXT PRIMARY KEY,
    tenantid TEXT NOT NULL,
    firstname TEXT,
    lastname TEXT,
    email TEXT NOT NULL,
    passwordsha256 TEXT,
    isadmin INTEGER NOT NULL DEFAULT 0,
    istenantadmin INTEGER NOT NULL DEFAULT 0,
    active INTEGER NOT NULL DEFAULT 1,
    isprotected INTEGER NOT NULL DEFAULT 0,
    createdutc TEXT NOT NULL,
    lastupdateutc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS credentials (
    id TEXT PRIMARY KEY,
    tenantid TEXT NOT NULL,
    userid TEXT NOT NULL,
    name TEXT NOT NULL,
    accesskey TEXT NOT NULL,
    secretkeysha256 TEXT,
    secretkeylast4 TEXT,
    authmode TEXT NOT NULL DEFAULT 'DirectHeader',
    active INTEGER NOT NULL DEFAULT 1,
    isprotected INTEGER NOT NULL DEFAULT 0,
    lastusedutc TEXT,
    expiresutc TEXT,
    createdutc TEXT NOT NULL,
    lastupdateutc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS authsessions (
    id TEXT PRIMARY KEY,
    tenantid TEXT NOT NULL,
    userid TEXT NOT NULL,
    token TEXT NOT NULL,
    active INTEGER NOT NULL DEFAULT 1,
    expiresutc TEXT NOT NULL,
    revokedutc TEXT,
    createdutc TEXT NOT NULL,
    lastupdateutc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS accountusermaps (
    id TEXT NOT NULL,
    tenantid TEXT NOT NULL,
    accountid TEXT NOT NULL,
    userid TEXT NOT NULL,
    createdutc TEXT NOT NULL,
    PRIMARY KEY (tenantid, accountid, userid)
);

CREATE TABLE IF NOT EXISTS apikeys (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    guid TEXT NOT NULL,
    tenantid TEXT NOT NULL DEFAULT '',
    userid TEXT NOT NULL DEFAULT '',
    name TEXT NOT NULL,
    apikey TEXT NOT NULL,
    secretkeysha256 TEXT,
    secretkeylast4 TEXT,
    active INTEGER NOT NULL DEFAULT 1,
    isadmin INTEGER NOT NULL DEFAULT 0,
    createdutc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS auditrecords (
    id TEXT PRIMARY KEY,
    tenantid TEXT,
    principalid TEXT,
    principaltype TEXT,
    eventtype TEXT NOT NULL,
    resourcetype TEXT,
    operationtype TEXT,
    resourceid TEXT,
    result TEXT NOT NULL,
    reason TEXT,
    requestid TEXT,
    createdutc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS requesthistory (
    id TEXT PRIMARY KEY,
    tenantid TEXT,
    principalid TEXT,
    principaltype TEXT,
    method TEXT NOT NULL,
    path TEXT NOT NULL,
    url TEXT NOT NULL,
    statuscode INTEGER NOT NULL,
    durationms REAL NOT NULL,
    sourceip TEXT,
    requestheaders TEXT NOT NULL DEFAULT '{}',
    requestbody TEXT,
    requestbodybytes INTEGER NOT NULL DEFAULT 0,
    requestbodytruncated INTEGER NOT NULL DEFAULT 0,
    responseheaders TEXT NOT NULL DEFAULT '{}',
    responsebody TEXT,
    responsebodybytes INTEGER NOT NULL DEFAULT 0,
    responsebodytruncated INTEGER NOT NULL DEFAULT 0,
    createdutc TEXT NOT NULL,
    completedutc TEXT
);

CREATE TABLE IF NOT EXISTS userroles (
    id TEXT PRIMARY KEY,
    tenantid TEXT NOT NULL DEFAULT '',
    name TEXT NOT NULL,
    isbuiltin INTEGER NOT NULL DEFAULT 0,
    active INTEGER NOT NULL DEFAULT 1,
    isprotected INTEGER NOT NULL DEFAULT 0,
    createdutc TEXT NOT NULL,
    lastupdateutc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS permissions (
    id TEXT PRIMARY KEY,
    tenantid TEXT NOT NULL DEFAULT '',
    name TEXT NOT NULL,
    resourcetypes TEXT NOT NULL DEFAULT '[]',
    operationtypes TEXT NOT NULL DEFAULT '[]',
    permissiontype TEXT NOT NULL DEFAULT 'Permit',
    active INTEGER NOT NULL DEFAULT 1,
    isprotected INTEGER NOT NULL DEFAULT 0,
    createdutc TEXT NOT NULL,
    lastupdateutc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS rolepermissionmaps (
    id TEXT PRIMARY KEY,
    tenantid TEXT NOT NULL DEFAULT '',
    roleid TEXT NOT NULL,
    permissionid TEXT NOT NULL,
    active INTEGER NOT NULL DEFAULT 1,
    isprotected INTEGER NOT NULL DEFAULT 0,
    createdutc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS userroleassignments (
    id TEXT PRIMARY KEY,
    tenantid TEXT NOT NULL,
    userid TEXT NOT NULL,
    roleid TEXT,
    rolename TEXT,
    resourcescope TEXT NOT NULL DEFAULT 'Tenant',
    resourceid TEXT,
    inheritstochildren INTEGER NOT NULL DEFAULT 1,
    active INTEGER NOT NULL DEFAULT 1,
    isprotected INTEGER NOT NULL DEFAULT 0,
    createdutc TEXT NOT NULL,
    lastupdateutc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS credentialscopeassignments (
    id TEXT PRIMARY KEY,
    tenantid TEXT NOT NULL,
    credentialid TEXT NOT NULL,
    roleid TEXT,
    rolename TEXT,
    resourcescope TEXT NOT NULL DEFAULT 'Tenant',
    resourceid TEXT,
    operationtypes TEXT NOT NULL DEFAULT '[]',
    resourcetypes TEXT NOT NULL DEFAULT '[]',
    active INTEGER NOT NULL DEFAULT 1,
    isprotected INTEGER NOT NULL DEFAULT 0,
    createdutc TEXT NOT NULL,
    lastupdateutc TEXT NOT NULL
);
";
        }

        /// <summary>
        /// Create the audit records table.
        /// </summary>
        /// <returns>SQL statement to create the audit records table.</returns>
        internal static string CreateAuditRecordsTable()
        {
            return @"
CREATE TABLE IF NOT EXISTS auditrecords (
    id TEXT PRIMARY KEY,
    tenantid TEXT,
    principalid TEXT,
    principaltype TEXT,
    eventtype TEXT NOT NULL,
    resourcetype TEXT,
    operationtype TEXT,
    resourceid TEXT,
    result TEXT NOT NULL,
    reason TEXT,
    requestid TEXT,
    createdutc TEXT NOT NULL
);";
        }

        /// <summary>
        /// Create indices for better query performance.
        /// </summary>
        /// <returns>SQL statements to create indices.</returns>
        internal static string CreateIndices()
        {
            return @"
CREATE INDEX IF NOT EXISTS idx_accounts_guid ON accounts(guid);
CREATE INDEX IF NOT EXISTS idx_accounts_tenantid_guid ON accounts(tenantid, guid);
CREATE INDEX IF NOT EXISTS idx_accounts_name ON accounts(name);
CREATE INDEX IF NOT EXISTS idx_accounts_tenantid_name ON accounts(tenantid, name);
CREATE INDEX IF NOT EXISTS idx_accounts_createdutc ON accounts(createdutc);

CREATE INDEX IF NOT EXISTS idx_entries_guid ON entries(guid);
CREATE INDEX IF NOT EXISTS idx_entries_tenantid_guid ON entries(tenantid, guid);
CREATE INDEX IF NOT EXISTS idx_entries_accountguid ON entries(accountguid);
CREATE INDEX IF NOT EXISTS idx_entries_tenantid_accountguid ON entries(tenantid, accountguid);
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

CREATE INDEX IF NOT EXISTS idx_tenants_name ON tenants(name);
CREATE UNIQUE INDEX IF NOT EXISTS idx_users_tenantid_email ON users(tenantid, email);
CREATE UNIQUE INDEX IF NOT EXISTS idx_credentials_accesskey ON credentials(accesskey);
CREATE INDEX IF NOT EXISTS idx_credentials_tenantid_userid ON credentials(tenantid, userid);
CREATE INDEX IF NOT EXISTS idx_authsessions_token ON authsessions(token);
CREATE INDEX IF NOT EXISTS idx_accountusermaps_userid ON accountusermaps(userid);
CREATE INDEX IF NOT EXISTS idx_auditrecords_tenantid_createdutc ON auditrecords(tenantid, createdutc);
CREATE INDEX IF NOT EXISTS idx_requesthistory_tenantid_createdutc ON requesthistory(tenantid, createdutc);
CREATE INDEX IF NOT EXISTS idx_requesthistory_principalid_createdutc ON requesthistory(principalid, createdutc);
CREATE INDEX IF NOT EXISTS idx_requesthistory_method_createdutc ON requesthistory(method, createdutc);
CREATE INDEX IF NOT EXISTS idx_requesthistory_statuscode_createdutc ON requesthistory(statuscode, createdutc);
CREATE UNIQUE INDEX IF NOT EXISTS idx_userroles_tenantid_name ON userroles(tenantid, name);
CREATE INDEX IF NOT EXISTS idx_permissions_tenantid_name ON permissions(tenantid, name);
CREATE INDEX IF NOT EXISTS idx_rolepermissionmaps_roleid ON rolepermissionmaps(roleid);
CREATE INDEX IF NOT EXISTS idx_userroleassignments_tenantid_userid ON userroleassignments(tenantid, userid);
CREATE INDEX IF NOT EXISTS idx_credentialscopeassignments_tenantid_credentialid ON credentialscopeassignments(tenantid, credentialid);
";
        }
    }
}



