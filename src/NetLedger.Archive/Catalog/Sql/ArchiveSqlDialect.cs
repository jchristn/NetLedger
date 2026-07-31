namespace NetLedger.Archive.Catalog.Sql
{
    using System;
    using System.Globalization;
    using NetLedger.Database;

    /// <summary>
    /// SQL dialect helpers for archive catalog providers.
    /// </summary>
    internal static class ArchiveSqlDialect
    {
        /// <summary>
        /// Quote a table or column name.
        /// </summary>
        /// <param name="databaseType">Database type.</param>
        /// <param name="name">Identifier name.</param>
        /// <returns>Quoted identifier.</returns>
        internal static string Identifier(DatabaseTypeEnum databaseType, string name)
        {
            return databaseType switch
            {
                DatabaseTypeEnum.Mysql => "`" + name + "`",
                DatabaseTypeEnum.SqlServer => "[" + name + "]",
                _ => name
            };
        }

        /// <summary>
        /// Format a UTC timestamp.
        /// </summary>
        /// <param name="value">Timestamp.</param>
        /// <returns>Formatted timestamp.</returns>
        internal static string Timestamp(DateTime value)
        {
            return value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Sanitize a string literal.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <returns>Sanitized value.</returns>
        internal static string Sanitize(string? value)
        {
            return String.IsNullOrEmpty(value) ? String.Empty : value.Replace("'", "''");
        }

        /// <summary>
        /// Build a quoted string literal or NULL.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <returns>SQL literal.</returns>
        internal static string Nullable(string? value)
        {
            return String.IsNullOrEmpty(value) ? "NULL" : "'" + Sanitize(value) + "'";
        }

        /// <summary>
        /// Build a boolean literal.
        /// </summary>
        /// <param name="databaseType">Database type.</param>
        /// <param name="value">Boolean value.</param>
        /// <returns>SQL literal.</returns>
        internal static string Bool(DatabaseTypeEnum databaseType, bool value)
        {
            return databaseType == DatabaseTypeEnum.Postgresql ? (value ? "TRUE" : "FALSE") : (value ? "1" : "0");
        }

        /// <summary>
        /// Build a limit clause.
        /// </summary>
        /// <param name="databaseType">Database type.</param>
        /// <param name="maxResults">Maximum results.</param>
        /// <param name="skip">Skip.</param>
        /// <returns>Limit clause.</returns>
        internal static string LimitOffset(DatabaseTypeEnum databaseType, int maxResults, int skip)
        {
            return databaseType == DatabaseTypeEnum.SqlServer
                ? " OFFSET " + skip.ToString(CultureInfo.InvariantCulture) + " ROWS FETCH NEXT " + maxResults.ToString(CultureInfo.InvariantCulture) + " ROWS ONLY"
                : " LIMIT " + maxResults.ToString(CultureInfo.InvariantCulture) + " OFFSET " + skip.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Build a select-one prefix.
        /// </summary>
        /// <param name="databaseType">Database type.</param>
        /// <param name="table">Table name.</param>
        /// <returns>Select prefix.</returns>
        internal static string SelectOne(DatabaseTypeEnum databaseType, string table)
        {
            return databaseType == DatabaseTypeEnum.SqlServer
                ? "SELECT TOP 1 * FROM " + Identifier(databaseType, table)
                : "SELECT * FROM " + Identifier(databaseType, table);
        }

        /// <summary>
        /// Build a select-one suffix.
        /// </summary>
        /// <param name="databaseType">Database type.</param>
        /// <returns>Select suffix.</returns>
        internal static string SelectOneSuffix(DatabaseTypeEnum databaseType)
        {
            return databaseType == DatabaseTypeEnum.SqlServer ? String.Empty : " LIMIT 1";
        }
    }
}
