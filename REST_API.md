# NetLedger REST API Documentation

This document provides comprehensive documentation for the NetLedger.Server REST API.

## Table of Contents

- [Overview](#overview)
- [Authentication](#authentication)
- [Common Response Headers](#common-response-headers)
- [Error Responses](#error-responses)
- [API Endpoints](#api-endpoints)
  - [Service Endpoints](#service-endpoints)
  - [Account Endpoints](#account-endpoints)
  - [Entry Endpoints](#entry-endpoints)
  - [Balance and Commit Endpoints](#balance-and-commit-endpoints)
  - [API Key Management Endpoints](#api-key-management-endpoints)
  - [Request History Endpoints](#request-history-endpoints)

---

## Overview

The NetLedger REST API provides programmatic access to ledger operations including account management, credit/debit entries, balance queries, and transaction commits. The API uses JSON for all request and response bodies.

**Base URL**: `http://localhost:8080` (configurable)

**API Version**: v1

**OpenAPI document**: `GET /openapi.json`

The dashboard API Explorer loads `GET /openapi.json` and executes requests with the current signed-in session. Request History is available in the dashboard and through the `/v1.0/api/request-history` API aliases.

### v3.0.0 Tenant And Metadata Contract

All v3.0.0 behavior remains under `/v1`. Clients can scope requests with the `x-tenant-id` header or use tenant route aliases:

```
x-tenant-id: ten_01h000000000000000000000000
GET /v1/tenants/{tenantId}/accounts
PUT /v1/tenants/{tenantId}/accounts/{accountId}/credits
```

If both a route tenant and `x-tenant-id` are provided, they must match.

Public IDs are PrettyId strings, not GUIDs. Examples:

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

API keys are managed through the API Key Management endpoints. Admin endpoints require an API key with admin privileges.

---

## Common Response Headers

All endpoints include the following response headers:

| Header | Description |
|--------|-------------|
| `x-hostname` | Server hostname |
| `x-api-version` | Current API version (v1) |
| `x-request-guid` | Unique request identifier for tracking |
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
  "Description": "Account GUID is required"
}
```

### HTTP Status Codes

| Code | Description |
|------|-------------|
| 200 | OK - Request succeeded |
| 201 | Created - Resource created successfully |
| 400 | Bad Request - Invalid request parameters |
| 401 | Unauthorized - Missing or invalid API key |
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
```

**Response**: `200 OK` (empty body)

---

#### Get Service Information

Retrieve service metadata including version and uptime.

```
GET /
```

**Response**: `200 OK`

```json
{
  "Name": "NetLedger.Server",
  "Version": "1.0.0",
  "StartTimeUtc": "2025-12-23T00:00:00Z",
  "UptimeSeconds": 3600,
  "UptimeFormatted": "1h 0m 0s"
}
```

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
| `continuationToken` | GUID | null | Token for pagination continuation |
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
      "GUID": "550e8400-e29b-41d4-a716-446655440000",
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
| `Labels` | string[] | No | Account labels. Empty values are ignored and duplicate labels are normalized. |
| `Tags` | object | No | Account tags as string key/value pairs. Tags are normalized by key. |

**Response**: `201 Created`

```json
{
  "GUID": "550e8400-e29b-41d4-a716-446655440000",
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

#### Check Account Exists

Check if an account exists by GUID.

```
HEAD /v1/accounts/{accountGuid}
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `accountGuid` | GUID | Account identifier |

**Response**: `200 OK` if exists, `404 Not Found` if not

---

#### Get Account by GUID

Retrieve a specific account by its GUID.

```
GET /v1/accounts/{accountGuid}
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `accountGuid` | GUID | Account identifier |

**Response**: `200 OK`

```json
{
  "GUID": "550e8400-e29b-41d4-a716-446655440000",
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
  "GUID": "550e8400-e29b-41d4-a716-446655440000",
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
DELETE /v1/accounts/{accountGuid}
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `accountGuid` | GUID | Account identifier |

**Response**: `200 OK` (empty body)

---

### Entry Endpoints

All entry endpoints require authentication.

#### Get Entries (Query Parameters)

Enumerate entries with query parameter-based filtering.

```
GET /v1/accounts/{accountGuid}/entries
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `accountGuid` | GUID | Account identifier |

**Query Parameters**:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `maxResults` | int | 1000 | Maximum results per page (1-1000) |
| `skip` | int | 0 | Number of records to skip |
| `continuationToken` | GUID | null | Token for pagination continuation |
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
GET /v1/accounts/{accountGuid}/entries?debitMin=5&debitMax=50&labels=blue&tags=color=blue&ordering=AmountDescending
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
      "GUID": "660e8400-e29b-41d4-a716-446655440001",
      "AccountGUID": "550e8400-e29b-41d4-a716-446655440000",
      "Type": "Credit",
      "Amount": 100.50,
      "Description": "Initial deposit",
      "IsCommitted": true,
      "CommittedUtc": "2025-12-23T00:00:00Z",
      "CommittedByGUID": null,
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
POST /v1/accounts/{accountGuid}/entries/enumerate
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `accountGuid` | GUID | Account identifier |

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
GET /v1/accounts/{accountGuid}/entries/pending
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `accountGuid` | GUID | Account identifier |

**Response**: `200 OK`

```json
[
  {
    "GUID": "660e8400-e29b-41d4-a716-446655440002",
    "AccountGUID": "550e8400-e29b-41d4-a716-446655440000",
    "Type": "Debit",
    "Amount": 25.00,
    "Description": "Pending withdrawal",
    "IsCommitted": false,
    "CommittedUtc": null,
    "CommittedByGUID": null,
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
GET /v1/accounts/{accountGuid}/entries/pending/credits
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `accountGuid` | GUID | Account identifier |

**Response**: `200 OK` (array of credit Entry objects)

---

#### Get Pending Debits

Get all pending debit entries for an account.

```
GET /v1/accounts/{accountGuid}/entries/pending/debits
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `accountGuid` | GUID | Account identifier |

**Response**: `200 OK` (array of debit Entry objects)

---

#### Add Credit(s)

Add one or more credit entries to an account.

```
PUT /v1/accounts/{accountGuid}/credits
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `accountGuid` | GUID | Account identifier |

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
  "EntryGuids": [
    "660e8400-e29b-41d4-a716-446655440003",
    "660e8400-e29b-41d4-a716-446655440004"
  ]
}
```

---

#### Add Debit(s)

Add one or more debit entries to an account.

```
PUT /v1/accounts/{accountGuid}/debits
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `accountGuid` | GUID | Account identifier |

**Request Body**: Same format as Add Credit(s)

**Response**: `201 Created` (same format as Add Credit(s))

---

#### Cancel Entry

Cancel a pending entry. Only uncommitted entries can be canceled.

```
DELETE /v1/accounts/{accountGuid}/entries/{entryGuid}
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `accountGuid` | GUID | Account identifier |
| `entryGuid` | GUID | Entry identifier |

**Response**: `200 OK` (empty body)

---

### Balance and Commit Endpoints

All balance and commit endpoints require authentication.

#### Get Current Balance

Get the current balance for an account, including committed and pending amounts.

```
GET /v1/accounts/{accountGuid}/balance
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `accountGuid` | GUID | Account identifier |

**Response**: `200 OK`

```json
{
  "AccountGUID": "550e8400-e29b-41d4-a716-446655440000",
  "EntryGUID": "770e8400-e29b-41d4-a716-446655440000",
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
GET /v1/accounts/{accountGuid}/balance/asof
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `accountGuid` | GUID | Account identifier |

**Query Parameters**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `asOf` | DateTime | Yes | Point in time for balance query (UTC) |

**Response**: `200 OK`

```json
{
  "accountGuid": "550e8400-e29b-41d4-a716-446655440000",
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
  "550e8400-e29b-41d4-a716-446655440000": {
    "AccountGUID": "550e8400-e29b-41d4-a716-446655440000",
    "EntryGUID": "770e8400-e29b-41d4-a716-446655440000",
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
POST /v1/accounts/{accountGuid}/commit
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `accountGuid` | GUID | Account identifier |

**Request Body (Commit All Pending)**:

```json
{
  "EntryGuids": null
}
```

**Request Body (Commit Specific Entries)**:

```json
{
  "EntryGuids": [
    "660e8400-e29b-41d4-a716-446655440003",
    "660e8400-e29b-41d4-a716-446655440004"
  ]
}
```

**Response**: `200 OK`

```json
{
  "AccountGUID": "550e8400-e29b-41d4-a716-446655440000",
  "EntryGUID": "880e8400-e29b-41d4-a716-446655440000",
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
GET /v1/accounts/{accountGuid}/verify
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `accountGuid` | GUID | Account identifier |

**Response**: `200 OK`

```json
{
  "accountGuid": "550e8400-e29b-41d4-a716-446655440000",
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

| Category | Count | Endpoints |
|----------|-------|-----------|
| Service | 2 | Health check, service info |
| Account | 6 | List, create, check exists, get by GUID, get by name, delete |
| Entry | 8 | Get entries, enumerate, pending entries, pending credits, pending debits, add credits, add debits, cancel |
| Balance/Commit | 5 | Get balance, historical balance, all balances, commit, verify |
| API Key Management | 3 | List, create, revoke |
| **Total** | **24** | |
