namespace Test.Shared
{
    using System;
    using NetLedger.Archive.Settings;
    using NetLedger.Database;

    /// <summary>
    /// Shared database configuration parser for NetLedger test runners.
    /// </summary>
    public static class NetLedgerTestConfiguration
    {
        /// <summary>
        /// Parse test database settings from command-line arguments.
        /// SQLite is the default database provider.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>Database settings.</returns>
        /// <exception cref="ArgumentException">Thrown when an argument is unknown or invalid.</exception>
        public static DatabaseSettings ParseArguments(string[] args)
        {
            DatabaseSettings settings = CreateDefaultSettings();

            if (args == null)
            {
                ApplyProviderDefaults(settings);
                return settings;
            }

            for (int i = 0; i < args.Length; i++)
            {
                string argument = args[i] ?? String.Empty;
                if (String.IsNullOrWhiteSpace(argument)) continue;

                string? inlineValue;
                string normalized = NormalizeArgument(argument, out inlineValue);

                if (normalized == "dbtype" || normalized == "type" || normalized == "t")
                {
                    settings.Type = ParseDatabaseType(ReadValue(args, ref i, argument, inlineValue));
                }
                else if (normalized == "dbfilename" || normalized == "filename" || normalized == "file" || normalized == "f")
                {
                    settings.Filename = ReadValue(args, ref i, argument, inlineValue);
                }
                else if (normalized == "dbhostname" || normalized == "dbhost" || normalized == "hostname" || normalized == "host" || normalized == "h")
                {
                    settings.Hostname = ReadValue(args, ref i, argument, inlineValue);
                }
                else if (normalized == "dbport" || normalized == "port" || normalized == "p")
                {
                    settings.Port = ReadIntValue(args, ref i, argument, inlineValue, "Port");
                }
                else if (normalized == "dbusername" || normalized == "dbuser" || normalized == "username" || normalized == "user" || normalized == "u")
                {
                    settings.Username = ReadValue(args, ref i, argument, inlineValue);
                }
                else if (normalized == "dbpassword" || normalized == "password")
                {
                    settings.Password = ReadValue(args, ref i, argument, inlineValue);
                }
                else if (normalized == "dbdatabase" || normalized == "dbname" || normalized == "databasename" || normalized == "database" || normalized == "d")
                {
                    settings.DatabaseName = ReadValue(args, ref i, argument, inlineValue);
                }
                else if (normalized == "dbschema" || normalized == "schema")
                {
                    settings.Schema = ReadValue(args, ref i, argument, inlineValue);
                }
                else if (normalized == "dbinstance" || normalized == "instance")
                {
                    settings.Instance = ReadValue(args, ref i, argument, inlineValue);
                }
                else if (normalized == "dbrequireencryption" || normalized == "requireencryption" || normalized == "dbencrypt" || normalized == "encrypt")
                {
                    settings.RequireEncryption = ReadBoolValue(args, ref i, argument, inlineValue, true);
                }
                else if (normalized == "dbconnectiontimeoutseconds" || normalized == "dbtimeout" || normalized == "connectiontimeout")
                {
                    settings.ConnectionTimeoutSeconds = ReadIntValue(args, ref i, argument, inlineValue, "Connection timeout");
                }
                else if (normalized == "dbmaxpoolsize" || normalized == "maxpoolsize")
                {
                    settings.MaxPoolSize = ReadIntValue(args, ref i, argument, inlineValue, "Maximum pool size");
                }
                else if (normalized == "dblogqueries" || normalized == "logqueries")
                {
                    settings.LogQueries = ReadBoolValue(args, ref i, argument, inlineValue, true);
                }
                else
                {
                    throw new ArgumentException("Unknown argument '" + argument + "'.");
                }
            }

            ApplyProviderDefaults(settings);
            return settings;
        }

