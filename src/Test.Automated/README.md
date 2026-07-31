# Test.Automated

Touchstone console runner for the shared NetLedger test suite. The same suite can be run against SQLite, MySQL, PostgreSQL, or SQL Server by selecting the provider on the command line.

## Requirements

- .NET 8.0 SDK or .NET 10.0 SDK
- For non-SQLite providers, a reachable database server and an existing database

## Usage

SQLite uses isolated temporary database files for test fixtures:

```bash
dotnet run --project src/Test.Automated/Test.Automated.csproj -- --dbtype sqlite
```

MySQL:

```bash
dotnet run --project src/Test.Automated/Test.Automated.csproj -- --dbtype mysql --dbhostname localhost --dbport 43306 --dbusername netledger --dbpassword netledger --dbname netledger
```

PostgreSQL:

```bash
dotnet run --project src/Test.Automated/Test.Automated.csproj -- --dbtype postgres --dbhostname localhost --dbport 45432 --dbusername netledger --dbpassword netledger --dbname netledger
```

SQL Server:

```bash
dotnet run --project src/Test.Automated/Test.Automated.csproj -- --dbtype sqlserver --dbhostname localhost --dbport 41433 --dbusername sa --dbpassword NetLedger!Passw0rd --dbname netledger
```

xUnit and NUnit are launched by `dotnet test`, so pass database settings as test-process environment variables from the CLI:

```bash
dotnet test src/Test.Xunit/Test.Xunit.csproj --framework net8.0 -e NETLEDGER_TEST_DBTYPE=postgres -e NETLEDGER_TEST_DBHOSTNAME=localhost -e NETLEDGER_TEST_DBPORT=45432 -e NETLEDGER_TEST_DBUSERNAME=netledger -e NETLEDGER_TEST_DBPASSWORD=netledger -e NETLEDGER_TEST_DBNAME=netledger
dotnet test src/Test.Nunit/Test.Nunit.csproj --framework net8.0 -e NETLEDGER_TEST_DBTYPE=mysql -e NETLEDGER_TEST_DBHOSTNAME=localhost -e NETLEDGER_TEST_DBPORT=43306 -e NETLEDGER_TEST_DBUSERNAME=netledger -e NETLEDGER_TEST_DBPASSWORD=netledger -e NETLEDGER_TEST_DBNAME=netledger
```

## Command Line Options

| Option | Short | Description |
| --- | --- | --- |
| `--dbtype` | `-t` | Database type: `sqlite`, `mysql`, `postgres`, `postgresql`, `sqlserver`, or `mssql`. Default is `sqlite`. |
| `--dbfilename` | `-f` | SQLite database filename. Omit this for isolated temporary files. |
| `--dbhostname` | `-h` | Database hostname. Default is `localhost` for external providers. |
| `--dbport` | `-p` | Database port. Provider default is used when omitted. |
| `--dbusername` | `-u` | Database username. |
| `--dbpassword` | | Database password. |
| `--dbname` | `-d` | Database name. Default is `netledger` for external providers. |
| `--dbschema` | | Provider schema name where supported. |
| `--dbinstance` | | SQL Server instance name. |
| `--dbrequireencryption` | | Require encrypted database connections. |
| `--dbconnectiontimeoutseconds` | | Database connection timeout. |
| `--dbmaxpoolsize` | | Maximum database connection pool size. |
| `--dblogqueries` | | Enable query logging. |
| `--suite` | | Run only one Touchstone suite, for example `archive`. |
| `--test` | | Run only one Touchstone test case, for example `automatic_archive_account_override_exports_old_entries`. |
| `--help` | `-?` | Show help. |

The older aliases `--type`, `--host`, `--port`, `--user`, `--password`, `--database`, `--schema`, and `--log-queries` remain supported.

Filters can be combined with database settings:

```bash
dotnet run --project src/Test.Automated/Test.Automated.csproj -- --dbtype sqlite --suite archive --test automatic_archive_account_override_exports_old_entries
```

## Environment Variables

Framework runners and CI jobs can use:

| Variable | Description |
| --- | --- |
| `NETLEDGER_TEST_DBTYPE` | Database type. |
| `NETLEDGER_TEST_DBFILENAME` | SQLite filename. |
| `NETLEDGER_TEST_DBHOSTNAME` | External database hostname. |
| `NETLEDGER_TEST_DBPORT` | External database port. |
| `NETLEDGER_TEST_DBUSERNAME` | External database username. |
| `NETLEDGER_TEST_DBPASSWORD` | External database password. |
| `NETLEDGER_TEST_DBNAME` | External database name. |
| `NETLEDGER_TEST_DBSCHEMA` | Provider schema name. |
| `NETLEDGER_TEST_DBINSTANCE` | SQL Server instance name. |
| `NETLEDGER_TEST_DBREQUIREENCRYPTION` | Require encrypted database connections. |
| `NETLEDGER_TEST_DBLOGQUERIES` | Enable query logging. |

Provider-matrix compatibility variables such as `NETLEDGER_POSTGRESQL_HOST`, `NETLEDGER_MYSQL_PORT`, and `NETLEDGER_SQLSERVER_DATABASE` are still supported.

The archive S3 storage test is opt-in. Set these variables to run it against Less3 or another S3-compatible endpoint:

| Variable | Description |
| --- | --- |
| `NETLEDGER_ARCHIVE_TEST_S3_ENDPOINT` | S3-compatible endpoint, for example `http://localhost:59714`. |
| `NETLEDGER_ARCHIVE_TEST_S3_BUCKET` | Existing bucket name. |
| `NETLEDGER_ARCHIVE_TEST_S3_REGION` | Region. Default is `us-west-1`. |
| `NETLEDGER_ARCHIVE_TEST_S3_ACCESS_KEY` | S3 access key. |
| `NETLEDGER_ARCHIVE_TEST_S3_SECRET_KEY` | S3 secret key. |
| `NETLEDGER_ARCHIVE_TEST_S3_PREFIX` | Object prefix for test objects. Default is `netledger-archive-tests`. |

## Exit Codes

- `0` - All tests passed
- Non-zero - One or more tests failed, or the runner arguments were invalid
