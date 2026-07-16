namespace NetLedger.Database
{
    using System;
    using NetLedger.Database.Mysql;
    using NetLedger.Database.Postgresql;
    using NetLedger.Database.Sqlite;
    using NetLedger.Database.SqlServer;

    /// <summary>
    /// Factory for database drivers.
    /// </summary>
    public static class DatabaseDriverFactory
    {
        /// <summary>
        /// Create a database driver from settings.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <returns>Database driver.</returns>
        public static DatabaseDriverBase Create(DatabaseSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            switch (settings.Type)
            {
                case DatabaseTypeEnum.Sqlite:
                    return new SqliteDatabaseDriver(settings);
                case DatabaseTypeEnum.Mysql:
                    return new MysqlDatabaseDriver(settings);
                case DatabaseTypeEnum.Postgresql:
                    return new PostgresqlDatabaseDriver(settings);
                case DatabaseTypeEnum.SqlServer:
                    return new SqlServerDatabaseDriver(settings);
                default:
                    throw new ArgumentException("Unsupported database type: " + settings.Type);
            }
        }
    }
}
