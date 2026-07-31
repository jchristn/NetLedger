# NetLedger Archival User Guide

NetLedger archival lets you keep the active NetLedger database small without losing retained ledger history. It does this by separating live and cold access:

- NetLedger Server serves active data.
- NetLedger Archive Server serves archived data.
- Archived payloads are stored in an archive storage pool, preferably Less3 or another S3-compatible object store.
- The dashboard and SDKs make the active/archive data source explicit.

NetLedger does not blend active and archived rows into one response. Users query NetLedger Server for active data and query NetLedger Archive Server for archived data.

## What Changes For Users

Active data remains available through the normal NetLedger API and dashboard views. This is the data inside the active retention window.

Archived data is available through the Archive Server API and archive-aware dashboard views. This is data that has been exported from NetLedger Server and committed into the Archive Server catalog and storage pool.

The active server will not silently go to cold storage for an old query. When archival enforcement is enabled, active endpoints return an explicit conflict for archived or split ranges. The user should then run the same historical query against Archive Server.

Archive Server responses identify themselves with:

```text
x-netledger-data-scope: archive
```

NetLedger Server responses identify themselves with:

```text
x-netledger-data-scope: active
```

## Components

NetLedger Server is the active ledger server. It owns live accounts, pending entries, committed active entries, request history, active authentication, tenant configuration, and the export APIs that push old rows to Archive Server.

NetLedger Archive Server is the cold-data server. It has its own `netledger.json`, its own archive catalog, its own request history, and one or more storage pools. It exposes APIs that are consistent with NetLedger Server read patterns but backed by archived data.

Less3 is the preferred local S3-compatible archive provider in the Docker deployment. The repository Compose deployment pins:

```text
jchristn77/less3:v3.0.0
jchristn77/less3-ui:v3.0.0
```

The NetLedger dashboard can be configured with both a NetLedger Server endpoint and a NetLedger Archive Server endpoint. Active views use NetLedger Server; archive views use Archive Server.

## Docker Quick Start

From the repository root:

```bash
cd docker
docker compose up -d
```

The default Compose deployment starts:

| Service | URL | Purpose |
| --- | --- | --- |
| NetLedger Server | `http://localhost:8080` | Active ledger API |
| NetLedger Archive Server | `http://localhost:8081` | Archived ledger API |
| NetLedger Dashboard | `http://localhost:3000` | NetLedger UI |
| Less3 | `http://localhost:8000` | S3-compatible archive object store |
| Less3 UI | `http://localhost:3001` | Less3 UI |

The bundled Less3 configuration persists its runtime files under:

```text
docker/less3/db
docker/less3/disk
docker/less3/temp
docker/less3/logs
```

Less3 v3 container bootstrap creates tenant `default`, user `admin@less3`, password `password`, bucket `default`, and S3 credential `default/default` when it starts with an empty database. The default Archive Server storage pool points to that bucket and credential.

The sample NetLedger user is:

```text
admin@netledger / password
```

The sample credentials are for local development only. Replace them before exposing the deployment.

## Factory Reset

The Docker factory reset is destructive. It removes containers, PostgreSQL data, active SQLite files, archive catalog files, Less3 object data, Less3 temporary files, Less3 logs, and dashboard runtime data for the Compose deployment.

Windows:

```bat
docker\factory\reset.bat
```

Linux or macOS:

```bash
sh docker/factory/reset.sh
```

The reset scripts restore:

- `docker/server/netledger.json`
- `docker/archive-server/netledger.json`
- `docker/less3/system.json`

They remove the Less3 SQLite database and object directories. On the next `docker compose up -d --build`, Less3 v3 runs its container bootstrap path and recreates the default tenant, credential, bucket, and sample content.

## Active Server Configuration

NetLedger Server controls whether archive-aware boundaries and export APIs are active.

