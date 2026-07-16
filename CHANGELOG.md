# Change Log

## Current Version

### v3.0.0 (In development)

**MAJOR VERSION - BREAKING CHANGES**

NetLedger v3.0.0 introduces tenant-scoped ledger data, PrettyId string identifiers, metadata labels/tags, credential-oriented authentication, dashboard metadata workflows, and Touchstone-backed tests.

#### Breaking Changes

- Public model identifiers are now `string Id` values generated with PrettyId K-sortable IDs, for example `acct_...`, `ent_...`, `ten_...`, `usr_...`, and `cred_...`.
- Accounts and entries now include `TenantId`; tenant context can be supplied through `x-tenant-id` or `/v1/tenants/{tenantId}/...` route aliases.
- API key management routes are replaced by tenant/user-scoped credential routes with access keys and secret verifier material. Legacy `/v1/apikeys` management routes are removed.
- Accounts and entries support `Labels: string[]` and `Tags: Dictionary<string,string>` request/response fields.
- Enumeration queries support all-must-match `Labels`/`Tags` filters and `CreditMinimum`, `CreditMaximum`, `DebitMinimum`, and `DebitMaximum`.
- SDKs now prefer string IDs and expose tenant/header and metadata support.

#### New Features

- v3 schema declarations are for net-new deployments or manually migrated deployments.
- New v3 domain models: `Tenant`, `User`, `Credential`, `AuthSession`, `AccountUserMap`, `AuditRecord`, `UserRole`, `Permission`, `RolePermissionMap`, `UserRoleAssignment`, and `CredentialScopeAssignment`.
- Credential creation now returns the raw secret key once, then stores only verifier material and `SecretKeyLast4`.
- Watson server dependency upgraded to the Watson 7 line.
- Dashboard login accepts tenant context and account/entry forms can set labels and tags.
- REST route aliases under `/v1/tenants/{tenantId}/accounts/...` preserve `/v1` while making tenant scope explicit.
- Touchstone test projects added: `Test.Shared`, `Test.Automated`, `Test.Xunit`, and `Test.Nunit`.

#### Validation

- SQLite, MySQL, PostgreSQL, and SQL Server provider certification is tracked through Touchstone-backed tests.
- v3 does not include automated v2-to-v3 data migration; operators should deploy net-new or perform manual migration.

### v2.0.0 (October 2025)

**MAJOR VERSION - BREAKING CHANGES**

This is a complete rewrite of NetLedger with significant architectural improvements and breaking API changes. Applications using v1.x will require code changes to upgrade to v2.0.0.

#### Breaking Changes

- **Async/Await Throughout**: All public methods are now async and must be awaited
  - `CreateAccount()` → `CreateAccountAsync()`
  - `AddCredit()` → `AddCreditAsync()`
  - `AddDebit()` → `AddDebitAsync()`
  - `GetBalance()` → `GetBalanceAsync()`
  - `Commit()` → `CommitEntriesAsync()`
  - All other methods follow the same async pattern

- **GUID Type Change**: Changed from `string` GUIDs to `System.Guid` type throughout
  - Account GUIDs are now `Guid` instead of `string`
  - Entry GUIDs are now `Guid` instead of `string`
  - All method parameters and return types updated accordingly

- **ORM Replacement**: Replaced Watson.ORM with Durable.Sqlite custom ORM
  - No longer depends on Watson.ORM.Sqlite or DatabaseWrapper.Core
  - New dependency: Durable.Sqlite (v0.1.10)
  - New dependency: Timestamps (v1.0.11)

- **Method Renames**: Several methods renamed for consistency
  - `DeleteAccountByGuid()` → `DeleteAccountByGuidAsync()`
  - `DeleteAccountByName()` → `DeleteAccountByNameAsync()`
  - `GetAccountByGuid()` → `GetAccountByGuidAsync()`
  - `GetAccountByName()` → `GetAccountByNameAsync()`
  - `Commit()` → `CommitEntriesAsync()`

- **IAsyncDisposable**: Ledger now implements `IAsyncDisposable` instead of `IDisposable`
  - Use `await ledger.DisposeAsync()` instead of `ledger.Dispose()`

#### New Features

- **Connection Pooling**: High-performance connection pool with configurable limits
  - Maximum 500 connections per pool
  - 120-second connection timeout
  - Automatic connection management

- **Batch Operations**: Efficiently add multiple entries in a single operation
  - `AddCreditsAsync()` - Add multiple credits with one call
  - `AddDebitsAsync()` - Add multiple debits with one call
  - Supports immediate commit flag for batch operations

- **Enumeration Support**: Paginated transaction queries with continuation tokens
  - `EnumerateTransactionsAsync()` - Query entries with filtering and pagination
  - `EnumerationQuery` class for building complex queries
  - `EnumerationResult<T>` with metadata (total records, records remaining, continuation token)
  - Support for up to 1000 records per query
  - Filtering by date range, amount range, and entry type
  - Ordering by created date or amount (ascending/descending)

- **Balance Chain Verification**: Validate audit trail integrity
  - `VerifyBalanceChainAsync()` - Verify the complete balance entry chain
  - Ensures immutable audit trail hasn't been tampered with
  - Critical for forensic accounting requirements

- **Point-in-Time Balances**: Historical balance queries
  - `GetBalanceAsOfAsync()` - Get account balance as of specific UTC timestamp
  - Useful for historical reporting and reconciliation

