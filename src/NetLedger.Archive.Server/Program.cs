namespace NetLedger.Archive.Server
{
    using System.Threading.Tasks;

    /// <summary>
    /// Program entry point.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Main entry point.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <returns>Exit code.</returns>
        public static Task<int> Main(string[] args)
        {
            return NetLedgerArchiveServer.RunAsync(args);
        }
    }
}
