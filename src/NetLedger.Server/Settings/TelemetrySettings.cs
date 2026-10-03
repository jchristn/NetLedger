namespace NetLedger.Server.Settings
{
    using System;

    /// <summary>
    /// Telemetry settings for the NetLedger server: OpenTelemetry export through Radiant (OTLP traces, metrics, and
    /// logs), an in-process Prometheus scrape endpoint, optional direct Loki log export, and the Watson webserver's
    /// built-in HTTP telemetry. Defaults bind to the 127.0.0.1 loopback address. In containers set
    /// PrometheusHostname to the container's DNS name (for example the compose service name) so the scrape
    /// endpoint binds that interface and is reachable from Prometheus.
    /// </summary>
    public class TelemetrySettings
    {
        #region Public-Members

        /// <summary>
        /// Master switch for exporting telemetry. Default true. When false no exporter runs and no port is bound;
        /// emission inside NetLedger stays a near-zero-cost no-op.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Service name reported as service.name on every signal. Default "netledger-server". Cannot be empty.
        /// </summary>
        public string ServiceName
        {
            get { return _ServiceName; }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(ServiceName));
                _ServiceName = value.Trim();
            }
        }

        /// <summary>
        /// Whether to push traces, metrics, and logs over OTLP. Default true.
        /// </summary>
        public bool OtlpEnabled { get; set; } = true;

        /// <summary>
        /// OTLP collector endpoint. Default "http://127.0.0.1:4317" (gRPC). Use port 4318 with the "httpprotobuf"
        /// protocol. Must be an absolute HTTP or HTTPS URI.
        /// </summary>
        public string OtlpEndpoint
        {
            get { return _OtlpEndpoint; }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(OtlpEndpoint));
                _OtlpEndpoint = value.Trim();
            }
        }

        /// <summary>
        /// OTLP protocol: "grpc" (default) or "httpprotobuf".
        /// </summary>
        public string OtlpProtocol { get; set; } = "grpc";

        /// <summary>
        /// Whether to serve an in-process Prometheus scrape endpoint covering every NetLedger, Watson, and .NET
        /// runtime metric. Default true.
        /// </summary>
        public bool PrometheusEnabled { get; set; } = true;

        /// <summary>
        /// Hostname the Prometheus endpoint binds. Default "127.0.0.1". The listener binds the address this name
        /// resolves to and only answers requests whose Host header matches it, so in a container use the DNS name
        /// Prometheus scrapes (for example "server"). Wildcards ("*", "+", "0.0.0.0") are rejected by the
        /// OpenTelemetry Prometheus listener.
        /// </summary>
        public string PrometheusHostname
        {
            get { return _PrometheusHostname; }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(PrometheusHostname));
                _PrometheusHostname = value.Trim();
            }
        }

        /// <summary>
        /// Port the Prometheus endpoint binds. Default 9464, range 1 through 65535.
        /// </summary>
        public int PrometheusPort
        {
            get { return _PrometheusPort; }
            set { _PrometheusPort = Math.Clamp(value, 1, 65535); }
        }

        /// <summary>
        /// Path of the Prometheus endpoint. Default "/metrics". Must begin with '/'.
        /// </summary>
        public string PrometheusPath
        {
            get { return _PrometheusPath; }
            set
            {
                if (String.IsNullOrWhiteSpace(value) || !value.StartsWith("/", StringComparison.Ordinal))
                {
                    throw new ArgumentException("PrometheusPath must begin with '/'.", nameof(PrometheusPath));
                }

                _PrometheusPath = value;
            }
        }

        /// <summary>
        /// Whether to push logs directly to a Loki 3.x OTLP endpoint. Default false. Logs cover background work
        /// such as automatic archival and carry trace and span identifiers for correlation.
        /// </summary>
        public bool LokiEnabled { get; set; } = false;

        /// <summary>
        /// Loki OTLP base endpoint. Default "http://127.0.0.1:3100/otlp".
        /// </summary>
        public string LokiEndpoint
        {
            get { return _LokiEndpoint; }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(LokiEndpoint));
                _LokiEndpoint = value.Trim();
            }
        }

        /// <summary>
        /// Head-based trace sampling ratio. Default 1.0 (sample everything), range 0.0 through 1.0.
        /// </summary>
        public double TraceSamplingRatio
        {
            get { return _TraceSamplingRatio; }
            set { _TraceSamplingRatio = Double.IsNaN(value) ? 1.0 : Math.Clamp(value, 0.0, 1.0); }
        }

        /// <summary>
        /// Whether to include .NET runtime metrics (GC, heap, thread pool). Default true.
        /// </summary>
        public bool IncludeRuntimeMetrics { get; set; } = true;

        /// <summary>
        /// Whether the Watson webserver emits its built-in HTTP metrics and per-request spans. Default true.
        /// </summary>
        public bool WatsonTelemetryEnabled { get; set; } = true;

        #endregion

        #region Private-Members

        private string _ServiceName = "netledger-server";
        private string _OtlpEndpoint = "http://127.0.0.1:4317";
        private string _PrometheusHostname = "127.0.0.1";
        private int _PrometheusPort = 9464;
        private string _PrometheusPath = "/metrics";
        private string _LokiEndpoint = "http://127.0.0.1:3100/otlp";
        private double _TraceSamplingRatio = 1.0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate with defaults.
        /// </summary>
        public TelemetrySettings()
        {
        }

        #endregion
    }
}