- **Bulk Balance Retrieval**: Get all account balances efficiently
  - `GetAllBalancesAsync()` - Returns dictionary of all accounts with their balances
  - Optimized for dashboard and reporting scenarios

- **Enhanced Pending Entry Management**: More granular control
  - `GetPendingEntriesAsync()` - Get all pending entries
  - `GetPendingCreditsAsync()` - Get only pending credits
  - `GetPendingDebitsAsync()` - Get only pending debits
  - `CancelPendingAsync()` - Cancel individual pending entries

- **Advanced Entry Queries**: Comprehensive filtering options
  - `GetEntriesAsync()` with extensive parameters
  - Filter by date range, search term, entry type, amount range
  - Pagination support with skip/take
  - Excludes balance entries by default

- **CancellationToken Support**: Responsive async operations
  - All async methods accept optional `CancellationToken`
  - Enables timeout and cancellation of long-running operations
  - Proper cancellation handling throughout

- **PendingTransactionSummary**: Rich pending transaction information
  - `Count` - Number of pending transactions
  - `Total` - Sum of pending amounts
  - `Entries` - Full list of pending Entry objects
  - Included in Balance object for credits and debits separately

#### Architectural Improvements

- **Async Account Locking**: Thread-safe concurrent operations
  - Changed from `ConcurrentDictionary<string, DateTime>` to `ConcurrentDictionary<Guid, SemaphoreSlim>`
  - Async lock acquisition with `SemaphoreSlim.WaitAsync()`
  - Proper async/await lock pattern prevents deadlocks
  - Account-level locking allows parallel operations on different accounts

- **Transaction Support**: ACID-compliant operations
  - Explicit `ITransaction` usage from Durable library
  - Atomic commits with automatic rollback on failure
  - Transaction disposal in finally blocks

- **Event System Improvements**: Non-blocking event notifications
  - Events fire asynchronously via `Task.Run()`
  - Prevents blocking of caller during event handler execution
  - Events include richer context data

- **Enhanced Error Handling**: Specific exception types
  - `ArgumentNullException` for null parameters
  - `ArgumentException` for invalid values
  - `KeyNotFoundException` for missing accounts
  - `ArgumentOutOfRangeException` for range violations
  - Meaningful error messages with context

- **Code Documentation**: Comprehensive XML documentation
  - All public members fully documented
  - Parameter descriptions and return value documentation
  - Exception documentation for all thrown exceptions
  - Thread safety guarantees documented

#### Testing

- **Comprehensive Test Suite**: New Test.Automated project
  - 100+ automated tests covering all functionality
  - Test categories: Account creation, retrieval, transactions, batch operations, balances, commits, pending entries, enumeration, cancellation, deletion, events, error handling, edge cases, performance, concurrency
  - Detailed test results with pass/fail counts
  - Clean setup and teardown for each test run

- **Enhanced Interactive Test**: Updated Test project
  - Modern async/await patterns
  - Demonstrates all v2.0 features
  - Improved user interface with Inputty library

#### Performance Improvements

- **Connection Pooling**: Reduces connection overhead
- **Batch Operations**: Reduces round-trips for multiple entries
- **Async I/O**: Prevents thread pool starvation
- **Account-Level Locking**: Minimizes lock contention
- **Optimized Queries**: Efficient database access patterns

#### Migration Guide from v1.x

To upgrade from v1.x to v2.0.0:

1. **Update all method calls to async**:
   ```csharp
   // v1.x
   string guid = ledger.CreateAccount("Account");
   ledger.AddCredit(guid, 100.00m, "Credit");
   Balance balance = ledger.GetBalance(guid);

   // v2.0.0
   Guid guid = await ledger.CreateAccountAsync("Account");
   await ledger.AddCreditAsync(guid, 100.00m, "Credit");
   Balance balance = await ledger.GetBalanceAsync(guid);
   ```

2. **Change GUID types from string to Guid**:
   ```csharp
   // v1.x
   string accountGuid = "...";

   // v2.0.0
   Guid accountGuid = Guid.Parse("...");
   ```

3. **Update disposal pattern**:
   ```csharp
   // v1.x
   ledger.Dispose();

   // v2.0.0
   await ledger.DisposeAsync();
   ```

4. **Add CancellationToken support (optional)**:
   ```csharp
   CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
   Guid guid = await ledger.CreateAccountAsync("Account", token: cts.Token);
   ```

5. **Update NuGet packages**:
   - Remove: Watson.ORM.Sqlite, DatabaseWrapper.Core
   - Dependencies now automatically installed: Durable.Sqlite, Timestamps

#### Known Issues

- None at this time

#### Contributors

- Joel Christner - Complete v2.0 rewrite and architecture

---

## Previous Versions

### v1.2.0 (March 2024)

- Upgraded to .NET 8.0
- Minor bug fixes and improvements

### v1.1.1

- Dependency updates
- Bug fixes

### v1.1.0

- Breaking changes
- `SummarizedGUIDs` renamed to `CommittedByGUID` in committed entries
- Entry now has `CommittedUtc` timestamp
- Balance object now has `EntryGUID` property
- Balance object now has list of committed GUIDs in `Committed` property
- Bug fix when adding committed entries

### v1.0.0 (Initial Release)

- Basic ledger functionality
- Account creation and management
- Credit and debit operations
- Balance tracking with pending/committed model
- Commit operations
- Event notifications
- SQLite storage with Watson.ORM
- String-based GUIDs
- Synchronous API
