# NetLedger REST API Documentation

This document provides comprehensive documentation for the NetLedger.Server REST API.

## Table of Contents

- [Overview](#overview)
  - [v4.0.0 Archive Contract](#v400-archive-contract)
  - [v3.0.0 Tenant And Metadata Contract](#v300-tenant-and-metadata-contract)
- [Authentication](#authentication)
- [Common Response Headers](#common-response-headers)
- [Error Responses](#error-responses)
- [API Endpoints](#api-endpoints)
  - [Service Endpoints](#service-endpoints)
  - [Account Endpoints](#account-endpoints)
  - [Entry Endpoints](#entry-endpoints)
  - [Balance and Commit Endpoints](#balance-and-commit-endpoints)
  - [Credential Management Endpoints](#credential-management-endpoints)
  - [Request History Endpoints](#request-history-endpoints)
  - [Active Archive Export and Settings Endpoints](#active-archive-export-and-settings-endpoints)
  - [Archive Server Endpoints](#archive-server-endpoints)

---

## Overview

The NetLedger REST API provides programmatic access to ledger operations including account management, credit/debit entries, balance queries, and transaction commits. The API uses JSON for all request and response bodies.

**Base URL**: `http://localhost:8080` (configurable)

**API Version**: v1

**OpenAPI document**: `GET /openapi.json`

The dashboard API Explorer loads `GET /openapi.json` and executes requests with the current signed-in session. Request History is available in the dashboard and through the `/v1.0/api/request-history` API aliases.

### v4.0.0 Archive Contract

NetLedger v4.0.0 keeps active APIs under `/v1` and introduces archive-aware server configuration plus the Archive Server contract. NetLedger Server remains active-only.

NetLedger Archive Server is queried directly for cold data by clients such as the existing NetLedger dashboard and SDK archive clients.

Active server archive settings:

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

Retention values clamp to 1 through `Int32.MaxValue` days. The minimum effective retention window is one day.

Archive settings have three levels of granularity:

- Server-level `Archive` settings control whether archival exists, which Archive Server is used, and the default retention and automatic archival policy.
- Tenant retention overrides can adjust the active-data retention window for all accounts in a tenant.
- Account archive settings can override automatic archival behavior for one account while preserving worker state.

When archiving is enabled, `ArchiveServerEndpoint` must be an absolute HTTP or HTTPS URI. `Archive.ServiceAccessKey` and `Archive.ServiceSecretKey` are sent by background automatic archival tasks when calling Archive Server.

Both service credential fields must be specified together or left empty.

`Archive.Automatic.Enabled` is the global default for the background worker. Account-specific settings can override it; `Archive.Enabled=false` still disables all archive movement.

The worker archives committed entries older than the effective `MaxRetentionDays`, writes worker state to `accountarchivalsettings`, retries per policy, and optionally performs active cleanup after Archive Server commit.

Deployment overrides are available for archive-related operational values.

NetLedger Server accepts `NETLEDGER_DATABASE_*`, `NETLEDGER_ARCHIVE_ENABLED`, `NETLEDGER_ARCHIVE_SERVER_ENDPOINT`, `NETLEDGER_ARCHIVE_SERVICE_ACCESS_KEY`, `NETLEDGER_ARCHIVE_SERVICE_SECRET_KEY`, and `NETLEDGER_ARCHIVE_DEFAULT_ACTIVE_DATA_RETENTION_DAYS`.

It also accepts the `NETLEDGER_ARCHIVE_AUTO_*` / `NETLEDGER_ARCHIVE_AUTOMATIC_*` settings for automatic archival, schedule, batching, cleanup, storage-pool, and retry policy.

NetLedger Archive Server accepts `NETLEDGER_ARCHIVE_CATALOG_*` for catalog database settings and `NETLEDGER_ARCHIVE_STORAGE_*` or `NETLEDGER_ARCHIVE_STORAGE_{POOL_ID}_*` for storage-pool paths, S3-compatible endpoint fields, and secret material.

Storage-pool secret overrides are runtime-only and are not exposed by archive metadata responses.

Archive reads, archive metadata management, and migration APIs are implemented by NetLedger Archive Server. NetLedger Server must not silently blend active and cold rows in one response.

`GET /` and `GET /v1/service` on NetLedger Server include an `Archive` object with `Enabled`, `ArchiveServerEndpoint`, `ActiveDataRetentionDays`, and `ActiveBoundaryUtc` so clients can show the active/cold cutoff without probing archived ranges.

When archiving is enabled, NetLedger Server enforces the active-data boundary before returning entries, request history, and historical balances. Requests wholly older than the configured boundary return `409` with `Error` set to `DataArchived`.

Requests that cross the boundary return `409` with `Error` set to `DataRangeSplit` unless the caller supplies `allowPartial=true`; in that case NetLedger Server clamps the lower bound to the active boundary and returns active rows only.

Responses from NetLedger Server include `x-netledger-data-scope: active`. Responses from NetLedger Archive Server include `x-netledger-data-scope: archive`.

Active server export route:

```
POST /v1/archive/exports/entries
POST /v1/archive/exports/request-history
POST /v1/tenants/{tenantId}/accounts/{accountId}/archive/export
```

The export routes push committed active entries or request-history rows to NetLedger Archive Server through the migration protocol. `DeleteAfterCommit=true` runs active cleanup only after Archive Server commit succeeds.

Ledger-entry cleanup acquires the account lock, rejects pending rows at or before the cutoff, and creates or reuses a committed balance anchor at the cutoff.

It then deletes committed rows in bounded batches while preserving that anchor and verifies the remaining active balance chain.

Request-history cleanup deletes the exported tenant/range scope after commit. Export responses include `ActiveCleanupExecuted` and `ActiveCleanupRowsDeleted`.

Active server automatic archival settings routes:

```
GET    /v1/accounts/{accountId}/archive/settings
PUT    /v1/accounts/{accountId}/archive/settings
DELETE /v1/accounts/{accountId}/archive/settings
GET    /v1/tenants/{tenantId}/accounts/{accountId}/archive/settings
PUT    /v1/tenants/{tenantId}/accounts/{accountId}/archive/settings
DELETE /v1/tenants/{tenantId}/accounts/{accountId}/archive/settings
```

PUT replaces account override fields. Null values inherit the global `Archive.Automatic` policy. DELETE clears override fields while preserving worker state such as `LastArchivedThroughUtc`, `LastSuccessUtc`, and retry metadata.

Archive Server default base URL:

```
http://localhost:8081
```

Archive Server currently exposes these v4 contract routes:

```
GET  /v1/service
GET  /v1/health
GET  /openapi.json
GET  /v1/archive/ranges
GET  /v1/tenants/{tenantId}/archive/ranges
GET  /v1/tenants/{tenantId}/accounts/{accountId}/archive/ranges
GET  /v1/archive/manifests
GET  /v1/archive/manifests/{manifestId}
GET  /v1/archive/manifests/{manifestId}/objects
GET  /v1/archive/objects/{objectId}/metadata
GET  /v1/archive/manifests/{manifestId}/checkpoints
POST /v1/archive/manifests/{manifestId}/verify
POST /v1/archive/manifests/{manifestId}/quarantine
POST /v1/archive/manifests/{manifestId}/supersede
GET  /v1/archive/storage-pools
GET  /v1/archive/storage-pools/{storagePoolId}/health
GET  /v1/archive/accounts/{accountId}/entries
GET  /v1/archive/accounts/{accountId}/balance/asof
GET  /v1/archive/accounts/{accountId}/verify
GET  /v1/request-history
GET  /v1/request-history/summary
GET  /v1/request-history/{id}
GET  /v1/tenants/{tenantId}/accounts/{accountId}/entries
POST /v1/tenants/{tenantId}/accounts/{accountId}/entries/enumerate
GET  /v1/tenants/{tenantId}/accounts/{accountId}/balance/asof
GET  /v1/tenants/{tenantId}/accounts/{accountId}/verify
GET  /v1/archive/migrations
GET  /v1/archive/migrations/{migrationId}
GET  /v1/archive/migrations/{migrationId}/batches
POST /v1/archive/migrations
POST /v1/archive/migrations/{migrationId}/batches
PUT  /v1/archive/migrations/{migrationId}/batches/{batchId}/content
POST /v1/archive/migrations/{migrationId}/seal
POST /v1/archive/migrations/{migrationId}/commit
POST /v1/archive/migrations/{migrationId}/abort
GET  /v1/archive-server/request-history
GET  /v1/archive-server/request-history/summary
GET  /v1/archive-server/request-history/{id}
```

Archive Server also registers `/api/v1/...` aliases for the archive routes above. Cold entry enumeration returns the active API's `EnumerationResult<Entry>` envelope and cold request-history enumeration returns the active API's `RequestHistoryResult` envelope.

Both can read committed JSONL.Gzip archive objects from filesystem or S3-compatible storage pools.

Archive enumerations accept `maxResults`, `skip`, and returned `continuationToken` values; continuation tokens are opaque and rejected when filters change.

Archive Server rejects partially covered cold ranges by default when `Archive.RequireCompleteCoverage=true`; send `allowPartial=true` only when a partial cold result is acceptable.

Migration create, batch metadata, batch content upload, seal, commit, and abort are catalog-backed and idempotency-aware.

Manifest state transitions, object metadata reads, balance-as-of archive reads, archive account verification, and Archive Server operational request-history reads are catalog-backed.

JSONL.Gzip is the only accepted v4 runtime archive format; Parquet is reserved for a future format/index workstream and is rejected by v4 migration creation.

### v3.0.0 Tenant And Metadata Contract

All v3.0.0 behavior remains under `/v1`. Clients can scope requests with the `x-tenant-id` header or use tenant route aliases:

```
x-tenant-id: ten_01h000000000000000000000000
GET /v1/tenants/{tenantId}/accounts
PUT /v1/tenants/{tenantId}/accounts/{accountId}/credits
```

If both a route tenant and `x-tenant-id` are provided, they must match.

Public IDs are opaque PrettyId strings. Examples:

| Entity | Prefix |
| --- | --- |
| Tenant | `ten_` |
| User | `usr_` |
| Credential | `cred_` |
| Account | `acct_` |
| Entry | `ent_` |

Create account with metadata:

```json
{
  "Name": "Operating Account",
  "InitialBalance": 1000.0,
  "Labels": ["operating", "usd"],
  "Tags": {
    "department": "finance",
    "region": "west"
  }
}
```

Create a debit or credit with metadata:

```json
{
  "Amount": 25.0,
  "Notes": "Customer payment",
  "IsCommitted": false,
  "Labels": ["credit", "blue"],
  "Tags": {
    "user": "foo",
    "source": "dashboard"
  }
}
```

Batch entries use the same metadata fields per entry:

```json
{
  "IsCommitted": false,
  "Entries": [
    {
      "Amount": 25.0,
      "Notes": "Customer payment",
      "Labels": ["credit"],
      "Tags": { "user": "foo" }
    }
  ]
}
```

Enumeration accepts all-must-match metadata filters for accounts and entries. Entry enumeration also accepts debit/credit amount bounds:

```json
{
  "MaxResults": 50,
  "ContinuationToken": null,
  "Ordering": "CreatedDescending",
  "Labels": ["credit", "blue"],
  "Tags": { "user": "foo" },
  "CreditMinimum": 10.0,
  "CreditMaximum": null,
  "DebitMinimum": null,
  "DebitMaximum": null
}
```

The same metadata filters are available as query parameters on list endpoints:

```
GET /v1/accounts?labels=operating,blue&tags=department=finance,color=blue
GET /v1/accounts/{accountId}/entries?debitMin=5&debitMax=50&labels=blue&tags=color=blue&ordering=AmountDescending
```

Tenant-aware dashboard login uses server-side sessions:

```
POST /v1/auth/tenants
POST /v1/auth/login
POST /v1/auth/logout
GET  /v1/me/permissions
GET  /v1/tenants/{tenantId}/roles
PUT  /v1/tenants/{tenantId}/roles
GET  /v1/tenants/{tenantId}/permissions
PUT  /v1/tenants/{tenantId}/permissions
PUT  /v1/tenants/{tenantId}/users/{userId}/roles
```

Tenant discovery request:

```json
{
  "Email": "user@example.com"
}
```

Login request:

```json
{
  "TenantId": "ten_01h000000000000000000000000",
  "Email": "user@example.com",
  "Password": "password"
}
```

Login responses include a revocable `Session.Token`; send it as `Authorization: Bearer <token>` and include `x-tenant-id` for tenant-scoped requests.

Security administration endpoints:

```
GET    /v1/tenants
PUT    /v1/tenants
GET    /v1/tenants/{tenantId}
DELETE /v1/tenants/{tenantId}
GET    /v1/tenants/{tenantId}/users
PUT    /v1/tenants/{tenantId}/users
GET    /v1/tenants/{tenantId}/users/{userId}
GET    /v1/tenants/{tenantId}/accounts/{accountId}/users
PUT    /v1/tenants/{tenantId}/accounts/{accountId}/users/{userId}
DELETE /v1/tenants/{tenantId}/accounts/{accountId}/users/{userId}
GET    /v1/tenants/{tenantId}/sessions
DELETE /v1/tenants/{tenantId}/sessions/{sessionId}
GET    /v1/tenants/{tenantId}/audit
GET    /v1.0/api/request-history
GET    /v1.0/api/request-history/summary
GET    /v1.0/api/request-history/{id}
DELETE /v1.0/api/request-history
DELETE /v1.0/api/request-history/{id}
```

Authorization behavior:

- System admins can access all tenants and records.
- Tenant admins can administer records in their tenant.
- Regular users can read themselves and use accounts mapped through `accountusermaps`.
- Request history follows the same boundary: system admins can see all entries, tenant admins see entries in their tenant, and regular users see only their own entries.
- Denials are written to the append-only audit store.

---

## Authentication

All endpoints except `HEAD /`, `GET /`, `POST /v1/auth/tenants`, and `POST /v1/auth/login` require authentication via Bearer token:

```
Authorization: Bearer <session-token-or-access-key>
```

Credentials are managed through the Credential Management endpoints. Admin endpoints require a session or credential with admin privileges.

---

## Common Response Headers

All endpoints include the following response headers:

| Header | Description |
|--------|-------------|
| `x-hostname` | Server hostname |
| `x-api-version` | Current API version (v1) |
| `x-request-id` | Unique request identifier for tracking |
| `x-netledger-data-scope` | `active` from NetLedger Server or `archive` from NetLedger Archive Server on data-bearing responses |
| `Content-Type` | `application/json` |

---

## Error Responses

All endpoints return standardized error responses:

```json
{
  "Error": "BadRequest",
  "Message": "Bad request",
  "StatusCode": 400,
  "Context": null,
  "Description": "Account identifier is required"
}
```

### HTTP Status Codes

| Code | Description |
|------|-------------|
| 200 | OK - Request succeeded |
| 201 | Created - Resource created successfully |
| 400 | Bad Request - Invalid request parameters |
| 401 | Unauthorized - Missing or invalid session or credential |
| 403 | Forbidden - Insufficient permissions |
| 404 | Not Found - Resource does not exist |
| 408 | Request Timeout |
| 409 | Conflict - Resource conflict |
| 500 | Internal Server Error |
| 503 | Service Unavailable |

---

## API Endpoints

### Service Endpoints

These endpoints do not require authentication.

#### Health Check

Check if the service is running.

```
HEAD /
GET /v1/health
```

**Response**: `200 OK` (empty body)

---

#### Get Service Information

Retrieve service metadata including version and uptime.

```
GET /
GET /v1/service
```

**Response**: `200 OK`

```json
{
  "Name": "NetLedger.Server",
  "Version": "4.0.0",
  "StartTimeUtc": "2025-12-23T00:00:00Z",
  "UptimeSeconds": 3600,
  "UptimeFormatted": "1h 0m 0s",
  "Archive": {
    "Enabled": true,
    "ArchiveServerEndpoint": "http://localhost:8081",
    "ActiveDataRetentionDays": 365,
    "ActiveBoundaryUtc": "2024-12-23T00:00:00Z"
  }
}
```

#### Get OpenAPI Document

Retrieve the OpenAPI document used by the dashboard API Explorer.

```
GET /openapi.json
```

**Response**: `200 OK`

---

### Account Endpoints

All account endpoints require authentication.

#### List Accounts

Enumerate all accounts with pagination and filtering.

```
GET /v1/accounts
```

**Query Parameters**:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `maxResults` | int | 1000 | Maximum results per page (1-1000) |
| `skip` | int | 0 | Number of records to skip |
| `continuationToken` | string | null | Token for pagination continuation |
| `ordering` | enum | CreatedDescending | Sort order: `CreatedAscending`, `CreatedDescending`, `AmountAscending`, `AmountDescending` |
| `search` | string | null | Search filter for account name |
| `startTime` | DateTime | null | Filter by creation date (UTC) |
| `endTime` | DateTime | null | Filter by creation date (UTC) |
| `balanceMin` | decimal | null | Minimum committed balance filter |
| `balanceMax` | decimal | null | Maximum committed balance filter |
| `labels` | string | null | Comma-separated labels that must all exist |
| `tags` | string | null | Comma-separated `key=value` tags that must all match |

Example metadata search:

```
GET /v1/accounts?search=Operating&labels=operating,blue&tags=department=finance,color=blue&ordering=CreatedDescending
```

**Response**: `200 OK`

```json
{
  "Success": true,
  "Timestamp": {
    "Start": "2025-12-23T00:00:00Z",
    "End": "2025-12-23T00:00:01Z",
    "TotalMs": 15.5
  },
  "MaxResults": 1000,
  "Skip": 0,
  "IterationsRequired": 1,
  "ContinuationToken": null,
  "EndOfResults": true,
  "TotalRecords": 1,
  "RecordsRemaining": 0,
  "Objects": [
    {
      "Id": "acct_operating_001",
      "Name": "Checking Account",
      "Notes": null,
      "Labels": ["operating", "blue"],
      "Tags": {
        "department": "finance",
        "color": "blue"
      },
      "CreatedUtc": "2025-12-23T00:00:00Z"
    }
  ]
}
```

---

#### Create Account

Create a new ledger account.

```
PUT /v1/accounts
```

**Request Body**:

```json
{
  "Name": "Checking Account",
  "InitialBalance": 0,
  "Notes": "Primary operating account",
  "Units": "USD",
  "Labels": ["operating", "blue"],
  "Tags": {
    "department": "finance",
    "color": "blue"
  }
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Name` | string | Yes | Account name |
| `InitialBalance` | decimal | No | Initial committed balance (default: 0) |
| `Notes` | string | No | Optional account notes. |
| `Units` | string | No | Unit/currency label for the account, e.g. USD or tokens. Max 64 chars. |
| `Labels` | string[] | No | Account labels. Empty values are ignored and duplicate labels are normalized. |
| `Tags` | object | No | Account tags as string key/value pairs. Tags are normalized by key. |

**Response**: `201 Created`

```json
{
  "Id": "acct_operating_001",
  "Name": "Checking Account",
  "Notes": "Primary operating account",
  "Units": "USD",
  "Labels": ["operating", "blue"],
  "Tags": {
    "department": "finance",
    "color": "blue"
  },
  "CreatedUtc": "2025-12-23T00:00:00Z"
}
```

---

#### Update Account

Update an existing ledger account. Replaces the editable fields of the account. The account identifier, owning tenant, and creation timestamp cannot be changed.

```
PUT /v1/accounts/{accountId}
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `accountId` | string | Account identifier |

**Request Body**:

```json
{
  "Name": "Checking Account",
  "Notes": "Primary operating account",
  "Units": "USD",
  "Labels": ["operating", "blue"],
  "Tags": {
    "department": "finance",
    "color": "blue"
  },
  "Active": true
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Name` | string | Yes | Account name |
| `Notes` | string | No | Optional account notes. Null clears the value. |
| `Units` | string | No | Unit/currency label for the account, e.g. USD or tokens. Max 64 chars. Null clears the value. |
| `Labels` | string[] | No | Account labels. Omitting clears existing labels. Empty values are ignored and duplicate labels are normalized. |
| `Tags` | object | No | Account tags as string key/value pairs. Omitting clears existing tags. Tags are normalized by key. |
| `Active` | bool | No | Account active state. Null leaves the current value unchanged. |

**Response**: `200 OK`

```json
{
  "Id": "acct_operating_001",
  "Name": "Checking Account",
  "Notes": "Primary operating account",
  "Units": "USD",
  "Labels": ["operating", "blue"],
  "Tags": {
    "department": "finance",
    "color": "blue"
  },
  "CreatedUtc": "2025-12-23T00:00:00Z"
}
```

---

#### Check Account Exists

Check if an account exists by identifier.

```
HEAD /v1/accounts/{accountId}
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `accountId` | string | Account identifier |

**Response**: `200 OK` if exists, `404 Not Found` if not

---

#### Get Account by Identifier

Retrieve a specific account by its identifier.

```
GET /v1/accounts/{accountId}
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `accountId` | string | Account identifier |

**Response**: `200 OK`

```json
{
  "Id": "acct_operating_001",
  "Name": "Checking Account",
  "Notes": null,
  "Labels": ["operating", "blue"],
  "Tags": {
    "department": "finance",
    "color": "blue"
  },
  "CreatedUtc": "2025-12-23T00:00:00Z"
}
```

---

#### Get Account by Name

Retrieve a specific account by its name.

```
GET /v1/accounts/byname/{accountName}
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `accountName` | string | Account name |

**Response**: `200 OK`

```json
{
  "Id": "acct_operating_001",
  "Name": "Checking Account",
  "Notes": null,
  "Labels": ["operating", "blue"],
  "Tags": {
    "department": "finance",
    "color": "blue"
  },
  "CreatedUtc": "2025-12-23T00:00:00Z"
}
```

---

#### Delete Account

Delete an account and all its entries.

```
DELETE /v1/accounts/{accountId}
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `accountId` | string | Account identifier |

**Response**: `200 OK` (empty body)

---

### Entry Endpoints

All entry endpoints require authentication.

#### Get Entries (Query Parameters)

Enumerate entries with query parameter-based filtering.

```
GET /v1/accounts/{accountId}/entries
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `accountId` | string | Account identifier |

**Query Parameters**:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `maxResults` | int | 1000 | Maximum results per page (1-1000) |
| `skip` | int | 0 | Number of records to skip |
| `continuationToken` | string | null | Token for pagination continuation |
| `ordering` | enum | CreatedDescending | Sort order |
| `search` | string | null | Description search term |
| `startTime` | DateTime | null | Filter entries created after (UTC) |
| `endTime` | DateTime | null | Filter entries created before (UTC) |
| `amountMin` | decimal | null | Minimum amount filter |
| `amountMax` | decimal | null | Maximum amount filter |
| `creditMin` | decimal | null | Minimum credit amount filter |
| `creditMax` | decimal | null | Maximum credit amount filter |
| `debitMin` | decimal | null | Minimum debit amount filter |
| `debitMax` | decimal | null | Maximum debit amount filter |
| `labels` | string | null | Comma-separated labels that must all exist |
| `tags` | string | null | Comma-separated `key=value` tags that must all match |

Example complex search:

```
GET /v1/accounts/{accountId}/entries?debitMin=5&debitMax=50&labels=blue&tags=color=blue&ordering=AmountDescending
```

**Response**: `200 OK`

```json
{
  "Success": true,
  "Timestamp": {
    "Start": "2025-12-23T00:00:00Z",
    "End": "2025-12-23T00:00:01Z",
    "TotalMs": 15.5
  },
  "MaxResults": 1000,
  "Skip": 0,
  "IterationsRequired": 1,
  "ContinuationToken": null,
  "EndOfResults": true,
  "TotalRecords": 1,
  "RecordsRemaining": 0,
  "Objects": [
    {
      "Id": "ent_credit_001",
      "AccountId": "acct_operating_001",
      "Type": "Credit",
      "Amount": 100.50,
      "Description": "Initial deposit",
      "IsCommitted": true,
      "CommittedUtc": "2025-12-23T00:00:00Z",
      "CommittedById": null,
      "Replaces": null,
      "Labels": ["blue"],
      "Tags": {
        "color": "blue"
      },
      "CreatedUtc": "2025-12-23T00:00:00Z"
    }
  ]
}
```

---

#### Enumerate Entries (Request Body)

Enumerate entries with request body-based filtering.

```
POST /v1/accounts/{accountId}/entries/enumerate
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `accountId` | string | Account identifier |

**Request Body**:

```json
{
  "MaxResults": 100,
  "Skip": 0,
  "ContinuationToken": null,
  "Ordering": "CreatedDescending",
  "SearchTerm": null,
  "CreatedAfterUtc": null,
  "CreatedBeforeUtc": null,
  "AmountMinimum": null,
  "AmountMaximum": null,
  "CreditMinimum": null,
  "CreditMaximum": null,
  "DebitMinimum": 5.0,
  "DebitMaximum": 50.0,
  "Labels": ["blue"],
  "Tags": { "color": "blue" }
}
```

**Response**: `200 OK` (same format as Get Entries)

---

#### Get Pending Entries

Get all pending (uncommitted) entries for an account.

```
GET /v1/accounts/{accountId}/entries/pending
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `accountId` | string | Account identifier |

**Response**: `200 OK`

```json
[
  {
    "Id": "ent_debit_001",
    "AccountId": "acct_operating_001",
    "Type": "Debit",
    "Amount": 25.00,
    "Description": "Pending withdrawal",
    "IsCommitted": false,
    "CommittedUtc": null,
    "CommittedById": null,
    "Replaces": null,
    "Labels": ["blue"],
    "Tags": {
      "color": "blue"
    },
    "CreatedUtc": "2025-12-23T00:00:00Z"
  }
]
```

---

#### Get Pending Credits

Get all pending credit entries for an account.

```
GET /v1/accounts/{accountId}/entries/pending/credits
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `accountId` | string | Account identifier |

**Response**: `200 OK` (array of credit Entry objects)

---

#### Get Pending Debits

Get all pending debit entries for an account.

```
GET /v1/accounts/{accountId}/entries/pending/debits
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `accountId` | string | Account identifier |

**Response**: `200 OK` (array of debit Entry objects)

---

#### Add Credit(s)

Add one or more credit entries to an account.

```
PUT /v1/accounts/{accountId}/credits
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `accountId` | string | Account identifier |

**Request Body (Single Entry)**:

```json
{
  "Amount": 100.00,
  "Notes": "Customer payment",
  "IsCommitted": false,
  "Labels": ["blue"],
  "Tags": {
    "color": "blue",
    "source": "dashboard"
  }
}
```

**Request Body (Batch Entries)**:

```json
{
  "Entries": [
    {
      "Amount": 50.00,
      "Notes": "Payment 1",
      "Labels": ["blue"],
      "Tags": { "color": "blue" }
    },
    {
      "Amount": 50.00,
      "Notes": "Payment 2",
      "Labels": ["green"],
      "Tags": { "color": "green" }
    }
  ],
  "IsCommitted": false
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Amount` | decimal | Yes | Credit amount (must be positive) |
| `Notes` | string | No | Entry description |
| `IsCommitted` | bool | No | Whether to commit immediately (default: false) |
| `Entries` | array | No | Array of entries for batch operations |
| `Labels` | string[] | No | Entry labels for single-entry requests or per batch item |
| `Tags` | object | No | Entry tags as string key/value pairs for single-entry requests or per batch item |

**Response**: `201 Created`

```json
{
  "EntryIds": [
    "ent_credit_002",
    "ent_credit_003"
  ]
}
```

---

#### Add Debit(s)

Add one or more debit entries to an account.

```
PUT /v1/accounts/{accountId}/debits
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `accountId` | string | Account identifier |

**Request Body**: Same format as Add Credit(s)

**Response**: `201 Created` (same format as Add Credit(s))

---

#### Cancel Entry

Cancel a pending entry. Only uncommitted entries can be canceled.

```
DELETE /v1/accounts/{accountId}/entries/{entryId}
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `accountId` | string | Account identifier |
| `entryId` | string | Entry identifier |

**Response**: `200 OK` (empty body)

---

### Balance and Commit Endpoints

All balance and commit endpoints require authentication.

#### Get Current Balance

Get the current balance for an account, including committed and pending amounts.

```
GET /v1/accounts/{accountId}/balance
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `accountId` | string | Account identifier |

**Response**: `200 OK`

```json
{
  "AccountId": "acct_operating_001",
  "EntryId": "ent_balance_001",
  "Name": "Checking Account",
  "CreatedUtc": "2025-12-23T00:00:00Z",
  "BalanceTimestampUtc": "2025-12-23T01:00:00Z",
  "CommittedBalance": 100.00,
  "PendingBalance": 125.00,
  "PendingCredits": {
    "Count": 1,
    "Total": 50.00,
    "Entries": [...]
  },
  "PendingDebits": {
    "Count": 1,
    "Total": 25.00,
    "Entries": [...]
  },
  "Committed": [...]
}
```

---

#### Get Historical Balance (As-Of)

Get the balance at a specific point in time.

```
GET /v1/accounts/{accountId}/balance/asof
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `accountId` | string | Account identifier |

**Query Parameters**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `asOf` | DateTime | Yes | Point in time for balance query (UTC) |

**Response**: `200 OK`

```json
{
  "accountId": "acct_operating_001",
  "asOfUtc": "2025-12-22T00:00:00Z",
  "balance": 75.00
}
```

---

#### Get All Account Balances

Get current balances for all accounts.

```
GET /v1/balances
```

**Response**: `200 OK`

```json
{
  "acct_operating_001": {
    "AccountId": "acct_operating_001",
    "EntryId": "ent_balance_001",
    "Name": "Checking Account",
    "CreatedUtc": "2025-12-23T00:00:00Z",
    "BalanceTimestampUtc": "2025-12-23T01:00:00Z",
    "CommittedBalance": 100.00,
    "PendingBalance": 125.00,
    "PendingCredits": {...},
    "PendingDebits": {...},
    "Committed": [...]
  }
}
```

---

#### Commit Entries

Commit pending entries, creating a new balance snapshot.

```
POST /v1/accounts/{accountId}/commit
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `accountId` | string | Account identifier |

**Request Body (Commit All Pending)**:

```json
{
  "EntryIds": null
}
```

**Request Body (Commit Specific Entries)**:

```json
{
  "EntryIds": [
    "ent_credit_002",
    "ent_credit_003"
  ]
}
```

**Response**: `200 OK`

```json
{
  "AccountId": "acct_operating_001",
  "EntryId": "ent_balance_002",
  "Name": "Checking Account",
  "CreatedUtc": "2025-12-23T00:00:00Z",
  "BalanceTimestampUtc": "2025-12-23T02:00:00Z",
  "CommittedBalance": 200.00,
  "PendingBalance": 200.00,
  "PendingCredits": {
    "Count": 0,
    "Total": 0,
    "Entries": []
  },
  "PendingDebits": {
    "Count": 0,
    "Total": 0,
    "Entries": []
  },
  "Committed": [...]
}
```

---

#### Verify Balance Chain

Verify the integrity of the balance entry chain (audit trail).

```
GET /v1/accounts/{accountId}/verify
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `accountId` | string | Account identifier |

**Response**: `200 OK`

```json
{
  "accountId": "acct_operating_001",
  "isValid": true
}
```

---

### Credential Management Endpoints

Credential management endpoints require admin access. Use `/v1/credentials` or `/v1/tenants/{tenantId}/credentials`; legacy `/v1/apikeys` management routes are removed in v3.

#### Enumerate Credentials

List credentials (access keys are partially redacted).

```
GET /v1/credentials
GET /v1/tenants/{tenantId}/credentials
```

**Query Parameters**:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `maxResults` | int | 1000 | Maximum results per page (1-1000) |
| `skip` | int | 0 | Number of records to skip |
| `continuationToken` | string | null | Token for pagination continuation |
| `ordering` | enum | CreatedDescending | Sort order: `CreatedAscending`, `CreatedDescending` |
| `search` | string | null | Search filter for key name |
| `tenantId` | string | null | Tenant filter when not using the tenant route |
| `startTime` | DateTime | null | Filter by creation date (UTC) |
| `endTime` | DateTime | null | Filter by creation date (UTC) |

**Response**: `200 OK`

```json
{
  "Success": true,
  "Timestamp": {
    "Start": "2025-12-23T00:00:00Z",
    "End": "2025-12-23T00:00:01Z",
    "TotalMs": 15.5
  },
  "MaxResults": 1000,
  "Skip": 0,
  "IterationsRequired": 1,
  "ContinuationToken": null,
  "EndOfResults": true,
  "TotalRecords": 1,
  "RecordsRemaining": 0,
  "Objects": [
    {
      "Id": "cred_01k2t4v6x8z9abcdefghijklm",
      "TenantId": "ten_01k2t4v6x8z9abcdefghijklm",
      "UserId": "usr_01k2t4v6x8z9abcdefghijklm",
      "Name": "Admin Key",
      "Key": "ak_0****wxyz",
      "SecretKeyLast4": "9abc",
      "Active": true,
      "IsAdmin": true,
      "CreatedUtc": "2025-12-23T00:00:00Z"
    }
  ]
}
```

---

#### Create Credential

Create a new credential. The access key is returned with the credential and the raw secret key is returned once in `SecretKey`. Later responses only expose `SecretKeyLast4`.

```
PUT /v1/credentials
PUT /v1/tenants/{tenantId}/credentials
```

**Request Body**:

```json
{
  "TenantId": "ten_01k2t4v6x8z9abcdefghijklm",
  "UserId": "usr_01k2t4v6x8z9abcdefghijklm",
  "Name": "Integration Key",
  "IsAdmin": false
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `TenantId` | string | No | Tenant identifier; route tenant or `x-tenant-id` may also provide it |
| `UserId` | string | No | Owning user identifier |
| `Name` | string | Yes | Descriptive name for the credential |
| `IsAdmin` | bool | No | Whether the key has admin privileges (default: false) |

**Response**: `201 Created`

```json
{
  "Id": "cred_01k2t4v6x8z9abcdefghijklm",
  "TenantId": "ten_01k2t4v6x8z9abcdefghijklm",
  "UserId": "usr_01k2t4v6x8z9abcdefghijklm",
  "Name": "Integration Key",
  "Key": "ak_01k2t4v6x8z9abcdefghijklm",
  "SecretKeyLast4": "9abc",
  "Active": true,
  "IsAdmin": false,
  "CreatedUtc": "2025-12-23T00:00:00Z"
}
```

> **Important**: Store the access `Key` securely. It will be partially redacted in subsequent API calls.

---

#### Revoke Credential

Revoke and delete a credential.

```
DELETE /v1/credentials/{credentialId}
DELETE /v1/tenants/{tenantId}/credentials/{credentialId}
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `credentialId` | string | Credential identifier |

**Response**: `200 OK` (empty body)

---

### Request History Endpoints

Request history captures REST request and response metadata for authenticated API traffic. Captured bodies are size-limited and sensitive request headers such as `Authorization`, cookies, API keys, and token-like headers are redacted.

Access is scoped by role:

- System admins can enumerate, read, summarize, and delete any request-history entry.
- Tenant admins can enumerate, read, summarize, and delete entries only inside their tenant.
- Regular users can enumerate, read, and summarize only their own entries; they cannot delete request history.

#### Enumerate Request History

```
GET /v1.0/api/request-history
GET /v1/request-history
```

**Query Parameters**:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `tenantId` | string | auth scope | Tenant filter; system admins may specify any tenant |
| `principalId` | string | auth scope | Principal/user filter |
| `method` | string | null | HTTP method filter |
| `statusCode` | int | null | Exact HTTP response status |
| `pathContains` | string | null | Path substring filter |
| `fromUtc` | DateTime | null | Lower created timestamp bound |
| `toUtc` | DateTime | null | Upper created timestamp bound |
| `maxResults` | int | 25 | Maximum results per page (1-1000) |
| `skip` | int | 0 | Number of records to skip |

List responses omit request and response bodies. Read a single entry to retrieve captured bodies.

#### Summarize Request History

```
GET /v1.0/api/request-history/summary
GET /v1/request-history/summary
```

Supports the same filters as enumeration plus `bucketMinutes`. The response includes `TotalCount`, `TotalSuccess`, `TotalFailure`, `AverageDurationMs`, and time buckets.

#### Read Request History Entry

```
GET /v1.0/api/request-history/{id}
GET /v1/request-history/{id}
```

Returns one request-history entry, including captured headers and body content where available.

#### Delete Request History

```
DELETE /v1.0/api/request-history/{id}
DELETE /v1/request-history/{id}
DELETE /v1.0/api/request-history
DELETE /v1/request-history
```

Bulk delete accepts the same filters as enumeration and deletes only records within the caller's authorization scope.

---

### Active Archive Export and Settings Endpoints

These endpoints live on NetLedger Server (`http://localhost:8080` by default). They move active rows to NetLedger Archive Server and configure account-level automatic archival overrides. NetLedger Server remains the source for active rows only.

#### Export Active Entries

```
POST /v1/archive/exports/entries
POST /v1/tenants/{tenantId}/accounts/{accountId}/archive/export
```

**Request Body**:

```json
{
  "TenantId": "default",
  "AccountId": "acct_01h000000000000000000000",
  "FromUtc": "2024-01-01T00:00:00Z",
  "ToUtc": "2025-01-01T00:00:00Z",
  "StoragePoolId": "asp_default",
  "IdempotencyKey": "entries-2024",
  "MaxBatchRows": 50000,
  "DeleteAfterCommit": false
}
```

`DeleteAfterCommit=true` deletes active rows only after Archive Server commit succeeds and after NetLedger Server preserves the account balance anchor.

#### Export Active Request History

```
POST /v1/archive/exports/request-history
```

The request body is the same shape as entry export, except `AccountId` is optional and normally omitted for tenant-scoped request-history export.

#### Account Archive Settings

```
GET    /v1/accounts/{accountId}/archive/settings
PUT    /v1/accounts/{accountId}/archive/settings
DELETE /v1/accounts/{accountId}/archive/settings
GET    /v1/tenants/{tenantId}/accounts/{accountId}/archive/settings
PUT    /v1/tenants/{tenantId}/accounts/{accountId}/archive/settings
DELETE /v1/tenants/{tenantId}/accounts/{accountId}/archive/settings
```

**PUT Request Body**:

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

Null fields inherit from the global `Archive.Automatic` policy. DELETE clears override fields while retaining worker state.

---

### Archive Server Endpoints

These endpoints live on NetLedger Archive Server (`http://localhost:8081` by default). Archive Server direct reads return cold data and include `x-netledger-data-scope: archive`.

#### Service and Metadata

```
GET  /v1/service
GET  /v1/health
GET  /openapi.json
GET  /v1/archive/ranges
GET  /v1/tenants/{tenantId}/archive/ranges
GET  /v1/tenants/{tenantId}/accounts/{accountId}/archive/ranges
GET  /v1/archive/manifests
GET  /v1/archive/manifests/{manifestId}
GET  /v1/archive/manifests/{manifestId}/objects
GET  /v1/archive/manifests/{manifestId}/checkpoints
GET  /v1/archive/objects/{objectId}/metadata
GET  /v1/archive/storage-pools
GET  /v1/archive/storage-pools/{storagePoolId}/health
```

#### Cold Ledger Reads

```
GET  /v1/archive/accounts/{accountId}/entries
GET  /v1/archive/accounts/{accountId}/balance/asof
GET  /v1/archive/accounts/{accountId}/verify
GET  /v1/tenants/{tenantId}/accounts/{accountId}/entries
POST /v1/tenants/{tenantId}/accounts/{accountId}/entries/enumerate
GET  /v1/tenants/{tenantId}/accounts/{accountId}/balance/asof
GET  /v1/tenants/{tenantId}/accounts/{accountId}/verify
```

Cold entry routes accept the active entry filters plus archive controls such as `allowPartial`, `continuationToken`, `fromUtc`, and `toUtc`.

#### Cold Request History

```
GET /v1/request-history
GET /v1/request-history/summary
GET /v1/request-history/{id}
GET /v1/archive-server/request-history
GET /v1/archive-server/request-history/summary
GET /v1/archive-server/request-history/{id}
```

`/v1/request-history` reads archived NetLedger Server request history. `/v1/archive-server/request-history` reads Archive Server operational request history.

#### Migration Lifecycle

```
GET  /v1/archive/migrations
POST /v1/archive/migrations
GET  /v1/archive/migrations/{migrationId}
GET  /v1/archive/migrations/{migrationId}/batches
POST /v1/archive/migrations/{migrationId}/batches
PUT  /v1/archive/migrations/{migrationId}/batches/{batchId}/content
POST /v1/archive/migrations/{migrationId}/seal
POST /v1/archive/migrations/{migrationId}/commit
POST /v1/archive/migrations/{migrationId}/abort
```

The v4.0.0 migration lifecycle accepts only JSONL.Gzip payloads. Archive Server validates uploaded content before committing manifests and coverage ranges.

---

## Data Types

### Entry Types

| Value | Description |
|-------|-------------|
| `Credit` | Money added to account |
| `Debit` | Money removed from account |
| `Balance` | Balance snapshot entry (created by commits) |

### Ordering Options

| Value | Description |
|-------|-------------|
| `CreatedAscending` | Oldest first |
| `CreatedDescending` | Newest first |
| `AmountAscending` | Lowest amount first |
| `AmountDescending` | Highest amount first |

---

## API Summary

| Category | Surface |
|----------|---------|
| Service | Health, service info, and OpenAPI document |
| Identity, Tenant, and RBAC | Tenant discovery, login/logout, effective permissions, tenants, users, sessions, audit, roles, permissions, and account-user maps |
| Account | Active account create, enumerate, read, exists, read by name, and delete, including tenant route aliases |
| Entry | Active entry list/enumerate, pending reads, credit/debit writes, and pending cancellation, including tenant route aliases |
| Balance/Commit | Active current balance, balance as-of, all balances, commit, and balance-chain verification |
| Credential Management | Credential enumerate, create, and revoke, including tenant route aliases |
| Request History | Active request-history enumerate, summarize, read, and delete, including `/v1.0/api/request-history` compatibility aliases |
| Active Archive Export and Settings | Active entry export, request-history export, tenant-account export, and account automatic archival settings |
| Archive Server | Cold entries, cold request history, archive metadata, object metadata, storage pools, manifest actions, migration lifecycle, and Archive Server operational request history |