```json
{
  "Archive": {
    "Enabled": false,
    "ArchiveServerEndpoint": "http://localhost:8081",
    "ServiceAccessKey": "default",
    "ServiceSecretKey": "default",
    "DefaultActiveDataRetentionDays": 365,
    "Tenants": [
      {
        "TenantId": "ten_example",
        "ActiveDataRetentionDays": 365
      }
    ],
    "Automatic": {
      "Enabled": false,
      "MaxRetentionDays": 365,
      "IntervalSeconds": 3600,
      "InitialDelaySeconds": 30,
      "MaxAccountsPerRun": 100,
      "MaxBatchRows": 50000,
      "DeleteAfterCommit": false,
      "StoragePoolId": "asp_default",
      "Retry": {
        "MaxAttempts": 3,
        "InitialDelaySeconds": 5,
        "MaxDelaySeconds": 300
      }
    }
  }
}
```

`Enabled` turns on archive integration, active-data boundary enforcement, export APIs, and the automatic archival worker host. Set it to `false` to stop all active-to-archive movement.

`ArchiveServerEndpoint` is the Archive Server URL used by NetLedger Server when exporting data.

`ServiceAccessKey` and `ServiceSecretKey` are sent by automatic archival tasks as `x-access-key` and `x-secret-key` when calling Archive Server. In Docker, these are wired to the local `default/default` development credential.

`DefaultActiveDataRetentionDays` defaults to 365 days. Values are clamped to at least 1 day and at most `Int32.MaxValue` days.

Each tenant can override active retention with `Tenants[].ActiveDataRetentionDays`. Tenant values use the same clamp.

`Automatic.Enabled` is the global default for the background archival worker. Account-specific settings can override it. This means an account can opt in even when the global automatic default is false, as long as `Archive.Enabled=true`.

`Automatic.MaxRetentionDays` is the global automatic cutoff. The worker archives committed entries older than this value unless an account override provides its own `MaxRetentionDays`. Values are clamped to 1 through `Int32.MaxValue`.

`Automatic.IntervalSeconds` controls how often the worker wakes up. Account overrides can use their own interval. `InitialDelaySeconds` delays the first run after server startup.

`Automatic.MaxAccountsPerRun` bounds account scans per worker pass. `MaxBatchRows` bounds Archive Server migration batches.

`Automatic.DeleteAfterCommit=false` archives data but leaves active rows in place. `true` deletes committed active rows only after Archive Server commit confirmation and balance-chain safety checks.

`Automatic.Retry` controls per-account retry attempts and backoff when Archive Server or storage is temporarily unavailable.

Environment overrides are also supported:

```text
NETLEDGER_ARCHIVE_ENABLED=true
NETLEDGER_ARCHIVE_SERVER_ENDPOINT=http://archive-server:8081
NETLEDGER_ARCHIVE_SERVICE_ACCESS_KEY=default
NETLEDGER_ARCHIVE_SERVICE_SECRET_KEY=default
NETLEDGER_ARCHIVE_DEFAULT_ACTIVE_DATA_RETENTION_DAYS=365
NETLEDGER_ARCHIVE_AUTO_ENABLED=true
NETLEDGER_ARCHIVE_AUTO_MAX_RETENTION_DAYS=365
NETLEDGER_ARCHIVE_AUTO_INTERVAL_SECONDS=3600
NETLEDGER_ARCHIVE_AUTO_INITIAL_DELAY_SECONDS=30
NETLEDGER_ARCHIVE_AUTO_MAX_ACCOUNTS_PER_RUN=100
NETLEDGER_ARCHIVE_AUTO_MAX_BATCH_ROWS=50000
NETLEDGER_ARCHIVE_AUTO_DELETE_AFTER_COMMIT=false
NETLEDGER_ARCHIVE_AUTO_STORAGE_POOL_ID=asp_default
NETLEDGER_ARCHIVE_AUTO_RETRY_MAX_ATTEMPTS=3
NETLEDGER_ARCHIVE_AUTO_RETRY_INITIAL_DELAY_SECONDS=5
NETLEDGER_ARCHIVE_AUTO_RETRY_MAX_DELAY_SECONDS=300
```

The Docker sample keeps `Archive.Enabled=false` by default so data movement is never accidental. Enable it when you are ready to enforce archive boundaries and run exports.

## Archive Server Configuration

Archive Server uses its own `netledger.json`.

