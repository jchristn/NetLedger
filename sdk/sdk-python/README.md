# NetLedger SDK for Python

A Python SDK for interacting with the NetLedger Server REST API.

## v4.0.0 Archive Support

NetLedger Server clients remain active-data clients. v4.0.0 adds Archive Server methods for cold reads, archive metadata inspection, and migration lifecycle operations. Keep active and archive clients configured with separate base URLs.

```python
from datetime import datetime, timedelta, timezone

active = NetLedgerClient('http://localhost:8080', token, tenant_id=tenant_id)
archive = NetLedgerClient('http://localhost:8081', token, tenant_id=tenant_id)

health = archive.archive.health()
manifests = archive.archive.manifests({'maxResults': 50})
pools = archive.archive.storage_pools()
pool_health = archive.archive.storage_pool_health(pools[0]['Id'])
export_result = active.archive.export_tenant_account_entries(tenant_id, account_id, {
    'ToUtc': (datetime.now(timezone.utc) - timedelta(days=365)).isoformat(),
    'DeleteAfterCommit': False
})
migration = archive.archive.migration(export_result['MigrationId'])
batches = archive.archive.migration_batches(migration['Id'])
history_export = active.archive.export_request_history({
    'TenantId': tenant_id,
    'ToUtc': (datetime.now(timezone.utc) - timedelta(days=365)).isoformat()
})
cold_entries = archive.archive.tenant_entries(tenant_id, account_id, {
    'maxResults': 25,
    'ordering': 'CreatedDescending',
    'allowPartial': True
})
cold_balance = archive.archive.tenant_balance_as_of(
    tenant_id,
    account_id,
    (datetime.now(timezone.utc) - timedelta(days=365)).isoformat()
)
verification = archive.archive.verify_tenant_account(tenant_id, account_id)
cold_history = archive.archive.request_history({
    'maxResults': 25,
    'allowPartial': True
})
```

For externally prepared archive payloads, the Archive Server client also exposes the lower-level migration lifecycle: `create_migration`, `create_migration_batch`, `upload_migration_batch_content`, `seal_migration`, `commit_migration`, and `abort_migration`.

For an end-to-end local validation that starts disposable active and archive servers, exports old entries, and verifies hot/cold retrieval seams, run `dotnet run --project src/ArchivalValidation/ArchivalValidation.csproj --framework net8.0` from the repository root.

## Installation

```bash
pip install netledger-sdk
```

Or install from source:

```bash
pip install -e .
```

## Quick Start

```python
from netledger_sdk import NetLedgerClient

# Create a client
client = NetLedgerClient('http://localhost:8080', 'your-api-key')

# Check server health
is_healthy = client.service.health_check()

# Create an account
account = client.account.create('My Account', 'Optional notes')

# Add credits and debits
credit = client.entry.add_credit(account.id, 100.00, 'Initial deposit')
debit = client.entry.add_debit(account.id, 25.50, 'Purchase')

# Get balance
balance = client.balance.get(account.id)
print(f'Committed: {balance.committed_balance}, Pending: {balance.pending_balance}')

# Commit pending entries
result = client.balance.commit(account.id)

# Close the client when done
client.close()
```

## Context Manager Support

```python
with NetLedgerClient('http://localhost:8080', 'your-api-key') as client:
    account = client.account.create('My Account')
    # Client is automatically closed at the end
```

## Features

### Service Operations

```python
# Health check
healthy = client.service.health_check()

# Get service info
info = client.service.get_info()

# Get the OpenAPI document used by the dashboard API Explorer
openapi_spec = client.service.get_openapi_spec()
```

### Account Management

```python
# Create account
account = client.account.create('Account Name', 'Notes')

# Get account by ID
account = client.account.get(account_id)

# Get account by name
account = client.account.get_by_name('Account Name')

# Check if account exists
exists = client.account.exists(account_id)

# Delete account
client.account.delete(account_id)

# Enumerate accounts with pagination
from netledger_sdk import AccountEnumerationQuery

result = client.account.enumerate(AccountEnumerationQuery(
    max_results=50,
    skip=0,
    search_term='search'
))
```

### Entry Operations

