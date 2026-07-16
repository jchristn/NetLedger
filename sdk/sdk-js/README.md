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
const credit = await client.entry.addCredit(account.Id, 100.00, 'Initial deposit');
const debit = await client.entry.addDebit(account.Id, 25.50, 'Purchase');

// Get balance
const balance = await client.balance.get(account.Id);
console.log(`Committed: ${balance.CommittedBalance}, Pending: ${balance.PendingBalance}`);

// Commit pending entries
const result = await client.balance.commit(account.Id);
```

## Features

### Service Operations

```typescript
// Health check
const healthy = await client.service.healthCheck();

// Get service info
const info = await client.service.getInfo();

// Get the OpenAPI document used by the dashboard API Explorer
const openApiSpec = await client.service.getOpenApiSpec();
```

### Account Management

```typescript
// Create account
const account = await client.account.create('Account Name', 'Notes');

// Get account by ID
const account = await client.account.get(accountId);

// Get account by name
const account = await client.account.getByName('Account Name');

// Check if account exists
const exists = await client.account.exists(accountId);

// Delete account
await client.account.delete(accountId);

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
const credit = await client.entry.addCredit(accountId, 100.00, 'Description');

// Add multiple credits
const credits = await client.entry.addCredits(accountId, [
    { amount: 50.00, description: 'First credit' },
    { amount: 25.00, description: 'Second credit' }
]);

// Add single debit
const debit = await client.entry.addDebit(accountId, 30.00, 'Description');

// Add multiple debits
const debits = await client.entry.addDebits(accountId, [
    { amount: 10.00, description: 'First debit' },
    { amount: 15.00, description: 'Second debit' }
]);

// Get all entries
const entries = await client.entry.getAll(accountId);

// Enumerate with filters
const result = await client.entry.enumerate(accountId, {
    MaxResults: 100,
    CreatedAfterUtc: new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString(),
    AmountMinimum: 10.00,
    Ordering: EnumerationOrder.AmountDescending
});

// Enumerate debits between $5 and $50 with all metadata filters matched
const blueDebits = await client.entry.enumerate(accountId, {
    MaxResults: 50,
    DebitMinimum: 5.00,
    DebitMaximum: 50.00,
    Labels: ['blue'],
    Tags: { color: 'blue' },
    Ordering: EnumerationOrder.AmountDescending
});

// Get pending entries
const pending = await client.entry.getPending(accountId);
const pendingCredits = await client.entry.getPendingCredits(accountId);
const pendingDebits = await client.entry.getPendingDebits(accountId);

// Cancel a pending entry
await client.entry.cancel(accountId, entryId);
```

### Balance Operations

```typescript
// Get current balance
const balance = await client.balance.get(accountId);

// Get historical balance
const historical = await client.balance.getAsOf(accountId, new Date(Date.now() - 7 * 24 * 60 * 60 * 1000));

// Get all account balances
const balances = await client.balance.getAll();

// Commit all pending entries
const result = await client.balance.commit(accountId);

// Commit specific entries
const result = await client.balance.commit(accountId, [entry1Id, entry2Id]);

// Verify balance chain integrity
const isValid = await client.balance.verify(accountId);
```

### API Key Management

```typescript
// Create API key
const apiKey = await client.apiKey.create('Key Name', false); // isAdmin = false
console.log(`Key: ${apiKey.SecretKey}`); // Only available on creation

// Enumerate API keys
const result = await client.apiKey.enumerate({
    MaxResults: 50,
    Skip: 0
});

// Revoke API key
await client.apiKey.revoke(apiKey.Credential?.Id ?? '');
```

### Request History

Request history is available to administrators according to the server's tenant access rules. System administrators can inspect all tenants, while tenant administrators are scoped to their tenant.

```typescript
const history = await client.requestHistory.enumerate({
    TenantId: 'default',
    MaxResults: 25,
    Skip: 0
});

const summary = await client.requestHistory.summarize({
    MaxResults: 100,
    BucketMinutes: 15
});

const entry = await client.requestHistory.read(history.Objects[0].Id);
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
    const account = await client.account.get(accountId);
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
