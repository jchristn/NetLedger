# NetLedger Telemetry

NetLedger ships with metrics, traces, and logs built in. An on-call engineer can use the Grafana dashboards and Tempo traces to see where the time went and what failed. They don't need to read the source or attach a debugger.

This document is the contract for every telemetry point: names, units, labels, spans, configuration, alerts, and the dashboard map. Metric names, label keys, and span names are a public API. Grafana dashboards and alert rules depend on them, so a rename is a breaking change.

## Table of Contents

1. [How it fits together](#how-it-fits-together)
2. [Meter and activity source names](#meter-and-activity-source-names)
3. [Enabling and configuring](#enabling-and-configuring)
4. [Subscribing from your own host (library users)](#subscribing-from-your-own-host-library-users)
5. [Metrics catalog](#metrics-catalog)
6. [Spans catalog](#spans-catalog)
7. [Logs](#logs)
8. [The local observability stack](#the-local-observability-stack)
9. [Dashboard map](#dashboard-map)
10. [Recommended alerts (PromQL)](#recommended-alerts-promql)
11. [Conventions and guarantees](#conventions-and-guarantees)
12. [Production notes](#production-notes)

---

## How it fits together

| Layer | Emits | Collected by |
| --- | --- | --- |
| `NetLedger` library (`Ledger`, database providers, account locks) | `NetLedger` meter and activity source (System.Diagnostics only) | Any host that subscribes; nothing is emitted when nobody listens |
| `NetLedger.Archive` library (object storage, archive catalog SQL) | Same `NetLedger` meter and activity source | Same |
| NetLedger Server and NetLedger Archive Server (Watson 7.2) | Watson's built-in `Watson` meter and activity source (HTTP metrics, one server span per request) plus app-level instruments on `NetLedger` | One [Radiant](https://www.nuget.org/packages/Radiant) host per server process, exporting OTLP (traces, metrics, logs), an in-process Prometheus endpoint, and optionally Loki |

Watson already covers the HTTP layer: request rate, latency, status, active requests, and the per-request server span. NetLedger does not duplicate any of it. NetLedger instruments everything behind the routes: ledger operations, database round trips, account locks, authentication and authorization, the archive export pipeline and each of its stages, the automatic archival worker, background request-history writes, every outbound call (Archive Server, NetLedger introspection, S3 or filesystem storage), and the Archive Server migration workflows.

A single trace runs from the inbound HTTP request through the ledger operation down to each database round trip. It continues across the NetLedger Server to Archive Server boundary through W3C `traceparent` propagation.

## Meter and activity source names

| Name | Kind | Owner |
| --- | --- | --- |
| `NetLedger` | `Meter` and `ActivitySource` | NetLedger libraries and both servers (`NetLedger.Telemetry.TelemetryNames.MeterName` / `ActivitySourceName`) |
| `Watson` | `Meter` and `ActivitySource` | Watson webserver inside both servers |
| `Padlock` | `Meter` and `ActivitySource` | Padlock keyed-lock library guarding in-process ledger account locks (`padlock.name` = `ledger.account`, `TelemetryNames.PadlockAccountLockName`); exported by NetLedger Server |

Every instrument name, label key, span name, and bounded label value is defined in one constants class: `src/NetLedger/Telemetry/TelemetryNames.cs`.

## Enabling and configuring

### Servers

Telemetry is on by default. Each server reads a `Telemetry` section from its settings file. Every key can be overridden with an environment variable.

```json
"Telemetry": {
  "Enabled": true,
  "ServiceName": "netledger-server",
  "OtlpEnabled": true,
  "OtlpEndpoint": "http://127.0.0.1:4317",
  "OtlpProtocol": "grpc",
  "PrometheusEnabled": true,
  "PrometheusHostname": "127.0.0.1",
  "PrometheusPort": 9464,
  "PrometheusPath": "/metrics",
  "LokiEnabled": false,
  "LokiEndpoint": "http://127.0.0.1:3100/otlp",
  "TraceSamplingRatio": 1.0,
  "IncludeRuntimeMetrics": true,
  "WatsonTelemetryEnabled": true
}
```

| Setting | Environment variable | Default (server / archive server) | Notes |
| --- | --- | --- | --- |
| `Enabled` | `NETLEDGER_TELEMETRY_ENABLED` | `true` | Master switch for export. When false, no exporter starts and no port is bound. |
| `ServiceName` | `NETLEDGER_TELEMETRY_SERVICE_NAME` | `netledger-server` / `netledger-archive-server` | Reported as `service.name`. |
| `OtlpEnabled` | `NETLEDGER_TELEMETRY_OTLP_ENABLED` | `true` | Push traces, metrics, and logs over OTLP. |
| `OtlpEndpoint` | `NETLEDGER_TELEMETRY_OTLP_ENDPOINT` | `http://127.0.0.1:4317` | Collector, Tempo, or any OTLP backend. |
| `OtlpProtocol` | `NETLEDGER_TELEMETRY_OTLP_PROTOCOL` | `grpc` | `grpc` (port 4317) or `httpprotobuf` (port 4318). |
| `PrometheusEnabled` | `NETLEDGER_TELEMETRY_PROMETHEUS_ENABLED` | `true` | In-process scrape endpoint with NetLedger, Watson, and .NET runtime metrics. |
| `PrometheusHostname` | `NETLEDGER_TELEMETRY_PROMETHEUS_HOSTNAME` | `127.0.0.1` | The listener binds the address this name resolves to and answers only that Host header. Inside containers, use the DNS name Prometheus scrapes (the compose file uses `server` and `archive-server`). Wildcards (`*`, `+`, `0.0.0.0`) are rejected by the OpenTelemetry Prometheus listener. |
| `PrometheusPort` | `NETLEDGER_TELEMETRY_PROMETHEUS_PORT` | `9464` / `9465` | Range 1 through 65535. |
| `PrometheusPath` | `NETLEDGER_TELEMETRY_PROMETHEUS_PATH` | `/metrics` | Must start with `/`. |
| `LokiEnabled` | `NETLEDGER_TELEMETRY_LOKI_ENABLED` | `false` | Push logs directly to Loki 3.x over OTLP. |
| `LokiEndpoint` | `NETLEDGER_TELEMETRY_LOKI_ENDPOINT` | `http://127.0.0.1:3100/otlp` | Loki OTLP base path. |
| `TraceSamplingRatio` | `NETLEDGER_TELEMETRY_TRACE_SAMPLING_RATIO` | `1.0` | Parent-based head sampling, 0.0 through 1.0. |
| `IncludeRuntimeMetrics` | `NETLEDGER_TELEMETRY_RUNTIME_METRICS` | `true` | GC, heap, thread pool, CPU (`dotnet_*`). |
| `WatsonTelemetryEnabled` | `NETLEDGER_TELEMETRY_WATSON_ENABLED` | `true` | Watson `Settings.Telemetry.Enable`. `EnableMetrics`, `EnableTraces`, and `PropagateContext` are always on. |

At startup, a server logs one line such as `[TelemetryService] telemetry enabled, service netledger-server, OTLP Grpc to http://tempo:4317, Prometheus http://server:9464/metrics, Loki http://loki:3100/otlp`. If the host can't start (for example, the port is in use), the server logs a warning and runs without exported telemetry. Instrumentation never breaks a request.

Defaults use `127.0.0.1` rather than `localhost`. On Windows, `localhost` resolves to IPv6 `::1` first and stalls before falling back.

## Subscribing from your own host (library users)

The `NetLedger` and `NetLedger.Archive` packages only emit through `System.Diagnostics.Metrics.Meter` and `System.Diagnostics.ActivitySource`. They take no exporter dependency, and an unobserved instrument costs a few nanoseconds. To collect the signals, subscribe to the `NetLedger` names in your host.

With Radiant:

```csharp
RadiantSettings settings = new RadiantSettings("my-ledger-app");
settings.Sources.AddMeter("NetLedger");            // NetLedger.Telemetry.TelemetryNames.MeterName
settings.Sources.AddActivitySource("NetLedger");   // NetLedger.Telemetry.TelemetryNames.ActivitySourceName
settings.Prometheus.Enable = true;
using (RadiantHost host = RadiantHost.Start(settings))
{
    await using Ledger ledger = new Ledger("./ledger.db");
    // ...
}
```

With the OpenTelemetry SDK:

```csharp
using MeterProvider meters = Sdk.CreateMeterProviderBuilder().AddMeter("NetLedger").AddOtlpExporter().Build();
using TracerProvider traces = Sdk.CreateTracerProviderBuilder().AddSource("NetLedger").AddOtlpExporter().Build();
```

In tests, attach a BCL `MeterListener` and `ActivityListener` to the `NetLedger` names (see `src/Test.Shared/TelemetryCapture.cs`).

Hosts that run several services in one process can call `NetLedgerTelemetry.RegisterService(component, version, configProvider)` to emit the build-info, uptime, and safe-configuration gauges.

## Metrics catalog

Instrument names are dotted. A Prometheus exporter rewrites them to snake case: it adds `_seconds` for unit `s`, `_bytes` for `By`, and `_total` for counters. Label keys with dots become underscores (`error.type` becomes `error_type`). All labels are bounded. Identifiers and free text never appear on a metric. They appear only on spans.

`error.type` is the exception's short type name, for example `KeyNotFoundException`. For non-exception failures it is a bounded token such as `http_503` or `export_failures`. It is only present when `outcome` is a failure.

Common `outcome` values: `success`, `failure`, `canceled`, `timeout`, `no_rows`, `skipped`, `partial`, `not_found`, `rejected`.

### Ledger (library)

| Instrument (Prometheus name) | Type | Unit | Labels | Description |
| --- | --- | --- | --- | --- |
| `netledger.ledger.operation.duration` (`netledger_ledger_operation_duration_seconds`) | Histogram | s | `operation`, `outcome` | Latency of every public `Ledger` operation. |
| `netledger.ledger.operations` (`netledger_ledger_operations_total`) | Counter | {operation} | `operation`, `outcome`, `error.type` | Ledger operations by outcome. |
| `netledger.ledger.entries.created` (`netledger_ledger_entries_created_total`) | Counter | {entry} | `entry_type` (`credit`, `debit`) | Entries created. |
| `netledger.ledger.entries.committed` (`netledger_ledger_entries_committed_total`) | Counter | {entry} | none | Entries committed into a balance. |
| `netledger.ledger.entries.canceled` (`netledger_ledger_entries_canceled_total`) | Counter | {entry} | none | Pending entries canceled. |
| `netledger.ledger.commit.size` (`netledger_ledger_commit_size`) | Histogram | {entry} | none | Entries per commit (0 when nothing was pending). |
| `netledger.ledger.balance_chain.verifications` (`netledger_ledger_balance_chain_verifications_total`) | Counter | {verification} | `result` (`valid`, `invalid`) | Balance-chain verifications. Any `invalid` result needs investigation. |
| `netledger.ledger.lock.wait.duration` (`netledger_ledger_lock_wait_duration_seconds`) | Histogram | s | `lock` (`process`, `database`), `outcome` | Time waiting for an account lock. This is the queued stage of every write. `timeout` means the database lock wasn't acquired. |
| `netledger.ledger.lock.active` (`netledger_ledger_lock_active`) | ObservableUpDownCounter | {lock} | `lock` | Account locks currently held. |

`operation` values: `CreateAccount`, `UpdateAccount`, `DeleteAccountByName`, `DeleteAccountById`, `GetAccountByName`, `GetAccountById`, `GetAllAccounts`, `EnumerateAccounts`, `AddCredit`, `AddDebit`, `AddCredits`, `AddDebits`, `CancelPending`, `GetEntry`, `GetEntries`, `SearchEntries`, `EnumerateEntries`, `GetBalance`, `GetBalanceAsOf`, `CommitEntries`, `GetPendingEntries`, `GetPendingCredits`, `GetPendingDebits`, `GetAllBalances`, `GetBalancesForAccounts`, `VerifyBalanceChain`.

### Database (library, every provider)

| Instrument | Type | Unit | Labels | Description |
| --- | --- | --- | --- | --- |
| `netledger.db.client.operation.duration` (`netledger_db_client_operation_duration_seconds`) | Histogram | s | `component` (`active_db`, `archive_catalog`), `db.system.name` (`sqlite`, `postgresql`, `mysql`, `microsoft.sql_server`), `db.operation.name`, `outcome` | Duration of each SQL round trip. |
| `netledger.db.client.operations` (`netledger_db_client_operations_total`) | Counter | {operation} | same plus `error.type` | SQL round trips by outcome. |
| `netledger.db.client.operations.active` (`netledger_db_client_operations_active`) | UpDownCounter | {operation} | `component`, `db.system.name` | Round trips in flight. Compare with the `database.max_pool_size` config gauge. |

`db.operation.name` is the statement's first keyword (`SELECT`, `INSERT`, `UPDATE`, `DELETE`, `CREATE`, `ALTER`, `DROP`, `PRAGMA`, `BEGIN`, `COMMIT`, `ROLLBACK`, `WITH`, `MERGE`, `TRUNCATE`, `EXEC`, `SET`, `IF`, `DECLARE`). Unknown verbs become `OTHER`, and multi-statement batches become `BATCH`. Query text is never recorded.

### Outbound integrations and storage

| Instrument | Type | Unit | Labels | Description |
| --- | --- | --- | --- | --- |
| `netledger.integration.request.duration` (`netledger_integration_request_duration_seconds`) | Histogram | s | `service`, `operation`, `outcome` | Latency of each outbound call. |
| `netledger.integration.requests` (`netledger_integration_requests_total`) | Counter | {request} | `service`, `operation`, `outcome`, `error.type` | Outbound calls by outcome. |
| `netledger.archive.storage.bytes` (`netledger_archive_storage_bytes_total`) | Counter | By | `service`, `direction` (`read`, `write`) | Bytes moved to or from archive object storage. |

| `service` | `operation` values | Caller |
| --- | --- | --- |
| `archive_server` | `create_migration`, `create_batch`, `upload_batch_content`, `seal_migration`, `commit_migration` | NetLedger Server archive export |
| `netledger_server` | `introspect` | Archive Server authentication |
| `s3`, `filesystem` | `write_temporary`, `commit`, `read`, `read_metadata`, `update_metadata`, `delete_temporary` | Archive object storage |

### Authentication and authorization (both servers)

| Instrument | Type | Unit | Labels | Description |
| --- | --- | --- | --- | --- |
| `netledger.auth.attempts` (`netledger_auth_attempts_total`) | Counter | {attempt} | `component` (`server`, `archive_server`), `method` (`bearer`, `x_token`, `access_key`, `none`, `disabled`, `introspection`), `result`, `outcome` | Authentication attempts. `result` is the server `AuthResult` (`success`, `invalidapikey`, `inactiveapikey`, `inactivesession`, `nocredentials`, `notrequired`) or, on the archive server, `success`, `failed`, `notrequired`. |
| `netledger.auth.duration` (`netledger_auth_duration_seconds`) | Histogram | s | same | Authentication latency, including Archive Server introspection. |
| `netledger.auth.logins` (`netledger_auth_logins_total`) | Counter | {login} | `outcome` (`success`, `rejected`, `failure`) | Password logins. |
| `netledger.authz.decisions` (`netledger_authz_decisions_total`) | Counter | {decision} | `component`, `resource`, `operation`, `decision` (`permit`, `deny`) | Authorization decisions. Resource types outside the fixed allow-list collapse to `other`. |
| `netledger.authz.duration` (`netledger_authz_duration_seconds`) | Histogram | s | `component`, `decision`, `outcome` | Authorization decision latency (server). |
| `netledger.archive.introspection.cache.lookups` (`netledger_archive_introspection_cache_lookups_total`) | Counter | {lookup} | `result` (`hit`, `miss`) | Archive Server introspection cache efficiency. |
| `netledger.archive.introspection.cache.size` (`netledger_archive_introspection_cache_size`) | Gauge | {entry} | none | Cached introspection results. |

### Request history (background hand-off)

| Instrument | Type | Unit | Labels | Description |
| --- | --- | --- | --- | --- |
| `netledger.request_history.writes` (`netledger_request_history_writes_total`) | Counter | {write} | `component`, `outcome`, `error.type` | Background request-history writes. |
| `netledger.request_history.write.duration` (`netledger_request_history_write_duration_seconds`) | Histogram | s | `component`, `outcome` | Write latency. |
| `netledger.request_history.pending` (`netledger_request_history_pending`) | ObservableUpDownCounter | {write} | `component` | Writes queued or in flight (backlog). |

### Archive export pipeline (NetLedger Server)

A job is one call to export entries for an account or request history for a tenant, triggered by the API or by the automatic worker. Stages run in order: `queued` (account lock wait, only when `DeleteAfterCommit`), `validate`, `enumerate` (one per page), `create_migration`, `upload_batch` (one per batch), `seal`, `commit`, then `cleanup` (only when `DeleteAfterCommit`).

| Instrument | Type | Unit | Labels | Description |
| --- | --- | --- | --- | --- |
| `netledger.archive.export.jobs` (`netledger_archive_export_jobs_total`) | Counter | {job} | `entity` (`entries`, `request_history`), `trigger` (`api`, `automatic`), `outcome`, `error.type` | Jobs by outcome (`success`, `no_rows`, `failure`). |
| `netledger.archive.export.duration` (`netledger_archive_export_duration_seconds`) | Histogram | s | `entity`, `trigger`, `outcome` | End-to-end job duration. |
| `netledger.archive.export.stage.duration` (`netledger_archive_export_stage_duration_seconds`) | Histogram | s | `entity`, `stage`, `outcome` | Per-stage duration. |
| `netledger.archive.export.stage.events` (`netledger_archive_export_stage_events_total`) | Counter | {event} | `entity`, `stage`, `outcome`, `error.type` | Per-stage executions. |
| `netledger.archive.export.rows` (`netledger_archive_export_rows_total`) | Counter | {row} | `entity` | Rows exported. |
| `netledger.archive.export.bytes` (`netledger_archive_export_bytes_total`) | Counter | By | `entity` | Compressed bytes uploaded. |
| `netledger.archive.export.cleanup.rows` (`netledger_archive_export_cleanup_rows_total`) | Counter | {row} | `entity` | Active rows deleted after a committed export. |
| `netledger.archive.export.last_success` (`netledger_archive_export_last_success_seconds`) | Gauge | s | `entity` | Unix time of the last successful job. |

### Automatic archival worker (NetLedger Server)

| Instrument | Type | Unit | Labels | Description |
| --- | --- | --- | --- | --- |
| `netledger.archive.automatic.runs` (`netledger_archive_automatic_runs_total`) | Counter | {run} | `outcome` (`success`, `partial`, `failure`, `skipped`), `error.type` | Worker runs. `skipped` means the previous run still held the run lock. |
| `netledger.archive.automatic.run.duration` (`netledger_archive_automatic_run_duration_seconds`) | Histogram | s | `outcome` | Run duration. |
| `netledger.archive.automatic.accounts` (`netledger_archive_automatic_accounts_total`) | Counter | {account} | `result` (`exported`, `no_rows`, `failed`, `error`, `canceled`, `skipped_disabled`, `skipped_backoff`, `skipped_interval`, `skipped_no_range`) | Accounts evaluated per result. |
| `netledger.archive.automatic.retries` (`netledger_archive_automatic_retries_total`) | Counter | {retry} | none | Export retry attempts after a failure. |
| `netledger.archive.automatic.last_run` (`netledger_archive_automatic_last_run_seconds`) | Gauge | s | none | Unix time of the last completed run. |
| `netledger.archive.automatic.last_success` (`netledger_archive_automatic_last_success_seconds`) | Gauge | s | none | Unix time of the last run without export failures. |
| `netledger.archive.automatic.running` (`netledger_archive_automatic_running`) | ObservableUpDownCounter | {run} | none | 1 while a run executes. |

### Archive Server workflows

| Instrument | Type | Unit | Labels | Description |
| --- | --- | --- | --- | --- |
| `netledger.archive.migration.events` (`netledger_archive_migration_events_total`) | Counter | {event} | `event` (`created`, `reused`, `batch_created`, `batch_uploaded`, `batch_rejected`, `sealed`, `committed`, `aborted`), `entity` | Migration lifecycle. `batch_rejected` means a hash, byte-count, or content validation failure. |
| `netledger.archive.workflow.stage.duration` (`netledger_archive_workflow_stage_duration_seconds`) | Histogram | s | `workflow`, `stage`, `outcome`, `error.type` | Per-stage latency: `upload` (`receive`, `validate`, `store`), `commit` (`promote_objects`, `create_manifest`), `verify` (`verify_manifest`), `query` (`read_objects`). |
| `netledger.archive.upload.bytes` (`netledger_archive_upload_bytes_total`) | Counter | By | none | Migration batch bytes received. |
| `netledger.archive.query.rows` (`netledger_archive_query_rows_total`) | Counter | {row} | `entity` | Archived rows returned by queries. |
| `netledger.archive.verifications` (`netledger_archive_verifications_total`) | Counter | {verification} | `result` (`valid`, `invalid`) | Archived balance-chain verifications. |

### Errors, build info, configuration, uptime

| Instrument | Type | Unit | Labels | Description |
| --- | --- | --- | --- | --- |
| `netledger.errors` (`netledger_errors_total`) | Counter | {error} | `component` (`server`, `archive_server`, `archive_export`, `automatic_archive`, `request_history`, `archive_storage`), `error.type` | Errors observed by NetLedger components, including route exception handlers. |
| `netledger.build.info` (`netledger_build_info`) | Gauge | 1 | `component`, `version`, `runtime` | Always 1. |
| `netledger.uptime` (`netledger_uptime_seconds`) | Gauge | s | `component` | Seconds since the service started. |
| `netledger.config` (`netledger_config`) | Gauge | 1 | `component`, `setting` | Safe numeric configuration (booleans are 1 or 0). It never carries secrets, hostnames, or keys. |

Server `setting` values: `authentication.enabled`, `request_history.enabled`, `request_history.retention_days`, `database.max_pool_size`, `database.connection_timeout_seconds`, `archive.enabled`, `archive.automatic.enabled`, `archive.automatic.interval_seconds`, `archive.automatic.max_accounts_per_run`, `archive.automatic.max_batch_rows`, `archive.automatic.max_retention_days`, `archive.automatic.retry_max_attempts`, `telemetry.trace_sampling_ratio`.

Archive Server `setting` values: `authentication.enabled`, `authentication.introspection_cache_seconds`, `archive.max_enumeration_results`, `archive.max_migration_batch_rows`, `archive.max_migration_batch_bytes`, `archive.require_complete_coverage`, `archive.storage_pools`, `catalog.max_pool_size`, `request_history.enabled`, `telemetry.trace_sampling_ratio`.

### Watson (HTTP, both servers)

Watson emits these itself. NetLedger only enables and exports them. See Watson's `TELEMETRY.md` for the full list.

| Prometheus name | Labels |
| --- | --- |
| `http_server_request_duration_seconds` | `http_request_method`, `http_response_status_code`, `http_route` (template) |
| `http_server_active_requests` | `http_request_method`, `url_scheme` |
| `http_server_request_body_size_bytes`, `http_server_response_body_size_bytes` | method, status |
| `watson_server_up`, `watson_server_uptime_seconds`, `watson_server_connections_*`, `watson_server_exceptions_total`, `watson_route_matches_total`, `watson_route_unmatched_total`, `watson_auth_requests_total` | see Watson |

### Padlock (account locks, NetLedger Server)

Padlock emits these itself; the ledger names its account lock `ledger.account` so the series stay bounded. NetLedger Server subscribes to the `Padlock` source. See Padlock's `TELEMETRY.md` for the full list.

| Instrument | Labels |
| --- | --- |
| `padlock.lock.wait.duration`, `padlock.lock.hold.duration`, `padlock.lock.holders`, `padlock.lock.pending`, `padlock.keys.active`, `padlock.pool.*` | `padlock.name` (`ledger.account`), `padlock.mode`, `padlock.outcome`, `padlock.contended` |

Each acquisition opens a `padlock.acquire` span tagged with `padlock.name`. NetLedger's own `netledger.ledger.lock.wait.duration` histogram and `ledger.lock.wait` span remain the primary account-lock signals.

### .NET runtime

When `IncludeRuntimeMetrics` is on, Radiant adds OpenTelemetry runtime instrumentation: `dotnet_gc_*`, `dotnet_thread_pool_*`, `dotnet_process_cpu_time_seconds_total`, `dotnet_process_memory_working_set_bytes`, `dotnet_exceptions_total`, `dotnet_monitor_lock_contentions_total`, and related series.

## Spans catalog

All NetLedger spans come from the `NetLedger` activity source. They nest under Watson's per-request `Server` span (named `{method} {route}`) when work happens inside a request. Span status is set explicitly: `Ok` on success, or `Error` with an `exception` event (`exception.type`, truncated `exception.message`) and an `error.type` attribute on failure. Every metric label on a measured span is also stamped on the span, along with an `outcome` attribute.

| Span name | Kind | Parent | Attributes (beyond labels) |
| --- | --- | --- | --- |
| `ledger {Operation}`, for example `ledger AddCredit` | Internal | Request span or caller | `netledger.account.id`, `netledger.entry.id` |
| `ledger.lock.wait` | Internal | Ledger operation or export stage | `lock`, `netledger.account.id`, `netledger.attempt` (database lock) |
| `{db.system.name} {db.operation.name}`, for example `postgresql SELECT` | Client | Ledger operation, auth, or export stage | `component`, `db.system.name`, `db.operation.name` (no query text) |
| `auth.authenticate` | Internal | Request span | `component`, `method`, `result`, `netledger.tenant.id`, `netledger.principal.id`, `netledger.auth.cache` (archive server) |
| `auth.login` | Internal | Request span | `netledger.tenant.id`, `netledger.principal.id` |
| `authz.authorize` | Internal | Request span | `resource`, `operation`, `decision`, `netledger.authz.reason`, `netledger.tenant.id`, `netledger.principal.id`, `netledger.resource.id` |
| `request_history.write` | Internal | Explicitly parented to the request span (background hand-off) | `component` |
| `archive.automatic.run` | Internal | Root (one trace per worker run) | `netledger.archive.accounts_scanned`, `netledger.archive.exports_succeeded`, `netledger.archive.exports_failed`, `netledger.rows`, `netledger.bytes` |
| `archive.automatic.account` | Internal | `archive.automatic.run` | `netledger.tenant.id`, `netledger.account.id`, `result`, `netledger.attempt` |
| `archive.export entries`, `archive.export request_history` | Internal | Request span (API) or `archive.automatic.account` | `entity`, `trigger`, `netledger.tenant.id`, `netledger.account.id`, `netledger.archive.migration.id`, `netledger.archive.manifest.id`, `netledger.rows`, `netledger.bytes` |
| `stage:{stage}`, for example `stage:upload_batch` | Internal | Export job span or Archive Server request span | `entity`/`workflow`, `stage` |
| `archive_server {operation}`, for example `archive_server create_migration` | Client | Export stage | `http.request.method`, `http.response.status_code`, `netledger.archive.batch.id`, `netledger.rows`, `netledger.bytes` |
| `netledger_server introspect` | Client | Archive Server `auth.authenticate` | `http.request.method`, `http.response.status_code` |
| `s3 {operation}`, `filesystem {operation}` | Client | Archive Server stage | `netledger.archive.object.path`, `netledger.bytes` |

Context propagation: every outbound HTTP call (`archive_server *`, `netledger_server introspect`) injects a W3C `traceparent` from its client span. Watson's `PropagateContext` adopts it on the receiving server, so an export appears as one trace that spans both servers. Background hand-offs (request-history writes) carry the request span's context explicitly.

## Logs

The NetLedger Server does background work (the automatic archival worker and request-history writes), so it ships structured logs to Loki (`LokiEnabled`) and over OTLP. Log records carry `trace_id` and `span_id`, so Grafana links a log line to its trace and back. Logged events include automatic run summaries and failures, export commits and failures, request-history write failures, and unhandled 5xx route exceptions. The Archive Server has no background workers. It logs only request-scoped warnings (introspection failures and unhandled route exceptions) through the same pipeline. Logs never include secrets, credentials, or payloads.

Existing console and syslog logging (SyslogLogging) is unchanged.

## The local observability stack

`docker/compose.yaml` brings up the whole stack wired together:

```
cd docker
docker compose up -d
```

| Service | Image | Host URL | Credentials | Role |
| --- | --- | --- | --- | --- |
| Grafana | `grafana/grafana-oss:13.0.2` | http://localhost:3002 | `admin` / `admin` (local default) | Dashboards in the **NetLedger** folder |
| Prometheus | `prom/prometheus:v3.5.4` | http://localhost:9090 | none | Scrapes `server:9464` and `archive-server:9465` |
| Tempo | `grafana/tempo:2.6.1` | http://localhost:3200 (OTLP 4317/4318) | none | Traces |
| Loki | `grafana/loki:3.5.5` | http://localhost:3100 | none | Logs (OTLP at `/otlp`) |

Grafana is on host port 3002 because the NetLedger dashboard uses 3000 and the Less3 UI uses 3001. Inside the compose network, the services export traces to `http://tempo:4317` and logs to `http://loki:3100/otlp`. Prometheus scrapes each service's Radiant endpoint by container name. Those scrape ports are not published to the host. Grafana waits for Prometheus, Tempo, and Loki to be healthy, and the NetLedger services wait for Tempo and Loki.

`docker/prometheus.yaml` pins `scrape_protocols: ['PrometheusText0.0.4']` and `metric_name_validation_scheme: legacy`. Without them, Prometheus 3 negotiates UTF-8 OpenMetrics, the OpenTelemetry exporter emits dotted names with `# UNIT` lines, and Prometheus rejects the scrape (`unit "bytes" not a suffix of metric ...`). Use the same settings for any Prometheus 3 that scrapes NetLedger directly.

On shutdown (Ctrl+C or `SIGTERM`), each server disposes its Radiant host, which flushes pending spans, metrics, and logs.

Provisioning as code:

- `docker/prometheus.yaml`, `docker/tempo.yaml`, `docker/loki.yaml`
- `docker/grafana/provisioning/datasources/netledger-datasources.yaml`: Prometheus (`uid: prometheus`), Tempo (`uid: tempo`, linked to Loki by trace id), and Loki (`uid: loki`, with a `trace_id` derived field linking to Tempo)
- `docker/grafana/provisioning/dashboards/netledger-dashboards.yaml`: loads `Assets/grafana/*.json` into the **NetLedger** folder

The NetLedger dashboard home page shows an **External Services** card to system administrators. It lists these URLs and default credentials, with copy buttons. Override them at container start with `NETLEDGER_GRAFANA_URL`, `NETLEDGER_GRAFANA_USERNAME`, `NETLEDGER_GRAFANA_PASSWORD`, `NETLEDGER_PROMETHEUS_URL`, `NETLEDGER_TEMPO_URL`, `NETLEDGER_LOKI_URL`, `NETLEDGER_LESS3_URL`, `NETLEDGER_LESS3_ACCESS_KEY`, `NETLEDGER_LESS3_SECRET_KEY`, and `NETLEDGER_LESS3_ADMIN_API_KEY`. Set a URL to an empty string to hide that service.

The in-compose OTLP endpoint is Tempo, which ingests traces only. Each service also pushes OTLP metrics and logs that Tempo declines. The OTLP exporter drops those quietly and Prometheus and Loki still receive the data through their own paths. In production, point `OtlpEndpoint` at an OpenTelemetry Collector (or Grafana Cloud OTLP) that routes each signal.

## Dashboard map

All dashboards live in the **NetLedger** Grafana folder (`Assets/grafana/`). Each one has a `Service` (`job`) selector and a dropdown that links to the others.

| Dashboard | UID | Answers |
| --- | --- | --- |
| NetLedger - Overview | `netledger-overview` | Are both services up? What are the request rate, 5xx ratio, p95, errors per minute, ledger failure ratio, DB p95, archive freshness, and request-history backlog? Shows build info, safe config, recent warning logs (Loki), and recent failed traces (Tempo). Start here. |
| NetLedger - HTTP | `netledger-http` | Which route is slow or failing? Rate by route and status, p50/p95/p99, top routes by p95, 4xx/5xx by route, unmatched routes, Watson exceptions, connections, bytes. |
| NetLedger - Ledger | `netledger-ledger` | Which ledger operation is slow or failing, and why (error type)? Entries created, committed, and canceled, commit size, invalid balance chains, account lock wait p95, locks held, lock timeouts. |
| NetLedger - Database | `netledger-database` | Is the database the bottleneck? Round trips and p95 by component (active DB or archive catalog) and verb, p99 by provider, failures by error type, in-flight versus max pool size. |
| NetLedger - Archival Pipeline | `netledger-archive` | Is archival keeping up, and which stage is stuck? Jobs by outcome, job p95, per-stage p95 and failures, the queued stage, rows and bytes, worker runs, accounts by result, retries, last run and last success, Archive Server migration events, workflow stage p95, archival logs, and recent run traces. |
| NetLedger - Integrations & Storage | `netledger-integrations` | Is a downstream the cause? Calls, errors, and p95 by service and operation (Archive Server, NetLedger introspection, S3, filesystem), error ratio, storage bytes, introspection cache hit ratio. |
| NetLedger - Auth & Request History | `netledger-security` | Authentication attempts by method and result, auth p95, logins by outcome, rejected authentications by result, authorization decisions and deny ratio by resource, authz p95, request-history writes, p95, and backlog. |
| NetLedger - Runtime | `netledger-runtime` | .NET process health: CPU, working set, GC collections, heap, pause time, allocation rate, thread pool, lock contention, exceptions. |

## Recommended alerts (PromQL)

Tune thresholds to your traffic. Each rule names the dashboard to open first.

```yaml
groups:
  - name: netledger
    rules:
      - alert: NetLedgerTargetDown          # Overview
        expr: up{job=~"netledger-.*"} == 0
        for: 2m
      - alert: NetLedgerHttp5xxRatioHigh    # HTTP
        expr: sum by (job) (rate(http_server_request_duration_seconds_count{job=~"netledger-.*",http_response_status_code=~"5.."}[5m])) / clamp_min(sum by (job) (rate(http_server_request_duration_seconds_count{job=~"netledger-.*"}[5m])), 1e-9) > 0.05
        for: 10m
      - alert: NetLedgerHttpP95High         # HTTP
        expr: histogram_quantile(0.95, sum by (le, job) (rate(http_server_request_duration_seconds_bucket{job=~"netledger-.*"}[5m]))) > 1
        for: 10m
      - alert: NetLedgerLedgerFailures      # Ledger
        expr: sum by (operation, error_type) (rate(netledger_ledger_operations_total{outcome="failure",error_type!~"KeyNotFoundException|ArgumentException|ArgumentNullException"}[5m])) > 0.1
        for: 10m
      - alert: NetLedgerBalanceChainInvalid # Ledger
        expr: increase(netledger_ledger_balance_chain_verifications_total{result="invalid"}[15m]) > 0
      - alert: NetLedgerAccountLockTimeouts # Ledger
        expr: increase(netledger_ledger_lock_wait_duration_seconds_count{outcome="timeout"}[10m]) > 0
      - alert: NetLedgerDatabaseErrors      # Database
        expr: sum by (job, component, error_type) (rate(netledger_db_client_operations_total{outcome!="success"}[5m])) > 0.1
        for: 5m
      - alert: NetLedgerDatabaseP95High     # Database
        expr: histogram_quantile(0.95, sum by (le, job, component) (rate(netledger_db_client_operation_duration_seconds_bucket[5m]))) > 0.5
        for: 10m
      - alert: NetLedgerArchiveExportFailing     # Archival Pipeline
        expr: increase(netledger_archive_export_jobs_total{outcome="failure"}[30m]) > 0
      - alert: NetLedgerAutomaticArchivalStale   # Archival Pipeline
        expr: |
          (time() - max(netledger_archive_automatic_last_success_seconds))
            > 3 * max(netledger_config{setting="archive.automatic.interval_seconds"})
          and on() max(netledger_config{setting="archive.automatic.enabled"}) == 1
        for: 15m
      - alert: NetLedgerArchiveBatchRejected     # Archival Pipeline
        expr: increase(netledger_archive_migration_events_total{event="batch_rejected"}[15m]) > 0
      - alert: NetLedgerArchiveVerificationInvalid
        expr: increase(netledger_archive_verifications_total{result="invalid"}[1h]) > 0
      - alert: NetLedgerIntegrationErrors        # Integrations
        expr: sum by (service, operation) (rate(netledger_integration_requests_total{outcome!~"success|not_found|rejected"}[5m])) > 0.05
        for: 10m
      - alert: NetLedgerRequestHistoryBacklog    # Auth & Request History
        expr: sum by (job) (netledger_request_history_pending) > 500
        for: 10m
      - alert: NetLedgerRejectedLoginSpike       # Auth & Request History
        expr: sum(rate(netledger_auth_logins_total{outcome="rejected"}[5m])) > 1
        for: 10m
```

## Conventions and guarantees

- **One constants class.** Every name lives in `NetLedger.Telemetry.TelemetryNames`. Application families are prefixed with `netledger.`. OpenTelemetry semantic-convention keys are used where one exists (`db.system.name`, `db.operation.name`, `error.type`, `http.response.status_code`, `exception.*`). Units are UCUM (`s`, `By`, `{entry}`).
- **Bounded labels only.** Metric labels come from fixed sets defined in code. Caller-supplied values (authorization resource types) pass through an allow-list and collapse to `other`. Tenant, account, entry, migration, manifest, batch, principal ids, object paths, and messages appear on spans only.
- **No secrets, PII, or payloads.** SQL text, request bodies, credentials, tokens, and passwords are never recorded. Exception messages on spans are truncated to 512 characters.
- **No in-process quantiles.** Histograms export raw buckets, and Grafana computes p50/p95/p99 with `histogram_quantile`.
- **Best-effort.** Every recording path catches its own failures. When nothing listens, a `TelemetryScope` is a shared inert instance, and the cost is a few flag checks.
- **Tested.** `src/Test.Shared/NetLedgerSuites.Telemetry.cs` (suite `telemetry`) proves emission with in-memory BCL listeners. It covers the name contract, the no-listener path, ledger and DB metrics and nested spans, ledger failures, the archive export pipeline with every stage and W3C propagation to the Archive Server, archive export failure paths (HTTP 503, retries, failed accounts, errors), auth and authz decisions with bounded labels, background request-history success and failure with trace joining, object storage and archive catalog telemetry with failures, Archive Server recorders, build info and config gauges, and `traceparent` injection.

## Production notes

- Change Grafana's admin credentials before sharing the stack: set `GRAFANA_ADMIN_USER` and `GRAFANA_ADMIN_PASSWORD` in the environment that runs `docker compose`. Sign-up is disabled.
- Do not expose Prometheus, Tempo, Loki, or the services' metrics ports (`9464`, `9465`) on a public interface. They have no authentication. The compose file keeps the metrics ports internal.
- Point `OtlpEndpoint` at an OpenTelemetry Collector when you run more than one instance. Lower `TraceSamplingRatio` under heavy traffic, since spans are the expensive signal.
- Watson's forwarded-header trust stays off. Turn it on only behind a known proxy (see Watson's `TELEMETRY.md`).
