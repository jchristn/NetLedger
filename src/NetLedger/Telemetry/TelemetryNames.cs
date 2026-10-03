namespace NetLedger.Telemetry
{
    /// <summary>
    /// Stable telemetry names for NetLedger: the meter and activity source names, every instrument name, every
    /// metric label key, span names, and the bounded label values. These strings are a public contract consumed by
    /// collectors, Grafana dashboards, and alert rules. Treat any change as a breaking change.
    /// Prometheus exporters rewrite dotted instrument names to snake case and append unit and _total suffixes,
    /// for example netledger.ledger.operation.duration becomes netledger_ledger_operation_duration_seconds.
    /// </summary>
    public static class TelemetryNames
    {
        #region Sources

        /// <summary>
        /// Name of the NetLedger meter. Subscribe a collector to this name to receive every NetLedger metric.
        /// </summary>
        public const string MeterName = "NetLedger";

        /// <summary>
        /// Name of the NetLedger activity source. Subscribe a collector to this name to receive every NetLedger span.
        /// </summary>
        public const string ActivitySourceName = "NetLedger";

        /// <summary>
        /// Name of the meter and activity source emitted by the Watson webserver hosting the NetLedger services.
        /// </summary>
        public const string WatsonSourceName = "Watson";

        #endregion

        #region Label-Keys

        /// <summary>Label key: logical operation name (bounded set defined by NetLedger code).</summary>
        public const string LabelOperation = "operation";

        /// <summary>Label key: operation outcome (see the Outcome constants).</summary>
        public const string LabelOutcome = "outcome";

        /// <summary>Label key: exception type name for failures (OpenTelemetry semantic convention).</summary>
        public const string LabelErrorType = "error.type";

        /// <summary>Label key: component emitting an error or database operation.</summary>
        public const string LabelComponent = "component";

        /// <summary>Label key: database system (OpenTelemetry semantic convention).</summary>
        public const string LabelDbSystem = "db.system.name";

        /// <summary>Label key: database operation verb such as SELECT or INSERT (OpenTelemetry semantic convention).</summary>
        public const string LabelDbOperation = "db.operation.name";

        /// <summary>Label key: downstream service name for outbound integrations.</summary>
        public const string LabelService = "service";

        /// <summary>Label key: pipeline or workflow stage name.</summary>
        public const string LabelStage = "stage";

        /// <summary>Label key: workflow name for archive server workflows.</summary>
        public const string LabelWorkflow = "workflow";

        /// <summary>Label key: archived entity type (entries or request_history).</summary>
        public const string LabelEntity = "entity";

        /// <summary>Label key: what triggered an archive export (api or automatic).</summary>
        public const string LabelTrigger = "trigger";

        /// <summary>Label key: ledger entry type (credit or debit).</summary>
        public const string LabelEntryType = "entry_type";

        /// <summary>Label key: lock kind (process or database).</summary>
        public const string LabelLock = "lock";

        /// <summary>Label key: generic bounded result value.</summary>
        public const string LabelResult = "result";

        /// <summary>Label key: authentication method.</summary>
        public const string LabelAuthMethod = "method";

        /// <summary>Label key: authorization decision (permit or deny).</summary>
        public const string LabelDecision = "decision";

        /// <summary>Label key: authorization resource type.</summary>
        public const string LabelResource = "resource";

        /// <summary>Label key: archive migration lifecycle event.</summary>
        public const string LabelEvent = "event";

        /// <summary>Label key: byte transfer direction (read or write).</summary>
        public const string LabelDirection = "direction";

        /// <summary>Label key: configuration setting name for the config gauge.</summary>
        public const string LabelSetting = "setting";

        /// <summary>Label key: service version for the build info gauge.</summary>
        public const string LabelVersion = "version";

        /// <summary>Label key: .NET runtime version for the build info gauge.</summary>
        public const string LabelRuntime = "runtime";

        #endregion

        #region Span-Attribute-Keys

        /// <summary>Span attribute key: tenant identifier (spans only, never a metric label).</summary>
        public const string AttributeTenantId = "netledger.tenant.id";

        /// <summary>Span attribute key: account identifier (spans only, never a metric label).</summary>
        public const string AttributeAccountId = "netledger.account.id";

        /// <summary>Span attribute key: entry identifier (spans only, never a metric label).</summary>
        public const string AttributeEntryId = "netledger.entry.id";

        /// <summary>Span attribute key: archive migration identifier (spans only).</summary>
        public const string AttributeMigrationId = "netledger.archive.migration.id";

        /// <summary>Span attribute key: archive batch identifier (spans only).</summary>
        public const string AttributeBatchId = "netledger.archive.batch.id";

        /// <summary>Span attribute key: archive manifest identifier (spans only).</summary>
        public const string AttributeManifestId = "netledger.archive.manifest.id";

        /// <summary>Span attribute key: row count processed by an operation (spans only).</summary>
        public const string AttributeRowCount = "netledger.rows";

        /// <summary>Span attribute key: byte count processed by an operation (spans only).</summary>
        public const string AttributeByteCount = "netledger.bytes";

        /// <summary>Span attribute key: retry attempt number (spans only).</summary>
        public const string AttributeAttempt = "netledger.attempt";

        /// <summary>Span attribute key: principal identifier (spans only).</summary>
        public const string AttributePrincipalId = "netledger.principal.id";

        /// <summary>Span attribute key: HTTP response status code of an outbound call (OpenTelemetry semantic convention).</summary>
        public const string AttributeHttpStatusCode = "http.response.status_code";

        /// <summary>Span attribute key: HTTP request method of an outbound call (OpenTelemetry semantic convention).</summary>
        public const string AttributeHttpMethod = "http.request.method";

        /// <summary>Span attribute key: exception message (spans only).</summary>
        public const string AttributeExceptionMessage = "exception.message";

        /// <summary>Span attribute key: exception type (OpenTelemetry semantic convention).</summary>
        public const string AttributeExceptionType = "exception.type";

        #endregion

        #region Outcomes

        /// <summary>Outcome value: the operation succeeded.</summary>
        public const string OutcomeSuccess = "success";

        /// <summary>Outcome value: the operation failed.</summary>
        public const string OutcomeFailure = "failure";

        /// <summary>Outcome value: the operation was canceled.</summary>
        public const string OutcomeCanceled = "canceled";

        /// <summary>Outcome value: the operation found nothing to do.</summary>
        public const string OutcomeNoRows = "no_rows";

        /// <summary>Outcome value: the operation was skipped.</summary>
        public const string OutcomeSkipped = "skipped";

        /// <summary>Outcome value: the operation timed out.</summary>
        public const string OutcomeTimeout = "timeout";

        /// <summary>Outcome value: the operation completed with some failures.</summary>
        public const string OutcomePartial = "partial";

        /// <summary>Outcome value: the requested object or record does not exist.</summary>
        public const string OutcomeNotFound = "not_found";

        /// <summary>Outcome value: the operation was rejected by validation or policy.</summary>
        public const string OutcomeRejected = "rejected";

        #endregion

        #region Ledger-Metrics

        /// <summary>Histogram (s): duration of each public Ledger operation. Labels: operation, outcome.</summary>
        public const string LedgerOperationDuration = "netledger.ledger.operation.duration";

        /// <summary>Counter: Ledger operations by outcome. Labels: operation, outcome, error.type (failures only).</summary>
        public const string LedgerOperations = "netledger.ledger.operations";

        /// <summary>Counter: ledger entries created. Labels: entry_type.</summary>
        public const string LedgerEntriesCreated = "netledger.ledger.entries.created";

        /// <summary>Counter: ledger entries committed into a balance.</summary>
        public const string LedgerEntriesCommitted = "netledger.ledger.entries.committed";

        /// <summary>Counter: pending ledger entries canceled.</summary>
        public const string LedgerEntriesCanceled = "netledger.ledger.entries.canceled";

        /// <summary>Histogram ({entry}): number of entries committed per commit.</summary>
        public const string LedgerCommitSize = "netledger.ledger.commit.size";

        /// <summary>Counter: balance chain verifications. Labels: result (valid or invalid).</summary>
        public const string LedgerBalanceChainVerifications = "netledger.ledger.balance_chain.verifications";

        /// <summary>Histogram (s): time spent waiting for an account lock (the queued stage of every write). Labels: lock, outcome.</summary>
        public const string LedgerLockWaitDuration = "netledger.ledger.lock.wait.duration";

        /// <summary>UpDownCounter ({lock}): account locks currently held. Labels: lock.</summary>
        public const string LedgerLocksActive = "netledger.ledger.lock.active";

        #endregion

        #region Database-Metrics

        /// <summary>Histogram (s): duration of database round trips. Labels: component, db.system.name, db.operation.name, outcome.</summary>
        public const string DbOperationDuration = "netledger.db.client.operation.duration";

        /// <summary>Counter: database round trips. Labels: component, db.system.name, db.operation.name, outcome, error.type (failures only).</summary>
        public const string DbOperations = "netledger.db.client.operations";

        /// <summary>UpDownCounter ({operation}): database round trips in flight. Labels: component, db.system.name.</summary>
        public const string DbOperationsActive = "netledger.db.client.operations.active";

        #endregion

        #region Integration-Metrics

        /// <summary>Histogram (s): duration of outbound integration calls. Labels: service, operation, outcome.</summary>
        public const string IntegrationRequestDuration = "netledger.integration.request.duration";

        /// <summary>Counter: outbound integration calls. Labels: service, operation, outcome, error.type (failures only).</summary>
        public const string IntegrationRequests = "netledger.integration.requests";

        /// <summary>Counter (By): bytes moved to or from archive object storage. Labels: service, direction.</summary>
        public const string StorageBytes = "netledger.archive.storage.bytes";

        #endregion

        #region Error-And-Info-Metrics

        /// <summary>Counter: errors observed by NetLedger components. Labels: component, error.type.</summary>
        public const string Errors = "netledger.errors";

        /// <summary>Gauge: always 1, carries build metadata. Labels: component, version, runtime.</summary>
        public const string BuildInfo = "netledger.build.info";

        /// <summary>Gauge: safe numeric configuration values (never secrets). Labels: component, setting.</summary>
        public const string ConfigValue = "netledger.config";

        /// <summary>Gauge (s): seconds since the service process started. Labels: component.</summary>
        public const string Uptime = "netledger.uptime";

        #endregion

        #region Auth-Metrics

        /// <summary>Counter: authentication attempts. Labels: component, method, result.</summary>
        public const string AuthAttempts = "netledger.auth.attempts";

        /// <summary>Histogram (s): authentication duration. Labels: component, method, result.</summary>
        public const string AuthDuration = "netledger.auth.duration";

        /// <summary>Counter: password logins. Labels: outcome.</summary>
        public const string AuthLogins = "netledger.auth.logins";

        /// <summary>Counter: authorization decisions. Labels: component, resource, operation, decision.</summary>
        public const string AuthzDecisions = "netledger.authz.decisions";

        /// <summary>Histogram (s): authorization decision duration. Labels: component, decision.</summary>
        public const string AuthzDuration = "netledger.authz.duration";

        /// <summary>Counter: archive server introspection cache lookups. Labels: result (hit or miss).</summary>
        public const string IntrospectionCacheLookups = "netledger.archive.introspection.cache.lookups";

        /// <summary>Gauge ({entry}): archive server introspection cache entries.</summary>
        public const string IntrospectionCacheSize = "netledger.archive.introspection.cache.size";

        #endregion

        #region Request-History-Metrics

        /// <summary>Counter: background request history writes. Labels: component, outcome.</summary>
        public const string RequestHistoryWrites = "netledger.request_history.writes";

        /// <summary>Histogram (s): background request history write duration. Labels: component, outcome.</summary>
        public const string RequestHistoryWriteDuration = "netledger.request_history.write.duration";

        /// <summary>UpDownCounter ({write}): background request history writes queued or in flight. Labels: component.</summary>
        public const string RequestHistoryPending = "netledger.request_history.pending";

        #endregion

        #region Archive-Export-Metrics

        /// <summary>Counter: archive export jobs. Labels: entity, trigger, outcome.</summary>
        public const string ArchiveExportJobs = "netledger.archive.export.jobs";

        /// <summary>Histogram (s): end-to-end archive export job duration. Labels: entity, trigger, outcome.</summary>
        public const string ArchiveExportDuration = "netledger.archive.export.duration";

        /// <summary>Histogram (s): archive export duration per stage. Labels: entity, stage, outcome.</summary>
        public const string ArchiveExportStageDuration = "netledger.archive.export.stage.duration";

        /// <summary>Counter: archive export stage executions. Labels: entity, stage, outcome.</summary>
        public const string ArchiveExportStageEvents = "netledger.archive.export.stage.events";

        /// <summary>Counter ({row}): rows exported to the archive server. Labels: entity.</summary>
        public const string ArchiveExportRows = "netledger.archive.export.rows";

        /// <summary>Counter (By): compressed bytes uploaded to the archive server. Labels: entity.</summary>
        public const string ArchiveExportBytes = "netledger.archive.export.bytes";

        /// <summary>Counter ({row}): active rows deleted after a committed archive export. Labels: entity.</summary>
        public const string ArchiveExportCleanupRows = "netledger.archive.export.cleanup.rows";

        /// <summary>Gauge (s): Unix time of the last successful archive export. Labels: entity.</summary>
        public const string ArchiveExportLastSuccess = "netledger.archive.export.last_success";

        #endregion

        #region Automatic-Archive-Metrics

        /// <summary>Counter: automatic archival worker runs. Labels: outcome.</summary>
        public const string AutomaticArchiveRuns = "netledger.archive.automatic.runs";

        /// <summary>Histogram (s): automatic archival worker run duration. Labels: outcome.</summary>
        public const string AutomaticArchiveRunDuration = "netledger.archive.automatic.run.duration";

        /// <summary>Counter: accounts evaluated by the automatic archival worker. Labels: result.</summary>
        public const string AutomaticArchiveAccounts = "netledger.archive.automatic.accounts";

        /// <summary>Counter: automatic archive export retry attempts after a failure.</summary>
        public const string AutomaticArchiveRetries = "netledger.archive.automatic.retries";

        /// <summary>Gauge (s): Unix time of the last completed automatic archival run.</summary>
        public const string AutomaticArchiveLastRun = "netledger.archive.automatic.last_run";

        /// <summary>Gauge (s): Unix time of the last automatic archival run that finished without export failures.</summary>
        public const string AutomaticArchiveLastSuccess = "netledger.archive.automatic.last_success";

        /// <summary>UpDownCounter ({run}): automatic archival runs currently executing (0 or 1).</summary>
        public const string AutomaticArchiveRunning = "netledger.archive.automatic.running";

        #endregion

        #region Archive-Server-Metrics

        /// <summary>Counter: archive migration lifecycle events on the archive server. Labels: event, entity.</summary>
        public const string ArchiveMigrationEvents = "netledger.archive.migration.events";

        /// <summary>Histogram (s): archive server workflow duration per stage. Labels: workflow, stage, outcome.</summary>
        public const string ArchiveWorkflowStageDuration = "netledger.archive.workflow.stage.duration";

        /// <summary>Counter (By): migration batch bytes received by the archive server.</summary>
        public const string ArchiveUploadBytes = "netledger.archive.upload.bytes";

        /// <summary>Counter ({row}): archived rows returned by archive queries. Labels: entity.</summary>
        public const string ArchiveQueryRows = "netledger.archive.query.rows";

        /// <summary>Counter: archive manifest verifications. Labels: result (valid or invalid).</summary>
        public const string ArchiveVerifications = "netledger.archive.verifications";

        #endregion

        #region Components

        /// <summary>Component value: the NetLedger ledger library.</summary>
        public const string ComponentLedger = "ledger";

        /// <summary>Component value: the active ledger database.</summary>
        public const string ComponentActiveDatabase = "active_db";

        /// <summary>Component value: the archive catalog database.</summary>
        public const string ComponentArchiveCatalog = "archive_catalog";

        /// <summary>Component value: archive object storage.</summary>
        public const string ComponentArchiveStorage = "archive_storage";

        /// <summary>Component value: the NetLedger REST server.</summary>
        public const string ComponentServer = "server";

        /// <summary>Component value: the NetLedger Archive Server.</summary>
        public const string ComponentArchiveServer = "archive_server";

        /// <summary>Component value: the automatic archival worker.</summary>
        public const string ComponentAutomaticArchive = "automatic_archive";

        /// <summary>Component value: the archive export pipeline.</summary>
        public const string ComponentArchiveExport = "archive_export";

        /// <summary>Component value: request history capture.</summary>
        public const string ComponentRequestHistory = "request_history";

        #endregion

        #region Services

        /// <summary>Integration service value: the NetLedger Archive Server, called by the NetLedger server.</summary>
        public const string ServiceArchiveServer = "archive_server";

        /// <summary>Integration service value: the NetLedger server, called by the archive server for introspection.</summary>
        public const string ServiceNetLedgerServer = "netledger_server";

        /// <summary>Integration service value: S3-compatible object storage.</summary>
        public const string ServiceS3 = "s3";

        /// <summary>Integration service value: filesystem object storage.</summary>
        public const string ServiceFilesystem = "filesystem";

        #endregion

        #region Archive-Export-Stages

        /// <summary>Archive export stage: waiting for the account lock before export.</summary>
        public const string StageQueued = "queued";

        /// <summary>Archive export stage: validation of the account, range, and cleanup preconditions.</summary>
        public const string StageValidate = "validate";

        /// <summary>Archive export stage: reading a page of active rows.</summary>
        public const string StageEnumerate = "enumerate";

        /// <summary>Archive export stage: creating the archive migration.</summary>
        public const string StageCreateMigration = "create_migration";

        /// <summary>Archive export stage: serializing and uploading one batch.</summary>
        public const string StageUploadBatch = "upload_batch";

        /// <summary>Archive export stage: sealing the migration.</summary>
        public const string StageSeal = "seal";

        /// <summary>Archive export stage: committing the migration to a manifest.</summary>
        public const string StageCommit = "commit";

        /// <summary>Archive export stage: deleting archived active rows.</summary>
        public const string StageCleanup = "cleanup";

        #endregion

        #region Archive-Workflows

        /// <summary>Archive server workflow: batch content upload.</summary>
        public const string WorkflowUpload = "upload";

        /// <summary>Archive server workflow: migration commit.</summary>
        public const string WorkflowCommit = "commit";

        /// <summary>Archive server workflow: manifest verification.</summary>
        public const string WorkflowVerify = "verify";

        /// <summary>Archive server workflow: archived data query.</summary>
        public const string WorkflowQuery = "query";

        /// <summary>Archive server workflow stage: receiving upload content to a temporary file.</summary>
        public const string StageReceive = "receive";

        /// <summary>Archive server workflow stage: storing content in object storage.</summary>
        public const string StageStore = "store";

        /// <summary>Archive server workflow stage: promoting temporary objects to committed objects.</summary>
        public const string StagePromoteObjects = "promote_objects";

        /// <summary>Archive server workflow stage: building and persisting the manifest.</summary>
        public const string StageCreateManifest = "create_manifest";

        /// <summary>Archive server workflow stage: verifying a manifest's objects.</summary>
        public const string StageVerifyManifest = "verify_manifest";

        /// <summary>Archive server workflow stage: reading archived objects for a query.</summary>
        public const string StageReadObjects = "read_objects";

        #endregion

        #region Span-Names

        /// <summary>Span name prefix for Ledger operations, for example "ledger AddCredit".</summary>
        public const string SpanLedgerPrefix = "ledger ";

        /// <summary>Span name for account lock waits.</summary>
        public const string SpanLockWait = "ledger.lock.wait";

        /// <summary>Span name prefix for pipeline and workflow stages, for example "stage:upload_batch".</summary>
        public const string SpanStagePrefix = "stage:";

        /// <summary>Span name prefix for archive export jobs, for example "archive.export entries".</summary>
        public const string SpanArchiveExportPrefix = "archive.export ";

        /// <summary>Span name for an automatic archival worker run (a root span).</summary>
        public const string SpanAutomaticArchiveRun = "archive.automatic.run";

        /// <summary>Span name for one account evaluated by the automatic archival worker.</summary>
        public const string SpanAutomaticArchiveAccount = "archive.automatic.account";

        /// <summary>Span name for request authentication.</summary>
        public const string SpanAuthenticate = "auth.authenticate";

        /// <summary>Span name for password login.</summary>
        public const string SpanLogin = "auth.login";

        /// <summary>Span name for authorization decisions.</summary>
        public const string SpanAuthorize = "authz.authorize";

        /// <summary>Span name for background request history writes.</summary>
        public const string SpanRequestHistoryWrite = "request_history.write";

        #endregion
    }
}
