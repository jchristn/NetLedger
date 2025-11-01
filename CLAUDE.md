# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

NetLedger is a self-contained ledgering library for .NET that uses SQLite for storage. It enables adding debits and credits to accounts, checking balances, and committing pending entries to finalized balances. The library is distributed as a NuGet package targeting .NET 8.0.

## Build and Development Commands

### Building the Project
```bash
dotnet build src/NetLedger.sln
```

### Running the Interactive Test Application
```bash
dotnet run --project src/Test/Test.csproj
```

### Running the Automated Test Suite
```bash
dotnet run --project src/Test.Automated/Test.Automated.csproj
```

### Building for Release (creates NuGet package)
```bash
dotnet build src/NetLedger.sln -c Release
```

## Architecture

### Core Components

**Ledger (src/NetLedger/Ledger.cs)**
- Central coordinator that manages all ledger operations
- Uses Durable.Sqlite (a custom ORM built on the Durable library) for data persistence with connection pooling
- Implements account locking via `ConcurrentDictionary<Guid, SemaphoreSlim>` to ensure thread-safe operations
- All public methods that modify account state acquire locks via `LockAccountAsync()/UnlockAccount()`
- Lock acquisition uses `SemaphoreSlim.WaitAsync()` with cancellation token support
- Fires events asynchronously using `Task.Run()` to avoid blocking callers
- Implements `IAsyncDisposable` for proper resource cleanup
- Supports database transactions for atomic operations

**Account (src/NetLedger/Account.cs)**
- Represents a ledger account with a name and GUID
- Decorated with Durable library attributes (`[Entity]`, `[Property]`) for database mapping
- Stored in the `accounts` table

**Entry (src/NetLedger/Entry.cs)**
- Represents individual transactions: credits, debits, or balance snapshots
- Key fields: `Type` (Credit/Debit/Balance), `Amount`, `IsCommitted`, `CommittedUtc`, `CommittedByGUID`
- Balance entries are special entries created during commits that summarize the current state
- `Replaces` field links new balance entries to old ones, creating an audit trail
- Stored in the `entries` table

**Balance (src/NetLedger/Balance.cs)**
- Computed view (not persisted) showing account state at a point in time
- `CommittedBalance`: finalized balance from latest balance entry
- `PendingBalance`: projected balance including uncommitted entries
- Contains `PendingCredits` and `PendingDebits` objects (PendingTransactionSummary) with counts, totals, and entry lists

**Enumeration Support (src/NetLedger/EnumerationQuery.cs, EnumerationResult.cs)**
- Paginated querying of entries with continuation tokens
- Filters: date range (CreatedAfterUtc/BeforeUtc), amount range (minimum/maximum)
- Ordering: by created date or amount, ascending or descending
- MaxResults: 1-1000 entries per query
- Results include total count, records remaining, and end-of-results indicator

### Transaction Flow

1. **Adding Credits/Debits**: Creates Entry records with `IsCommitted = false`
2. **Checking Balance**: Retrieves latest balance entry and computes pending totals
3. **Committing**:
   - Marks selected pending entries as committed
   - Creates new balance entry summarizing the updated state
   - Links committed entries to the balance entry via `CommittedByGUID`
   - Updates old balance entry's `Replaces` field to point to new balance
   - This creates a chain of balance entries for audit history
   - All operations wrapped in database transaction for atomicity

### Database Schema

The library uses Durable.Sqlite which auto-initializes tables from C# attributes:
- `accounts`: id, guid, name, notes, createdutc
- `entries`: id, guid, accountguid, type, amount, description, replaces, committed, committedbyguid, committedutc, createdutc

### Account Locking Mechanism

Operations on the same account are serialized using SemaphoreSlim-based locking:
- `LockAccountAsync()` retrieves or creates a `SemaphoreSlim` from `ConcurrentDictionary<Guid, SemaphoreSlim>`
- Uses `await semaphore.WaitAsync(cancellationToken)` to acquire lock asynchronously
- All account operations are wrapped in try/finally to ensure unlocks occur via `UnlockAccount()`
- Internal methods assume the caller has acquired the lock (suffix: `Internal`)
- Supports cancellation via `CancellationToken`

### Events

All state changes emit events: `AccountCreated`, `AccountDeleted`, `CreditAdded`, `DebitAdded`, `EntryCanceled`, `EntriesCommitted`. Events fire asynchronously via `Task.Run()` after unlocking accounts to avoid deadlocks.

## Important Patterns

- **Pending vs Committed**: Entries exist in pending state until explicitly committed. Only committed entries affect `CommittedBalance`.
- **Balance Entries Chain**: Each commit creates a new balance entry that replaces the previous one, maintaining history via the `Replaces` field.
- **GUID-based Identification**: All accounts and entries use GUIDs for identification, not auto-increment IDs.
- **UTC Timestamps**: All timestamps are stored in UTC (`DateTime.Now.ToUniversalTime()`).
- **Async/Await Pattern**: All public APIs are async with `CancellationToken` support and use `.ConfigureAwait(false)`.
- **Transaction Support**: Database operations use the `ITransaction` interface from Durable library for atomicity.
- **Batch Operations**: Support for adding multiple credits or debits in a single call via `AddCreditsAsync()` and `AddDebitsAsync()`.

## Testing

**Test Project (src/Test/Program.cs)**
- Interactive console application demonstrating all library features
- Provides a command-line interface for creating accounts, adding credits/debits, viewing balances, and committing entries
- Uses the GetSomeInput library (Inputty) for user input handling

