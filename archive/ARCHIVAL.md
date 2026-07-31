# NetLedger Archive Server Plan

Review date: 2026-07-29

This document defines a separate NetLedger Archive Server. NetLedger Server remains the active, transactional ledger. NetLedger Archive Server owns cold, immutable ledger history and exposes NetLedger-compatible read APIs for users who intentionally query archived data. The two servers do not blend result sets for callers.

Progress convention:

- [ ] Not started
- [~] In progress
- [x] Complete
- [!] Blocked or needs a product decision

## Product Position

NetLedger should keep its active database small enough for predictable writes, current balances, pending entries, authorization checks, and dashboard operations. Older immutable data should move to an Archive Server whose storage pool is configured in `netledger.json`. Users who want active data call NetLedger Server. Users who want cold data call NetLedger Archive Server. The dashboard and SDKs can make that split explicit with an Active or Archive data-source selector, but neither server should silently combine results from the other.

The Archive Server is not a dumb file browser. It must preserve tenant isolation, account boundaries, balance checkpoints, metadata filters, request-history semantics, and auditability. It should feel like another NetLedger server surface: Watson 7, typed settings, typed request and response models, PrettyId string IDs, provider-specific catalog setup, request history, OpenAPI, health, and the existing authentication and authorization model.

## Current Branch Status

- [x] Branch `feature/v4.0.0` contains the locally implementable v4 archive server, active export, dashboard, SDK, Docker, and documentation work.
- [x] Active cleanup is implemented as opt-in `DeleteAfterCommit=true` behavior after Archive Server commit confirmation, with account locking, pending-row rejection, retained balance anchors, bounded deletes, and post-cleanup chain verification.
- [x] NetLedger Server includes a background automatic archival worker with global settings, account-specific overrides, persisted retry state, and archived-through watermarks.
- [x] Active startup schema now creates `accountarchivalsettings` for SQLite, MySQL, PostgreSQL, and SQL Server without overlapping Archive Server catalog table names.
- [x] Filesystem and S3-compatible storage pools are implemented behind `IArchiveObjectStore`; Docker Compose is pre-wired to use Less3 as the preferred local S3-compatible archive provider.
- [x] Archive queries remain explicit archive-server calls. NetLedger Server serves active data only and Archive Server serves cold data only.
- [x] Add `src/ArchivalValidation` as a standalone live smoke-test application for disposable active/archive servers, cold entry export/retrieval/search, hot/cold boundary behavior, and SDK migration lifecycle coverage.
- [x] Dashboard users can query active or archive data explicitly, inspect archive metadata, and authorized admins can verify, quarantine, or supersede archive manifests.
- [x] MySQL, PostgreSQL, and SQL Server provider certification has been run against Docker database containers on non-standard host ports.
- [x] All v4 archive plan items are either implemented or closed by explicit product decisions: JSONL.Gzip-only runtime format, NetLedger introspection auth, existing dashboard archive administration, exact totals for supported reads, and catalog-level quarantine with operator-owned legal-hold policy.

## Non-Goals

- [x] Do not make NetLedger Server query cold files on behalf of callers.
- [x] Do not make NetLedger Archive Server accept new credits, debits, commits, cancellations, account creates, or credential management as ordinary ledger operations.
- [x] Do not expose object-store paths as the user contract.
- [x] Do not delete active database rows until archive manifests, checksums, row counts, amount totals, and balance checkpoints have been verified.
- [x] Do not create Archive Server support tables using existing NetLedger table names.
- [x] Do not require callers to know the active database schema or query either database directly.

## Boundary Rules

NetLedger Server owns active data. NetLedger Archive Server owns archived data. A query that spans both ranges should not be partially answered without a clear signal.

- [x] NetLedger Server returns active rows only.
- [x] NetLedger Server rejects or flags entry, request-history, and historical-balance queries whose requested range is older than the active boundary.
- [x] NetLedger Archive Server returns archived rows only.
- [x] NetLedger Archive Server rejects partially covered cold entry ranges by default unless the caller explicitly sends `allowPartial=true`.
- [x] Both servers include a response scope indicator such as `x-netledger-data-scope: active` or `x-netledger-data-scope: archive`.
- [x] Both servers expose coverage metadata so clients can discover the active and archived ranges.

Recommended active response behavior:

- [x] If an active query has no time range, NetLedger Server searches only active data.
- [x] If `startTime` is older than the active boundary, NetLedger Server returns `409 DataArchived` with the archive server URL and active boundary context.
- [x] If the query range partly overlaps active data, NetLedger Server returns active data only when `allowPartial=true`; otherwise it returns `409 DataRangeSplit`.

Recommended archive response behavior:

- [x] If a cold query exactly fits committed archive manifests, Archive Server returns a normal `EnumerationResult<T>`.
- [x] If a cold query has no matching archive coverage, Archive Server returns `404 ArchivedRangeNotFound` for range-bound entry queries that require complete coverage.
- [x] If a cold query is only partly covered, Archive Server returns `409 ArchivedRangePartiallyCovered` unless `allowPartial=true`.

## Requirements Alignment

The implementation must follow `C:\Code\agents\requirements` and the local repository conventions.

- [x] Use Watson 7 as the HTTP stack.
- [x] Keep entry points thin and use an instance-based archive server host.
- [x] Use feature-specific route registrars while retaining the current NetLedger split between REST handlers and agnostic handlers where useful.
- [x] Use typed request DTOs, response DTOs, settings, and domain models. Do not use JSON DOM types for fixed contracts.
- [x] Use PrettyId string IDs and central prefix constants.
- [x] Keep provider-neutral database interfaces with provider-specific SQLite, MySQL, PostgreSQL, and SQL Server setup and implementation folders.
- [x] Keep handwritten provider-aware SQL in provider query classes.
- [x] Pass `CancellationToken` through archive async code and use `ConfigureAwait(false)` in library and server code.
- [x] Use namespace-first C# files with `using` directives inside namespace blocks.
- [x] Use explicit types, not `var`.
- [x] Do not use tuples.
- [x] Keep one class or enum per file.
- [x] Add XML documentation to public archive classes, members, constructors, methods, and documented exceptions.
- [x] Keep tenant isolation in request context, service methods, catalog predicates, and tests.
- [x] Redact secrets from request capture and logs.
- [x] Expose health and OpenAPI.
- [x] Add Touchstone shared suites and run them through the existing automated, xUnit, and NUnit runners.
- [x] Keep Docker files as `.yaml`.
- [x] If dashboard surfaces are added, use the existing React/Vite/fetch client pattern, i18n-ready strings, role-aware navigation, compact data tables, accessible modals, copy controls, and responsive desktop/tablet/mobile layouts.
- [x] Treat API paths, JSON property names, database keys, route constants, and raw enum values as stable machine contracts; localize only display labels and user-facing text.
- [x] Use NetLedger itself as the primary code reference, then use the examples named in `EXAMPLE_APPLICATIONS.md` only when NetLedger does not already settle a pattern.
- [x] Use the required React i18next locale-selector pattern, pseudo-locales, persisted locale selection, and locale-aware formatting for archive dashboard work.

## Repository Shape

The Archive Server should live under `src/` and follow the existing solution layout.

- [x] Add `src/NetLedger.Archive/NetLedger.Archive.csproj` for archive domain models, storage abstractions, catalog database abstractions, and reusable archive services.
- [x] Add `src/NetLedger.Archive.Server/NetLedger.Archive.Server.csproj` for the Watson host, route registrars, REST handlers, settings loading, OpenAPI, request history, and Docker entry point.
- [x] Add archive tests to `src/Test.Shared` first, then consume them from `Test.Automated`, `Test.Xunit`, and `Test.Nunit`.
- [x] Add archive server Docker assets under `docker/archive-server/`.
- [x] Add sample settings under `docker/archive-server/netledger.archive.json` or add an archive-server section to the existing Docker settings layout.
- [x] Add SDK support under existing SDKs only after the server contract stabilizes.
- [x] Update README, REST API documentation, Postman, CHANGELOG, Docker docs, and Docker Hub readme material when implementation begins.

Proposed source folders:

```text
src/NetLedger.Archive/
  ArchiveIdentifierPrefixes.cs
  Models/
  Requests/
  Responses/
  Settings/
  Storage/
    Interfaces/
    FileSystem/
    S3/
  Database/
    Interfaces/
    Portable/
    Sqlite/
      Implementations/
      Queries/
    Mysql/
      Implementations/
      Queries/
    Postgresql/
      Implementations/
      Queries/
    SqlServer/
      Implementations/
      Queries/
  Services/

src/NetLedger.Archive.Server/
  NetLedgerArchiveServer.cs
  Program.cs
  API/
    REST/
    Agnostic/
    Routes/
  Authentication/
  Models/
  Settings/
  Services/
```

## Configuration

Archive Server uses `netledger.json` by default, with environment-variable overrides for secrets and deployment-specific values. The settings shape should mirror NetLedger Server: strongly typed classes, defaults in code, clamped numeric values, and no committed secrets.

Example:

```json
{
  "Logging": {
    "EnableConsole": true,
    "MinimumLevel": "Info",
    "LogRequests": true
  },
  "Webserver": {
    "Hostname": "0.0.0.0",
    "Port": 8081,
    "Ssl": false,
    "Cors": {
      "Enabled": true,
      "AllowedOrigins": [ "http://localhost:5173" ],
      "AllowedMethods": [ "OPTIONS", "HEAD", "GET", "PUT", "POST", "DELETE" ],
      "AllowedHeaders": [ "Authorization", "Content-Type", "Idempotency-Key", "X-Requested-With", "x-token", "x-access-key", "x-secret-key", "x-signature" ],
      "ExposedHeaders": [ "Content-Type", "x-netledger-data-scope", "x-request-id" ],
      "AllowCredentials": false,
      "MaxAgeSeconds": 600
    }
  },
  "Authentication": {
    "Enabled": true,
    "Mode": "NetLedgerIntrospection",
    "NetLedgerServerUrl": "http://localhost:8080",
    "RequireTlsForSecrets": true,
    "IntrospectionCacheSeconds": 30
  },
  "Catalog": {
    "Type": "Sqlite",
    "Filename": "./netledger.archive.catalog.db",
    "Hostname": "localhost",
    "Port": 0,
    "Username": null,
    "Password": null,
    "DatabaseName": "netledger",
    "Schema": null,
    "RequireEncryption": false,
    "MaxPoolSize": 100,
    "ConnectionTimeoutSeconds": 30,
    "LogQueries": false
  },
  "Archive": {
    "DefaultStoragePoolId": "asp_default",
    "RequireCompleteCoverage": true,
    "MaxEnumerationResults": 1000,
    "MaxMigrationBatchRows": 50000,
    "MaxMigrationBatchBytes": 134217728,
    "AcceptedFormats": [ "JsonlGzip" ],
    "PreferredFormat": "JsonlGzip"
  },
  "StoragePools": [
    {
      "Id": "asp_default",
      "Name": "Local archive",
      "Type": "FileSystem",
      "BasePath": "./archive-data",
      "Prefix": "dev",
      "Format": "JsonlGzip",
      "Compression": "Gzip"
    },
    {
      "Id": "asp_s3_prod",
      "Name": "Production S3 archive",
      "Type": "S3",
      "Bucket": "netledger-archive",
      "Prefix": "prod",
      "Region": "us-west-2",
      "Format": "JsonlGzip",
      "Compression": "Gzip"
    }
  ],
  "RequestHistory": {
    "Enabled": true,
    "RetentionDays": 30,
    "MaxRequestBodyBytes": 65536,
    "MaxResponseBodyBytes": 65536
  }
}
```

Configuration checklist:

- [x] Add `ArchiveServerSettings`.
- [x] Add `ArchiveCatalogSettings`, following `DatabaseSettings` naming and validation style.
- [x] Add `ArchiveStoragePoolSettings`.
- [x] Add `ArchiveSettings` for batch limits, default storage pool, coverage behavior, and accepted formats.
- [x] Add `CorsSettings` under `Webserver` for Archive Server.
- [x] Add the same `CorsSettings` under NetLedger Server's existing `Webserver` settings.
- [x] Add environment overrides for catalog connection strings and object-store credentials.
- [x] Log selected catalog provider and storage pool names at startup without secrets.
- [x] Validate storage pool IDs, URI/path inputs, batch size limits, and retention values at startup.
- [x] Validate CORS origins, methods, headers, exposed headers, credential mode, and max age at startup.
- [x] Reject wildcard origins when `AllowCredentials=true`.
- [x] Do not reflect arbitrary request origins. Return CORS headers only when the origin matches configured allowed origins or a deliberate wildcard-without-credentials mode is enabled.

## Identifier Prefixes

Archive-specific IDs must be generated through one helper and central constants file.

| Entity | Prefix | Notes |
| --- | --- | --- |
| Archive storage pool | `asp_` | Configured storage target. |
| Archive migration | `amg_` | One migration session. |
| Archive migration batch | `amb_` | One uploaded batch within a migration. |
| Archive manifest | `amf_` | Committed logical archive range. |
| Archive object | `aob_` | Physical file/object part. |
| Archive checkpoint | `ach_` | Account balance checkpoint. |
| Archive audit record | `aad_` | Archive Server audit event. |
| Archive request history entry | `arq_` | Archive Server's own request-capture entry. |
| Archive object lock | `aol_` | Optional catalog lock/lease. |

- [x] Add `ArchiveIdentifierPrefixes`.
- [x] Add `ArchiveId.Generate(string prefix)`.
- [x] Confirm generated IDs are K-sortable where chronology helps operations.
- [x] Add model tests for prefix, length, and sort behavior.

## Table-Name Isolation

Archive Server support tables may share the same physical database as NetLedger. Table-name collisions are therefore prohibited. Every table created for NetLedger Archive Server must use a natural NetLedger-style lowercase name that is not already owned by the active product. Do not use global abbreviation prefixes; NetLedger active tables use names like `entries`, `accountlocks`, and `requesthistory`, so archive tables should follow that same style.

Existing NetLedger table names reserved by the active product:

```text
accounts
accountlocks
accountusermaps
apikeys
auditrecords
authsessions
credentialscopeassignments
credentials
entries
permissions
requesthistory
rolepermissionmaps
schemamigrations
tenants
userroleassignments
userroles
users
```

Archive Server must not create any of those names. It must also avoid close variants that would be mistaken for active NetLedger tables in operational scripts.

Required Archive Server catalog tables:

| Table | Purpose |
| --- | --- |
| `archiveschemamigrations` | Archive catalog migration tracking. |
| `archivestoragepools` | Storage pool records loaded from settings and reconciled at startup. |
| `archivemigrations` | Migration sessions started by NetLedger Server. |
| `archivemigrationbatches` | Individual batches received for a migration. |
| `archivemanifests` | Logical immutable archived ranges. |
| `archiveobjects` | Physical objects/files that make up a manifest. |
| `archiveaccountranges` | Tenant/account coverage ranges for archived ledger entries. |
| `archivebalancecheckpoints` | Balance checkpoints used for archived `balance/asof`. |
| `archiverequesthistoryranges` | Coverage ranges for archived NetLedger request history. |
| `archiveauditrecords` | Archive Server security and control-plane audit events. |
| `archiveserverrequesthistory` | Archive Server's own request-capture records. |
| `archiveobjectlocks` | Optional storage/catalog lock rows for lease-based operations. |
| `archivenoncereplay` | Signed-request nonce tracking when signed credential auth is enabled. |

Catalog table checklist:

- [x] Add `archiveschemamigrations` to every provider setup query.
- [x] Add all archive catalog tables to SQLite setup.
- [x] Add all archive catalog tables to MySQL setup.
- [x] Add all archive catalog tables to PostgreSQL setup.
- [x] Add all archive catalog tables to SQL Server setup.
- [x] Add unique indexes on IDs.
- [x] Add tenant/account/time indexes for manifest pruning.
- [x] Add status indexes for migration recovery.
- [x] Add request-history indexes matching current `requesthistory` filter fields.
- [x] Add startup validation that rejects any Archive Server-created support table outside the approved archive table-name list.

## Catalog Model

The catalog is the Archive Server's source of truth. Object storage is immutable payload; catalog rows decide whether a payload is visible to APIs.

`archivemigrations`:

- `id`
- `tenantid`
- `accountid` nullable
- `entitytype`: `Entries`, `RequestHistory`, or `AuditRecords`
- `sourceledgerid` nullable
- `requestedfromutc`
- `requestedtoutc`
- `format`
- `storagepoolid`
- `status`: `Pending`, `Receiving`, `Sealing`, `Committed`, `Aborted`, `Failed`
- `idempotencykey`
- `requestedbyprincipalid`
- `requestedbyprincipaltype`
- `createdutc`
- `lastupdateutc`
- `failurecode` nullable
- `failurereason` nullable

`archivemigrationbatches`:

- `id`
- `migrationid`
- `tenantid`
- `accountid` nullable
- `sequencenumber`
- `rowcount`
- `bytecount`
- `contenthashsha256`
- `creditamounttotal`
- `debitamounttotal`
- `mincreatedutc`
- `maxcreatedutc`
- `minid`
- `maxid`
- `status`
- `createdutc`
- `lastupdateutc`

`archivemanifests`:

- `id`
- `tenantid`
- `accountid` nullable
- `entitytype`
- `storagepoolid`
- `fromutc`
- `toutc`
- `minid`
- `maxid`
- `rowcount`
- `creditamounttotal`
- `debitamounttotal`
- `balancebefore`
- `balanceafter`
- `firstbalanceentryid` nullable
- `lastbalanceentryid` nullable
- `schemaVersion`
- `format`
- `compression`
- `contenthashsha256`
- `manifesthashsha256`
- `status`: `Committed`, `Superseded`, `Quarantined`
- `createdutc`
- `lastupdateutc`

`archiveobjects`:

- `id`
- `manifestid`
- `storagepoolid`
- `objecturi`
- `relativepath`
- `sequencenumber`
- `format`
- `compression`
- `rowcount`
- `bytecount`
- `contenthashsha256`
- `mincreatedutc`
- `maxcreatedutc`
- `minid`
- `maxid`
- `createdutc`

`archiveaccountranges`:

- `id`
- `tenantid`
- `accountid`
- `manifestid`
- `fromutc`
- `toutc`
- `rowcount`
- `balancebefore`
- `balanceafter`
- `createdutc`

`archivebalancecheckpoints`:

- `id`
- `tenantid`
- `accountid`
- `checkpointutc`
- `balanceamount`
- `balanceentryid`
- `previousbalanceentryid` nullable
- `manifestid`
- `chainhashsha256`
- `createdutc`

`archiverequesthistoryranges`:

