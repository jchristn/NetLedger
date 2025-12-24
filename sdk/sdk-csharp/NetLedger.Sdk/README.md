# NetLedger SDK for .NET

A .NET SDK for interacting with the NetLedger Server REST API.

## Installation

```bash
dotnet add package NetLedger.Sdk
```

Or add a project reference:

```bash
dotnet add reference path/to/NetLedger.Sdk.csproj
```

## Quick Start

```csharp
using NetLedger.Sdk;

// Create a client
using var client = new NetLedgerClient("http://localhost:8080", "your-api-key");

// Check server health
bool isHealthy = await client.Service.HealthCheckAsync();

// Create an account
Account account = await client.Account.CreateAsync("My Account", "Optional notes");

// Add credits and debits
Entry credit = await client.Entry.AddCreditAsync(account.GUID, 100.00m, "Initial deposit");
Entry debit = await client.Entry.AddDebitAsync(account.GUID, 25.50m, "Purchase");

// Get balance
Balance balance = await client.Balance.GetAsync(account.GUID);
Console.WriteLine($"Committed: {balance.CommittedBalance}, Pending: {balance.PendingBalance}");

// Commit pending entries
CommitResult result = await client.Balance.CommitAsync(account.GUID);
```

## Features

### Service Operations

```csharp
// Health check
bool healthy = await client.Service.HealthCheckAsync();

// Get service info
ServiceInfo info = await client.Service.GetInfoAsync();
```

### Account Management

```csharp
// Create account
Account account = await client.Account.CreateAsync("Account Name", "Notes");

// Get account by GUID
Account account = await client.Account.GetAsync(accountGuid);

// Get account by name
Account account = await client.Account.GetByNameAsync("Account Name");

// Check if account exists
bool exists = await client.Account.ExistsAsync(accountGuid);

// Delete account
await client.Account.DeleteAsync(accountGuid);

// Enumerate accounts with pagination
var result = await client.Account.EnumerateAsync(new AccountEnumerationQuery
{
    MaxResults = 50,
    Skip = 0,
    SearchTerm = "search"
});
```

### Entry Operations

```csharp
// Add single credit
Entry credit = await client.Entry.AddCreditAsync(accountGuid, 100.00m, "Description");

// Add multiple credits
var credits = await client.Entry.AddCreditsAsync(accountGuid, new List<EntryInput>
{
    new EntryInput(50.00m, "First credit"),
    new EntryInput(25.00m, "Second credit")
});

// Add single debit
Entry debit = await client.Entry.AddDebitAsync(accountGuid, 30.00m, "Description");

// Add multiple debits
var debits = await client.Entry.AddDebitsAsync(accountGuid, new List<EntryInput>
{
    new EntryInput(10.00m, "First debit"),
    new EntryInput(15.00m, "Second debit")
});

// Get all entries
List<Entry> entries = await client.Entry.GetAllAsync(accountGuid);

// Enumerate with filters
var result = await client.Entry.EnumerateAsync(accountGuid, new EntryEnumerationQuery
{
    MaxResults = 100,
    CreatedAfterUtc = DateTime.UtcNow.AddDays(-30),
    AmountMin = 10.00m,
    Ordering = EnumerationOrder.AmountDescending
});

// Get pending entries
List<Entry> pending = await client.Entry.GetPendingAsync(accountGuid);
List<Entry> pendingCredits = await client.Entry.GetPendingCreditsAsync(accountGuid);
List<Entry> pendingDebits = await client.Entry.GetPendingDebitsAsync(accountGuid);

// Cancel a pending entry
await client.Entry.CancelAsync(accountGuid, entryGuid);
```

### Balance Operations

```csharp
// Get current balance
Balance balance = await client.Balance.GetAsync(accountGuid);

// Get historical balance
Balance historical = await client.Balance.GetAsOfAsync(accountGuid, DateTime.UtcNow.AddDays(-7));

// Get all account balances
List<Balance> balances = await client.Balance.GetAllAsync();

// Commit all pending entries
CommitResult result = await client.Balance.CommitAsync(accountGuid);

// Commit specific entries
CommitResult result = await client.Balance.CommitAsync(accountGuid, new List<Guid> { entry1Guid, entry2Guid });

// Verify balance chain integrity
bool isValid = await client.Balance.VerifyAsync(accountGuid);
```

### API Key Management

```csharp
// Create API key
ApiKeyInfo apiKey = await client.ApiKey.CreateAsync("Key Name", isAdmin: false);
Console.WriteLine($"Key: {apiKey.ApiKey}"); // Only available on creation

// Enumerate API keys
var result = await client.ApiKey.EnumerateAsync(new ApiKeyEnumerationQuery
{
    MaxResults = 50,
    Skip = 0
});

// Revoke API key
await client.ApiKey.RevokeAsync(apiKeyGuid);
```

## Error Handling

The SDK throws specific exceptions for different error scenarios:

```csharp
try
{
    var account = await client.Account.GetAsync(accountGuid);
}
catch (NetLedgerConnectionException ex)
{
    // Unable to connect to the server
    Console.WriteLine($"Connection error: {ex.Message}");
}
catch (NetLedgerApiException ex)
{
    // Server returned an error
    Console.WriteLine($"API error {ex.StatusCode}: {ex.Message}");
    if (ex.Details != null)
        Console.WriteLine($"Details: {ex.Details}");
}
catch (NetLedgerValidationException ex)
{
    // Invalid input parameters
    Console.WriteLine($"Validation error for {ex.ParameterName}: {ex.Message}");
}
```

## Configuration

```csharp
var client = new NetLedgerClient("http://localhost:8080", "your-api-key");

// Set custom timeout (default: 30 seconds)
client.TimeoutMs = 60000; // 60 seconds
```

## Thread Safety

The `NetLedgerClient` is thread-safe and can be reused across multiple operations. It is recommended to create a single instance and share it across your application.

## Disposal

The client implements `IDisposable`. Always dispose of it when done:

```csharp
using var client = new NetLedgerClient("http://localhost:8080", "your-api-key");
// Use client...
// Automatically disposed at end of scope
```

Or manually:

```csharp
var client = new NetLedgerClient("http://localhost:8080", "your-api-key");
try
{
    // Use client...
}
finally
{
    client.Dispose();
}
```

## License

MIT License - see the LICENSE file for details.