**Test.Automated Project (src/Test.Automated/Program.cs)**
- Comprehensive automated test suite with multiple test categories
- Tests account creation, retrieval, credit/debit operations, batch operations, balance calculations, commits, pending entries, entry search, cancellation, account deletion, and events
- Provides detailed test results with pass/fail counts
- Database file: `test_automated.db` (cleaned up before each run)

## Dependencies

- **Durable.Sqlite** (v0.1.10): Custom ORM library built on the Durable library for SQLite database operations
- The Durable library provides connection pooling (max 500 connections, 120s timeout), repository pattern, and transaction support
- Durable uses attributes (`[Entity]`, `[Property]`, `Flags`) for entity mapping
- **Timestamps** (v1.0.11): Timestamp utility library

## Code Style and Implementation Rules

These rules MUST be followed STRICTLY when working with this codebase:

### File Structure and Organization

- **Namespace Declaration**: Namespace should always be at the top, with `using` statements contained INSIDE the namespace block
- **Using Statement Order**:
  - Microsoft and standard system library usings first, in alphabetical order
  - Followed by third-party and project usings, in alphabetical order
- **One Type Per File**: Each file should contain exactly one class OR exactly one enum. Do not nest multiple classes or enums in a single file
- **Regions**: Use regions for `Public-Members`, `Private-Members`, `Constructors-and-Factories`, `Public-Methods`, and `Private-Methods`

### Naming Conventions

- **Private Class Members**: Must start with underscore followed by PascalCase (e.g., `_FooBar` NOT `_fooBar`)
- **Variable Declarations**: Do NOT use `var` when defining variables. Always use the actual type
- **No Tuples**: Avoid tuples unless absolutely necessary (batch operations are an exception where tuples are used for parameter lists)

### Documentation

- **Public Members**: All public members, constructors, and public methods MUST have XML code documentation
- **Private Members**: No code documentation should be applied to private members or private methods
- **Exception Documentation**: Document which exceptions public methods can throw using `/// <exception>` tags
- **Default Values**: Code documentation should outline default values, minimum values, and maximum values where appropriate
- **Nullability**: Document nullability expectations in XML comments
- **Thread Safety**: Document thread safety guarantees in XML comments

### Property Implementation

- **Backing Variables**: All public members should have explicit getters and setters using backing variables when value requires range or null validation
- **Configurability**: Avoid using constant values for things developers may later want to configure. Instead use public members with backing private members set to reasonable defaults

### Asynchronous Programming

- **ConfigureAwait**: Async calls should use `.ConfigureAwait(false)` where appropriate
- **CancellationToken**: Every async method should accept a `CancellationToken` as an input parameter, unless the class has a `CancellationToken` or `CancellationTokenSource` as a class member
- **Cancellation Checks**: Async calls should check whether cancellation has been requested at appropriate places
- **IEnumerable Variants**: When implementing a method that returns an `IEnumerable`, also create an async variant that includes a `CancellationToken`

### Exception Handling

- **Specific Exceptions**: Use specific exception types rather than generic `Exception`
- **Meaningful Messages**: Always include meaningful error messages with context
- **Custom Exceptions**: Consider using custom exception types for domain-specific errors
- **Transaction Rollback**: Always rollback transactions in catch blocks before re-throwing

### Resource Management

- **IDisposable**: Implement `IDisposable`/`IAsyncDisposable` when holding unmanaged resources or disposable objects
- **Using Statements**: Use `using` statements or `using` declarations for `IDisposable` objects
- **Dispose Pattern**: Follow the full Dispose pattern with `protected virtual void Dispose(bool disposing)`
- **Transaction Disposal**: Always dispose transactions in finally blocks

### Null Safety

- **Nullable Reference Types**: Use nullable reference types (enable `<Nullable>enable</Nullable>` in project files)
- **Guard Clauses**: Validate input parameters with guard clauses at method start
- **Null Checks**: Use manual null checks with `throw new ArgumentNullException(nameof(parameter))`
- **Proactive Null Handling**: Proactively identify and eliminate any situations in code where null might cause exceptions to be thrown

### LINQ and Collections

- **Prefer LINQ**: Use LINQ methods over manual loops when readability is not compromised
- **Existence Checks**: Use `.Any()` instead of `.Count() > 0` for existence checks
- **Multiple Enumeration**: Be aware of multiple enumeration issues - consider `.ToList()` when needed
- **Safe Retrieval**: Use `.FirstOrDefault()` with null checks rather than `.First()` when element might not exist
- **Query Pattern**: Use Durable repository query pattern: `_Repository.Query().Where(...).ExecuteAsync()`

### Concurrency

- **SemaphoreSlim**: Use `SemaphoreSlim` for async-compatible locking
- **ConcurrentDictionary**: Use `ConcurrentDictionary.GetOrAdd()` for thread-safe dictionary operations
- **Thread Safety Documentation**: Document thread safety guarantees in public API

### Library-Specific Rules

- **No Console Output**: Ensure NO `Console.WriteLine` statements are added to library code (Test and Test.Automated projects are exempt)
- **Opaque Classes**: Do not make assumptions about class members or methods on classes from the Durable library. The Durable library provides the ORM infrastructure but is not part of this repository
- **Durable Attributes**: Use `[Entity]` for table mapping and `[Property]` for column mapping with appropriate `Flags` (e.g., `Flags.PrimaryKey`, `Flags.AutoIncrement`, `Flags.String`)

### Validation and Compilation

- **README Accuracy**: If a README exists, analyze it and ensure it is accurate
- **Compile Before Committing**: Compile the code and ensure it is free of errors and warnings to the best of your ability