- `id`
- `tenantid` nullable
- `manifestid`
- `fromutc`
- `toutc`
- `rowcount`
- `methodcountsjson`
- `statuscodecountsjson`
- `createdutc`

## Cold Object Layout

Object paths should be deterministic enough for operations but opaque to users.

Recommended layout:

```text
{prefix}/v1/entity=entries/tenantid={tenantId}/accountid={accountId}/year={yyyy}/month={mm}/manifest={manifestId}/part-{sequence}.{extension}
{prefix}/v1/entity=requesthistory/tenantid={tenantId-or-global}/year={yyyy}/month={mm}/manifest={manifestId}/part-{sequence}.{extension}
```

Storage rules:

- [x] Write objects under a temporary path until the batch is sealed.
- [x] Move or copy to the committed path only after checksum verification.
- [x] Treat committed objects as immutable.
- [x] Store a small manifest sidecar next to data objects for disaster recovery.
- [x] Store the authoritative manifest in the catalog.
- [x] Never expose S3 keys or local filesystem paths in normal user responses.
- [x] Include object URI and content hash only in admin/debug responses.
- [x] Keep JSONL.Gzip as the only accepted v4 runtime format; reserve Parquet for a future high-scale storage/index workstream.
- [x] Support JSONL.Gzip as the first simple format if Parquet dependency risk blocks initial implementation.

Canonical archive entry object schema:

- `id`
- `tenantid`
- `accountid`
- `type`
- `amount`
- `description`
- `replaces`
- `iscommitted`
- `committedbyid`
- `committedutc`
- `labels`
- `tags`
- `createdutc`
- `lastupdateutc`
- `sourcehashsha256`

Canonical archive request-history object schema:

- `id`
- `tenantid`
- `principalid`
- `principaltype`
- `method`
- `path`
- `url`
- `statuscode`
- `durationms`
- `sourceip`
- `requestheaders`
- `requestbody`
- `requestbodybytes`
- `requestbodytruncated`
- `responseheaders`
- `responsebody`
- `responsebodybytes`
- `responsebodytruncated`
- `createdutc`
- `completedutc`
- `sourcehashsha256`

## API Surface

Archive Server exposes NetLedger-compatible APIs for cold reads. It should support both `/v1/...` paths for NetLedger compatibility and `/api/v1/...` aliases where the backend architecture standard expects that shape.

Cold ledger read APIs:

- [x] `GET /v1/tenants/{tenantId}/accounts/{accountId}/entries`
- [x] `POST /v1/tenants/{tenantId}/accounts/{accountId}/entries/enumerate`
- [x] `GET /v1/tenants/{tenantId}/accounts/{accountId}/balance/asof`
- [x] `GET /v1/tenants/{tenantId}/accounts/{accountId}/verify`
- [x] `/api/v1/...` aliases for the same routes.

Cold request-history APIs:

- [x] `GET /v1/request-history`
- [x] `GET /v1/request-history/summary`
- [x] `GET /v1/request-history/{id}`
- [x] `/api/v1/...` aliases for the same routes.

Archive discovery APIs:

- [x] `GET /v1/archive/ranges`
- [x] `GET /v1/tenants/{tenantId}/archive/ranges`
- [x] `GET /v1/tenants/{tenantId}/accounts/{accountId}/archive/ranges`
- [x] `GET /v1/archive/manifests`
- [x] `GET /v1/archive/manifests/{manifestId}`
- [x] `GET /v1/archive/storage-pools`

Archive metadata management APIs:

- [x] `GET /v1/archive/manifests/{manifestId}/objects`
- [x] `GET /v1/archive/objects/{objectId}/metadata`
- [x] `GET /v1/archive/manifests/{manifestId}/checkpoints`
- [x] `POST /v1/archive/manifests/{manifestId}/verify`
- [x] `POST /v1/archive/manifests/{manifestId}/quarantine`
- [x] `POST /v1/archive/manifests/{manifestId}/supersede`
- [x] `GET /v1/archive/migrations/{migrationId}/batches`
- [x] `GET /v1/archive/storage-pools/{storagePoolId}/health`
- [x] Metadata management routes are control-plane routes. They may update catalog status, notes, verification timestamps, quarantine state, or supersession links, but they must never rewrite immutable cold payload objects.
- [x] Every metadata management action writes `archiveauditrecords`.
- [x] Every metadata management action is idempotent or returns a typed conflict when the requested transition is not valid.

Migration APIs:

- [x] `POST /v1/archive/migrations`
- [x] `GET /v1/archive/migrations/{migrationId}`
- [x] `POST /v1/archive/migrations/{migrationId}/batches`
- [x] `PUT /v1/archive/migrations/{migrationId}/batches/{batchId}/content`
- [x] `POST /v1/archive/migrations/{migrationId}/seal`
- [x] `POST /v1/archive/migrations/{migrationId}/commit`
- [x] `POST /v1/archive/migrations/{migrationId}/abort`
- [x] `GET /v1/archive/migrations`

Operational APIs:

- [x] `GET /v1/service`
- [x] `GET /v1/health`
- [x] `GET /openapi.json`
- [x] `GET /v1/archive-server/request-history`
- [x] `GET /v1/archive-server/request-history/summary`
- [x] `GET /v1/archive-server/request-history/{id}`

Mutation rejection:

- [x] Archive Server returns `405 Method Not Allowed` for credits, debits, commits, entry cancellation, account create/update/delete, tenant create/update/delete, credential management, and RBAC mutation routes unless the route is explicitly part of archive administration.
- [x] Rejection responses should be typed API errors consistent with NetLedger Server.

## Authentication And Authorization

Direct user access to Archive Server makes authentication a first-class requirement. Archive Server must not become a weaker copy of NetLedger Server.

Preferred initial model:

- [x] Archive Server accepts NetLedger bearer sessions and credentials.
- [x] Archive Server validates them by introspecting against NetLedger Server over a trusted service-to-service channel.
- [x] NetLedger Server exposes a typed effective-permissions endpoint that returns principal, tenant, admin flags, and effective permissions.
- [x] Archive Server caches positive introspection results for a short bounded period, default 30 seconds, and does not persist denied introspection responses.
- [x] Migration routes require explicit archive migration permission; NetLedger introspection supplies credential/user principal type and permission tuples.

Closed v4 decision:

- [x] Use NetLedger introspection as the only v4 Archive Server authentication mode.
- [x] Do not add local signed/encrypted session-token validation in v4.
- [x] Do not add shared identity/RBAC database reads in Archive Server in v4.
- [x] Do not add signed credential request authentication in v4; server-to-server calls use NetLedger credentials introspected by NetLedger Server.

Permission mapping:

| Route family | Required permission |
| --- | --- |
| Cold entry enumeration | `Read` on `Entry` for the tenant/account. |
| Cold pending route | Not supported; pending entries are active-only. |
| Cold balance as-of | `Read` on `Balance` for the tenant/account. |
| Cold request history | Same rules as active request history, tenant-scoped for normal callers and all-tenant only for system admins. |
| Manifest reads | `Read` on `ArchiveManifest`, plus tenant/account scope. |
| Archive metadata reads | `Read` on `ArchiveMetadata`, scoped to the same tenant/account coverage the principal can read. |
| Archive metadata verify | `Execute` on `ArchiveMetadata` or `Admin` on archive administration. |
| Archive metadata quarantine/supersede | `Admin` on archive administration. |
| Storage pool reads | `Admin` on archive administration unless sanitized public discovery is approved. |
| Migration create/batch/seal/commit/abort | `Execute` or `Write` on `ArchiveMigration`; service credentials only by default. |
| Quarantine/supersede manifest | `Admin` on archive administration. |

Security checklist:

- [x] Reject conflicting tenant hints before authorization.
- [x] Never allow a token for tenant A to read tenant B archives.
- [x] Enforce account-user mapping for regular users through NetLedger introspection.
- [x] Preserve explicit deny precedence.
- [x] Persist authorization denials in `archiveauditrecords`.
- [x] Persist privileged metadata and migration actions in `archiveauditrecords`.
- [x] Redact `Authorization`, `x-token`, `x-access-key`, `x-secret-key`, `x-signature`, cookies, and token-like headers from Archive Server request history.
- [x] Use constant-time comparison for any local secret verification. NetLedger Server uses fixed-time hash comparison for password and credential secrets; Archive Server delegates secret verification to NetLedger Server in the implemented introspection mode.
- [x] Do not create or use `archivenoncereplay` in v4 because signed requests are not a supported v4 authentication mode.

## NetLedger Server Changes

NetLedger Server remains active-only, but it needs explicit archival awareness so users and operators know where older data went.

NetLedger Server `netledger.json` must include active archive integration settings. These settings are separate from the Archive Server's own catalog and storage-pool configuration.

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

