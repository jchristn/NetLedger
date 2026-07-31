# NetLedger Docker Images

NetLedger provides Docker images for running the REST server and dashboard. The v4.0.0 release adds the Archive Server deployment model so operators can keep active ledger databases small while retaining cold data in filesystem or S3-compatible storage. The repository Docker Compose assets use Less3 as the preferred local S3-compatible archive provider.

## Images

Use immutable version tags in production.

| Image | Purpose |
| --- | --- |
| `jchristn77/netledger:v4.0.0` | NetLedger Server for active ledger data. |
| `jchristn77/netledger-ui:v4.0.0` | NetLedger dashboard. |
| `jchristn77/netledger-archive:v4.0.0` | NetLedger Archive Server for cold data. |

The bundled Compose deployment also uses these pinned companion images:

| Image | Purpose |
| --- | --- |
| `jchristn77/less3:v3.0.0` | S3-compatible object storage for archived payloads. |
| `jchristn77/less3-ui:v3.0.0` | Less3 object-store dashboard. |

See repository `ARCHIVAL.md` for the user guide covering active versus archived data, Less3-backed Docker setup, reset behavior, and archive operations.

The `latest` tag is convenient for local evaluation, but production deployments should pin to a version tag such as `v4.0.0`.

## NetLedger Server

NetLedger Server exposes the active REST API. Mount a `netledger.json` file into the container and pass it with `-f`.

```bash
docker run --rm \
  -p 8080:8080 \
  -v ./server:/app/data \
  jchristn77/netledger:v4.0.0 \
  -f /app/data/netledger.json
```

Minimal active-server archive configuration:

```json
{
  "Archive": {
    "Enabled": false,
    "ArchiveServerEndpoint": "http://localhost:8081",
    "ServiceAccessKey": "default",
    "ServiceSecretKey": "default",
    "DefaultActiveDataRetentionDays": 365,
    "Tenants": [],
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

Set `Archive.Enabled` to `true` only after the Archive Server endpoint is reachable and migration credentials have been configured. `Archive.Automatic.Enabled` is the global default for the background archival worker; account-specific settings can override it. Per-tenant active retention overrides belong in `Archive.Tenants`.

Deployment-owned active database and archive endpoint values can be overridden without editing `netledger.json` by setting `NETLEDGER_DATABASE_*`, `NETLEDGER_ARCHIVE_ENABLED`, `NETLEDGER_ARCHIVE_SERVER_ENDPOINT`, `NETLEDGER_ARCHIVE_SERVICE_ACCESS_KEY`, `NETLEDGER_ARCHIVE_SERVICE_SECRET_KEY`, `NETLEDGER_ARCHIVE_DEFAULT_ACTIVE_DATA_RETENTION_DAYS`, and the `NETLEDGER_ARCHIVE_AUTO_*` automatic archival settings.

When enabled, NetLedger Server keeps active APIs active-only. Entry, request-history, and historical balance queries outside the active retention window return a typed `409` response that points callers at the configured Archive Server endpoint. Use `POST /v1/archive/exports/entries` or `POST /v1/tenants/{tenantId}/accounts/{accountId}/archive/export` on NetLedger Server to push committed entries into Archive Server migration batches. Use `POST /v1/archive/exports/request-history` to push active request-history rows into Archive Server. The background worker can also push committed account entries automatically according to global policy plus account-level overrides. `DeleteAfterCommit=true` is opt-in cleanup: active rows are removed only after Archive Server commit succeeds, and the response reports `ActiveCleanupExecuted` plus `ActiveCleanupRowsDeleted`.

## Archive Server

NetLedger Archive Server is the cold-data API. It owns archive catalog metadata and object storage. Users query it directly for archived data; NetLedger Server remains active-only.

Example filesystem storage deployment:

```bash
docker run --rm \
  -p 8081:8081 \
  -v ./archive-server:/app/data \
  -v ./archive-data:/archive-data \
  jchristn77/netledger-archive:v4.0.0 \
  -f /app/data/netledger.json
