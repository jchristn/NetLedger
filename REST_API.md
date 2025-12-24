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

---

## Overview

The NetLedger REST API provides programmatic access to ledger operations including account management, credit/debit entries, balance queries, and transaction commits. The API uses JSON for all request and response bodies.

**Base URL**: `http://localhost:8080` (configurable)

**API Version**: v1

---

## Authentication

All endpoints except `HEAD /` and `GET /` require authentication via Bearer token:

```
Authorization: Bearer <api-key>
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
  "InitialBalance": 0
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Name` | string | Yes | Account name |
| `InitialBalance` | decimal | No | Initial committed balance (default: 0) |

**Response**: `201 Created`

```json
{
  "GUID": "550e8400-e29b-41d4-a716-446655440000",
  "Name": "Checking Account",
  "Notes": null,
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
| `startTime` | DateTime | null | Filter entries created after (UTC) |
| `endTime` | DateTime | null | Filter entries created before (UTC) |
| `amountMin` | decimal | null | Minimum amount filter |
| `amountMax` | decimal | null | Maximum amount filter |

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
  "CreatedAfterUtc": null,
  "CreatedBeforeUtc": null,
  "AmountMinimum": null,
  "AmountMaximum": null
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
  "IsCommitted": false
}
```

**Request Body (Batch Entries)**:

```json
{
  "Entries": [
    {
      "Amount": 50.00,
      "Notes": "Payment 1"
    },
    {
      "Amount": 50.00,
      "Notes": "Payment 2"
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

### API Key Management Endpoints

All API key management endpoints require authentication with an admin API key.

#### Enumerate API Keys

List all API keys (values are partially redacted).

```
GET /v1/apikeys
```

**Query Parameters**:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `maxResults` | int | 1000 | Maximum results per page (1-1000) |
| `skip` | int | 0 | Number of records to skip |
| `continuationToken` | GUID | null | Token for pagination continuation |
| `ordering` | enum | CreatedDescending | Sort order: `CreatedAscending`, `CreatedDescending` |
| `search` | string | null | Search filter for key name |
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
      "GUID": "770e8400-e29b-41d4-a716-446655440000",
      "Name": "Admin Key",
      "Key": "a1b2c3d4-****-****-****-567890abcdef",
      "Active": true,
      "IsAdmin": true,
      "CreatedUtc": "2025-12-23T00:00:00Z"
    }
  ]
}
```

---

#### Create API Key

Create a new API key. The full key value is only returned once at creation time.

```
PUT /v1/apikeys
```

**Request Body**:

```json
{
  "Name": "Integration Key",
  "IsAdmin": false
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Name` | string | Yes | Descriptive name for the API key |
| `IsAdmin` | bool | No | Whether the key has admin privileges (default: false) |

**Response**: `201 Created`

```json
{
  "GUID": "880e8400-e29b-41d4-a716-446655440000",
  "Name": "Integration Key",
  "Key": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Active": true,
  "IsAdmin": false,
  "CreatedUtc": "2025-12-23T00:00:00Z"
}
```

> **Important**: Store the `Key` securely. It will be partially redacted in subsequent API calls.

---

#### Revoke API Key

Revoke and delete an API key.

```
DELETE /v1/apikeys/{apiKeyGuid}
```

**URL Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `apiKeyGuid` | GUID | API key identifier |

**Response**: `200 OK` (empty body)

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
