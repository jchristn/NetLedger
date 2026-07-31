namespace NetLedger.Archive.Catalog
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Archived ledger entry methods.
    /// </summary>
    public interface IArchivedEntryMethods
    {
        /// <summary>
        /// Enumerate archived entries for an account.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="query">Enumeration query.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result.</returns>
        Task<EnumerationResult<Entry>> EnumerateAsync(string tenantId, string accountId, EnumerationQuery query, CancellationToken token = default);
    }
}