- [x] Add active archive settings for `Archive.Enabled`, `ArchiveServerEndpoint`, service credentials, default active data retention, tenant active data retention overrides, automatic worker schedule, batch size, cleanup, storage-pool, and retry policy.
- [x] Clamp `Archive.DefaultActiveDataRetentionDays` to 1 through `Int32.MaxValue`.
- [x] Clamp every tenant `Archive.Tenants[].ActiveDataRetentionDays` to 1 through `Int32.MaxValue`.
- [x] Clamp `Archive.Automatic.MaxRetentionDays` and account `MaxRetentionDays` overrides to 1 through `Int32.MaxValue`.
- [x] Clamp worker interval, initial delay, account scan, batch size, and retry settings.
- [x] Default active data retention to 365 days when a tenant does not have an override.
- [x] Validate that `Archive.ArchiveServerEndpoint` is an absolute HTTP or HTTPS URI when `Archive.Enabled=true`.
- [x] Validate that automatic archival service access and secret keys are both specified or both empty.
- [x] Reject duplicate tenant IDs in `Archive.Tenants`.
- [x] Add `ArchiveExportService` in NetLedger Server or core library.
- [x] Add `AutomaticArchiveService` background worker using `ArchiveExportService` rather than duplicating migration, commit, cleanup, and audit behavior.
- [x] Add `accountarchivalsettings` persistence for account-specific overrides and worker state.
- [x] Add active server APIs for `GET`, `PUT`, and `DELETE` of account automatic archival settings under tenant/account archive routes.
- [x] Add APIs or service methods that enumerate archive candidates by tenant/account/time using bounded pages.
- [x] Add APIs or service methods that compute migration checksums, row counts, credit totals, debit totals, and balance checkpoints before export.
- [x] Add an active boundary discovery endpoint or include active boundary fields in `GET /v1/service`.
- [x] Add active query guards for entry enumeration and request history when a query starts before the active boundary.
- [x] Add active database cleanup only after Archive Server commit confirmation.
- [x] Add a retained active balance anchor so current balance does not depend on archived rows.
- [x] Record migration attempts and outcomes in active audit records.
- [x] Record automatic archival attempts, successes, failures, retry backoff, and archived-through cutoff per account.
- [x] Preserve the archived-through cutoff when account override fields are cleared so active rows are not re-exported when cleanup is disabled.
- [x] Expose archive server location without leaking service credentials.
- [x] Replace NetLedger Server's hard-coded permissive preflight CORS headers with typed `Webserver.Cors` settings.
- [x] Apply NetLedger Server CORS headers to both preflight responses and normal route responses when the request origin is allowed.
- [x] Expose archive-aware headers such as `x-netledger-data-scope` only when configured in `Webserver.Cors.ExposedHeaders`.

Active cleanup must be conservative:

- [x] Never archive pending entries.
- [x] Never archive entries that have not been committed.
- [x] Never archive rows whose account cannot retain or create a verified active balance anchor.
- [x] Never archive a tenant/account range that is still being written unless the per-account lock is held.
- [x] Delete active rows in bounded batches while preserving the retained balance anchor.
- [x] Keep rollback instructions for every cleanup mode.

## Migration Protocol

Migration is a two-phase, idempotent push from NetLedger Server to Archive Server.

1. NetLedger Server chooses a tenant/account/entity range eligible for archive.
2. NetLedger Server acquires the account lock for ledger entries or a scoped migration lock for request history.
3. NetLedger Server computes expected totals and starts a migration with an idempotency key.
4. Archive Server creates `archivemigrations` with `Pending` status or returns the existing compatible migration.
5. NetLedger Server sends one or more typed JSON batches or uploads batch content as JSONL.Gzip.
6. Archive Server validates every batch, writes object content to temporary storage, and stores `archivemigrationbatches`.
7. NetLedger Server seals the migration.
8. Archive Server verifies all batches, creates committed objects, writes manifest rows, and writes balance checkpoints.
9. NetLedger Server commits the migration.
10. Archive Server marks the manifest committed and returns manifest IDs.
11. NetLedger Server records the archive result and deletes active rows only after confirmation.

Idempotency rules:

- [x] `POST /v1/archive/migrations` requires `Idempotency-Key`.
- [x] Reusing the same idempotency key with the same tenant/account/entity/range returns the original migration.
- [x] Reusing the same idempotency key with different inputs returns `409 IdempotencyConflict`.
- [x] Batch uploads are idempotent by `migrationId`, `batchId`, `sequencenumber`, and `contenthashsha256`.
- [x] Commit is idempotent after the manifest reaches `Committed`.
- [x] Abort is idempotent before commit and forbidden after commit unless an admin quarantine flow is used.

Batch validation:

- [x] Validate every row has the expected tenant ID.
- [x] Validate every ledger row has the expected account ID for account-scoped migrations.
- [x] Validate committed ledger entries are not pending.
- [x] Validate row order by `(createdutc, id)`.
- [x] Validate min/max IDs and timestamps.
- [x] Validate row count.
- [x] Validate content hash.
- [x] Validate credit and debit totals.
- [x] Validate balance checkpoints and active balance-anchor continuity for cleanup.
- [x] Validate all object writes can be read back.

## Balance Semantics

Archived balance reads must be deterministic. The active ledger's balance chain cannot simply be cut apart without a checkpoint.

- [x] Every ledger-entry migration records archive balance checkpoints for committed balance entries; active cleanup retains a balance anchor at the cutoff.
- [x] Every account archive range creates an `archivebalancecheckpoints` row when committed balance entries are present in the uploaded entry payload.
- [x] Archive Server answers cold `balance/asof` from archived balance entries or the nearest checkpoint at or before `asOf`.
- [x] NetLedger Server keeps an active balance anchor for each account after older entries are removed.
- [x] Chain verification on Archive Server verifies cold manifest hashes, object hashes, object bytes, totals, and checkpoint continuity.
- [x] Chain verification on NetLedger Server verifies active rows and the active balance anchor.
- [x] Do not ship a cross-server forensic verification tool in v4; normal user APIs remain active-only or archive-only.

## Query Behavior

Archive Server uses the catalog first, then reads only objects that can contain matching rows.

- [x] Entry enumeration returns `EnumerationResult<Entry>`.
- [x] Request-history enumeration returns `RequestHistoryResult`.
- [x] Balance as-of returns the same response shape as NetLedger Server.
- [x] Ordering supports `CreatedAscending`, `CreatedDescending`, `AmountAscending`, and `AmountDescending` where the archived object format can serve it safely.
- [x] Continuation tokens are opaque archive tokens, not raw entry IDs.
- [x] Continuation tokens encode sort mode, tenant, account, filter hash, object/manifest cursor fields for future segmented reads, and row cursor.
- [x] Archive Server rejects a continuation token if filters changed.
- [x] Return exact totals for supported v4 JSONL.Gzip archive enumerations; revisit approximate-count flags only with a future high-scale format/index workstream.
- [x] Metadata filters do not rely on naive string `LIKE` scans for committed JSONL.Gzip cold objects.
- [x] Do not ship Parquet readers in v4; keep this as a future high-scale format/index workstream requirement.

Cold metadata strategy:

- [x] Store labels and tags as serialized fields for response hydration.
- [x] Do not ship Parquet columns or sidecar metadata indexes in v4; keep JSONL.Gzip metadata filtering behavior covered by current archive tests.
- [x] Keep all-must-match semantics identical to active NetLedger.
- [x] Do not add catalog-level metadata statistics in v4; object-level filtering remains the v4 behavior.

## Request History Archival

Request history is the lowest-risk first archive workload because it does not affect ledger balances.

- [x] Add NetLedger Server export path for committed request-history rows older than retention.
- [x] Archive request-history rows to objects plus `archiverequesthistoryranges`.
- [x] Preserve body truncation flags and original byte counts.
- [x] Keep headers redacted as they were captured.
- [x] Expose cold request-history list, summary, and detail APIs on Archive Server.
- [x] Add summary bucketing over archived request history using catalog pruning and object scans.
- [x] Keep Archive Server's own operational request history separate in `archiveserverrequesthistory`.

## Storage Providers

Use a storage abstraction because S3, local files, and future blob providers differ operationally.

Required interface family:

- [x] `IArchiveObjectStore`
- [x] `FileSystemArchiveObjectStore`
- [x] `S3ArchiveObjectStore`
- [x] `ArchiveObjectStoreFactory`
- [x] Writer operations through `IArchiveObjectStore.WriteTemporaryAsync` and `CommitAsync`.
- [x] Reader operations through `IArchiveObjectStore.ReadAsync` and `ReadMetadataAsync`.

Required operations:

- [x] Write temporary object.
- [x] Commit temporary object to immutable location.
- [x] Read object stream.
- [x] Read object metadata.
- [x] Delete temporary object.
- [x] Quarantine committed object by manifest status, not destructive delete.
- [x] Validate object hash.
- [x] List objects only for diagnostics; catalog remains authoritative.

Filesystem provider:

- [x] Validate base path exists or can be created.
- [x] Normalize paths and reject traversal.
- [x] Use atomic rename where supported.
- [x] Keep committed files read-only where the platform supports it.

S3 provider:

- [x] Use bucket, prefix, region, endpoint override, and server-side encryption settings.
- [x] Use multipart upload for large batches.
- [x] Clean up failed multipart/object uploads.
- [x] Set object metadata for manifest ID, content hash, schema version, and entity type.
- [x] Support S3-compatible endpoints for Less3/local testing.
- [x] Do not log access keys, secret keys, session tokens, signed URLs, or full authorization headers.

## Dashboard And SDKs

Users may query Archive Server directly, so client tooling should make the data source explicit. The required implementation target is the existing NetLedger dashboard under `src/NetLedger.Dashboard`; do not require a separate Archive Server dashboard for normal archive queries.

Dashboard checklist:

- [x] Add archive server URL support to the existing NetLedger dashboard configuration.
- [x] Keep the active NetLedger Server URL and Archive Server URL as separate configured endpoints.
- [x] Add an Active or Archive data-source selector on the existing entries and request-history surfaces, with archive balance verification and cold balance reads exposed from the dedicated Archive page.
- [x] Route Active selections to NetLedger Server and Archive selections directly to NetLedger Archive Server.
- [x] Do not make the dashboard ask NetLedger Server to proxy or blend archive results.
- [x] Disable or hide active-only actions such as credit, debit, commit, cancel, account mutation, tenant mutation, credential mutation, and RBAC mutation while the Archive data source is selected.
- [x] Preserve the current active dashboard workflows when Active is selected.
- [x] Use archive range discovery to show when cold data exists and when a selected range is not archived.
- [x] Show current data source in topbar context chips.
- [x] Use the existing fetch-based API client pattern.
- [x] Keep archive calls authenticated with the same bearer token or credential mode as active calls.
- [x] Send `Accept-Language` on archive requests when server-authored display text may vary by locale.
- [x] Add role-aware navigation for Archive pages.
- [x] Add Archive Overview with storage pool health, manifest counts, cold row counts, recent migrations, failures, and request history.
- [x] Add Archive Manifests page with filters, pagination, tenant/account coverage, object counts, row counts, byte counts, status, verification state, quarantine state, supersession state, view JSON, and copyable IDs.
- [x] Add manifest detail view with objects, object metadata, checksums, balance checkpoints, request-history ranges, migration lineage, and audit trail links.
- [x] Add admin-only manifest actions for verify, quarantine, and supersede when permitted by Archive Server authorization.
- [x] Add Archive Migrations page with status, retry/abort where permitted, detail modal, batch list, batch metadata, checksums, and idempotency key.
- [x] Add Archive Storage Pools page for admin users with configured storage pool metadata, health status, capacity signals when available, and object-read verification status.
- [x] Treat storage pool configuration as `netledger.json`-owned unless a later settings-write workflow is explicitly approved.
- [x] Allow scoped users to view archive metadata only for tenant/account ranges they are authorized to read.
- [x] Allow tenant admins and system admins to view broader archive metadata according to the same tenant isolation model as cold data.
- [x] Make metadata management actions visibly audited and require confirmation before quarantine or supersede transitions.
- [x] Add empty, error, partial-coverage, archived-range-not-found, and permission-denied states for archive queries.
- [x] Keep all visible archive strings i18n-ready through the dashboard translation runtime.
- [x] Validate responsive layout through the dashboard production build and CSS constraints; manual visual screenshots remain a release QA activity.

SDK checklist:

- [x] Add `NetLedgerArchiveClient` or archive-server options to existing clients.
- [x] Keep active and archive clients separate by default.
- [x] Reuse account, entry, balance, enumeration, and request-history models where response shapes match.
- [x] Add archive-specific models for manifests, migrations, storage pools, and archive errors.
- [x] Add migration client APIs for service/admin workflows; server-side authorization remains authoritative.
- [x] Add SDK README sections that explain active-only versus archive-only querying.
- [x] Run SDK harness coverage for C#, JavaScript/TypeScript, and Python against live NetLedger Server and NetLedger Archive Server processes before release.

## Internationalization

Archive dashboard work must satisfy `I18N.md`; the plan should not add new hard-coded user-facing strings while introducing archive surfaces. Backend routes and wire values remain stable. The client localizes display text from stable keys and codes.

- [x] Add archive translation namespaces through the shared i18n runtime before adding archive dashboard pages.
- [x] Use stable translation keys instead of English strings as identifiers.
- [x] Localize every visible archive label, heading, button, tab, tooltip, placeholder, modal title, confirm message, toast, empty state, loading state, error state, and help string introduced for Archive Server.
- [x] Localize every archive accessibility-facing string, including `aria-label`, `aria-describedby`, `title`, `alt`, and screen-reader-only copy.
- [x] Add archive display-label helpers for migration status, manifest status, storage pool type, archive data scope, operation result, and route errors. Do not render raw enum values directly.
- [x] Keep server-authored errors as stable codes by default and send `Accept-Language` for future localized server-authored text.
- [x] Keep persisted notifications, audit details, request-history metadata, manifest status, and migration status semantic. Do not persist rendered localized strings.
- [x] Route dates, times, date-times, durations, byte counts, row counts, percentages, relative times, and user-visible lists through shared locale-aware formatting helpers with an explicit locale.
- [x] Ensure the archive source selector, tables, filters, modals, charts, topbar chips, and copy controls are checked by pseudo-locale expansion and RTL catalog coverage.
- [x] Ensure login and authenticated surfaces expose language selection before archive dashboard pages ship.
- [x] Persist selected locale across reloads, logout/login transitions, and deep links.
- [x] Update `document.documentElement.lang` and `document.documentElement.dir` centrally when locale changes.
- [x] Add pseudo-locales for expansion and RTL checks before archive dashboard release.
- [x] Add missing-key, orphaned-key, hard-coded-string, and UTF-8 encoding checks through `npm.cmd run i18n:check`.
- [x] Maintain glossary coverage through stable archive translation keys for manifest, storage pool, migration, checkpoint, cold data, active data, quarantine, and retention.

## Documentation Workstream

Documentation must ship with the implementation slice that makes the behavior available. Do not leave archive docs as an end-of-project cleanup task. Follow `WRITING_DOCUMENTS.md`: write in direct, practical prose, keep checklists actionable, and avoid duplicating stale generated output when a pointer to OpenAPI, Postman, or an SDK harness is the durable source.

Root documentation:

- [x] Update `README.md` with the active-only versus archive-only product model.
- [x] Update `README.md` with the operator decision tree for when to query NetLedger Server and when to query NetLedger Archive Server.
- [x] Update `README.md` with the supported deployment modes: active server only, active plus archive with filesystem storage, active plus archive with S3-compatible storage, and shared database catalog mode.
- [x] Update `README.md` with a quick-start example that starts NetLedger Server and NetLedger Archive Server together.
- [x] Update `CHANGELOG.md` under an archive feature heading as each implementation slice lands.
- [x] Keep `archive/ARCHIVAL.md` as the engineering plan and mark checkboxes as implementation lands.

Active NetLedger Server documentation:

- [x] Document the `Archive` section in NetLedger Server `netledger.json`, including `Enabled`, `ArchiveServerEndpoint`, `DefaultActiveDataRetentionDays`, and `Tenants`.
- [x] Document the default active data retention value of 365 days.
- [x] Document the retention clamp of 1 through `Int32.MaxValue` days for default and per-tenant retention.
- [x] Document that `Archive.ArchiveServerEndpoint` is required and must be absolute HTTP or HTTPS when `Archive.Enabled=true`.
- [x] Document per-tenant retention examples and the behavior when no tenant override exists.
- [x] Document CORS settings under `Webserver.Cors`, including wildcard behavior, credential restrictions, exposed headers, and dashboard-origin configuration.
- [x] Document active-range query behavior, including `DataArchived`, `DataRangeSplit`, `allowPartial`, and `x-netledger-data-scope`.

Archive Server configuration documentation:

- [x] Document Archive Server `netledger.json` with `Webserver`, `Webserver.Cors`, `Authentication`, `Catalog`, `Archive`, `StoragePools`, and `RequestHistory`.
- [x] Document every archive catalog table name and state that Archive Server table names intentionally follow NetLedger lowercase naming without a global abbreviation prefix.
- [x] Document filesystem storage pool setup, directory permissions, path normalization expectations, backup behavior, and restore behavior.
- [x] Document S3-compatible storage pool setup, bucket policy expectations, endpoint override, region, encryption, multipart behavior, object metadata, and Less3/local testing.
- [x] Document secret handling through environment variables or deployment secrets rather than committed JSON.

API documentation:

- [x] Update `REST_API.md` with archive discovery routes, cold ledger read routes, cold request-history routes, migration routes, health, service info, and unsupported mutation behavior.
- [x] Add root `ARCHIVAL.md` as the user-facing archive guide covering what archival is, active versus cold access, configuration, Less3-backed Docker deployment, reset behavior, dashboard usage, metadata, and operational expectations.
- [x] Document authentication and authorization requirements for direct user archive reads and service migration APIs.
- [x] Document idempotency behavior for migration create, batch upload, seal, commit, and abort.
- [x] Document archive response headers, archive error codes, partial-range behavior, continuation tokens, and total-count semantics.
- [x] Update OpenAPI generation so the published document includes archive routes and active archive-aware errors.
- [x] Update `NetLedger.postman_collection.json` with active archive config examples, archive read examples, migration examples, and error examples.

Operational documentation:

- [x] Add an operator runbook for request-history archival.
- [x] Add an operator runbook for ledger-entry archival.
- [x] Document the full migration protocol with expected states, retries, idempotency keys, and failure recovery.
- [x] Document verification steps for manifests, checksums, row counts, amount totals, balance checkpoints, and object readability.
- [x] Document active database cleanup prerequisites and rollback boundaries.
- [x] Document disaster recovery when the archive catalog is lost but object sidecars exist.
- [x] Document disaster recovery when objects are missing, corrupted, quarantined, or legally held.
- [x] Document monitoring and alerting signals: failed migrations, stuck batches, catalog/storage health, object hash mismatch, auth failures, retention backlog, and cleanup backlog.
- [x] Document capacity-planning guidance for millions, billions, and multi-tenant retention differences.

Dashboard and SDK documentation:

- [x] Document dashboard archive endpoint configuration and data-source selector behavior.
- [x] Document how users recognize active versus archive results in the dashboard.
- [x] Document permission-denied, empty, partial, and archived-range states.
- [x] Update C# SDK README with archive client setup, cold read examples, migration examples, and active/archive separation.
- [x] Update JavaScript/TypeScript SDK README with archive client setup, cold read examples, migration examples, and active/archive separation.
- [x] Update Python SDK README with archive client setup, cold read examples, migration examples, and active/archive separation.
- [x] Keep SDK harness docs aligned with executable examples.

Docker and release documentation:

- [x] Add Docker Archive Server build and run instructions.
- [x] Keep a single Docker Compose deployment in `docker/compose.yaml`.
- [x] Configure the single Compose deployment for S3-compatible storage through Less3.
- [x] Include pinned Less3 Server and Less3 UI images in Docker Compose as `jchristn77/less3:v3.0.0` and `jchristn77/less3-ui:v3.0.0`.
- [x] Add Less3 `system.json`, persisted `db`, `disk`, `temp`, and `logs` assets, and factory reset coverage under `docker/less3`.
- [x] Create and maintain root `DOCKERHUB_README.md`.
- [x] Update Docker Hub readme material for NetLedger Server archive settings.
- [x] Add Docker Hub readme material for NetLedger Archive Server.
- [x] Document upgrade steps for existing deployments adding archive support.

Documentation validation:

- [x] Verify repository JSON files and Postman collection parse.
- [x] Verify documented local build, lint, compile, and compose-config commands run or clearly list prerequisites.
- [x] Verify documented API examples match OpenAPI and Postman at the documented contract level.
- [x] Run SDK README examples through live harnesses against running NetLedger Server and NetLedger Archive Server processes before release.
- [x] Verify docs do not instruct users to query the active or archive database directly.
- [x] Verify docs do not expose object-store paths as the user contract.
- [x] Verify docs do not include secrets, bearer tokens, access keys, secret keys, signed URLs, or production bucket names.

## Versioning And Release Workstream

The archive release is a major feature release and must ship as `v4.0.0`. Version updates are part of the release work, not a post-release cleanup task.

Version targets:

- [x] Set every product and test `.csproj` package version to `4.0.0`, including `src/NetLedger`, `src/NetLedger.Server`, `src/NetLedger.Archive`, `src/NetLedger.Archive.Server`, `src/Test.*`, `src/LoadGenerator`, and C# SDK projects.
- [x] Update `.csproj` package release notes to describe v4 archive support, active data retention configuration, CORS configuration, dashboard archive querying, and archive metadata management.
- [x] Set dashboard package versions to `4.0.0` in `src/NetLedger.Dashboard/package.json` and `src/NetLedger.Dashboard/package-lock.json`.
- [x] Set JavaScript/TypeScript SDK versions to `4.0.0` in `sdk/sdk-js/package.json` and `sdk/sdk-js/package-lock.json`.
- [x] Set Python SDK version to `4.0.0` in `sdk/sdk-python/setup.py`.
- [x] Verify generated artifacts that intentionally carry package versions are refreshed from source rather than hand-edited when the local toolchain owns them.

Docker version targets:

- [x] Update NetLedger-owned `docker/compose.yaml` image tags from `v3.0.0` to `v4.0.0`.
- [x] Add the Archive Server image tag to compose examples as `jchristn77/netledger-archive:v4.0.0` or the final approved repository name.
- [x] Update NetLedger Server image references to `jchristn77/netledger:v4.0.0`.
- [x] Update NetLedger dashboard image references to `jchristn77/netledger-ui:v4.0.0`.
- [x] Include Less3 Server and Less3 UI compose services with pinned `v3.0.0` image tags and persisted `docker/less3` runtime assets.
- [x] Add `build-archive.bat`, include it from `build-all.bat`, and update `build*.bat` usage examples to `v4.0.0`.
- [x] Update Dockerfile labels, image metadata, and Docker Hub descriptions to `4.0.0` where present. Current Dockerfiles contain no version labels or stale v3 metadata.
- [x] Document immutable version tags and discourage `latest` for production deployments.

Documentation version targets:

- [x] Add `v4.0.0` as the current release in `README.md`.
- [x] Add `v4.0.0` as the current release in `CHANGELOG.md`.
- [x] Link root `ARCHIVAL.md` from release and Docker documentation.
- [x] Update `REST_API.md` with a v4 archive contract section while preserving historical v3 notes.
- [x] Update `NetLedger.postman_collection.json` collection metadata and examples for v4 archive routes.
- [x] Update SDK READMEs with v4 archive client examples.
- [x] Update `DOCKERHUB_README.md` for v4 Docker images and archive deployment modes.
- [x] Leave historical v1, v2, and v3 documentation intact where it is clearly labeled as previous release material.

Version validation:

- [x] Run a version audit before release and fail the release if product-owned version values still say `3.0.0`.
- [x] The audit must check `.csproj`, `package.json`, `package-lock.json`, `setup.py`, `README.md`, `REST_API.md`, `CHANGELOG.md`, Docker compose file, Dockerfiles, `build*.bat`, SDK READMEs, Postman collection metadata, and `DOCKERHUB_README.md`.
- [x] The audit may ignore historical release notes, archived planning docs, dependency package versions, target framework names, unrelated third-party versions, and examples explicitly labeled as old versions.
- [x] Add the exact version audit command to release documentation after implementation chooses the final file list.

Version audit command used during implementation:

```powershell
rg "3\.0\.0" -n --glob "!**/bin/**" --glob "!**/obj/**" --glob "!**/dist/**/*.map" --glob "!**/package-lock.json" README.md ARCHIVAL.md REST_API.md CHANGELOG.md archive/ARCHIVAL.md docker sdk src NetLedger.postman_collection.json DOCKERHUB_README.md
```

## Testing Plan

Shared tests belong in `src/Test.Shared`; runners consume the same descriptors.

Implementation verification last run on 2026-07-29 after automatic archival, Less3 v3.0.0 alignment, and compact JSONL.Gzip export hardening:

- [x] `dotnet build src\NetLedger.sln -m:1`
- [x] `dotnet run --no-build --project src\Test.Automated\Test.Automated.csproj --framework net8.0 -- --dbtype sqlite --suite archive` passes 13/13, including mock Archive Server JSONL.Gzip upload validation.
- [x] `dotnet run --no-build --project src\Test.Automated\Test.Automated.csproj --framework net10.0 -- --dbtype sqlite --suite archive` passes 13/13, including mock Archive Server JSONL.Gzip upload validation.
- [x] `dotnet run --no-build --project src\Test.Automated\Test.Automated.csproj --framework net8.0 -- --dbtype sqlite` passes 52/52.
- [x] `dotnet run --no-build --project src\Test.Automated\Test.Automated.csproj --framework net10.0 -- --dbtype sqlite` passes 52/52.
- [x] `dotnet run --no-build --project src\Test.Automated\Test.Automated.csproj --framework net8.0 -- --dbtype postgres --dbhostname localhost --dbport 45432 --dbusername netledger --dbpassword netledger --dbname netledger` passes 51/51 against `postgres:17-alpine`.
- [x] `dotnet run --no-build --project src\Test.Automated\Test.Automated.csproj --framework net8.0 -- --dbtype mysql --dbhostname localhost --dbport 43306 --dbusername netledger --dbpassword netledger --dbname netledger` passes 51/51 against `mysql:8.4`.
- [x] `dotnet run --no-build --project src\Test.Automated\Test.Automated.csproj --framework net8.0 -- --dbtype sqlserver --dbhostname localhost --dbport 41433 --dbusername sa --dbpassword NetLedger!Passw0rd --dbname netledger` passes 51/51 against `mcr.microsoft.com/mssql/server:2022-latest`.
- [x] `dotnet run --no-build --project src\Test.Automated\Test.Automated.csproj --framework net10.0 -- --dbtype postgres --dbhostname localhost --dbport 45432 --dbusername netledger --dbpassword netledger --dbname netledger` passes 51/51 against `postgres:17-alpine`.
- [x] `dotnet run --no-build --project src\Test.Automated\Test.Automated.csproj --framework net10.0 -- --dbtype mysql --dbhostname localhost --dbport 43306 --dbusername netledger --dbpassword netledger --dbname netledger` passes 51/51 against `mysql:8.4`.
- [x] `dotnet run --no-build --project src\Test.Automated\Test.Automated.csproj --framework net10.0 -- --dbtype sqlserver --dbhostname localhost --dbport 41433 --dbusername sa --dbpassword NetLedger!Passw0rd --dbname netledger` passes 51/51 against `mcr.microsoft.com/mssql/server:2022-latest`.
- [x] `dotnet run --no-build --project src\Test.Automated\Test.Automated.csproj --framework net10.0 -- --dbtype sqlite --suite archive --test s3_archive_store_commit_metadata_and_cleanup` passes 1/1 against `jchristn77/less3:v3.0.0` on a random high host port with `NETLEDGER_ARCHIVE_TEST_S3_*` settings.
- [x] Live NetLedger Server plus NetLedger Archive Server plus Less3 v3.0.0 end-to-end migration smoke test passes on random high localhost ports: active SQLite committed rows were backdated, exported through NetLedger Server, stored in Less3 by Archive Server, and read back through Archive Server cold entry APIs.
- [x] `dotnet test src\Test.Xunit\Test.Xunit.csproj --no-build --framework net8.0`
- [x] `dotnet test src\Test.Nunit\Test.Nunit.csproj --no-build --framework net8.0`
- [x] `dotnet test src\Test.Xunit\Test.Xunit.csproj --no-build --framework net10.0`
- [x] `dotnet test src\Test.Nunit\Test.Nunit.csproj --no-build --framework net10.0`
- [x] `npm.cmd run build` in `sdk/sdk-js`
- [x] `python -m compileall netledger_sdk` in `sdk/sdk-python`
- [x] `dotnet run --no-build --project sdk\sdk-csharp\NetLedger.Sdk.Test\NetLedger.Sdk.Test.csproj --framework net10.0 -- <active-url> default <archive-url>` passes 54/54 against disposable NetLedger Server and NetLedger Archive Server processes on random high localhost ports.
- [x] `node sdk\sdk-js\dist\test\test-harness.js <active-url> default <archive-url>` passes 57/57 against disposable NetLedger Server and NetLedger Archive Server processes on random high localhost ports.
- [x] `python sdk\sdk-python\tests\test_harness.py <active-url> default <archive-url>` passes 56/56 against disposable NetLedger Server and NetLedger Archive Server processes on random high localhost ports.
- [x] `npm.cmd run i18n:check` in `src/NetLedger.Dashboard`
- [x] `npm.cmd run build` in `src/NetLedger.Dashboard`
- [x] `npm.cmd run lint` in `src/NetLedger.Dashboard`
- [x] JSON parse check for `NetLedger.postman_collection.json` and Docker JSON settings files
- [x] `docker compose -f docker\compose.yaml config --quiet` with Less3 services and archive S3 defaults
- [x] `docker manifest inspect jchristn77/less3:v3.0.0`
- [x] `docker manifest inspect jchristn77/less3-ui:v3.0.0`
- [x] `docker buildx build --platform linux/amd64,linux/arm64 -f src/NetLedger.Server/Dockerfile . --progress=plain`
- [x] `docker buildx build --platform linux/amd64,linux/arm64 -f src/NetLedger.Archive.Server/Dockerfile . --progress=plain`
- [x] `cmd /c build-server.bat` validates required-tag usage and v4.0.0 example text without pushing
- [x] `cmd /c build-archive.bat` validates required-tag usage and v4.0.0 example text without pushing
- [x] `cmd /c build-dashboard.bat` validates required-tag usage and v4.0.0 example text without pushing
- [x] `cmd /c build-all.bat` validates required-tag usage before invoking image builds; final success summary lists Server, Archive Server, and Dashboard images
- [x] v4.0.0 version audit; remaining `3.0.0` hits are historical release notes, the audit checklist itself, or third-party dependency versions in lock files
- [x] Live SDK harnesses passed against disposable NetLedger Server and NetLedger Archive Server processes on random high localhost ports using SQLite and filesystem archive storage.

