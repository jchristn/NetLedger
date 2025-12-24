namespace NetLedger.Sdk.Interfaces
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// API key management operations for the NetLedger API.
    /// Requires admin privileges for most operations.
    /// </summary>
    public interface IApiKeyMethods
    {
        /// <summary>
        /// Create a new API key.
        /// </summary>
        /// <param name="name">The display name for the API key.</param>
        /// <param name="isAdmin">Whether the API key should have admin privileges.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created API key info (includes the API key value).</returns>
        /// <exception cref="ArgumentNullException">Thrown when name is null or empty.</exception>
        /// <exception cref="NetLedgerConnectionException">Thrown when unable to connect to the server.</exception>
        /// <exception cref="NetLedgerApiException">Thrown when the server returns an error (401 if not authorized).</exception>
        Task<ApiKeyInfo> CreateAsync(string name, bool isAdmin = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Create a new API key.
        /// </summary>
        /// <param name="apiKey">The API key info to create.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created API key info (includes the API key value).</returns>
        /// <exception cref="ArgumentNullException">Thrown when apiKey is null.</exception>
        /// <exception cref="NetLedgerConnectionException">Thrown when unable to connect to the server.</exception>
        /// <exception cref="NetLedgerApiException">Thrown when the server returns an error (401 if not authorized).</exception>
        Task<ApiKeyInfo> CreateAsync(ApiKeyInfo apiKey, CancellationToken cancellationToken = default);

        /// <summary>
        /// Enumerate API keys with pagination.
        /// </summary>
        /// <param name="query">Query parameters for pagination.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Enumeration result containing API key info (API key values are not included).</returns>
        /// <exception cref="NetLedgerConnectionException">Thrown when unable to connect to the server.</exception>
        /// <exception cref="NetLedgerApiException">Thrown when the server returns an error (401 if not authorized).</exception>
        Task<EnumerationResult<ApiKeyInfo>> EnumerateAsync(ApiKeyEnumerationQuery? query = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Revoke (delete) an API key.
        /// </summary>
        /// <param name="apiKeyGuid">The GUID of the API key to revoke.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task representing the async operation.</returns>
        /// <exception cref="NetLedgerConnectionException">Thrown when unable to connect to the server.</exception>
        /// <exception cref="NetLedgerApiException">Thrown when the server returns an error (401 if not authorized, 404 if not found).</exception>
        Task RevokeAsync(Guid apiKeyGuid, CancellationToken cancellationToken = default);
    }
}