```json
{
  "Authentication": {
    "Enabled": true,
    "Mode": "NetLedgerIntrospection",
    "NetLedgerServerUrl": "http://server:8080"
  },
  "Catalog": {
    "Type": "Sqlite",
    "Filename": "/app/data/netledger.archive.catalog.db"
  },
  "Archive": {
    "DefaultStoragePoolId": "asp_default",
    "RequireCompleteCoverage": true,
    "MaxEnumerationResults": 1000,
    "MaxMigrationBatchRows": 50000,
    "MaxMigrationBatchBytes": 134217728,
    "PreferredFormat": "JsonlGzip"
  },
  "StoragePools": [
    {
      "Id": "asp_default",
      "Name": "Less3 archive",
      "Type": "S3",
      "Bucket": "default",
      "Prefix": "netledger-archive",
      "Region": "us-west-1",
      "Endpoint": "http://less3:8000",
      "AccessKey": "default",
      "SecretKey": "default",
      "Format": "JsonlGzip",
      "Compression": "Gzip"
    }
  ]
}
```

`Catalog` stores archive metadata: manifests, ranges, objects, balance checkpoints, migrations, storage pools, audit records, and Archive Server request history. The archive catalog can use the same physical database as NetLedger Server because archive support tables use non-overlapping table names.

`StoragePools` define where archived payloads are written. The Docker deployment uses Less3 over S3-compatible APIs. Filesystem storage pools are also supported for local or controlled deployments.

Secret-bearing storage settings can be supplied by environment variables:

```text
NETLEDGER_ARCHIVE_STORAGE_TYPE=S3
NETLEDGER_ARCHIVE_STORAGE_BUCKET=default
NETLEDGER_ARCHIVE_STORAGE_PREFIX=netledger-archive
NETLEDGER_ARCHIVE_STORAGE_REGION=us-west-1
NETLEDGER_ARCHIVE_STORAGE_ENDPOINT=http://less3:8000
NETLEDGER_ARCHIVE_STORAGE_ACCESS_KEY=default
NETLEDGER_ARCHIVE_STORAGE_SECRET_KEY=default
```

For production, use TLS, private networking, secret-manager injection, and a least-privilege bucket or prefix policy.

## CORS Configuration

Both NetLedger Server and NetLedger Archive Server have typed CORS settings.

```json
{
  "Webserver": {
    "Cors": {
      "Enabled": true,
      "AllowedOrigins": [ "http://localhost:3000" ],
      "AllowedMethods": [ "OPTIONS", "HEAD", "GET", "PUT", "POST", "DELETE" ],
      "AllowedHeaders": [ "*" ],
      "ExposedHeaders": [ "Content-Type", "x-netledger-data-scope", "x-request-id", "x-hostname", "x-api-version" ],
      "AllowCredentials": false,
      "MaxAgeSeconds": 600
    }
  }
}
```

Use the dashboard origin in `AllowedOrigins`. Avoid wildcard origins with credentials in production.

## How Data Is Archived

Archiving can run automatically in the background or be triggered explicitly through export APIs. Both paths use the same Archive Server migration protocol and the same post-commit cleanup safeguards.

Automatic archival:

1. Configure and start NetLedger Server, Archive Server, and the archive storage provider.
2. Set `Archive.Enabled=true` on NetLedger Server.
3. Set `Archive.Automatic.Enabled=true` for the global default, or enable individual accounts with account archival settings.
4. The background worker wakes up after `InitialDelaySeconds` and then every `IntervalSeconds`.
5. For each due account, the worker resolves the effective policy from global settings plus account overrides.
6. The worker archives committed entries older than `MaxRetentionDays`, in bounded batches.
7. The worker records `LastAttemptUtc`, `LastSuccessUtc`, `LastFailureUtc`, `NextAttemptUtc`, `FailureCount`, `LastError`, and `LastArchivedThroughUtc` in the active database.
8. If `DeleteAfterCommit=true`, active cleanup runs only after Archive Server commit confirmation.

Explicit archival:

1. Choose a tenant, account, and time range that is older than the allowed retention boundary.
2. Call a NetLedger Server export API.
3. NetLedger Server reads committed active rows in bounded batches.
4. Archive Server creates a migration and accepts batch content.
5. Archive Server validates row counts, hashes, ranges, totals, and balance checkpoints.
6. Archive Server commits the migration into manifest, range, object, and checkpoint metadata.
7. Optional active cleanup runs only after Archive Server commit.