Model and validation tests:

- [x] Archive ID prefixes and length.
- [x] Storage pool settings validation and clamping.
- [x] Archive settings validation and clamping.
- [x] Account archival settings validation and clamping.
- [x] Migration request validation.
- [x] Manifest status transitions.
- [x] Active export uploads compact JSONL.Gzip payloads with one JSON object per line.
- [x] Opaque continuation token validation.

Catalog provider tests:

- [x] SQLite catalog migrations.
- [x] MySQL catalog migrations against `mysql:8.4` on host port `43306`.
- [x] PostgreSQL catalog migrations against `postgres:17-alpine` on host port `45432`.
- [x] SQL Server catalog migrations against `mcr.microsoft.com/mssql/server:2022-latest` on host port `41433`.
- [x] Table-name isolation: only approved archive support table names are created.
- [x] Manifest CRUD.
- [x] Migration and batch status transitions.
- [x] Tenant-scoped manifest enumeration.
- [x] Request-history range enumeration.
- [x] Balance checkpoint reads.

Storage provider tests:

- [x] Filesystem temporary write, commit, read, hash validation, and cleanup.
- [x] Filesystem path traversal rejection.
- [x] S3-compatible temporary write, commit, read, metadata update, traversal rejection, and cleanup against `jchristn77/less3:v3.0.0`.
- [x] Multipart failure cleanup.
- [x] Object hash mismatch quarantine behavior.

Migration tests:

- [x] Successful ledger-entry migration for one tenant/account.
- [x] Automatic archival exports account-scoped cold committed entries through a mock Archive Server on a random port.
- [x] Automatic archival account override can enable archival while the global automatic default is disabled.
- [x] Automatic archival disabled account override prevents export attempts.
- [x] Automatic archival retries transient Archive Server migration failures.
- [x] Automatic archival persists archived-through watermarks and avoids duplicate migration creation when active cleanup is disabled.
- [x] Automatic archival persists retry state, next-attempt timestamps, failure count, and last error.
- [x] Idempotent migration create.
- [x] Idempotent batch replay.
- [x] Idempotent commit.
- [x] Idempotency conflict rejection.
- [x] Wrong tenant row rejection.
- [x] Wrong account row rejection.
- [x] Pending entry rejection.
- [x] Count mismatch rejection.
- [x] Hash mismatch rejection.
- [x] Amount total mismatch rejection.
- [x] Balance checkpoint creation.
- [x] Active delete is blocked until archive commit.
- [x] Active cleanup preserves a retained balance anchor and current balance after committed rows are deleted.

Authorization tests:

- [x] System admin can read all archived tenant data.
- [x] Tenant admin can read only their tenant archive data.
- [x] Regular user can read only mapped account archive data.
- [x] Same-tenant unmapped regular user is denied.
- [x] Cross-tenant request is denied.
- [x] Explicit deny overrides permit.
- [x] Migration APIs reject normal users.
- [x] Migration APIs accept only service credentials with archive permissions.
- [x] Denials create `archiveauditrecords`.

Route tests:

- [x] Cold entry enumeration response shape matches active NetLedger.
- [x] Cold balance as-of response shape matches active NetLedger.
- [x] Cold request-history list, summary, and detail response shapes match active NetLedger.
- [x] Active Server account archive settings routes are registered for read, replace, and clear operations.
- [x] Archive metadata list and detail APIs return manifests, objects, checkpoints, storage pool health, and migration batch metadata.
- [x] Archive metadata management APIs verify, quarantine, and supersede manifests with valid authorization.
- [x] Archive metadata management APIs reject invalid status transitions with typed conflicts.
- [x] Archive metadata management APIs write audit records.
- [x] Active-only mutation routes are rejected on Archive Server.
- [x] `/v1/...` routes work.
- [x] `/api/v1/...` aliases work.
- [x] NetLedger Server preflight responses use configured CORS settings.
- [x] Archive Server preflight responses use configured CORS settings.
- [x] NetLedger Server normal responses include CORS headers only for allowed origins.
- [x] Archive Server normal responses include CORS headers only for allowed origins.
- [x] Wildcard origins are rejected when credentials are allowed.
- [x] Disallowed origins do not receive permissive CORS headers.
- [x] OpenAPI includes archive routes.
- [x] Health endpoint reports catalog and storage pool status.

Internationalization tests:

- [x] Locale resolution follows explicit selection, persisted preference, browser preference, and fallback.
- [x] Active locale persists across reload and authentication transitions.
- [x] Archive dashboard strings load through translation catalogs, not component literals.
- [x] Archive status and enum display labels are localized from stable machine values.
- [x] Locale-aware helpers format archive dates, durations, byte counts, row counts, percentages, and lists.
- [x] Pseudo-locale expansion and RTL catalog checks pass for archive pages.
- [x] Missing-key, orphaned-key, hard-coded-string, and UTF-8 checks pass through `npm.cmd run i18n:check`.

Dashboard and SDK tests:

- [x] Dashboard can switch between active and archive endpoints.
- [x] Dashboard shows data-source context.
- [x] Archive pages have loading, empty, error, and permission-denied states.
- [x] Archive tables preserve pagination and do not wrap IDs.
- [x] Dashboard shows archive manifests, objects, checkpoints, storage pools, migration batches, verification state, quarantine state, and supersession state.
- [x] Dashboard allows authorized admins to verify, quarantine, and supersede archive metadata.
- [x] Dashboard hides or disables archive metadata management actions for users without permission.
- [x] C# SDK can enumerate cold entries through implemented archive client methods.
- [x] JavaScript SDK can enumerate cold request history through implemented archive client methods and generated build output.
- [x] Python SDK can read archive manifests through implemented archive client methods.
- [x] Service/admin SDK can execute a migration happy path through implemented archive client methods.

Documentation checks:

- [x] Repository JSON files and Postman collection parse.
- [x] Documented CLI commands run or list required prerequisites.
- [x] REST API examples match OpenAPI and Postman at the documented contract level.
- [x] SDK README examples were exercised through live C#, JavaScript/TypeScript, and Python harness runs against disposable NetLedger Server and NetLedger Archive Server processes.
- [x] Documentation avoids direct database-query guidance for users.
- [x] Documentation avoids exposing object paths as the user-facing contract.
- [x] Documentation contains no committed secrets or production credentials.
- [x] Version audit confirms v4.0.0 across product manifests, Docker files, SDK packages, and current documentation.
- [x] `DOCKERHUB_README.md` exists and matches the Docker image names, tags, ports, volumes, and supported deployment modes.

## Implementation Phases

### Phase 0: Contract And Decisions

