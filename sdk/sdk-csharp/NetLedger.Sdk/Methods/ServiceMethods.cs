namespace NetLedger.Sdk.Methods
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Sdk.Interfaces;

    /// <summary>
    /// Implementation of service-level operations for the NetLedger API.
    /// </summary>
    internal class ServiceMethods : IServiceMethods
    {
        #region Private-Members

        private readonly NetLedgerClient _Client;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate service methods.
        /// </summary>
        /// <param name="client">The NetLedger client.</param>
        internal ServiceMethods(NetLedgerClient client)
        {
            _Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _Client.HeadAsync("/", cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<ServiceInfo> GetInfoAsync(CancellationToken cancellationToken = default)
        {
            ApiResponse<ServiceInfo> response = await _Client.SendAsync<ServiceInfo>(
                HttpMethod.Get,
                "/",
                null,
                cancellationToken).ConfigureAwait(false);

            return response.Data ?? throw new NetLedgerApiException(response.StatusCode, "No data returned from server.");
        }

        #endregion
    }
}
