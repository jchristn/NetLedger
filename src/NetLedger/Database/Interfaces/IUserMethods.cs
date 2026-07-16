namespace NetLedger.Database.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for user data access.
    /// </summary>
    public interface IUserMethods
    {
        /// <summary>
        /// Create a user.
        /// </summary>
        Task<User> CreateAsync(User user, CancellationToken token = default);

        /// <summary>
        /// Read a user by tenant and identifier.
        /// </summary>
        Task<User?> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Read a user by tenant and email.
        /// </summary>
        Task<User?> ReadByEmailAsync(string tenantId, string email, CancellationToken token = default);

        /// <summary>
        /// Enumerate users.
        /// </summary>
        Task<EnumerationResult<User>> EnumerateAsync(EnumerationQuery query, CancellationToken token = default);

        /// <summary>
        /// Update a user.
        /// </summary>
        Task<User> UpdateAsync(User user, CancellationToken token = default);

        /// <summary>
        /// Delete a user.
        /// </summary>
        Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default);
    }
}
