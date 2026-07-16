namespace NetLedger.Database.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for account-user map data access.
    /// </summary>
    public interface IAccountUserMapMethods
    {
        /// <summary>
        /// Create a mapping.
        /// </summary>
        Task<AccountUserMap> CreateAsync(AccountUserMap map, CancellationToken token = default);

        /// <summary>
        /// Check whether a user is mapped to an account.
        /// </summary>
        Task<bool> ExistsAsync(string tenantId, string accountId, string userId, CancellationToken token = default);

        /// <summary>
        /// Enumerate mappings.
        /// </summary>
        Task<EnumerationResult<AccountUserMap>> EnumerateAsync(EnumerationQuery query, CancellationToken token = default);

        /// <summary>
        /// Delete a mapping.
        /// </summary>
        Task<bool> DeleteAsync(string tenantId, string accountId, string userId, CancellationToken token = default);
    }
}
