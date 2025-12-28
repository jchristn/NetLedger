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
                @"CREATE TABLE IF NOT EXISTS `accounts` (
                    `id` INT NOT NULL AUTO_INCREMENT,
                    `guid` VARCHAR(36) NOT NULL,
                    `name` VARCHAR(256) NOT NULL,
                    `notes` TEXT NULL,
                    `createdutc` DATETIME(6) NOT NULL,
                    PRIMARY KEY (`id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;",

                @"CREATE TABLE IF NOT EXISTS `entries` (
                    `id` INT NOT NULL AUTO_INCREMENT,
                    `guid` VARCHAR(36) NOT NULL,
                    `accountguid` VARCHAR(36) NOT NULL,
                    `type` VARCHAR(16) NOT NULL,
                    `amount` DECIMAL(18, 8) NOT NULL,
                    `description` TEXT NULL,
                    `replaces` VARCHAR(36) NULL,
                    `iscommitted` TINYINT(1) NOT NULL DEFAULT 0,
                    `committedbyguid` VARCHAR(36) NULL,
                    `committedutc` DATETIME(6) NULL,
                    `createdutc` DATETIME(6) NOT NULL,
                    PRIMARY KEY (`id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;",

                @"CREATE TABLE IF NOT EXISTS `apikeys` (
                    `id` INT NOT NULL AUTO_INCREMENT,
                    `guid` VARCHAR(36) NOT NULL,
                    `name` VARCHAR(256) NOT NULL,
                    `apikey` VARCHAR(256) NOT NULL,
                    `active` TINYINT(1) NOT NULL DEFAULT 1,
                    `isadmin` TINYINT(1) NOT NULL DEFAULT 0,
                    `createdutc` DATETIME(6) NOT NULL,
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
                "CREATE INDEX `idx_accounts_name` ON `accounts` (`name`);",
                "CREATE INDEX `idx_accounts_createdutc` ON `accounts` (`createdutc`);",

                // Entries indices
                "CREATE INDEX `idx_entries_guid` ON `entries` (`guid`);",
                "CREATE INDEX `idx_entries_accountguid` ON `entries` (`accountguid`);",
                "CREATE INDEX `idx_entries_type` ON `entries` (`type`);",
                "CREATE INDEX `idx_entries_iscommitted` ON `entries` (`iscommitted`);",
                "CREATE INDEX `idx_entries_createdutc` ON `entries` (`createdutc`);",
                "CREATE INDEX `idx_entries_accountguid_type` ON `entries` (`accountguid`, `type`);",
                "CREATE INDEX `idx_entries_accountguid_iscommitted` ON `entries` (`accountguid`, `iscommitted`);",

                // API keys indices
                "CREATE INDEX `idx_apikeys_guid` ON `apikeys` (`guid`);",
                "CREATE INDEX `idx_apikeys_apikey` ON `apikeys` (`apikey`);",
                "CREATE INDEX `idx_apikeys_active` ON `apikeys` (`active`);",
                "CREATE INDEX `idx_apikeys_createdutc` ON `apikeys` (`createdutc`);"
            };
        }
    }
}
