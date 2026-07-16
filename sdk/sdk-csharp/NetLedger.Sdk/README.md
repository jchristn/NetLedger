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
using NetLedgerClient client = new NetLedgerClient("http://localhost:8080", "your-api-key");

// Check server health
bool isHealthy = await client.Service.HealthCheckAsync();

// Create an account
Account account = await client.Account.CreateAsync("My Account", "Optional notes");

// Add credits and debits
Entry credit = await client.Entry.AddCreditAsync(account.Id, 100.00m, "Initial deposit");
Entry debit = await client.Entry.AddDebitAsync(account.Id, 25.50m, "Purchase");

// Get balance
Balance balance = await client.Balance.GetAsync(account.Id);
Console.WriteLine($"Committed: {balance.CommittedBalance}, Pending: {balance.PendingBalance}");

// Commit pending entries
CommitResult result = await client.Balance.CommitAsync(account.Id);
```

## Features

### Service Operations

```csharp
// Health check
bool healthy = await client.Service.HealthCheckAsync();

// Get service info
ServiceInfo info = await client.Service.GetInfoAsync();

// Get the OpenAPI document JSON used by the dashboard API Explorer
string openApiJson = await client.Service.GetOpenApiJsonAsync();
```

### Account Management

```csharp
// Create account
Account account = await client.Account.CreateAsync("Account Name", "Notes");

// Get account by ID
Account account = await client.Account.GetAsync(accountId);

// Get account by name
Account account = await client.Account.GetByNameAsync("Account Name");

// Check if account exists
bool exists = await client.Account.ExistsAsync(accountId);

// Delete account
await client.Account.DeleteAsync(accountId);

// Enumerate accounts with pagination
EnumerationResult<Account> result = await client.Account.EnumerateAsync(new AccountEnumerationQuery
{
    MaxResults = 50,
    Skip = 0,
    SearchTerm = "search"
});
```

### Entry Operations

```csharp
// Add single credit
Entry credit = await client.Entry.AddCreditAsync(accountId, 100.00m, "Description");

// Add multiple credits
List<Entry> credits = await client.Entry.AddCreditsAsync(accountId, new List<EntryInput>
{
    new EntryInput(50.00m, "First credit"),
    new EntryInput(25.00m, "Second credit")
});

// Add single debit
Entry debit = await client.Entry.AddDebitAsync(accountId, 30.00m, "Description");

// Add multiple debits
List<Entry> debits = await client.Entry.AddDebitsAsync(accountId, new List<EntryInput>
{
    new EntryInput(10.00m, "First debit"),
    new EntryInput(15.00m, "Second debit")
});

// Get all entries
List<Entry> entries = await client.Entry.GetAllAsync(accountId);

// Enumerate with filters
EnumerationResult<Entry> result = await client.Entry.EnumerateAsync(accountId, new EntryEnumerationQuery
{
    MaxResults = 100,
    CreatedAfterUtc = DateTime.UtcNow.AddDays(-30),
    AmountMinimum = 10.00m,
    Ordering = EnumerationOrder.AmountDescending
});

// Enumerate debits between $5 and $50 with all metadata filters matched
EnumerationResult<Entry> blueDebits = await client.Entry.EnumerateAsync(accountId, new EntryEnumerationQuery
{
    MaxResults = 50,
    DebitMinimum = 5.00m,
    DebitMaximum = 50.00m,
    Labels = new List<string> { "blue" },
    Tags = new Dictionary<string, string> { { "color", "blue" } },
    Ordering = EnumerationOrder.AmountDescending
});

// Get pending entries
List<Entry> pending = await client.Entry.GetPendingAsync(accountId);
List<Entry> pendingCredits = await client.Entry.GetPendingCreditsAsync(accountId);
List<Entry> pendingDebits = await client.Entry.GetPendingDebitsAsync(accountId);

// Cancel a pending entry
await client.Entry.CancelAsync(accountId, entryId);
```

### Balance Operations

```csharp
// Get current balance
Balance balance = await client.Balance.GetAsync(accountId);

// Get historical balance
Balance historical = await client.Balance.GetAsOfAsync(accountId, DateTime.UtcNow.AddDays(-7));

// Get all account balances
List<Balance> balances = await client.Balance.GetAllAsync();

// Commit all pending entries
CommitResult result = await client.Balance.CommitAsync(accountId);

// Commit specific entries
CommitResult result = await client.Balance.CommitAsync(accountId, new List<string> { entry1Id, entry2Id });

// Verify balance chain integrity
bool isValid = await client.Balance.VerifyAsync(accountId);
```

### API Key Management

```csharp
// Create API key
CredentialCreateResponse apiKey = await client.ApiKey.CreateAsync("Key Name", isAdmin: false);
Console.WriteLine($"Key: {apiKey.SecretKey}"); // Only available on creation

// Enumerate API keys
EnumerationResult<ApiKeyInfo> result = await client.ApiKey.EnumerateAsync(new ApiKeyEnumerationQuery
{
    MaxResults = 50,
    Skip = 0
});

// Revoke API key
await client.ApiKey.RevokeAsync(apiKey.Credential?.Id ?? String.Empty);
```

### Request History

Request history is available to administrators according to the server's tenant access rules. System administrators can inspect all tenants, while tenant administrators are scoped to their tenant.

```csharp
EnumerationResult<RequestHistoryEntry> history = await client.RequestHistory.EnumerateAsync(new RequestHistoryQuery
{
    TenantId = "default",
    MaxResults = 25,
    Skip = 0
});

RequestHistorySummary summary = await client.RequestHistory.SummarizeAsync(new RequestHistoryQuery
{
    MaxResults = 100,
    BucketMinutes = 15
});

RequestHistoryEntry entry = await client.RequestHistory.ReadAsync(history.Objects[0].Id);
```

## Error Handling

The SDK throws specific exceptions for different error scenarios:

```csharp
try
{
    Account account = await client.Account.GetAsync(accountId);
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
using NetLedgerClient client = new NetLedgerClient("http://localhost:8080", "your-api-key");
// Use client...
// Automatically disposed at end of scope
```

Or manually:

```csharp
NetLedgerClient client = new NetLedgerClient("http://localhost:8080", "your-api-key");
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
