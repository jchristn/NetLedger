namespace NetLedger.Database.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for tenant data access.
    /// </summary>
    public interface ITenantMethods
    {
        /// <summary>
        /// Create a tenant.
        /// </summary>
        Task<Tenant> CreateAsync(Tenant tenant, CancellationToken token = default);

        /// <summary>
        /// Read a tenant by identifier.
        /// </summary>
        Task<Tenant?> ReadAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate tenants.
        /// </summary>
        Task<EnumerationResult<Tenant>> EnumerateAsync(EnumerationQuery query, CancellationToken token = default);

        /// <summary>
        /// Update a tenant.
        /// </summary>
        Task<Tenant> UpdateAsync(Tenant tenant, CancellationToken token = default);

        /// <summary>
        /// Delete a tenant.
        /// </summary>
        Task<bool> DeleteAsync(string id, CancellationToken token = default);
    }
}
