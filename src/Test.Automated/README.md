# Test.Automated

Automated test suite for the NetLedger library. This project tests the core ledgering functionality directly against the NetLedger library using various database backends.

## What It Does

This test suite validates:

- Account creation and retrieval
- Credit and debit operations
- Batch operations (multiple credits/debits at once)
- Balance calculations (committed and pending)
- Entry commits and balance chain integrity
- Pending entry management
- Entry enumeration with pagination and filtering
- Entry cancellation
- Event handling
- Account deletion

## Requirements

- .NET 8.0 SDK
- For non-SQLite backends: appropriate database server running and accessible

## Usage

### Basic Usage (SQLite - Default)

```bash
dotnet run --project src/Test.Automated/Test.Automated.csproj
```

### With Different Database Backends

**MySQL:**
```bash
dotnet run --project src/Test.Automated/Test.Automated.csproj -- -t mysql -h localhost -u root --password mypassword -d netledger_test
```

**PostgreSQL:**
```bash
dotnet run --project src/Test.Automated/Test.Automated.csproj -- -t postgresql -h localhost -u postgres --password mypassword -d netledger_test
```

**SQL Server:**
```bash
dotnet run --project src/Test.Automated/Test.Automated.csproj -- -t sqlserver -h localhost -u sa --password mypassword -d netledger_test
```

### Retain Test Data After Running

Use the `--no-cleanup` flag to keep the test database file (SQLite) after tests complete:

```bash
dotnet run --project src/Test.Automated/Test.Automated.csproj -- --no-cleanup
```

This is useful for inspecting the database state after tests run.

## Command Line Options

| Option | Short | Description |
|--------|-------|-------------|
| `--type` | `-t` | Database type: `sqlite`, `mysql`, `postgresql`, `sqlserver` (default: sqlite) |
| `--host` | `-h` | Database hostname (default: localhost) |
| `--port` | `-p` | Database port (uses default for database type if not specified) |
| `--user` | `-u` | Database username |
| `--password` | | Database password |
| `--database` | `-d` | Database name |
| `--log-queries` | | Enable query logging for debugging |
| `--no-cleanup` | | Do not delete test data after running |
| `--help` | `-?` | Show help message |

## Exit Codes

- `0` - All tests passed
- `1` - One or more tests failed

## Output

The test suite outputs results in real-time:

```
================================================================================
NetLedger Automated Test Suite
================================================================================

Database Type: Sqlite
Database File: test_automated.db

--- Account Tests ---

[PASS] Create account (15ms)
[PASS] Read account by GUID (3ms)
[PASS] Read account by name (4ms)
...

================================================================================
Test Summary
================================================================================

Total Tests:   45
Passed:        45
Failed:        0
Success Rate:  100.00%

================================================================================
ALL TESTS PASSED
================================================================================
```
