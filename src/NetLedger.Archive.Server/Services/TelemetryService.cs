namespace NetLedger.Archive.Server.Services
{
    using System;
    using Microsoft.Extensions.Logging;
    using NetLedger.Archive.Server.Settings;
    using NetLedger.Telemetry;
    using Radiant;
    using SyslogLogging;

    /// <summary>
    /// Owns the single Radiant telemetry host for the NetLedger Archive Server process. The host subscribes to the
    /// NetLedger and Watson meters and activity sources and exports them over OTLP, an in-process Prometheus
    /// endpoint, and optionally Loki. Initialization is best-effort: a failure logs a warning and the server runs
    /// without exported telemetry. Dispose on shutdown to flush pending telemetry.
    /// </summary>
    internal sealed class TelemetryService : IDisposable
    {
        private readonly string _Header = "[TelemetryService] ";
        private readonly LoggingModule _Logging;
        private readonly RadiantHost? _Host = null;
        private readonly ILogger? _Logger = null;
        private bool _Disposed = false;

        internal TelemetryService(TelemetrySettings settings, LoggingModule logging)
        {
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            if (settings == null || !settings.Enabled)
            {
                _Logging.Info(_Header + "telemetry export disabled");
                return;
            }

            try
            {
                RadiantSettings radiant = new RadiantSettings(settings.ServiceName);
                radiant.Sources.AddMeter(TelemetryNames.MeterName);
                radiant.Sources.AddActivitySource(TelemetryNames.ActivitySourceName);
                radiant.Sources.AddMeter(TelemetryNames.WatsonSourceName);
                radiant.Sources.AddActivitySource(TelemetryNames.WatsonSourceName);

                radiant.Otlp.Enable = settings.OtlpEnabled;
                radiant.Otlp.Endpoint = settings.OtlpEndpoint;
                radiant.Otlp.Protocol = String.Equals(settings.OtlpProtocol, "httpprotobuf", StringComparison.OrdinalIgnoreCase)
                    ? OtlpProtocolEnum.HttpProtobuf
                    : OtlpProtocolEnum.Grpc;

                radiant.Prometheus.Enable = settings.PrometheusEnabled;
                radiant.Prometheus.Hostname = settings.PrometheusHostname;
                radiant.Prometheus.Port = settings.PrometheusPort;
                radiant.Prometheus.Path = settings.PrometheusPath;

                radiant.Loki.Enable = settings.LokiEnabled;
                radiant.Loki.Endpoint = settings.LokiEndpoint;
                radiant.Logs.Enable = settings.OtlpEnabled || settings.LokiEnabled;
                radiant.Logs.MinimumSeverity = 2;

                radiant.Traces.SamplingRatio = settings.TraceSamplingRatio;
                radiant.Metrics.IncludeRuntime = settings.IncludeRuntimeMetrics;
                radiant.DiagnosticCallback = message => _Logging.Debug(_Header + "radiant: " + message);

                _Host = RadiantHost.Start(radiant);
                _Logger = _Host.CreateLogger("NetLedger.Archive.Server");

                _Logging.Info(_Header + "telemetry enabled, service " + settings.ServiceName +
                    (settings.OtlpEnabled ? ", OTLP " + radiant.Otlp.Protocol + " to " + settings.OtlpEndpoint : ", OTLP off") +
                    (settings.PrometheusEnabled ? ", Prometheus " + radiant.Prometheus.ToScrapeUrl() : ", Prometheus off") +
                    (settings.LokiEnabled ? ", Loki " + settings.LokiEndpoint : ", Loki off"));
            }
            catch (Exception e)
            {
                _Host = null;
                _Logger = null;
                _Logging.Warn(_Header + "telemetry disabled because initialization failed: " + DescribeException(e));
            }
        }

        /// <summary>
        /// Logger that exports to OTLP and Loki with trace correlation, or null when telemetry is unavailable.
        /// </summary>
        internal ILogger? Logger
        {
            get { return _Logger; }
        }

        /// <summary>
        /// Whether the Radiant host started.
        /// </summary>
        internal bool IsEnabled
        {
            get { return _Host != null; }
        }

        private static string DescribeException(Exception e)
        {
            string description = e.Message;
            Exception? inner = e.InnerException;
            while (inner != null)
            {
                description += " -> " + inner.GetType().Name + ": " + inner.Message;
                inner = inner.InnerException;
            }

            return description;
        }

        /// <summary>
        /// Flush and dispose the telemetry host. Never throws.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            try
            {
                _Host?.ForceFlush(5000);
            }
            catch (Exception)
            {
            }

            try
            {
                _Host?.Dispose();
            }
            catch (Exception)
            {
            }
        }
    }
}
