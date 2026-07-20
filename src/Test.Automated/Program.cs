namespace Test.Automated
{
    using System;
    using System.Threading.Tasks;
    using NetLedger.Database;
    using Test.Shared;
    using Touchstone.Cli;

    /// <summary>
    /// Touchstone console runner.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Run all shared Touchstone suites.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>Process exit code.</returns>
        public static async Task<int> Main(string[] args)
        {
            if (IsHelpRequested(args))
            {
                ShowHelp();
                return 0;
            }

            DatabaseSettings settings;
            try
            {
                settings = ParseArguments(args);
            }
            catch (ArgumentException e)
            {
                Console.Error.WriteLine(e.Message);
                Console.WriteLine();
                ShowHelp();
                return 1;
            }

            NetLedgerSuites.Configure(settings);
            Console.WriteLine("Database provider: " + settings.Type);
            if (settings.Type == DatabaseTypeEnum.Sqlite)
            {
                Console.WriteLine("SQLite fixtures use isolated temporary database files.");
            }
            else
            {
                Console.WriteLine("Host: " + settings.Hostname + ":" + settings.GetEffectivePort());
                Console.WriteLine("Database: " + settings.DatabaseName);
                Console.WriteLine("User: " + settings.Username);
            }

            Console.WriteLine();
            return await ConsoleRunner.RunAsync(NetLedgerSuites.All).ConfigureAwait(false);
        }

        private static DatabaseSettings ParseArguments(string[] args)
        {
            DatabaseSettings settings = new DatabaseSettings
            {
                Type = DatabaseTypeEnum.Sqlite,
                RequireEncryption = false,
                ConnectionTimeoutSeconds = 60
            };

            for (int i = 0; i < args.Length; i++)
            {
                string argument = args[i];
                string normalized = argument.Trim().ToLowerInvariant();

                if (normalized == "--type" || normalized == "-t")
                {
                    settings.Type = NetLedgerSuites.ParseDatabaseType(ReadValue(args, ref i, argument));
                }
                else if (normalized == "--host" || normalized == "-h")
                {
                    settings.Hostname = ReadValue(args, ref i, argument);
                }
                else if (normalized == "--port" || normalized == "-p")
                {
                    string portValue = ReadValue(args, ref i, argument);
                    if (!Int32.TryParse(portValue, out int port))
                    {
                        throw new ArgumentException("Port must be an integer.");
                    }

                    settings.Port = port;
                }
                else if (normalized == "--user" || normalized == "-u")
                {
                    settings.Username = ReadValue(args, ref i, argument);
                }
                else if (normalized == "--password")
                {
                    settings.Password = ReadValue(args, ref i, argument);
                }
                else if (normalized == "--database" || normalized == "-d")
                {
                    settings.DatabaseName = ReadValue(args, ref i, argument);
                }
                else if (normalized == "--schema")
                {
                    settings.Schema = ReadValue(args, ref i, argument);
                }
                else if (normalized == "--log-queries")
                {
                    settings.LogQueries = true;
                }
                else
                {
                    throw new ArgumentException("Unknown argument '" + argument + "'.");
                }
            }

            ApplyProviderDefaults(settings);
            return settings;
        }

        private static bool IsHelpRequested(string[] args)
        {
            foreach (string argument in args)
            {
                string normalized = argument.Trim().ToLowerInvariant();
                if (normalized == "--help" || normalized == "-?")
                {
                    return true;
                }
            }

            return false;
        }

        private static string ReadValue(string[] args, ref int index, string argument)
        {
            if (index + 1 >= args.Length)
            {
                throw new ArgumentException("Missing value for " + argument + ".");
            }

            index++;
            return args[index];
        }

        private static void ApplyProviderDefaults(DatabaseSettings settings)
        {
            if (settings.Type == DatabaseTypeEnum.Sqlite)
            {
                return;
            }

            if (String.IsNullOrEmpty(settings.Hostname))
            {
                settings.Hostname = "localhost";
            }

            if (String.IsNullOrEmpty(settings.DatabaseName))
            {
                settings.DatabaseName = "netledger";
            }

            if (settings.Type == DatabaseTypeEnum.SqlServer)
            {
                if (String.IsNullOrEmpty(settings.Username)) settings.Username = "sa";
                if (String.IsNullOrEmpty(settings.Password)) settings.Password = "NetLedger!Passw0rd";
            }
            else
            {
                if (String.IsNullOrEmpty(settings.Username)) settings.Username = "netledger";
                if (String.IsNullOrEmpty(settings.Password)) settings.Password = "netledger";
            }
        }

        private static void ShowHelp()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotnet run --project src/Test.Automated/Test.Automated.csproj -- --type sqlite");
            Console.WriteLine("  dotnet run --project src/Test.Automated/Test.Automated.csproj -- --type mysql --host localhost --port 3307 --user netledger --password netledger --database netledger");
            Console.WriteLine("  dotnet run --project src/Test.Automated/Test.Automated.csproj -- --type postgresql --host localhost --port 5433 --user netledger --password netledger --database netledger");
            Console.WriteLine("  dotnet run --project src/Test.Automated/Test.Automated.csproj -- --type sqlserver --host localhost --port 14330 --user sa --password NetLedger!Passw0rd --database netledger");
        }
    }
}
