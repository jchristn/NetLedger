namespace NetLedger.Database.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for API key CRUD operations.
    /// </summary>
    public interface IApiKeyMethods
    {
        /// <summary>
        /// Create a new API key.
        /// </summary>
        /// <param name="apiKey">API key to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created API key.</returns>
        Task<ApiKey> CreateAsync(ApiKey apiKey, CancellationToken token = default);

        /// <summary>
        /// Read an API key by GUID.
        /// </summary>
        /// <param name="guid">API key GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>API key if found, null otherwise.</returns>
        Task<ApiKey> ReadByGuidAsync(Guid guid, CancellationToken token = default);

        /// <summary>
        /// Read an API key by the key value.
        /// </summary>
        /// <param name="key">API key value.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>API key if found, null otherwise.</returns>
        Task<ApiKey> ReadByKeyAsync(string key, CancellationToken token = default);

        /// <summary>
        /// Read all API keys.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of all API keys.</returns>
        Task<List<ApiKey>> ReadAllAsync(CancellationToken token = default);

        /// <summary>
        /// Enumerate API keys with pagination.
        /// </summary>
        /// <param name="query">Enumeration query parameters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result with API keys.</returns>
        Task<EnumerationResult<ApiKey>> EnumerateAsync(EnumerationQuery query, CancellationToken token = default);

        /// <summary>
        /// Update an API key.
        /// </summary>
        /// <param name="apiKey">API key to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated API key.</returns>
        Task<ApiKey> UpdateAsync(ApiKey apiKey, CancellationToken token = default);

        /// <summary>
        /// Delete an API key by GUID.
        /// </summary>
        /// <param name="guid">API key GUID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task DeleteByGuidAsync(Guid guid, CancellationToken token = default);

        /// <summary>
        /// Check if an active API key exists.
        /// </summary>
        /// <param name="key">API key value.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if active API key exists.</returns>
        Task<bool> ExistsActiveKeyAsync(string key, CancellationToken token = default);

        /// <summary>
        /// Authenticate using an API key.
        /// Returns the API key if valid and active, null otherwise.
        /// </summary>
        /// <param name="key">API key value.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>API key if valid and active, null otherwise.</returns>
        Task<ApiKey> AuthenticateAsync(string key, CancellationToken token = default);
    }
}
