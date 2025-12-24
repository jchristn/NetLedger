# NetLedger SDK for Python

A Python SDK for interacting with the NetLedger Server REST API.

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
credit = client.entry.add_credit(account.guid, 100.00, 'Initial deposit')
debit = client.entry.add_debit(account.guid, 25.50, 'Purchase')

# Get balance
balance = client.balance.get(account.guid)
print(f'Committed: {balance.committed_balance}, Pending: {balance.pending_balance}')

# Commit pending entries
result = client.balance.commit(account.guid)

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
```

### Account Management

```python
# Create account
account = client.account.create('Account Name', 'Notes')

# Get account by GUID
account = client.account.get(account_guid)

# Get account by name
account = client.account.get_by_name('Account Name')

# Check if account exists
exists = client.account.exists(account_guid)

# Delete account
client.account.delete(account_guid)

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
credit = client.entry.add_credit(account_guid, 100.00, 'Description')

# Add multiple credits
credits = client.entry.add_credits(account_guid, [
    EntryInput(50.00, 'First credit'),
    EntryInput(25.00, 'Second credit')
])

# Add single debit
debit = client.entry.add_debit(account_guid, 30.00, 'Description')

# Add multiple debits
debits = client.entry.add_debits(account_guid, [
    EntryInput(10.00, 'First debit'),
    EntryInput(15.00, 'Second debit')
])

# Get all entries
entries = client.entry.get_all(account_guid)

# Enumerate with filters
result = client.entry.enumerate(account_guid, EntryEnumerationQuery(
    max_results=100,
    amount_min=10.00,
    ordering=EnumerationOrder.AMOUNT_DESCENDING
))

# Get pending entries
pending = client.entry.get_pending(account_guid)
pending_credits = client.entry.get_pending_credits(account_guid)
pending_debits = client.entry.get_pending_debits(account_guid)

# Cancel a pending entry
client.entry.cancel(account_guid, entry_guid)
```

### Balance Operations

```python
from datetime import datetime

# Get current balance
balance = client.balance.get(account_guid)

# Get historical balance
historical = client.balance.get_as_of(account_guid, datetime.utcnow())

# Get all account balances
balances = client.balance.get_all()

# Commit all pending entries
result = client.balance.commit(account_guid)

# Commit specific entries
result = client.balance.commit(account_guid, [entry1_guid, entry2_guid])

# Verify balance chain integrity
is_valid = client.balance.verify(account_guid)
```

### API Key Management

```python
from netledger_sdk import ApiKeyEnumerationQuery

# Create API key
api_key = client.api_key.create('Key Name', is_admin=False)
print(f'Key: {api_key.api_key}')  # Only available on creation

# Enumerate API keys
result = client.api_key.enumerate(ApiKeyEnumerationQuery(
    max_results=50,
    skip=0
))

# Revoke API key
client.api_key.revoke(api_key_guid)
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
    account = client.account.get(account_guid)
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
    timeout_seconds=60.0  # Default is 30.0
)
```

## Running the Test Harness

```bash
cd tests
python test_harness.py http://localhost:8080 your-api-key
```

## Requirements

- Python 3.8+
- requests >= 2.28.0

## License

MIT License - see the LICENSE file for details.
