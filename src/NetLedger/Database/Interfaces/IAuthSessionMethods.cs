namespace NetLedger.Database.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for authentication session data access.
    /// </summary>
    public interface IAuthSessionMethods
    {
        /// <summary>
        /// Create a session.
        /// </summary>
        Task<AuthSession> CreateAsync(AuthSession session, CancellationToken token = default);

        /// <summary>
        /// Read a session by token.
        /// </summary>
        Task<AuthSession?> ReadByTokenAsync(string tokenValue, CancellationToken token = default);

        /// <summary>
        /// Revoke a session.
        /// </summary>
        Task<bool> RevokeAsync(string tenantId, string id, string reason, CancellationToken token = default);

        /// <summary>
        /// Enumerate sessions.
        /// </summary>
        Task<EnumerationResult<AuthSession>> EnumerateAsync(EnumerationQuery query, CancellationToken token = default);
    }
}