        /// <summary>
        /// Read database settings from environment variables.
        /// SQLite is returned when no test database provider is configured.
        /// </summary>
        /// <returns>Database settings.</returns>
        public static DatabaseSettings FromEnvironment()
        {
            DatabaseSettings settings = CreateDefaultSettings();

            string? type = ReadEnvironment("NETLEDGER_TEST_DBTYPE", "NETLEDGER_TEST_TYPE", "NETLEDGER_TEST_PROVIDER");
            if (String.IsNullOrWhiteSpace(type))
            {
                ApplyProviderDefaults(settings);
                return settings;
            }

            settings.Type = ParseDatabaseType(type);
            string providerPrefix = "NETLEDGER_" + settings.Type.ToString().ToUpperInvariant() + "_";

            ApplyStringEnvironment(settings, value => settings.Filename = value, "NETLEDGER_TEST_DBFILENAME", "NETLEDGER_TEST_FILENAME", providerPrefix + "FILENAME");
            ApplyStringEnvironment(settings, value => settings.Hostname = value, "NETLEDGER_TEST_DBHOSTNAME", "NETLEDGER_TEST_DBHOST", "NETLEDGER_TEST_HOSTNAME", "NETLEDGER_TEST_HOST", providerPrefix + "HOST");
            ApplyIntEnvironment(settings, value => settings.Port = value, "NETLEDGER_TEST_DBPORT", "NETLEDGER_TEST_PORT", providerPrefix + "PORT");
            ApplyStringEnvironment(settings, value => settings.Username = value, "NETLEDGER_TEST_DBUSERNAME", "NETLEDGER_TEST_DBUSER", "NETLEDGER_TEST_USERNAME", "NETLEDGER_TEST_USER", providerPrefix + "USER");
            ApplyStringEnvironment(settings, value => settings.Password = value, "NETLEDGER_TEST_DBPASSWORD", "NETLEDGER_TEST_PASSWORD", providerPrefix + "PASSWORD");
            ApplyStringEnvironment(settings, value => settings.DatabaseName = value, "NETLEDGER_TEST_DBDATABASE", "NETLEDGER_TEST_DBNAME", "NETLEDGER_TEST_DATABASE", providerPrefix + "DATABASE");
            ApplyStringEnvironment(settings, value => settings.Schema = value, "NETLEDGER_TEST_DBSCHEMA", "NETLEDGER_TEST_SCHEMA", providerPrefix + "SCHEMA");
            ApplyStringEnvironment(settings, value => settings.Instance = value, "NETLEDGER_TEST_DBINSTANCE", "NETLEDGER_TEST_INSTANCE", providerPrefix + "INSTANCE");
            ApplyBoolEnvironment(settings, value => settings.RequireEncryption = value, "NETLEDGER_TEST_DBREQUIREENCRYPTION", "NETLEDGER_TEST_REQUIRE_ENCRYPTION");
            ApplyIntEnvironment(settings, value => settings.ConnectionTimeoutSeconds = value, "NETLEDGER_TEST_DBCONNECTIONTIMEOUTSECONDS", "NETLEDGER_TEST_DBTIMEOUT");
            ApplyIntEnvironment(settings, value => settings.MaxPoolSize = value, "NETLEDGER_TEST_DBMAXPOOLSIZE", "NETLEDGER_TEST_MAXPOOLSIZE");
            ApplyBoolEnvironment(settings, value => settings.LogQueries = value, "NETLEDGER_TEST_DBLOGQUERIES", "NETLEDGER_TEST_LOG_QUERIES");

            ApplyProviderDefaults(settings);
            return settings;
        }

        /// <summary>
        /// Parse a database type name.
        /// </summary>
        /// <param name="value">Database type name.</param>
        /// <returns>Database type.</returns>
        /// <exception cref="ArgumentException">Thrown when the database type is unsupported.</exception>
        public static DatabaseTypeEnum ParseDatabaseType(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return DatabaseTypeEnum.Sqlite;

            string normalized = value.Trim().ToLowerInvariant();
            if (normalized == "sqlite" || normalized == "sqlitedb") return DatabaseTypeEnum.Sqlite;
            if (normalized == "mysql") return DatabaseTypeEnum.Mysql;
            if (normalized == "postgres" || normalized == "postgresql") return DatabaseTypeEnum.Postgresql;
            if (normalized == "sqlserver" || normalized == "mssql") return DatabaseTypeEnum.SqlServer;

            throw new ArgumentException("Unsupported database type '" + value + "'.");
        }

