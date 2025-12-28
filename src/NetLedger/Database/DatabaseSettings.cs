namespace NetLedger.Database
{
    using System;

    /// <summary>
    /// Database configuration settings.
    /// </summary>
    public class DatabaseSettings
    {
        #region Public-Members

        /// <summary>
        /// Database type.
        /// Default is SQLite.
        /// </summary>
        public DatabaseTypeEnum Type
        {
            get { return _Type; }
            set { _Type = value; }
        }

        /// <summary>
        /// Database filename (for SQLite).
        /// Default is "./netledger.db".
        /// </summary>
        public string Filename
        {
            get { return _Filename; }
            set { _Filename = value; }
        }

        /// <summary>
        /// Database hostname (for MySQL, PostgreSQL, SQL Server).
        /// Default is "localhost".
        /// </summary>
        public string Hostname
        {
            get { return _Hostname; }
            set { _Hostname = value; }
        }

        /// <summary>
        /// Database port.
        /// Default values: MySQL=3306, PostgreSQL=5432, SQL Server=1433.
        /// Value must be between 1 and 65535.
        /// </summary>
        public int Port
        {
            get { return _Port; }
            set
            {
                if (value < 0 || value > 65535)
                    throw new ArgumentOutOfRangeException(nameof(Port), "Port must be between 0 and 65535.");
                _Port = value;
            }
        }

        /// <summary>
        /// Database username.
        /// </summary>
        public string Username
        {
            get { return _Username; }
            set { _Username = value; }
        }

        /// <summary>
        /// Database password.
        /// </summary>
        public string Password
        {
            get { return _Password; }
            set { _Password = value; }
        }

        /// <summary>
        /// Database name.
        /// Default is "netledger".
        /// </summary>
        public string DatabaseName
        {
            get { return _DatabaseName; }
            set { _DatabaseName = value; }
        }

        /// <summary>
        /// Instance name (for SQL Server).
        /// </summary>
        public string Instance
        {
            get { return _Instance; }
            set { _Instance = value; }
        }

        /// <summary>
        /// Schema name (for PostgreSQL).
        /// </summary>
        public string Schema
        {
            get { return _Schema; }
            set { _Schema = value; }
        }

        /// <summary>
        /// Enable query logging.
        /// Default is false.
        /// </summary>
        public bool LogQueries
        {
            get { return _LogQueries; }
            set { _LogQueries = value; }
        }

        /// <summary>
        /// Require encryption for database connections.
        /// Default is false.
        /// </summary>
        public bool RequireEncryption
        {
            get { return _RequireEncryption; }
            set { _RequireEncryption = value; }
        }

        /// <summary>
        /// Connection timeout in seconds.
        /// Default is 30 seconds. Must be between 1 and 300.
        /// </summary>
        public int ConnectionTimeoutSeconds
        {
            get { return _ConnectionTimeoutSeconds; }
            set
            {
                if (value < 1 || value > 300)
                    throw new ArgumentOutOfRangeException(nameof(ConnectionTimeoutSeconds), "Connection timeout must be between 1 and 300 seconds.");
                _ConnectionTimeoutSeconds = value;
            }
        }

        /// <summary>
        /// Maximum connection pool size.
        /// Default is 100. Must be between 1 and 500.
        /// </summary>
        public int MaxPoolSize
        {
            get { return _MaxPoolSize; }
            set
            {
                if (value < 1 || value > 500)
                    throw new ArgumentOutOfRangeException(nameof(MaxPoolSize), "Max pool size must be between 1 and 500.");
                _MaxPoolSize = value;
            }
        }

        #endregion

        #region Private-Members

        private DatabaseTypeEnum _Type = DatabaseTypeEnum.Sqlite;
        private string _Filename = "./netledger.db";
        private string _Hostname = "localhost";
        private int _Port = 0;
        private string _Username = null;
        private string _Password = null;
        private string _DatabaseName = "netledger";
        private string _Instance = null;
        private string _Schema = null;
        private bool _LogQueries = false;
        private bool _RequireEncryption = false;
        private int _ConnectionTimeoutSeconds = 30;
        private int _MaxPoolSize = 100;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate database settings with defaults.
        /// Default configuration is SQLite with filename "./netledger.db".
        /// </summary>
        public DatabaseSettings()
        {
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Get the default port for the configured database type.
        /// </summary>
        /// <returns>Default port number.</returns>
        public int GetDefaultPort()
        {
            switch (Type)
            {
                case DatabaseTypeEnum.Mysql:
                    return 3306;
                case DatabaseTypeEnum.Postgresql:
                    return 5432;
                case DatabaseTypeEnum.SqlServer:
                    return 1433;
                case DatabaseTypeEnum.Sqlite:
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Get the effective port (uses default if port is 0).
        /// </summary>
        /// <returns>Effective port number.</returns>
        public int GetEffectivePort()
        {
            return _Port > 0 ? _Port : GetDefaultPort();
        }

        #endregion
    }
}