```

Archive Server storage pools are configured in its `netledger.json`. This implementation supports local filesystem and S3-compatible archive storage with catalog-backed JSONL.Gzip cold entry and request-history reads.

Archive catalog settings can be overridden with `NETLEDGER_ARCHIVE_CATALOG_*` variables that match the database setting suffixes. The default storage pool can be overridden with `NETLEDGER_ARCHIVE_STORAGE_BASE_PATH`, `NETLEDGER_ARCHIVE_STORAGE_BUCKET`, `NETLEDGER_ARCHIVE_STORAGE_PREFIX`, `NETLEDGER_ARCHIVE_STORAGE_REGION`, `NETLEDGER_ARCHIVE_STORAGE_ENDPOINT`, `NETLEDGER_ARCHIVE_STORAGE_ACCESS_KEY`, `NETLEDGER_ARCHIVE_STORAGE_SECRET_KEY`, `NETLEDGER_ARCHIVE_STORAGE_SESSION_TOKEN`, `NETLEDGER_ARCHIVE_STORAGE_SERVER_SIDE_ENCRYPTION`, `NETLEDGER_ARCHIVE_STORAGE_FORMAT`, and `NETLEDGER_ARCHIVE_STORAGE_COMPRESSION`. Use `NETLEDGER_ARCHIVE_STORAGE_{POOL_ID}_*` for a named pool, with non-alphanumeric pool ID characters replaced by `_`. These secret-bearing values stay in process settings and are not returned by archive metadata APIs.

The repository Compose deployment starts Less3 by default and points Archive Server at `http://less3:8000` with Less3's seeded `default` bucket, `default/default` credential, and the `netledger-archive` object prefix. Less3 persists its SQLite catalog, object data, temporary uploads, and logs under `docker/less3/`.

The single `compose.yaml` starts NetLedger Server, NetLedger Archive Server, NetLedger Dashboard, Less3, and Less3 UI together:

```bash
cd docker
docker compose up -d
```

Production deployments should replace the sample Less3 `default/default` credential with secret-manager injection, TLS, and a least-privilege bucket or prefix policy.

Archive Server validates NetLedger bearer sessions and credentials by calling NetLedger Server's effective-permission API. Keep `Authentication.Enabled=true`, set `Authentication.Mode=NetLedgerIntrospection`, and point `Authentication.NetLedgerServerUrl` at the active server. Configure `Webserver.Cors` on both servers with the dashboard origin before exposing the containers outside local development.

## Request-History Archival Runbook

1. Confirm NetLedger Server `Archive.Enabled=true`, `Archive.ArchiveServerEndpoint` points at Archive Server, and both servers use compatible CORS settings for the dashboard origin.
2. Confirm Archive Server has initialized its catalog and the target storage pool reports healthy at `GET /v1/archive/storage-pools/{storagePoolId}/health`.
3. Choose a tenant and a `ToUtc` at or before the tenant's active boundary from NetLedger Server `GET /`.
4. Call `POST /v1/archive/exports/request-history` on NetLedger Server with `TenantId`, `ToUtc`, optional `FromUtc`, optional `StoragePoolId`, and a stable `IdempotencyKey`.
5. Confirm the response includes `MigrationId`, `ManifestId`, exported row count, and batch checksums.
6. Query Archive Server directly with `GET /v1/request-history`, `GET /v1/request-history/summary`, or `GET /v1/request-history/{id}` using the same tenant credential.
7. Leave `DeleteAfterCommit=false` for dry runs. Set `DeleteAfterCommit=true` only when the exported tenant/range is ready to be removed from active request history after Archive Server commit.

## Ledger-Entry Archival Runbook

1. Confirm the target account has no pending entries in the archive range and that `ToUtc` is at or before the active boundary returned by NetLedger Server `GET /`.
2. Call `POST /v1/archive/exports/entries` or `POST /v1/tenants/{tenantId}/accounts/{accountId}/archive/export` with `TenantId`, `AccountId`, `FromUtc`, `ToUtc`, optional `StoragePoolId`, and a stable `IdempotencyKey`.
3. Confirm the response includes `MigrationId`, `ManifestId`, exported row count, uploaded byte count, and batch checksums.
4. Query Archive Server directly with `GET /v1/tenants/{tenantId}/accounts/{accountId}/entries`, `GET /v1/tenants/{tenantId}/accounts/{accountId}/balance/asof`, and `GET /v1/tenants/{tenantId}/accounts/{accountId}/verify`.
5. Use `GET /v1/archive/manifests/{manifestId}`, `/objects`, `/checkpoints`, and `/archive/objects/{objectId}/metadata` to inspect catalog and object metadata.
6. Leave `DeleteAfterCommit=false` for dry runs. Set `DeleteAfterCommit=true` only after confirming the archive manifest is committed and the account can be pruned. Ledger-entry cleanup holds the account lock, preserves or creates a committed balance anchor at the cutoff, deletes committed rows in bounded batches, and verifies the remaining active balance chain.

## Migration Protocol And Recovery

Migrations are idempotent. Create the migration with `Idempotency-Key`, upload batch metadata, upload JSONL.Gzip batch content, seal, then commit. Reusing the same idempotency key with the same tenant, account, entity type, and range returns the same migration; changing those inputs returns a typed conflict.