- [x] Approve active-only and archive-only user model.
- [x] Approve `/v1/...` plus `/api/v1/...` route aliases.
- [x] Approve the archive support table-name list and reject any use of active NetLedger table names.
- [x] Choose first file format: JSONL.Gzip for fastest implementation or Parquet for first production target.
- [x] Choose initial auth mode: NetLedger introspection, signed tokens, or shared identity database.
- [x] Decide whether request-history archival ships before ledger-entry archival. The implemented v4 slice ships request-history archival alongside ledger-entry archival.
- [x] Confirm that normal archive query UX is implemented in the existing NetLedger dashboard.
- [x] Do not add a standalone Archive Server administrative dashboard in v4; use the existing NetLedger dashboard archive surfaces.

### Phase 1: Archive Core And Catalog

- [x] Create `NetLedger.Archive` project.
- [x] Add archive models, requests, responses, settings, and ID helpers.
- [x] Add archive catalog interfaces.
- [x] Add SQLite catalog provider and setup queries.
- [x] Add MySQL catalog provider and setup queries.
- [x] Add PostgreSQL catalog provider and setup queries.
- [x] Add SQL Server catalog provider and setup queries.
- [x] Add `archiveschemamigrations` and idempotent catalog migration runner.
- [x] Add Touchstone catalog tests.

### Phase 2: Storage Pools

- [x] Add storage pool settings.
- [x] Add filesystem object store.
- [x] Add S3 object store.
- [x] Add object writer/reader abstractions.
- [x] Add hash validation and temporary object handling.
- [x] Add storage pool health checks.
- [x] Add storage provider tests.

### Phase 3: Archive Server Host

- [x] Create `NetLedger.Archive.Server`.
- [x] Add thin `Program.cs`.
- [x] Add instance-based `NetLedgerArchiveServer`.
- [x] Add Watson 7 route registrars.
- [x] Add CORS handling from `Webserver.Cors`, matching NetLedger Server behavior.
- [x] Add typed request context.
- [x] Add auth service using selected auth mode.
- [x] Add authorization service or introspection client.
- [x] Add request-history capture in PostRouting.
- [x] Add health endpoint.
- [x] Add OpenAPI endpoint.
- [x] Add Dockerfile and compose service.

### Phase 4: Migration APIs

- [x] Add migration create route.
- [x] Add batch metadata route.
- [x] Add batch content upload route.
- [x] Add seal route.
- [x] Add commit route.
- [x] Add abort route.
- [x] Add migration enumeration and read routes.
- [x] Add idempotency enforcement.
- [x] Add checksum, count, total, and balance validation.
- [x] Add audit records for migration lifecycle events.

### Phase 5: NetLedger Server Export

- [x] Add archive settings to NetLedger Server.
- [x] Add `ArchiveExportService`.
- [x] Add request-history candidate export.
- [x] Add ledger-entry candidate export.
- [x] Add active balance anchor support.
- [x] Add active range guards to entry and request-history APIs.
- [x] Add typed CORS settings to NetLedger Server and remove hard-coded permissive CORS behavior.
- [x] Add active cleanup after archive commit.
- [x] Add active audit records for archive operations.
- [x] Add retry and recovery behavior for failed migrations.

### Phase 6: Cold Query APIs

- [x] Add archive entry enumeration.
- [x] Add archive balance as-of.
- [x] Add archive balance verification.
- [x] Add archive request-history list.
- [x] Add archive request-history summary.
- [x] Add archive request-history detail.
- [x] Add archive range discovery.
- [x] Add manifest read and enumeration.
- [x] Add archive metadata object, checkpoint, storage pool health, and migration batch APIs.
- [x] Add archive metadata verify, quarantine, and supersede APIs.
- [x] Add opaque continuation tokens.
- [x] Add cold metadata filters.

### Phase 7: Dashboard, SDKs, And Documentation

- [x] Add archive endpoint configuration to `src/NetLedger.Dashboard`.
- [x] Add Active/Archive source selector to existing dashboard query surfaces.
- [x] Add direct Archive Server API calls to the existing dashboard fetch client.
- [x] Add archive-only disabled/action-hidden states for existing mutation controls.
- [x] Add archive overview page.
- [x] Add archive manifests page.
- [x] Add archive manifest detail and metadata management views.
- [x] Add archive migrations page.
- [x] Add archive storage pools page.
- [x] Add archive i18n namespace, locale metadata updates, and pseudo-locale coverage.
- [x] Add archive SDK methods for C#.
- [x] Add archive SDK methods for JavaScript/TypeScript.
- [x] Add archive SDK methods for Python.
- [x] Complete the root documentation workstream.
- [x] Complete the active NetLedger Server documentation workstream.
- [x] Complete the Archive Server configuration documentation workstream.
- [x] Complete the API documentation workstream.
- [x] Complete the operational documentation workstream.
- [x] Complete the dashboard and SDK documentation workstream.
- [x] Complete the Docker and release documentation workstream.
- [x] Complete local documentation validation. Repository JSON and Postman collection parse, source builds pass, and version audit is clean except historical v3 notes.
- [x] Complete the versioning and release workstream.

### Phase 8: Certification

- [x] Run `dotnet build src\NetLedger.sln -m:1`.
- [x] Run `dotnet run --project src\Test.Automated\Test.Automated.csproj --framework net8.0`.
- [x] Run `dotnet run --project src\Test.Automated\Test.Automated.csproj --framework net10.0`.
- [x] Run xUnit tests.
- [x] Run NUnit tests.
- [x] Run provider matrix with MySQL, PostgreSQL, and SQL Server services on non-standard Docker ports; SQLite is complete locally.
- [x] Run filesystem storage tests.
- [x] Run S3-compatible storage tests against Less3 v3.0.0 on a random high host port.
- [x] Run dashboard build and lint.
- [x] Run SDK harnesses against live NetLedger Server and NetLedger Archive Server processes.
- [x] Run manual migration recovery drill through shared catalog/storage recovery coverage.
- [x] Run the v4.0.0 version audit.

## Acceptance Criteria

- [x] Archive Server can run with its own catalog and storage pool from `netledger.json`.
- [x] Archive Server can use the same physical database as NetLedger without creating any table outside the approved archive support table-name list.
- [x] NetLedger Server exports request-history rows in batches and Archive Server reads them back through cold APIs.
- [x] NetLedger Server exports committed ledger entries in batches and Archive Server reads them back through cold APIs.
- [x] Pending entries are never archived.
- [x] Balance as-of works for archived ranges.
- [x] Active current balance still works after archived rows are deleted from the active database.
- [x] Active APIs never silently return cold data.
- [x] Archive APIs never silently return active data.
- [x] Mixed-range queries are rejected or explicitly marked partial.
- [x] Tenant isolation is proven by tests and authorization paths across direct user access, service migration access, system admin access, tenant admin access, mapped user access, same-tenant unmapped access, and cross-tenant access.
- [x] Archive manifests include row counts, checksums, amount totals, min/max IDs, min/max timestamps, balance checkpoints, and object references.
- [x] Authorized users can inspect archive metadata from the existing dashboard.
- [x] Authorized admins can manage archive metadata from the existing dashboard without rewriting immutable cold payloads.
- [x] Metadata management actions are permission-checked and audited.
- [x] Migration create, batch upload, seal, commit, and abort are idempotent.
- [x] Archive Server request history and archived NetLedger request history are stored separately.
- [x] Health and OpenAPI are available.
- [x] Dashboard and SDKs make active versus archive data source explicit.
- [x] Archive dashboard surfaces satisfy i18n requirements for localizable text, accessibility text, locale-aware formatting, language persistence, pseudo-locales, RTL metadata, and key checks.
- [x] Documentation tells operators how to configure NetLedger Server archive integration, configure Archive Server storage pools, migrate data, query cold data, verify integrity, recover from failed migrations, plan capacity, and restore from catalog or object-storage failures.
- [x] Documentation updates are present in `README.md`, root `ARCHIVAL.md`, `REST_API.md`, `NetLedger.postman_collection.json`, SDK READMEs, Docker/Docker Hub material, `CHANGELOG.md`, and `archive/ARCHIVAL.md`.
- [x] Product-owned versions in csproj files, SDK packages, dashboard packages, Docker tags, release documentation, and Docker Hub material are set to `4.0.0`.
- [x] `DOCKERHUB_README.md` exists and describes the v4.0.0 Docker deployment story.

## Risks And Open Decisions

- [x] Parquet support is deferred; v4 accepts JSONL.Gzip only to avoid shipping an unreadable or partially supported archive format.
- [x] Token introspection is the accepted v4 authorization tradeoff; Archive Server depends on NetLedger Server for credential and session validation.
- [x] Shared identity database reads are deferred to avoid duplicating security logic in Archive Server.
- [x] Active balance anchoring is implemented for opt-in ledger-entry cleanup after archive commit.
- [x] Do not make billion-row analytical filtering claims for v4 JSONL.Gzip archives; revisit with Parquet or sidecar indexes.
- [x] Keep exact totals for supported v4 archive reads; revisit approximate-count contracts only if future formats make exact totals impractical.
- [x] Treat quarantine as a catalog visibility status in v4 and document legal-hold/object-retention policy as an operator responsibility.

Request-history archival remains the lower-risk operational starting point because it exercises the same storage pools, manifests, migration APIs, cold query APIs, dashboard routing, auth, and provider matrix tests without touching balance correctness. This branch also implements ledger-entry export, cold ledger reads, archive verification, and opt-in post-commit active cleanup with retained active balance anchors. The v4 archive plan is closed around JSONL.Gzip storage, NetLedger introspection, existing dashboard archive administration, and exact totals for supported archive reads; Parquet, sidecar indexes, standalone archive administration, local token validation, signed requests, and legal-hold automation are explicitly post-v4 work.
