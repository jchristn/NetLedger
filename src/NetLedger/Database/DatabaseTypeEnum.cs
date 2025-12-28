namespace NetLedger.Database
{
    using System.Runtime.Serialization;

    /// <summary>
    /// Supported database types.
    /// </summary>
    public enum DatabaseTypeEnum
    {
        /// <summary>
        /// SQLite database.
        /// </summary>
        [EnumMember(Value = "Sqlite")]
        Sqlite,

        /// <summary>
        /// MySQL database.
        /// </summary>
        [EnumMember(Value = "Mysql")]
        Mysql,

        /// <summary>
        /// PostgreSQL database.
        /// </summary>
        [EnumMember(Value = "Postgresql")]
        Postgresql,

        /// <summary>
        /// Microsoft SQL Server database.
        /// </summary>
        [EnumMember(Value = "SqlServer")]
        SqlServer
    }
}
