namespace Test.Automated
{
    using System.Threading.Tasks;
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
            return await ConsoleRunner.RunAsync(NetLedgerSuites.All).ConfigureAwait(false);
        }
    }
}
