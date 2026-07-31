namespace Test.Automated
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NetLedger.Database;
    using Test.Shared;
    using Touchstone.Cli;
    using Touchstone.Core;

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
                settings = NetLedgerTestConfiguration.ParseArguments(RemoveRunnerArguments(args));
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
            Console.WriteLine(NetLedgerTestConfiguration.Describe(settings));

            string? suiteFilter = ReadRunnerOption(args, "suite");
            string? testFilter = ReadRunnerOption(args, "test");
            IReadOnlyList<TestSuiteDescriptor> suites = FilterSuites(NetLedgerSuites.All, suiteFilter, testFilter);
            if (!String.IsNullOrWhiteSpace(suiteFilter) || !String.IsNullOrWhiteSpace(testFilter))
            {
                Console.WriteLine("Test filter: suite=" + (suiteFilter ?? "*") + " test=" + (testFilter ?? "*"));
            }

            Console.WriteLine();
            return await ConsoleRunner.RunAsync(suites).ConfigureAwait(false);
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

        private static void ShowHelp()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotnet run --project src/Test.Automated/Test.Automated.csproj -- --dbtype sqlite");
            Console.WriteLine("  dotnet run --project src/Test.Automated/Test.Automated.csproj -- --dbtype mysql --dbhostname localhost --dbport 43306 --dbusername netledger --dbpassword netledger --dbname netledger");
            Console.WriteLine("  dotnet run --project src/Test.Automated/Test.Automated.csproj -- --dbtype postgres --dbhostname localhost --dbport 45432 --dbusername netledger --dbpassword netledger --dbname netledger");
            Console.WriteLine("  dotnet run --project src/Test.Automated/Test.Automated.csproj -- --dbtype sqlserver --dbhostname localhost --dbport 41433 --dbusername sa --dbpassword NetLedger!Passw0rd --dbname netledger");
            Console.WriteLine("Optional filters: --suite archive --test automatic_archive_account_override_exports_old_entries");
        }

        private static string[] RemoveRunnerArguments(string[] args)
        {
            List<string> databaseArgs = new List<string>();
            if (args == null)
            {
                return databaseArgs.ToArray();
            }

            for (int i = 0; i < args.Length; i++)
            {
                string argument = args[i] ?? String.Empty;
                string? inlineValue;
                string normalized = NormalizeArgument(argument, out inlineValue);
                if (normalized == "suite" || normalized == "test")
                {
                    if (inlineValue == null && i + 1 < args.Length)
                    {
                        i++;
                    }

                    continue;
                }

                databaseArgs.Add(argument);
            }

            return databaseArgs.ToArray();
        }

        private static string? ReadRunnerOption(string[] args, string name)
        {
            if (args == null)
            {
                return null;
            }

            for (int i = 0; i < args.Length; i++)
            {
                string? inlineValue;
                string normalized = NormalizeArgument(args[i] ?? String.Empty, out inlineValue);
                if (normalized == name)
                {
                    if (inlineValue != null)
                    {
                        return inlineValue;
                    }

                    if (i + 1 >= args.Length)
                    {
                        throw new ArgumentException("--" + name + " requires a value.");
                    }

                    return args[i + 1];
                }
            }

            return null;
        }

        private static IReadOnlyList<TestSuiteDescriptor> FilterSuites(
            IReadOnlyList<TestSuiteDescriptor> suites,
            string? suiteFilter,
            string? testFilter)
        {
            if (suites == null) throw new ArgumentNullException(nameof(suites));

            List<TestSuiteDescriptor> filtered = new List<TestSuiteDescriptor>();
            foreach (TestSuiteDescriptor suite in suites)
            {
                if (!MatchesFilter(suite.SuiteId, suiteFilter))
                {
                    continue;
                }

                List<TestCaseDescriptor> cases = suite.Cases
                    .Where(testCase => MatchesFilter(testCase.CaseId, testFilter))
                    .ToList();
                if (cases.Count == 0)
                {
                    continue;
                }

                filtered.Add(new TestSuiteDescriptor(
                    suite.SuiteId,
                    suite.DisplayName,
                    cases,
                    suite.BeforeSuiteAsync,
                    suite.AfterSuiteAsync));
            }

            return filtered;
        }

        private static bool MatchesFilter(string value, string? filter)
        {
            if (String.IsNullOrWhiteSpace(filter))
            {
                return true;
            }

            return value.Contains(filter, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeArgument(string argument, out string? inlineValue)
        {
            inlineValue = null;
            if (String.IsNullOrWhiteSpace(argument))
            {
                return String.Empty;
            }

            string normalized = argument.Trim();
            while (normalized.StartsWith("-", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(1);
            }

            int equalsIndex = normalized.IndexOf('=');
            if (equalsIndex >= 0)
            {
                inlineValue = normalized.Substring(equalsIndex + 1);
                normalized = normalized.Substring(0, equalsIndex);
            }

            return normalized.Replace("-", String.Empty, StringComparison.Ordinal).Replace("_", String.Empty, StringComparison.Ordinal).ToLowerInvariant();
        }
    }
}
