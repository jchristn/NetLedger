namespace NetLedger.Database.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for account archival settings persistence.
    /// </summary>
    public interface IAccountArchivalSettingsMethods
    {
        /// <summary>
        /// Create or update account archival settings.
        /// </summary>
        /// <param name="settings">Account archival settings.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Saved settings.</returns>
        Task<AccountArchivalSettings> UpsertAsync(AccountArchivalSettings settings, CancellationToken token = default);

        /// <summary>
        /// Read account archival settings by tenant and account.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Settings when found.</returns>
        Task<AccountArchivalSettings?> ReadByAccountAsync(string tenantId, string accountId, CancellationToken token = default);

        /// <summary>
        /// Enumerate account archival settings.
        /// </summary>
        /// <param name="query">Enumeration query.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result.</returns>
        Task<EnumerationResult<AccountArchivalSettings>> EnumerateAsync(EnumerationQuery query, CancellationToken token = default);

        /// <summary>
        /// Delete account archival settings by tenant and account.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if a row was deleted.</returns>
        Task<bool> DeleteByAccountAsync(string tenantId, string accountId, CancellationToken token = default);
    }
}
