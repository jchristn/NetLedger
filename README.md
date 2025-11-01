<img src="https://github.com/jchristn/NetLedger/raw/main/Assets/icon.jpg" height="128" width="128">

# NetLedger

[![NuGet Version](https://img.shields.io/nuget/v/NetLedger.svg?style=flat)](https://www.nuget.org/packages/NetLedger/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/NetLedger.svg)](https://www.nuget.org/packages/NetLedger)
[![License](https://img.shields.io/github/license/jchristn/NetLedger)](https://github.com/jchristn/NetLedger/blob/main/LICENSE.md)

NetLedger is a thread-safe, self-contained ledgering library for .NET 8.0 that provides rigorous financial transaction control with full audit trails. Built on SQLite with async/await throughout, it enables strict separation between pending and committed transactions, making it ideal for applications requiring precise financial controls and auditability.

## Who Should Use NetLedger

NetLedger is designed for developers building applications that require:

- **Strict Financial Controls** - Separate pending and committed transaction states with explicit commit operations
- **Full Audit Trails** - Immutable transaction history with balance chains for forensic accounting
- **Thread Safety** - Account-level locking ensures safe concurrent access without race conditions
- **Embedded Storage** - Self-contained SQLite database with no external dependencies
- **Transactional Integrity** - ACID-compliant operations with atomic commits and rollback on failure
- **Async/Await Support** - Modern .NET async patterns with cancellation token support throughout

**Ideal use cases:** Financial applications, expense tracking systems, point-of-sale systems, accounting software, multi-user financial platforms, billing systems, payment processing, and any application requiring double-entry bookkeeping.

## What NetLedger Does

### Core Capabilities

- ✅ **Account Management** - Create, retrieve, search, and delete accounts with optional initial balances
- ✅ **Transaction Operations** - Add credits and debits as pending or immediately committed
- ✅ **Batch Operations** - Process multiple credits or debits in a single atomic operation
- ✅ **Dual Balance Tracking** - Separate committed balance (finalized) and pending balance (projected)
- ✅ **Selective Commits** - Commit all pending entries or specific entries by GUID
- ✅ **Entry Cancellation** - Cancel pending entries before commit
- ✅ **Transaction History** - Query entries with filtering by date range, amount range, and ordering
- ✅ **Pagination Support** - Continuation token-based enumeration for large datasets (up to 1000 records per query)
- ✅ **Point-in-Time Balances** - Calculate balances as of any historical timestamp
- ✅ **Balance Chain Verification** - Validate audit trail integrity across all balance entries
- ✅ **Event Notifications** - Async events for all state changes (account created/deleted, entries added/committed/canceled)
- ✅ **Thread-Safe Operations** - SemaphoreSlim-based account locking prevents concurrent modification issues
- ✅ **Connection Pooling** - High-performance connection pool (max 500 connections, 120s timeout)

### What NetLedger Does NOT Do

- ❌ **Multi-Currency Support** - Single currency per ledger (implement multiple ledgers for multi-currency)
- ❌ **Automatic Transfers** - No built-in inter-account transfers (manually debit one account and credit another)
- ❌ **Authentication/Authorization** - No user management or permissions (implement at application level)
- ❌ **Multi-Tenant Isolation** - Single database instance (use separate databases for tenants)
- ❌ **External Databases** - SQLite only (contact maintainer for external database support)
- ❌ **Transaction Reversal** - Cannot undo committed entries (create offsetting entries instead)
- ❌ **Scheduled Transactions** - No recurring or future-dated entries
- ❌ **Account Hierarchies** - No parent-child account relationships
- ❌ **Budget Enforcement** - No built-in spending limits or budget tracking
- ❌ **Custom Fields** - Fixed schema for accounts and entries

## Installation

```bash
dotnet add package NetLedger
```

Or via NuGet Package Manager:

```powershell
Install-Package NetLedger
```

## Quick Start

```csharp
using NetLedger;

// Initialize ledger (creates or opens SQLite database)
Ledger ledger = new Ledger("accounting.db");

// Create an account with optional initial balance
Guid accountGuid = await ledger.CreateAccountAsync("Operating Account", 1000.00m);

// Add a pending credit
Guid creditGuid = await ledger.AddCreditAsync(accountGuid, 500.00m, "Customer payment");

// Add a pending debit
Guid debitGuid = await ledger.AddDebitAsync(accountGuid, 150.00m, "Supplier invoice");

// Check balances before commit
Balance balance = await ledger.GetBalanceAsync(accountGuid);
Console.WriteLine($"Committed: ${balance.CommittedBalance}");  // 1000.00
Console.WriteLine($"Pending: ${balance.PendingBalance}");      // 1350.00

// Commit all pending entries
balance = await ledger.CommitEntriesAsync(accountGuid);
Console.WriteLine($"Committed: ${balance.CommittedBalance}");  // 1350.00

// Cleanup
await ledger.DisposeAsync();
```

## Detailed Usage

### Account Management

```csharp
// Create account with zero balance
Guid guid1 = await ledger.CreateAccountAsync("Checking Account");

// Create account with initial balance
Guid guid2 = await ledger.CreateAccountAsync("Savings Account", 5000.00m);

// Create account with negative balance (e.g., credit card)
Guid guid3 = await ledger.CreateAccountAsync("Credit Card", -250.00m);

// Retrieve account by name
Account account = await ledger.GetAccountByNameAsync("Checking Account");

// Retrieve account by GUID
Account account = await ledger.GetAccountByGuidAsync(guid1);

// Get all accounts
List<Account> accounts = await ledger.GetAllAccountsAsync();

// Search accounts with pagination
List<Account> results = await ledger.GetAllAccountsAsync(
    searchTerm: "Savings",
    skip: 0,
    take: 10
);

// Delete account by name
await ledger.DeleteAccountByNameAsync("Checking Account");

// Delete account by GUID
await ledger.DeleteAccountByGuidAsync(guid1);
```

### Adding Transactions

```csharp
Guid accountGuid = await ledger.CreateAccountAsync("Revenue Account", 0m);

// Add pending credit (default)
Guid entryGuid = await ledger.AddCreditAsync(
    accountGuid,
    amount: 250.00m,
    notes: "Invoice #1234"
);

// Add immediately committed credit
Guid committedGuid = await ledger.AddCreditAsync(
    accountGuid,
    amount: 100.00m,
    notes: "Cash sale",
    isCommitted: true
);

// Add pending debit
Guid debitGuid = await ledger.AddDebitAsync(
    accountGuid,
    amount: 50.00m,
    notes: "Bank fee"
);

// Batch add multiple credits
List<(decimal amount, string notes)> credits = new List<(decimal, string)>
{
    (100.00m, "Sale 1"),
    (200.00m, "Sale 2"),
    (150.00m, "Sale 3")
};
List<Guid> creditGuids = await ledger.AddCreditsAsync(accountGuid, credits);

// Batch add multiple debits
List<(decimal amount, string notes)> debits = new List<(decimal, string)>
{
    (25.00m, "Fee 1"),
    (30.00m, "Fee 2")
};
List<Guid> debitGuids = await ledger.AddDebitsAsync(accountGuid, debits);

// Batch add with immediate commit
List<Guid> committedGuids = await ledger.AddCreditsAsync(
    accountGuid,
    credits,
    isCommitted: true
);
```

### Working with Balances

```csharp
Guid accountGuid = await ledger.CreateAccountAsync("Main Account", 1000.00m);

// Add some pending transactions
await ledger.AddCreditAsync(accountGuid, 500.00m, "Pending credit");
await ledger.AddDebitAsync(accountGuid, 100.00m, "Pending debit");

// Get current balance
Balance balance = await ledger.GetBalanceAsync(accountGuid);

Console.WriteLine($"Account: {balance.Name}");
Console.WriteLine($"Committed Balance: ${balance.CommittedBalance}");  // 1000.00
Console.WriteLine($"Pending Balance: ${balance.PendingBalance}");      // 1400.00

// Examine pending transactions
Console.WriteLine($"Pending Credits: {balance.PendingCredits.Count} totaling ${balance.PendingCredits.Total}");
Console.WriteLine($"Pending Debits: {balance.PendingDebits.Count} totaling ${balance.PendingDebits.Total}");

// Access individual pending entries
foreach (Entry entry in balance.PendingCredits.Entries)
{
    Console.WriteLine($"  Credit: ${entry.Amount} - {entry.Description}");
}

// Get balances for all accounts
Dictionary<Guid, Balance> allBalances = await ledger.GetAllBalancesAsync();
foreach (KeyValuePair<Guid, Balance> kvp in allBalances)
{
    Console.WriteLine($"{kvp.Value.Name}: ${kvp.Value.CommittedBalance}");
}

// Get balance as of specific date/time
DateTime asOf = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc);
decimal historicalBalance = await ledger.GetBalanceAsOfAsync(accountGuid, asOf);
```

### Committing Transactions

```csharp
Guid accountGuid = await ledger.CreateAccountAsync("Operations", 500.00m);

// Add several pending entries
Guid credit1 = await ledger.AddCreditAsync(accountGuid, 100.00m, "Entry 1");
Guid credit2 = await ledger.AddCreditAsync(accountGuid, 200.00m, "Entry 2");
Guid debit1 = await ledger.AddDebitAsync(accountGuid, 50.00m, "Entry 3");

// Commit ALL pending entries
Balance balance = await ledger.CommitEntriesAsync(accountGuid);
Console.WriteLine($"New Balance: ${balance.CommittedBalance}");  // 750.00

// Add more pending entries
Guid credit3 = await ledger.AddCreditAsync(accountGuid, 300.00m, "Entry 4");
Guid credit4 = await ledger.AddCreditAsync(accountGuid, 400.00m, "Entry 5");
Guid debit2 = await ledger.AddDebitAsync(accountGuid, 75.00m, "Entry 6");

// Commit SPECIFIC entries only
List<Guid> toCommit = new List<Guid> { credit3, debit2 };
balance = await ledger.CommitEntriesAsync(accountGuid, toCommit);

Console.WriteLine($"Committed Balance: ${balance.CommittedBalance}");  // 975.00
Console.WriteLine($"Pending Balance: ${balance.PendingBalance}");      // 1375.00 (includes uncommitted credit4)

// Examine what was committed
Console.WriteLine($"Committed Entry GUIDs: {string.Join(", ", balance.Committed)}");
```

### Managing Pending Entries

```csharp
Guid accountGuid = await ledger.CreateAccountAsync("Test Account", 100.00m);

await ledger.AddCreditAsync(accountGuid, 50.00m, "Credit 1");
await ledger.AddCreditAsync(accountGuid, 75.00m, "Credit 2");
await ledger.AddDebitAsync(accountGuid, 25.00m, "Debit 1");
await ledger.AddDebitAsync(accountGuid, 30.00m, "Debit 2");

// Get all pending entries
List<Entry> allPending = await ledger.GetPendingEntriesAsync(accountGuid);
Console.WriteLine($"Total pending: {allPending.Count}");  // 4

// Get only pending credits
List<Entry> pendingCredits = await ledger.GetPendingCreditsAsync(accountGuid);
Console.WriteLine($"Pending credits: {pendingCredits.Count}");  // 2

// Get only pending debits
List<Entry> pendingDebits = await ledger.GetPendingDebitsAsync(accountGuid);
Console.WriteLine($"Pending debits: {pendingDebits.Count}");  // 2

// Cancel a pending entry
Guid entryToCancel = allPending[0].GUID;
await ledger.CancelPendingAsync(accountGuid, entryToCancel);

// Verify cancellation
List<Entry> afterCancel = await ledger.GetPendingEntriesAsync(accountGuid);
Console.WriteLine($"Remaining pending: {afterCancel.Count}");  // 3
```

### Querying Transaction History

```csharp
Guid accountGuid = await ledger.CreateAccountAsync("History Test", 0m);

// Add and commit various transactions
await ledger.AddCreditAsync(accountGuid, 100.00m, "January sale", isCommitted: true);
await Task.Delay(100);  // Ensure different timestamps
await ledger.AddDebitAsync(accountGuid, 50.00m, "February expense", isCommitted: true);
await Task.Delay(100);
await ledger.AddCreditAsync(accountGuid, 200.00m, "March sale", isCommitted: true);

// Get entries with basic filtering (excludes balance entries by default)
List<Entry> entries = await ledger.GetEntriesAsync(
    accountGuid: accountGuid,
    skip: 0,
    take: 10
);

// Paginated enumeration with filtering
EnumerationQuery query = new EnumerationQuery
{
    AccountGUID = accountGuid,
    MaxResults = 10,
    Ordering = EnumerationOrderEnum.CreatedDescending,
    AmountMinimum = 75.00m,     // Only entries >= $75
    AmountMaximum = 250.00m,    // Only entries <= $250
    CreatedAfterUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    CreatedBeforeUtc = DateTime.UtcNow
};

EnumerationResult<Entry> result = await ledger.EnumerateTransactionsAsync(query);

Console.WriteLine($"Found {result.TotalRecords} total records");
Console.WriteLine($"Returned {result.Objects.Count} records");
Console.WriteLine($"Records remaining: {result.RecordsRemaining}");

foreach (Entry entry in result.Objects)
{
    string type = entry.Type == EntryType.Credit ? "Credit" : "Debit";
    Console.WriteLine($"{entry.CreatedUtc:yyyy-MM-dd} {type}: ${entry.Amount} - {entry.Description}");
}

// Continue with next page if not at end
if (!result.EndOfResults && result.ContinuationToken != null)
{
    query.ContinuationToken = result.ContinuationToken;
    EnumerationResult<Entry> nextPage = await ledger.EnumerateTransactionsAsync(query);
}
```

### Audit Trail and Balance Verification

```csharp
Guid accountGuid = await ledger.CreateAccountAsync("Audit Test", 1000.00m);

// Perform several commit operations to create balance chain
await ledger.AddCreditAsync(accountGuid, 100.00m, isCommitted: true);
await ledger.AddDebitAsync(accountGuid, 50.00m, isCommitted: true);
await ledger.AddCreditAsync(accountGuid, 200.00m, isCommitted: true);

// Each commit creates a new balance entry that replaces the previous one
// This creates an immutable chain: Balance₁ → Balance₂ → Balance₃

// Verify the integrity of the balance chain
bool isValid = await ledger.VerifyBalanceChainAsync(accountGuid);
if (isValid)
{
    Console.WriteLine("Balance chain is valid - audit trail intact");
}
else
{
    Console.WriteLine("WARNING: Balance chain is broken - possible data corruption");
}

// Get balance entries specifically for forensic analysis
List<Entry> balanceEntries = await ledger.GetEntriesAsync(
    accountGuid: accountGuid,
    entryType: EntryType.Balance,
    skip: 0,
    take: 100
);

balanceEntries = balanceEntries.OrderBy(e => e.CreatedUtc).ToList();

Console.WriteLine("Balance Entry Chain:");
foreach (Entry balanceEntry in balanceEntries)
{
    Console.WriteLine($"  {balanceEntry.CreatedUtc:yyyy-MM-dd HH:mm:ss} - Balance: ${balanceEntry.Amount}");
    if (balanceEntry.Replaces != null)
    {
        Console.WriteLine($"    Replaces: {balanceEntry.Replaces}");
    }
}
```

### Event Handling

```csharp
Ledger ledger = new Ledger("events.db");

// Subscribe to events
ledger.AccountCreated += (sender, args) =>
{
    Console.WriteLine($"Account created: {args.Name} (GUID: {args.GUID})");
};

ledger.AccountDeleted += (sender, args) =>
{
    Console.WriteLine($"Account deleted: {args.Name}");
};

ledger.CreditAdded += (sender, args) =>
{
    Console.WriteLine($"Credit added to {args.Account.Name}: ${args.Entry.Amount}");
};

ledger.DebitAdded += (sender, args) =>
{
    Console.WriteLine($"Debit added to {args.Account.Name}: ${args.Entry.Amount}");
};

ledger.EntryCanceled += (sender, args) =>
{
    Console.WriteLine($"Entry canceled: {args.Entry.GUID}");
};

ledger.EntriesCommitted += (sender, args) =>
{
    Console.WriteLine($"Entries committed to {args.Account.Name}");
    Console.WriteLine($"  Before: ${args.BalanceBefore.CommittedBalance}");
    Console.WriteLine($"  After: ${args.BalanceAfter.CommittedBalance}");
};

// Perform operations - events will fire asynchronously
Guid accountGuid = await ledger.CreateAccountAsync("Event Test", 100.00m);
await ledger.AddCreditAsync(accountGuid, 50.00m);
await ledger.CommitEntriesAsync(accountGuid);

await ledger.DisposeAsync();
```

### Cancellation Token Support

```csharp
// Create a cancellation token source with timeout
using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

try
{
    // All async methods support cancellation
    Guid accountGuid = await ledger.CreateAccountAsync("Cancelable Account", token: cts.Token);

    await ledger.AddCreditAsync(accountGuid, 100.00m, token: cts.Token);

    Balance balance = await ledger.GetBalanceAsync(accountGuid, token: cts.Token);

    await ledger.CommitEntriesAsync(accountGuid, token: cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Operation was canceled");
}
```

### Thread Safety Example

```csharp
Guid accountGuid = await ledger.CreateAccountAsync("Concurrent Account", 0m);

// Multiple threads can safely operate on the same account
// NetLedger uses SemaphoreSlim-based locking per account
List<Task> tasks = new List<Task>();

for (int i = 0; i < 100; i++)
{
    int capture = i;
    tasks.Add(Task.Run(async () =>
    {
        await ledger.AddCreditAsync(accountGuid, 10.00m, $"Concurrent credit {capture}");
    }));
}

await Task.WhenAll(tasks);

Balance balance = await ledger.GetBalanceAsync(accountGuid);
Console.WriteLine($"Final pending balance: ${balance.PendingBalance}");  // 1000.00
```

## Architecture Notes

### Pending vs. Committed Model

NetLedger enforces a two-phase transaction model:

1. **Pending Phase** - Entries are created with `IsCommitted = false`
   - Can be canceled via `CancelPendingAsync()`
   - Visible in `PendingBalance` but not `CommittedBalance`
   - Retrievable via `GetPendingEntriesAsync()`, `GetPendingCreditsAsync()`, `GetPendingDebitsAsync()`

2. **Committed Phase** - Entries are finalized via `CommitEntriesAsync()`
   - Cannot be canceled or modified (immutable)
   - Included in `CommittedBalance`
   - Linked to a balance entry via `CommittedByGUID`
   - Creates a new balance entry in the audit chain

This model enables "draft transactions" that can be reviewed, approved, and finalized separately from the committed ledger state.

### Balance Entry Chain

Each commit operation creates a special `EntryType.Balance` entry that:
- Summarizes the current committed balance
- Links to the previous balance entry via the `Replaces` field
- Creates an immutable audit trail: Balance₁ → Balance₂ → Balance₃ → ...
- Can be verified for integrity via `VerifyBalanceChainAsync()`

This chain provides forensic accounting capabilities and prevents tampering with historical balances.

### Account-Level Locking

NetLedger uses `ConcurrentDictionary<Guid, SemaphoreSlim>` to provide per-account locking:
- Operations on different accounts execute in parallel
- Operations on the same account are serialized to prevent race conditions
- Locks are acquired asynchronously via `SemaphoreSlim.WaitAsync()`
- All locks are released in `finally` blocks to prevent deadlocks
- Supports cancellation tokens for responsive lock acquisition

### Database Schema

**accounts table:**
```sql
id INTEGER PRIMARY KEY AUTOINCREMENT
guid TEXT NOT NULL
name TEXT NOT NULL
notes TEXT
createdutc TEXT NOT NULL
```

**entries table:**
```sql
id INTEGER PRIMARY KEY AUTOINCREMENT
guid TEXT NOT NULL
accountguid TEXT NOT NULL
type INTEGER NOT NULL              -- 0=Debit, 1=Credit, 2=Balance
amount REAL NOT NULL
description TEXT
replaces TEXT                      -- Links to previous balance entry
committed INTEGER NOT NULL         -- 0=Pending, 1=Committed
committedbyguid TEXT              -- GUID of balance entry that committed this
committedutc TEXT
createdutc TEXT NOT NULL
```

## Performance Considerations

- **Connection Pooling**: Max 500 connections with 120-second timeout
- **Batch Operations**: Use `AddCreditsAsync()` and `AddDebitsAsync()` for bulk inserts
- **Pagination**: Use `EnumerateTransactionsAsync()` with continuation tokens for large result sets (max 1000 records per query)
- **Account Locking**: Lock contention only occurs within the same account; different accounts have no lock interaction
- **Async Throughout**: All I/O operations are async to prevent thread pool starvation

## Example: Simple Inter-Account Transfer

```csharp
// NetLedger does not have built-in transfer operations
// Implement transfers by debiting one account and crediting another

async Task TransferAsync(Ledger ledger, Guid fromAccount, Guid toAccount, decimal amount, string notes)
{
    string description = $"Transfer: {notes}";

    // Debit the source account
    Guid debitGuid = await ledger.AddDebitAsync(fromAccount, amount, description);

    // Credit the destination account
    Guid creditGuid = await ledger.AddCreditAsync(toAccount, amount, description);

    // Commit both entries
    await ledger.CommitEntriesAsync(fromAccount, new List<Guid> { debitGuid });
    await ledger.CommitEntriesAsync(toAccount, new List<Guid> { creditGuid });
}

// Usage
Guid checking = await ledger.CreateAccountAsync("Checking", 1000.00m);
Guid savings = await ledger.CreateAccountAsync("Savings", 500.00m);

await TransferAsync(ledger, checking, savings, 200.00m, "Monthly savings");
```

## Requirements

- **.NET 8.0** or later
- **SQLite** (included via Durable.Sqlite package)

## Dependencies

- **Durable.Sqlite** (v0.1.10) - Custom ORM with connection pooling
- **Timestamps** (v1.0.11) - Timestamp utilities

## License

MIT License - See [LICENSE.md](LICENSE.md) for details

## Contributing

Contributions are welcome! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## Support

- **Issues**: [GitHub Issues](https://github.com/jchristn/NetLedger/issues)
- **Discussions**: [GitHub Discussions](https://github.com/jchristn/NetLedger/discussions)
- **NuGet Package**: [NetLedger on NuGet](https://www.nuget.org/packages/NetLedger/)

## Version History

### v2.0.0 (Current)
- Full async/await support throughout
- Transaction support with ACID guarantees
- Batch operations for credits and debits
- Enhanced error handling with specific exception types
- Performance improvements with connection pooling
- Breaking changes from v1.x (see [CHANGELOG.md](CHANGELOG.md))

See [CHANGELOG.md](CHANGELOG.md) for complete version history.