If a batch upload fails before commit, rerun the same export with the same idempotency key. If a manifest is committed but later fails verification, use the Archive Server metadata action to quarantine or supersede the manifest; committed object payloads are not rewritten or destructively deleted by metadata actions.

## Verification

Before accepting an archive migration as operationally complete, verify:

- The export response row counts, byte counts, and SHA-256 hashes match the migration batches.
- Archive manifest `RowCount`, `CreditTotal`, `DebitTotal`, min/max timestamps, and object references are present for the archived entity type.
- Archive object metadata reports the object exists, byte counts match the catalog, and filesystem objects are read-only where the platform supports it.
- Cold entry or request-history queries return the expected tenant/account/range and never blend active results.
- Active NetLedger Server still rejects or flags ranges older than the active retention boundary.

## Cleanup Boundaries

Active cleanup is opt-in. Keep `DeleteAfterCommit=false` until the export range, tenant/account scope, and downstream retention policy have been reviewed. With `DeleteAfterCommit=true`, NetLedger Server deletes only after Archive Server commit succeeds. Entry cleanup preserves an active balance anchor at the archive cutoff and re-verifies the active balance chain; request-history cleanup deletes the exported tenant/range scope. Do not manually delete active `entries` or `requesthistory` rows outside the export workflow.

## Disaster Recovery

If the archive catalog is lost but object sidecars remain, restore the catalog from backup first. Sidecar manifests can help identify committed objects and ranges, but they are not a supported automatic catalog rebuild path in v4.0.0.

If an object is missing, corrupted, quarantined, or legally held, keep the affected manifest non-visible by status and rerun the migration with a new idempotency key only after confirming the active source rows still exist. Do not overwrite committed objects in place.

## Monitoring And Capacity

Monitor failed or stuck migrations, batch upload failures, seal/commit conflicts, catalog health, storage-pool health, object metadata read failures, object hash mismatches, auth failures, active retention backlog, and cleanup backlog. For millions of rows, keep batch sizes bounded and use filesystem or S3-compatible object storage with reliable backup. v4 accepts JSONL.Gzip archive objects only; plan Parquet plus sidecar indexes as a post-v4 high-scale read path before making billion-row analytical filtering claims.

## Upgrade Notes

Existing deployments can add archive support by upgrading product artifacts to `v4.0.0`, adding the `Archive` section to NetLedger Server `netledger.json`, deploying NetLedger Archive Server with its own `netledger.json`, configuring CORS for the dashboard origin on both servers, and setting the dashboard Archive Server URL. Leave `Archive.Enabled=false` until Archive Server health, storage-pool health, and NetLedger introspection authentication are verified.

## Dashboard

The dashboard connects to NetLedger Server for active data and directly to NetLedger Archive Server for cold data. Configure both endpoints in the dashboard deployment once archive support is enabled.

```bash
docker run --rm \
  -p 3000:80 \
  -e NETLEDGER_SERVER_URL=http://localhost:8080 \
  -e NETLEDGER_ARCHIVE_SERVER_URL=http://localhost:8081 \
  jchristn77/netledger-ui:v4.0.0
```

The dashboard keeps active workflows on the existing Accounts, Entries, Tenants, Users, Credentials, and Request History pages. Cold data is queried from the Archive page and from the Active/Archive source selectors on Entries and Request History. Archive calls use the Archive Server endpoint directly for archive metadata, archived entry reads, archived request-history reads, and account verification.

## Compose

The repository includes one Docker Compose file under `docker/compose.yaml`. For v4.0.0 deployments, it should pin images to:

- `jchristn77/netledger:v4.0.0`
- `jchristn77/netledger-ui:v4.0.0`
- `jchristn77/netledger-archive:v4.0.0`
- `jchristn77/less3:v3.0.0`
- `jchristn77/less3-ui:v3.0.0`

Use PostgreSQL, MySQL, SQL Server, or SQLite for the active database according to the server configuration. Archive Server may use its own catalog database or a shared physical database with non-overlapping archive table names.

## Security Notes

- Do not put production secrets in committed `netledger.json` files.
- Use TLS in front of public deployments.
- Configure `Webserver.Cors` with explicit dashboard origins for production.
- Do not expose direct database access to users.
- Treat object-store paths as operational details, not as user-facing contracts.
- Pin image versions rather than relying on `latest`.

## Documentation

See the repository documentation for the full API, SDK, Postman, dashboard, and archive migration details:

- `README.md`
- `ARCHIVAL.md`
- `REST_API.md`
- `archive/ARCHIVAL.md`
- `NetLedger.postman_collection.json`
- SDK README files under `sdk/`