        /// <summary>
        /// Clone database settings.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <returns>Cloned database settings.</returns>
        /// <exception cref="ArgumentNullException">Thrown when settings is null.</exception>
        public static DatabaseSettings CloneDatabaseSettings(DatabaseSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            return new DatabaseSettings
            {
                Type = settings.Type,
                Filename = settings.Filename,
                Hostname = settings.Hostname,
                Port = settings.Port,
                Username = settings.Username,
                Password = settings.Password,
                DatabaseName = settings.DatabaseName,
                Instance = settings.Instance,
                Schema = settings.Schema,
                LogQueries = settings.LogQueries,
                RequireEncryption = settings.RequireEncryption,
                ConnectionTimeoutSeconds = settings.ConnectionTimeoutSeconds,
                MaxPoolSize = settings.MaxPoolSize
            };
        }

        /// <summary>
        /// Convert database settings to archive catalog settings.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <returns>Archive catalog settings.</returns>
        /// <exception cref="ArgumentNullException">Thrown when settings is null.</exception>
        public static ArchiveCatalogSettings ToArchiveCatalogSettings(DatabaseSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            return new ArchiveCatalogSettings
            {
                Type = settings.Type,
                Filename = settings.Filename,
                Hostname = settings.Hostname,
                Port = settings.Port,
                Username = settings.Username,
                Password = settings.Password,
                DatabaseName = settings.DatabaseName,
                Instance = settings.Instance,
                Schema = settings.Schema,
                LogQueries = settings.LogQueries,
                RequireEncryption = settings.RequireEncryption,
                ConnectionTimeoutSeconds = settings.ConnectionTimeoutSeconds,
                MaxPoolSize = settings.MaxPoolSize
            };
        }

        /// <summary>
        /// Create provider-specific settings from environment variables.
        /// </summary>
        /// <param name="type">Database type.</param>
        /// <returns>Database settings.</returns>
        public static DatabaseSettings CreateProviderSettings(DatabaseTypeEnum type)
        {
            DatabaseSettings settings = CreateDefaultSettings();
            settings.Type = type;

            if (type == DatabaseTypeEnum.Sqlite)
            {
                ApplyProviderDefaults(settings);
                return settings;
            }

            string providerPrefix = "NETLEDGER_" + type.ToString().ToUpperInvariant() + "_";
            ApplyStringEnvironment(settings, value => settings.Hostname = value, providerPrefix + "HOST");
            ApplyIntEnvironment(settings, value => settings.Port = value, providerPrefix + "PORT");
            ApplyStringEnvironment(settings, value => settings.Username = value, providerPrefix + "USER");
            ApplyStringEnvironment(settings, value => settings.Password = value, providerPrefix + "PASSWORD");
            ApplyStringEnvironment(settings, value => settings.DatabaseName = value, providerPrefix + "DATABASE");
            ApplyStringEnvironment(settings, value => settings.Schema = value, providerPrefix + "SCHEMA");
            ApplyStringEnvironment(settings, value => settings.Instance = value, providerPrefix + "INSTANCE");
            ApplyProviderDefaults(settings);
            return settings;
        }

        /// <summary>
        /// Apply provider defaults to partially populated settings.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when settings is null.</exception>
        public static void ApplyProviderDefaults(DatabaseSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            if (settings.Type == DatabaseTypeEnum.Sqlite)
            {
                settings.RequireEncryption = false;
                if (String.IsNullOrWhiteSpace(settings.Filename) || String.Equals(settings.Filename, "./netledger.db", StringComparison.Ordinal))
                {
                    settings.Filename = String.Empty;
                }

                return;
            }

            if (String.IsNullOrWhiteSpace(settings.Hostname)) settings.Hostname = "localhost";
            if (String.IsNullOrWhiteSpace(settings.DatabaseName)) settings.DatabaseName = "netledger";
            if (settings.ConnectionTimeoutSeconds <= 0) settings.ConnectionTimeoutSeconds = 60;

            if (settings.Type == DatabaseTypeEnum.SqlServer)
            {
                if (String.IsNullOrWhiteSpace(settings.Username)) settings.Username = "sa";
                if (String.IsNullOrWhiteSpace(settings.Password)) settings.Password = "NetLedger!Passw0rd";
            }
            else
            {
                if (String.IsNullOrWhiteSpace(settings.Username)) settings.Username = "netledger";
                if (String.IsNullOrWhiteSpace(settings.Password)) settings.Password = "netledger";
            }
        }

