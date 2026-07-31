namespace NetLedger.Archive
{
    using NetLedger;

    /// <summary>
    /// Archive identifier generation helper.
    /// </summary>
    public static class ArchiveId
    {
        /// <summary>
        /// Generate a K-sortable archive identifier.
        /// </summary>
        /// <param name="prefix">Identifier prefix.</param>
        /// <returns>Generated identifier.</returns>
        public static string Generate(string prefix)
        {
            return NetLedgerId.Generate(prefix);
        }
    }
}
