# NetLedger SDK for JavaScript/TypeScript

A JavaScript/TypeScript SDK for interacting with the NetLedger Server REST API.

## Installation

```bash
npm install netledger-sdk
```

## Quick Start

```typescript
import { NetLedgerClient } from 'netledger-sdk';

// Create a client
const client = new NetLedgerClient('http://localhost:8080', 'your-api-key');

// Check server health
const isHealthy = await client.service.healthCheck();

// Create an account
const account = await client.account.create('My Account', 'Optional notes');

// Add credits and debits
const credit = await client.entry.addCredit(account.guid, 100.00, 'Initial deposit');
const debit = await client.entry.addDebit(account.guid, 25.50, 'Purchase');

// Get balance
const balance = await client.balance.get(account.guid);
console.log(`Committed: ${balance.committedBalance}, Pending: ${balance.pendingBalance}`);

// Commit pending entries
const result = await client.balance.commit(account.guid);
```

## Features

### Service Operations

```typescript
// Health check
const healthy = await client.service.healthCheck();

// Get service info
const info = await client.service.getInfo();
```

### Account Management

```typescript
// Create account
const account = await client.account.create('Account Name', 'Notes');

// Get account by GUID
const account = await client.account.get(accountGuid);

// Get account by name
const account = await client.account.getByName('Account Name');

// Check if account exists
const exists = await client.account.exists(accountGuid);

// Delete account
await client.account.delete(accountGuid);

// Enumerate accounts with pagination
const result = await client.account.enumerate({
    maxResults: 50,
    skip: 0,
    searchTerm: 'search'
});
```

### Entry Operations

```typescript
// Add single credit
const credit = await client.entry.addCredit(accountGuid, 100.00, 'Description');

// Add multiple credits
const credits = await client.entry.addCredits(accountGuid, [
    { amount: 50.00, description: 'First credit' },
    { amount: 25.00, description: 'Second credit' }
]);

// Add single debit
const debit = await client.entry.addDebit(accountGuid, 30.00, 'Description');

// Add multiple debits
const debits = await client.entry.addDebits(accountGuid, [
    { amount: 10.00, description: 'First debit' },
    { amount: 15.00, description: 'Second debit' }
]);

// Get all entries
const entries = await client.entry.getAll(accountGuid);

// Enumerate with filters
const result = await client.entry.enumerate(accountGuid, {
    maxResults: 100,
    createdAfterUtc: new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString(),
    amountMin: 10.00,
    ordering: EnumerationOrder.AmountDescending
});

// Get pending entries
const pending = await client.entry.getPending(accountGuid);
const pendingCredits = await client.entry.getPendingCredits(accountGuid);
const pendingDebits = await client.entry.getPendingDebits(accountGuid);

// Cancel a pending entry
await client.entry.cancel(accountGuid, entryGuid);
```

### Balance Operations

```typescript
// Get current balance
const balance = await client.balance.get(accountGuid);

// Get historical balance
const historical = await client.balance.getAsOf(accountGuid, new Date(Date.now() - 7 * 24 * 60 * 60 * 1000));

// Get all account balances
const balances = await client.balance.getAll();

// Commit all pending entries
const result = await client.balance.commit(accountGuid);

// Commit specific entries
const result = await client.balance.commit(accountGuid, [entry1Guid, entry2Guid]);

// Verify balance chain integrity
const isValid = await client.balance.verify(accountGuid);
```

### API Key Management

```typescript
// Create API key
const apiKey = await client.apiKey.create('Key Name', false); // isAdmin = false
console.log(`Key: ${apiKey.apiKey}`); // Only available on creation

// Enumerate API keys
const result = await client.apiKey.enumerate({
    maxResults: 50,
    skip: 0
});

// Revoke API key
await client.apiKey.revoke(apiKeyGuid);
```

## Error Handling

The SDK throws specific errors for different scenarios:

```typescript
import {
    NetLedgerConnectionError,
    NetLedgerApiError,
    NetLedgerValidationError
} from 'netledger-sdk';

try {
    const account = await client.account.get(accountGuid);
} catch (err) {
    if (err instanceof NetLedgerConnectionError) {
        // Unable to connect to the server
        console.log(`Connection error: ${err.message}`);
    } else if (err instanceof NetLedgerApiError) {
        // Server returned an error
        console.log(`API error ${err.statusCode}: ${err.message}`);
        if (err.details) {
            console.log(`Details: ${err.details}`);
        }
    } else if (err instanceof NetLedgerValidationError) {
        // Invalid input parameters
        console.log(`Validation error for ${err.parameterName}: ${err.message}`);
    }
}
```

## Configuration

```typescript
const client = new NetLedgerClient('http://localhost:8080', 'your-api-key', {
    timeoutMs: 60000 // 60 seconds (default: 30000)
});
```

## Building from Source

```bash
# Install dependencies
npm install

# Build
npm run build

# Run tests
npm test -- http://localhost:8080 your-api-key
```

## License

MIT License - see the LICENSE file for details.
