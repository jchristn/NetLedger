namespace NetLedger.Server.API.Agnostic
{
    using System;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Server.Models;
    using NetLedger.Server.Settings;
    using SyslogLogging;

    /// <summary>
    /// Service handler for health checks and service information.
    /// </summary>
    internal class ServiceHandler
    {
        #region Private-Members

        private readonly string _Header = "[ServiceHandler] ";
        private readonly ServerSettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly DateTime _StartTimeUtc;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="logging">Logging module.</param>
        internal ServiceHandler(ServerSettings settings, LoggingModule logging)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _StartTimeUtc = DateTime.UtcNow;

            _Logging.Debug(_Header + "initialized");
        }

        #endregion

        #region Internal-Methods

        /// <summary>
        /// Health check - service exists.
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response context.</returns>
        internal Task<ResponseContext> ExistsAsync(RequestContext req, CancellationToken token = default)
        {
            return Task.FromResult(new ResponseContext(req));
        }

        /// <summary>
        /// Get service information.
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response context with service info.</returns>
        internal Task<ResponseContext> GetInfoAsync(RequestContext req, CancellationToken token = default)
        {
            Version? version = Assembly.GetExecutingAssembly().GetName().Version;
            TimeSpan uptime = DateTime.UtcNow - _StartTimeUtc;

            object data = new
            {
                Name = "NetLedger.Server",
                Version = version?.ToString() ?? "1.0.0",
                StartTimeUtc = _StartTimeUtc,
                UptimeSeconds = (long)uptime.TotalSeconds,
                UptimeFormatted = FormatUptime(uptime)
            };

            return Task.FromResult(new ResponseContext(req, data));
        }

        #endregion

        #region Private-Methods

        private static string FormatUptime(TimeSpan uptime)
        {
            if (uptime.TotalDays >= 1)
            {
                return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
            }
            else if (uptime.TotalHours >= 1)
            {
                return $"{(int)uptime.TotalHours}h {uptime.Minutes}m {uptime.Seconds}s";
            }
            else if (uptime.TotalMinutes >= 1)
            {
                return $"{(int)uptime.TotalMinutes}m {uptime.Seconds}s";
            }
            else
            {
                return $"{uptime.Seconds}s";
            }
        }

        #endregion
    }
}
