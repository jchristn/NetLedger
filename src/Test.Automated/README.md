# Test.Automated

Touchstone console runner for the shared NetLedger test suite. The same suite can be run against SQLite, MySQL, PostgreSQL, or SQL Server by selecting the provider on the command line.

## Requirements

- .NET 8.0 SDK or .NET 10.0 SDK
- For non-SQLite providers, a reachable database server and an existing database

## Usage

SQLite uses isolated temporary database files for test fixtures:

```bash
dotnet run --project src/Test.Automated/Test.Automated.csproj -- --type sqlite
```

MySQL:

```bash
dotnet run --project src/Test.Automated/Test.Automated.csproj -- --type mysql --host localhost --port 3307 --user netledger --password netledger --database netledger
```

PostgreSQL:

```bash
dotnet run --project src/Test.Automated/Test.Automated.csproj -- --type postgresql --host localhost --port 5433 --user netledger --password netledger --database netledger
```

SQL Server:

```bash
dotnet run --project src/Test.Automated/Test.Automated.csproj -- --type sqlserver --host localhost --port 14330 --user sa --password NetLedger!Passw0rd --database netledger
```

## Command Line Options

| Option | Short | Description |
| --- | --- | --- |
| `--type` | `-t` | Database type: `sqlite`, `mysql`, `postgresql`, or `sqlserver`. Default is `sqlite`. |
| `--host` | `-h` | Database hostname. Default is `localhost` for external providers. |
| `--port` | `-p` | Database port. Provider default is used when omitted. |
| `--user` | `-u` | Database username. |
| `--password` | | Database password. |
| `--database` | `-d` | Database name. Default is `netledger` for external providers. |
| `--schema` | | Provider schema name where supported. |
| `--log-queries` | | Enable query logging. |
| `--help` | `-?` | Show help. |

## Exit Codes

- `0` - All tests passed
- Non-zero - One or more tests failed, or the runner arguments were invalid