```python
from netledger_sdk import EntryInput, EntryEnumerationQuery, EnumerationOrder

# Add single credit
credit = client.entry.add_credit(account_id, 100.00, 'Description')

# Add multiple credits
credits = client.entry.add_credits(account_id, [
    EntryInput(50.00, 'First credit'),
    EntryInput(25.00, 'Second credit')
])

# Add single debit
debit = client.entry.add_debit(account_id, 30.00, 'Description')

# Add multiple debits
debits = client.entry.add_debits(account_id, [
    EntryInput(10.00, 'First debit'),
    EntryInput(15.00, 'Second debit')
])

# Get all entries
entries = client.entry.get_all(account_id)

# Enumerate with filters
result = client.entry.enumerate(account_id, EntryEnumerationQuery(
    max_results=100,
    amount_min=10.00,
    ordering=EnumerationOrder.AMOUNT_DESCENDING
))

# Enumerate debits between $5 and $50 with all metadata filters matched
blue_debits = client.entry.enumerate(account_id, EntryEnumerationQuery(
    max_results=50,
    debit_minimum=5.00,
    debit_maximum=50.00,
    labels=['blue'],
    tags={'color': 'blue'},
    ordering=EnumerationOrder.AMOUNT_DESCENDING
))

# Get pending entries
pending = client.entry.get_pending(account_id)
pending_credits = client.entry.get_pending_credits(account_id)
pending_debits = client.entry.get_pending_debits(account_id)

# Cancel a pending entry
client.entry.cancel(account_id, entry_id)
```

### Balance Operations

```python
from datetime import datetime

# Get current balance
balance = client.balance.get(account_id)

# Get historical balance
historical = client.balance.get_as_of(account_id, datetime.utcnow())

# Get all account balances
balances = client.balance.get_all()

# Commit all pending entries
result = client.balance.commit(account_id)

# Commit specific entries
result = client.balance.commit(account_id, [entry1_id, entry2_id])

# Verify balance chain integrity
is_valid = client.balance.verify(account_id)
```

### API Key Management

```python
from netledger_sdk import ApiKeyEnumerationQuery

# Create API key
api_key = client.api_key.create('Key Name', is_admin=False)
print(f'Key: {api_key.secret_key}')  # Only available on creation

# Enumerate API keys
result = client.api_key.enumerate(ApiKeyEnumerationQuery(
    max_results=50,
    skip=0
))

# Revoke API key
client.api_key.revoke(api_key.credential.id)
```

### Request History

Request history is available to administrators according to the server's tenant access rules. System administrators can inspect all tenants, while tenant administrators are scoped to their tenant.

```python
from netledger_sdk import RequestHistoryQuery

history = client.request_history.enumerate(RequestHistoryQuery(
    tenant_id='default',
    max_results=25,
    skip=0
))

summary = client.request_history.summarize(RequestHistoryQuery(
    max_results=100,
    bucket_minutes=15
))

entry = client.request_history.read(history.objects[0].id)
```

## Error Handling

The SDK raises specific exceptions for different scenarios:

```python
from netledger_sdk import (
    NetLedgerConnectionError,
    NetLedgerApiError,
    NetLedgerValidationError
)

try:
    account = client.account.get(account_id)
except NetLedgerConnectionError as e:
    # Unable to connect to the server
    print(f'Connection error: {e}')
    if e.cause:
        print(f'Cause: {e.cause}')
except NetLedgerApiError as e:
    # Server returned an error
    print(f'API error {e.status_code}: {e}')
    if e.details:
        print(f'Details: {e.details}')
except NetLedgerValidationError as e:
    # Invalid input parameters
    print(f'Validation error for {e.parameter_name}: {e}')
```

## Configuration

```python
client = NetLedgerClient(
    'http://localhost:8080',
    'your-api-key',
    tenant_id='default',
    timeout_seconds=60.0  # Default is 30.0
)
```

## Running the Test Harness

```bash
cd tests
python test_harness.py http://localhost:8080 your-api-key
python test_harness.py http://localhost:8080 your-api-key http://localhost:8081
```

The third argument is optional. When supplied, the harness also runs Archive Server health, storage-pool, metadata, request-history, and active export checks against the archive endpoint.

## Requirements

- Python 3.8+
- requests >= 2.28.0

## License

MIT License - see the LICENSE file for details.