        /// <summary>
        /// Describe database settings for test-runner output.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <returns>Description.</returns>
        /// <exception cref="ArgumentNullException">Thrown when settings is null.</exception>
        public static string Describe(DatabaseSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (settings.Type == DatabaseTypeEnum.Sqlite)
            {
                if (!String.IsNullOrWhiteSpace(settings.Filename))
                {
                    return "SQLite fixture database: " + settings.Filename;
                }

                return "SQLite fixtures use isolated temporary database files.";
            }

            return "Host: " + settings.Hostname + ":" + settings.GetEffectivePort() +
                Environment.NewLine + "Database: " + settings.DatabaseName +
                Environment.NewLine + "User: " + settings.Username;
        }

        private static DatabaseSettings CreateDefaultSettings()
        {
            return new DatabaseSettings
            {
                Type = DatabaseTypeEnum.Sqlite,
                Filename = String.Empty,
                RequireEncryption = false,
                ConnectionTimeoutSeconds = 60
            };
        }

        private static string NormalizeArgument(string argument, out string? inlineValue)
        {
            inlineValue = null;
            string working = argument.Trim();
            int equalsIndex = working.IndexOf('=', StringComparison.Ordinal);
            if (equalsIndex >= 0)
            {
                inlineValue = working.Substring(equalsIndex + 1);
                working = working.Substring(0, equalsIndex);
            }

            working = working.TrimStart('-', '/').Trim().ToLowerInvariant();
            return working.Replace("-", String.Empty).Replace("_", String.Empty);
        }

        private static string ReadValue(string[] args, ref int index, string argument, string? inlineValue)
        {
            if (inlineValue != null) return inlineValue;

            if (index + 1 >= args.Length)
            {
                throw new ArgumentException("Missing value for " + argument + ".");
            }

            string next = args[index + 1] ?? String.Empty;
            if (next.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException("Missing value for " + argument + ".");
            }

            index++;
            return next;
        }

        private static int ReadIntValue(string[] args, ref int index, string argument, string? inlineValue, string displayName)
        {
            string value = ReadValue(args, ref index, argument, inlineValue);
            if (!Int32.TryParse(value, out int parsed))
            {
                throw new ArgumentException(displayName + " must be an integer.");
            }

            return parsed;
        }

        private static bool ReadBoolValue(string[] args, ref int index, string argument, string? inlineValue, bool defaultValue)
        {
            if (inlineValue != null) return ParseBoolean(inlineValue, argument);

            if (index + 1 >= args.Length) return defaultValue;

            string next = args[index + 1] ?? String.Empty;
            if (next.StartsWith("--", StringComparison.Ordinal))
            {
                return defaultValue;
            }

            if (String.Equals(next, "true", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(next, "false", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(next, "1", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(next, "0", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(next, "yes", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(next, "no", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                return ParseBoolean(next, argument);
            }

            return defaultValue;
        }

        private static bool ParseBoolean(string value, string argument)
        {
            if (String.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(value, "yes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (String.Equals(value, "false", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(value, "0", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(value, "no", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            throw new ArgumentException("Invalid Boolean value '" + value + "' for " + argument + ".");
        }

        private static string? ReadEnvironment(params string[] names)
        {
            foreach (string name in names)
            {
                string? value = Environment.GetEnvironmentVariable(name);
                if (!String.IsNullOrWhiteSpace(value)) return value;
            }

            return null;
        }

        private static void ApplyStringEnvironment(DatabaseSettings settings, Action<string> setter, params string[] names)
        {
            string? value = ReadEnvironment(names);
            if (!String.IsNullOrWhiteSpace(value)) setter(value);
        }

        private static void ApplyIntEnvironment(DatabaseSettings settings, Action<int> setter, params string[] names)
        {
            string? value = ReadEnvironment(names);
            if (String.IsNullOrWhiteSpace(value)) return;
            if (!Int32.TryParse(value, out int parsed))
            {
                throw new ArgumentException("Environment variable " + names[0] + " must be an integer.");
            }

            setter(parsed);
        }

        private static void ApplyBoolEnvironment(DatabaseSettings settings, Action<bool> setter, params string[] names)
        {
            string? value = ReadEnvironment(names);
            if (String.IsNullOrWhiteSpace(value)) return;
            setter(ParseBoolean(value, names[0]));
        }
    }
}
