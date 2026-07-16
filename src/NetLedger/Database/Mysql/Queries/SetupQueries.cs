namespace NetLedger.Database.Mysql.Queries
{
    using System;

    /// <summary>
    /// MySQL setup queries for table and index creation.
    /// </summary>
    internal static class SetupQueries
    {
        /// <summary>
        /// Timestamp format for MySQL DATETIME columns.
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
                @"CREATE TABLE IF NOT EXISTS `schemamigrations` (
                    `id` VARCHAR(64) NOT NULL,
                    `name` VARCHAR(256) NOT NULL,
                    `appliedutc` DATETIME(6) NOT NULL,
                    `checksum` VARCHAR(128) NOT NULL,
                    `success` TINYINT(1) NOT NULL DEFAULT 1,
                    PRIMARY KEY (`id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;",

                @"CREATE TABLE IF NOT EXISTS `accounts` (
                    `id` INT NOT NULL AUTO_INCREMENT,
                    `guid` VARCHAR(64) NOT NULL,
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
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;",

                @"CREATE TABLE IF NOT EXISTS `entries` (
                    `id` INT NOT NULL AUTO_INCREMENT,
                    `guid` VARCHAR(64) NOT NULL,
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
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;",

                @"CREATE TABLE IF NOT EXISTS `apikeys` (
                    `id` INT NOT NULL AUTO_INCREMENT,
                    `guid` VARCHAR(64) NOT NULL,
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
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;",

                @"CREATE TABLE IF NOT EXISTS `tenants` (
                    `id` VARCHAR(64) NOT NULL,
                    `parentid` VARCHAR(64) NULL,
                    `name` VARCHAR(256) NOT NULL,
                    `region` VARCHAR(64) NULL,
                    `active` TINYINT(1) NOT NULL DEFAULT 1,
                    `isprotected` TINYINT(1) NOT NULL DEFAULT 0,
                    `createdutc` DATETIME(6) NOT NULL,
                    `lastupdateutc` DATETIME(6) NOT NULL,
                    PRIMARY KEY (`id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;",

                @"CREATE TABLE IF NOT EXISTS `users` (
                    `id` VARCHAR(64) NOT NULL,
                    `tenantid` VARCHAR(64) NOT NULL,
                    `firstname` VARCHAR(128) NULL,
                    `lastname` VARCHAR(128) NULL,
                    `email` VARCHAR(256) NOT NULL,
                    `passwordsha256` VARCHAR(128) NULL,
                    `isadmin` TINYINT(1) NOT NULL DEFAULT 0,
                    `istenantadmin` TINYINT(1) NOT NULL DEFAULT 0,
                    `active` TINYINT(1) NOT NULL DEFAULT 1,
                    `isprotected` TINYINT(1) NOT NULL DEFAULT 0,
                    `createdutc` DATETIME(6) NOT NULL,
                    `lastupdateutc` DATETIME(6) NOT NULL,
                    PRIMARY KEY (`id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;",

                @"CREATE TABLE IF NOT EXISTS `authsessions` (
                    `id` VARCHAR(64) NOT NULL,
                    `tenantid` VARCHAR(64) NOT NULL,
                    `userid` VARCHAR(64) NOT NULL,
                    `token` VARCHAR(128) NOT NULL,
                    `active` TINYINT(1) NOT NULL DEFAULT 1,
                    `expiresutc` DATETIME(6) NOT NULL,
                    `revokedutc` DATETIME(6) NULL,
                    `createdutc` DATETIME(6) NOT NULL,
                    `lastupdateutc` DATETIME(6) NOT NULL,
                    PRIMARY KEY (`id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;",

                @"CREATE TABLE IF NOT EXISTS `accountusermaps` (
                    `id` VARCHAR(64) NOT NULL,
                    `tenantid` VARCHAR(64) NOT NULL,
                    `accountid` VARCHAR(64) NOT NULL,
                    `userid` VARCHAR(64) NOT NULL,
                    `createdutc` DATETIME(6) NOT NULL,
                    PRIMARY KEY (`tenantid`, `accountid`, `userid`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;",

                @"CREATE TABLE IF NOT EXISTS `auditrecords` (
                    `id` VARCHAR(64) NOT NULL,
                    `tenantid` VARCHAR(64) NULL,
                    `principalid` VARCHAR(64) NULL,
                    `principaltype` VARCHAR(64) NULL,
                    `eventtype` VARCHAR(64) NOT NULL,
                    `resourcetype` VARCHAR(64) NULL,
                    `operationtype` VARCHAR(64) NULL,
                    `resourceid` VARCHAR(64) NULL,
                    `result` VARCHAR(64) NOT NULL,
                    `reason` TEXT NULL,
                    `requestid` VARCHAR(64) NULL,
                    `createdutc` DATETIME(6) NOT NULL,
                    PRIMARY KEY (`id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;",

                @"CREATE TABLE IF NOT EXISTS `requesthistory` (
                    `id` VARCHAR(64) NOT NULL,
                    `tenantid` VARCHAR(64) NULL,
                    `principalid` VARCHAR(64) NULL,
                    `principaltype` VARCHAR(64) NULL,
                    `method` VARCHAR(16) NOT NULL,
                    `path` TEXT NOT NULL,
                    `url` TEXT NOT NULL,
                    `statuscode` INT NOT NULL,
                    `durationms` DOUBLE NOT NULL,
                    `sourceip` VARCHAR(128) NULL,
                    `requestheaders` MEDIUMTEXT NOT NULL,
                    `requestbody` MEDIUMTEXT NULL,
                    `requestbodybytes` BIGINT NOT NULL DEFAULT 0,
                    `requestbodytruncated` TINYINT(1) NOT NULL DEFAULT 0,
                    `responseheaders` MEDIUMTEXT NOT NULL,
                    `responsebody` MEDIUMTEXT NULL,
                    `responsebodybytes` BIGINT NOT NULL DEFAULT 0,
                    `responsebodytruncated` TINYINT(1) NOT NULL DEFAULT 0,
                    `createdutc` DATETIME(6) NOT NULL,
                    `completedutc` DATETIME(6) NULL,
                    PRIMARY KEY (`id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;",

                @"CREATE TABLE IF NOT EXISTS `userroles` (
                    `id` VARCHAR(64) NOT NULL,
                    `tenantid` VARCHAR(64) NOT NULL DEFAULT '',
                    `name` VARCHAR(128) NOT NULL,
                    `isbuiltin` TINYINT(1) NOT NULL DEFAULT 0,
                    `active` TINYINT(1) NOT NULL DEFAULT 1,
                    `isprotected` TINYINT(1) NOT NULL DEFAULT 0,
                    `createdutc` DATETIME(6) NOT NULL,
                    `lastupdateutc` DATETIME(6) NOT NULL,
                    PRIMARY KEY (`id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;",

                @"CREATE TABLE IF NOT EXISTS `permissions` (
                    `id` VARCHAR(64) NOT NULL,
                    `tenantid` VARCHAR(64) NOT NULL DEFAULT '',
                    `name` VARCHAR(128) NOT NULL,
                    `resourcetypes` TEXT NOT NULL,
                    `operationtypes` TEXT NOT NULL,
                    `permissiontype` VARCHAR(16) NOT NULL DEFAULT 'Permit',
                    `active` TINYINT(1) NOT NULL DEFAULT 1,
                    `isprotected` TINYINT(1) NOT NULL DEFAULT 0,
                    `createdutc` DATETIME(6) NOT NULL,
                    `lastupdateutc` DATETIME(6) NOT NULL,
                    PRIMARY KEY (`id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;",

                @"CREATE TABLE IF NOT EXISTS `rolepermissionmaps` (
                    `id` VARCHAR(64) NOT NULL,
                    `tenantid` VARCHAR(64) NOT NULL DEFAULT '',
                    `roleid` VARCHAR(64) NOT NULL,
                    `permissionid` VARCHAR(64) NOT NULL,
                    `active` TINYINT(1) NOT NULL DEFAULT 1,
                    `isprotected` TINYINT(1) NOT NULL DEFAULT 0,
                    `createdutc` DATETIME(6) NOT NULL,
                    PRIMARY KEY (`id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;",

                @"CREATE TABLE IF NOT EXISTS `userroleassignments` (
                    `id` VARCHAR(64) NOT NULL,
                    `tenantid` VARCHAR(64) NOT NULL,
                    `userid` VARCHAR(64) NOT NULL,
                    `roleid` VARCHAR(64) NULL,
                    `rolename` VARCHAR(128) NULL,
                    `resourcescope` VARCHAR(16) NOT NULL DEFAULT 'Tenant',
                    `resourceid` VARCHAR(64) NULL,
                    `inheritstochildren` TINYINT(1) NOT NULL DEFAULT 1,
                    `active` TINYINT(1) NOT NULL DEFAULT 1,
                    `isprotected` TINYINT(1) NOT NULL DEFAULT 0,
                    `createdutc` DATETIME(6) NOT NULL,
                    `lastupdateutc` DATETIME(6) NOT NULL,
                    PRIMARY KEY (`id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;",

                @"CREATE TABLE IF NOT EXISTS `credentialscopeassignments` (
                    `id` VARCHAR(64) NOT NULL,
                    `tenantid` VARCHAR(64) NOT NULL,
                    `credentialid` VARCHAR(64) NOT NULL,
                    `roleid` VARCHAR(64) NULL,
                    `rolename` VARCHAR(128) NULL,
                    `resourcescope` VARCHAR(16) NOT NULL DEFAULT 'Tenant',
                    `resourceid` VARCHAR(64) NULL,
                    `operationtypes` TEXT NOT NULL,
                    `resourcetypes` TEXT NOT NULL,
                    `active` TINYINT(1) NOT NULL DEFAULT 1,
                    `isprotected` TINYINT(1) NOT NULL DEFAULT 0,
                    `createdutc` DATETIME(6) NOT NULL,
                    `lastupdateutc` DATETIME(6) NOT NULL,
                    PRIMARY KEY (`id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;"
            };
        }

        /// <summary>
        /// Get the SQL statements to create all required indices.
        /// These use CREATE INDEX without IF NOT EXISTS for MySQL compatibility.
        /// The driver will catch and ignore duplicate index errors.
        /// </summary>
        /// <returns>Array of SQL statements.</returns>
        internal static string[] CreateIndices()
        {
            return new string[]
            {
                // Accounts indices
                "CREATE INDEX `idx_accounts_guid` ON `accounts` (`guid`);",
                "CREATE INDEX `idx_accounts_tenantid_guid` ON `accounts` (`tenantid`, `guid`);",
                "CREATE INDEX `idx_accounts_name` ON `accounts` (`name`);",
                "CREATE INDEX `idx_accounts_createdutc` ON `accounts` (`createdutc`);",

                // Entries indices
                "CREATE INDEX `idx_entries_guid` ON `entries` (`guid`);",
                "CREATE INDEX `idx_entries_tenantid_guid` ON `entries` (`tenantid`, `guid`);",
                "CREATE INDEX `idx_entries_accountguid` ON `entries` (`accountguid`);",
                "CREATE INDEX `idx_entries_tenantid_accountguid` ON `entries` (`tenantid`, `accountguid`);",
                "CREATE INDEX `idx_entries_type` ON `entries` (`type`);",
                "CREATE INDEX `idx_entries_iscommitted` ON `entries` (`iscommitted`);",
                "CREATE INDEX `idx_entries_createdutc` ON `entries` (`createdutc`);",
                "CREATE INDEX `idx_entries_accountguid_type` ON `entries` (`accountguid`, `type`);",
                "CREATE INDEX `idx_entries_accountguid_iscommitted` ON `entries` (`accountguid`, `iscommitted`);",

                // API keys indices
                "CREATE INDEX `idx_apikeys_guid` ON `apikeys` (`guid`);",
                "CREATE INDEX `idx_apikeys_apikey` ON `apikeys` (`apikey`);",
                "CREATE INDEX `idx_apikeys_active` ON `apikeys` (`active`);",
                "CREATE INDEX `idx_apikeys_createdutc` ON `apikeys` (`createdutc`);",
                "CREATE INDEX `idx_tenants_name` ON `tenants` (`name`);",
                "CREATE UNIQUE INDEX `idx_users_tenantid_email` ON `users` (`tenantid`, `email`);",
                "CREATE INDEX `idx_authsessions_token` ON `authsessions` (`token`);",
                "CREATE INDEX `idx_accountusermaps_userid` ON `accountusermaps` (`userid`);",
                "CREATE INDEX `idx_auditrecords_tenantid_createdutc` ON `auditrecords` (`tenantid`, `createdutc`);",
                "CREATE INDEX `idx_requesthistory_tenantid_createdutc` ON `requesthistory` (`tenantid`, `createdutc`);",
                "CREATE INDEX `idx_requesthistory_principalid_createdutc` ON `requesthistory` (`principalid`, `createdutc`);",
                "CREATE INDEX `idx_requesthistory_method_createdutc` ON `requesthistory` (`method`, `createdutc`);",
                "CREATE INDEX `idx_requesthistory_statuscode_createdutc` ON `requesthistory` (`statuscode`, `createdutc`);",
                "CREATE UNIQUE INDEX `idx_userroles_tenantid_name` ON `userroles` (`tenantid`, `name`);",
                "CREATE INDEX `idx_permissions_tenantid_name` ON `permissions` (`tenantid`, `name`);",
                "CREATE INDEX `idx_rolepermissionmaps_roleid` ON `rolepermissionmaps` (`roleid`);",
                "CREATE INDEX `idx_userroleassignments_tenantid_userid` ON `userroleassignments` (`tenantid`, `userid`);",
                "CREATE INDEX `idx_credentialscopeassignments_tenantid_credentialid` ON `credentialscopeassignments` (`tenantid`, `credentialid`);"
            };
        }
    }
}



