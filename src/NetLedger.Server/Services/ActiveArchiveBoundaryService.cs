namespace NetLedger.Server.Services
{
    using System;
    using NetLedger.Server.Models;
    using NetLedger.Server.Settings;

    /// <summary>
    /// Applies active data retention boundaries to active NetLedger Server reads.
    /// </summary>
    internal sealed class ActiveArchiveBoundaryService
    {
        private readonly ServerSettings _Settings;

        /// <summary>
        /// Instantiate active/archive boundary service.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        public ActiveArchiveBoundaryService(ServerSettings settings)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// Get the active boundary for a request.
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <returns>Oldest UTC timestamp retained as active data.</returns>
        public DateTime GetBoundaryUtc(RequestContext req)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));
            string tenantId = !String.IsNullOrWhiteSpace(req.TenantId) ? req.TenantId! : req.Auth?.TenantId ?? String.Empty;
            int retentionDays = _Settings.Archive.GetActiveDataRetentionDays(tenantId);
            return DateTime.UtcNow.AddDays(-retentionDays);
        }

        /// <summary>
        /// Validate and adjust an active data range.
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <param name="fromUtc">Lower range bound.</param>
        /// <param name="toUtc">Upper range bound.</param>
        /// <param name="dataType">Human-readable data type.</param>
        /// <returns>Error response when the range belongs to archive; otherwise null.</returns>
        public ResponseContext? ApplyActiveRange(RequestContext req, ref DateTime? fromUtc, DateTime? toUtc, string dataType)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));
            if (String.IsNullOrWhiteSpace(dataType)) throw new ArgumentNullException(nameof(dataType));
            if (_Settings.Archive == null || !_Settings.Archive.Enabled) return null;

            DateTime boundaryUtc = GetBoundaryUtc(req);
            if (!fromUtc.HasValue && !toUtc.HasValue)
            {
                return null;
            }

            object context = new
            {
                Error = "DataArchived",
                DataType = dataType,
                ActiveBoundaryUtc = boundaryUtc,
                ArchiveServerEndpoint = _Settings.Archive.ArchiveServerEndpoint,
                AllowPartial = req.AllowPartial
            };

            if (toUtc.HasValue && toUtc.Value.ToUniversalTime() < boundaryUtc)
            {
                return ResponseContext.FromError(req, ApiErrorEnum.Conflict, context, dataType + " range is outside the active retention window.");
            }

            if (fromUtc.HasValue && fromUtc.Value.ToUniversalTime() < boundaryUtc)
            {
                if (!req.AllowPartial)
                {
                    return ResponseContext.FromError(req, ApiErrorEnum.Conflict, context, dataType + " range crosses the active/archive boundary.");
                }

                fromUtc = boundaryUtc;
            }

            return null;
        }
    }
}
