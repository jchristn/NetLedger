namespace NetLedger.Sdk.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Service-level operations for the NetLedger API.
    /// </summary>
    public interface IServiceMethods
    {
        /// <summary>
        /// Check if the NetLedger server is available and responding.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the server is healthy, false otherwise.</returns>
        Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get information about the NetLedger service.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Service information including version and uptime.</returns>
        /// <exception cref="NetLedgerConnectionException">Thrown when unable to connect to the server.</exception>
        /// <exception cref="NetLedgerApiException">Thrown when the server returns an error.</exception>
        Task<ServiceInfo> GetInfoAsync(CancellationToken cancellationToken = default);
    }
}