The active export endpoints are:

```text
POST /v1/archive/exports/entries
POST /v1/archive/exports/request-history
POST /v1/tenants/{tenantId}/accounts/{accountId}/archive/export
```

Typical export fields:

```json
{
  "TenantId": "ten_example",
  "AccountId": "acct_example",
  "FromUtc": "2024-01-01T00:00:00Z",
  "ToUtc": "2024-12-31T23:59:59Z",
  "StoragePoolId": "asp_default",
  "MaxBatchRows": 50000,
  "DeleteAfterCommit": false
}
```

Use `DeleteAfterCommit=false` for dry runs. Use `DeleteAfterCommit=true` only after you are comfortable that the archive server and storage pool are healthy. For ledger entries, cleanup preserves or creates an active balance anchor before removing old committed rows. Pending entries are not archived.

## Account Automatic Archival Settings

Account settings are stored in the active database table `accountarchivalsettings`. This table is created on server startup for SQLite, MySQL, PostgreSQL, and SQL Server. It does not overlap with NetLedger Archive Server catalog table names.

Account settings override the global automatic policy. Null fields inherit global settings. The same row also stores worker state, so clearing overrides does not erase the account's last archived watermark.

Active Server account settings APIs:

```text
GET    /v1/accounts/{accountId}/archive/settings
PUT    /v1/accounts/{accountId}/archive/settings
DELETE /v1/accounts/{accountId}/archive/settings
GET    /v1/tenants/{tenantId}/accounts/{accountId}/archive/settings
PUT    /v1/tenants/{tenantId}/accounts/{accountId}/archive/settings
DELETE /v1/tenants/{tenantId}/accounts/{accountId}/archive/settings
```

PUT replaces override fields:

```json
{
  "Enabled": true,
  "MaxRetentionDays": 365,
  "IntervalSeconds": 3600,
  "MaxBatchRows": 50000,
  "DeleteAfterCommit": false,
  "StoragePoolId": "asp_default",
  "RetryMaxAttempts": 3,
  "RetryInitialDelaySeconds": 5,
  "RetryMaxDelaySeconds": 300
}
```

DELETE clears override fields and preserves state fields such as `LastArchivedThroughUtc`. This prevents accidental duplicate automatic exports when active cleanup is disabled.

## Querying Active Data

Use NetLedger Server for active data:

```text
GET /v1/accounts/{accountId}/entries
POST /v1/accounts/{accountId}/entries/enumerate
GET /v1/accounts/{accountId}/balance
GET /v1/request-history
GET /v1/request-history/summary
```

When archive enforcement is enabled, old or split ranges are rejected explicitly instead of silently returning incomplete data. A range entirely older than the active boundary can return `DataArchived`. A range that crosses the active boundary can return `DataRangeSplit` unless the API supports and the caller chooses partial active results.

## Querying Archived Data

Use Archive Server for archived data:

```text
GET /v1/archive/accounts/{accountId}/entries
GET /v1/archive/accounts/{accountId}/balance/asof
GET /v1/archive/accounts/{accountId}/verify
GET /v1/request-history
GET /v1/request-history/summary
GET /v1/request-history/{id}
```

Tenant-scoped archive entry routes are also available:

```text
GET /v1/tenants/{tenantId}/accounts/{accountId}/entries
POST /v1/tenants/{tenantId}/accounts/{accountId}/entries/enumerate
GET /v1/tenants/{tenantId}/accounts/{accountId}/balance/asof
GET /v1/tenants/{tenantId}/accounts/{accountId}/verify
```

Archive Server enforces complete archive coverage by default. If a requested cold range is not fully covered by committed archive manifests, Archive Server returns an explicit error. Where supported, callers can set `allowPartial=true` to ask for only the covered archived rows.

## Dashboard Behavior

The dashboard can be configured with:

```text
NETLEDGER_SERVER_URL=http://localhost:8080
NETLEDGER_ARCHIVE_SERVER_URL=http://localhost:8081
```

