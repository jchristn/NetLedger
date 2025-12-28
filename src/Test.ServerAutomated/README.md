# Test.ServerAutomated

Automated test suite for the NetLedger Server REST API. This project tests the server endpoints via HTTP using the NetLedger SDK client.

## What It Does

This test suite validates:

- Service health checks and info endpoints
- Account creation, retrieval, and deletion
- Credit and debit operations via REST API
- Batch operations
- Balance calculations and retrieval
- Commit operations
- Pending entry management
- Entry enumeration with pagination and filtering
- Entry cancellation
- Account enumeration
- Historical balance queries
- Balance chain verification
- Error handling (404s, validation errors)
- Edge cases (long names, zero balances, negative balances)
- Performance benchmarks
- Concurrent operations

## Requirements

- .NET 8.0 SDK
- A running NetLedger Server instance
- A valid API key for authentication

## Usage

### Basic Usage

```bash
dotnet run --project src/Test.ServerAutomated/Test.ServerAutomated.csproj -- --endpoint http://localhost:8080 --apikey your-api-key
```

### Short Form

```bash
dotnet run --project src/Test.ServerAutomated/Test.ServerAutomated.csproj -- -e http://localhost:8080 -k your-api-key
```

### Retain Test Data After Running

Use the `--no-cleanup` flag to keep test accounts on the server after tests complete:

```bash
dotnet run --project src/Test.ServerAutomated/Test.ServerAutomated.csproj -- -e http://localhost:8080 -k your-api-key --no-cleanup
```

This is useful for:
- Inspecting test data on the server after tests run
- Debugging failed tests by examining the server state
- Verifying data persistence

When `--no-cleanup` is used, the test suite will output a list of all retained account GUIDs at the end.

## Command Line Options

| Option | Short | Description |
|--------|-------|-------------|
| `--endpoint` | `-e` | The NetLedger server endpoint URL (required) |
| `--apikey` | `-k` | The API key for authentication (required) |
| `--no-cleanup` | | Do not delete test accounts after running |
| `--help` | `-h`, `-?` | Show help message |

## Exit Codes

- `0` - All tests passed
- `1` - One or more tests failed, or invalid arguments

## Output

The test suite outputs results in real-time:

```
================================================================================
NetLedger Server Automated Test Suite - v2.0.0
================================================================================

Endpoint: http://localhost:8080
API Key:  netledge...

--- Service Tests ---

[PASS] Health check returns true (45ms)
[PASS] Get service info returns valid data (12ms)

--- Account Creation Tests ---

[PASS] Create account with name only (23ms)
[PASS] Create account with Account object (18ms)
...

================================================================================
Test Summary
================================================================================

Total Tests:   78
Passed:        78
Failed:        0
Success Rate:  100.00%
Total Runtime: 15234ms (15.23s)

================================================================================
ALL TESTS PASSED
================================================================================
```

### With --no-cleanup

When using `--no-cleanup`, additional output appears at the end:

```
================================================================================
ALL TESTS PASSED
================================================================================

Cleanup skipped. 42 test account(s) retained on server:
  - a1b2c3d4-e5f6-7890-abcd-ef1234567890
  - b2c3d4e5-f6a7-8901-bcde-f12345678901
  ...
```

## Test Categories

1. **Service Tests** - Health checks and service info
2. **Account Creation Tests** - Creating accounts with various options
3. **Account Retrieval Tests** - Getting accounts by GUID and name
4. **Credit and Debit Tests** - Adding individual transactions
5. **Batch Operation Tests** - Adding multiple transactions at once
6. **Balance Tests** - Balance calculations and retrieval
7. **Commit Tests** - Committing pending entries
8. **Pending Entries Tests** - Managing uncommitted entries
9. **Entry Enumeration Tests** - Pagination and filtering of entries
10. **Entry Cancellation Tests** - Canceling pending entries
11. **Account Enumeration Tests** - Pagination of accounts
12. **Historical Balance Tests** - Point-in-time balance queries
13. **Balance Chain Verification Tests** - Integrity checking
14. **Error Handling Tests** - 404s and validation errors
15. **Edge Case Tests** - Boundary conditions and special cases
16. **Performance Tests** - Throughput benchmarks
17. **Concurrency Tests** - Parallel operation handling