Users should use active dashboard views for current data and archive dashboard views for cold data. The Entries and Request History views include active/archive selectors where archive querying is available.

The Archive page exposes archive-specific surfaces:

- Archive health
- Coverage ranges
- Manifests
- Manifest objects
- Balance checkpoints
- Storage pools and storage-pool health
- Migration history
- Cold entries
- Cold request history
- Account archive verification
- Object metadata

Administrative archive metadata actions such as verify, quarantine, and supersede are permission-gated.

## Archive Metadata

Archive metadata is the control plane for cold data. It tells the system what was archived, where the objects live, what time ranges are covered, which manifests are committed, and which checksums and totals were verified.

Users and operators should manage archive metadata through the dashboard or Archive Server API. Do not query the archive catalog database directly and do not treat object-store paths as the user contract.

Useful metadata APIs include:

```text
GET /v1/archive/ranges
GET /v1/archive/manifests
GET /v1/archive/manifests/{manifestId}
GET /v1/archive/manifests/{manifestId}/objects
GET /v1/archive/manifests/{manifestId}/checkpoints
GET /v1/archive/objects/{objectId}/metadata
GET /v1/archive/storage-pools
GET /v1/archive/storage-pools/{storagePoolId}/health
GET /v1/archive/migrations
GET /v1/archive/migrations/{migrationId}
```

## What Is Implemented In v4

The v4 archive implementation includes:

- NetLedger Archive Server project and Docker image.
- Archive catalog models and SQL catalog setup.
- Filesystem and S3-compatible archive storage pools.
- Less3-backed Docker deployment using Less3 v3.0.0.
- Less3-tested S3-compatible object-store writes, commits, reads, metadata updates, traversal rejection, and cleanup.
- End-to-end active NetLedger Server to NetLedger Archive Server migration smoke testing with Less3 v3.0.0 S3-compatible storage.
- Active-to-archive export APIs for committed ledger entries and request history.
- Background automatic archival for committed ledger entries with global policy, account overrides, retry/backoff state, and archived-through watermarks.
- Active database `accountarchivalsettings` startup table for account-level automatic archival policy and state.
- Migration lifecycle APIs for create, batch upload, seal, commit, and abort.
- JSONL.Gzip archived entry reads.
- JSONL.Gzip archived request-history reads.
- Compact JSONL.Gzip export validation so archive batches contain one JSON object per line.
- Active boundary enforcement on NetLedger Server.
- Archive metadata APIs and dashboard surfaces.
- C#, JavaScript/TypeScript, and Python SDK archive method groups.
- Live C#, JavaScript/TypeScript, and Python SDK harness coverage against disposable NetLedger Server and NetLedger Archive Server processes.
- `ArchivalValidation` standalone live smoke test that starts disposable NetLedger Server and NetLedger Archive Server processes, exports cold entries, validates Archive Server metadata/search/retrieval, checks hot/cold positive and negative cases, and exercises SDK migration create/upload/seal/commit/abort APIs.
- Automated test coverage for core archive behavior and automatic archival against SQLite, MySQL, PostgreSQL, and SQL Server.

The v4 plan is closed with these explicit product decisions:

- Manual migration recovery is covered by shared catalog/storage recovery validation.
- JSONL.Gzip is the only accepted v4 runtime archive format. Parquet readers, projection pruning, and sidecar indexes are post-v4 work.
- Supported v4 archive reads return exact totals. Approximate-count contracts are deferred until a future high-scale format/index workstream needs them.
- Quarantine is a catalog visibility status in v4. Legal-hold and object-retention automation remain an operator policy outside the v4 runtime.

## Operational Guidance

Start with `DeleteAfterCommit=false` exports and verify Archive Server manifests before enabling cleanup.

Keep independent backups of the active database, archive catalog, and archive object storage. Losing any one of those makes recovery harder.

Use small enough export batches that retries are practical. The defaults are intentionally bounded.

Use separate production credentials for NetLedger, Archive Server, and Less3 or external S3 storage. The sample `default/default` Less3 credential is for local Docker only.

Monitor failed migrations, stuck batches, storage-pool health, manifest verification failures, object hash mismatches, active cleanup failures, and retention backlog.
