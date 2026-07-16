# NetLedger LoadGenerator

`LoadGenerator` creates synthetic tenants, users, accounts, entries, balance snapshots, and request-history rows directly in a NetLedger database. Request-history rows are generated across the same time range with the same default density profile as ledger entries, producing dashboard traffic charts with realistic volume and randomness. It is intended for dashboard screenshots, demo databases, and repeatable local scale testing.

## Examples

SQLite demo data for the last 30 days:

```powershell
dotnet run --project src\LoadGenerator\LoadGenerator.csproj -- --db sqlite --file .\netledger-demo.db --from 2026-06-01 --to 2026-07-01 --density medium
```

PostgreSQL with an explicit entry count:

```powershell
dotnet run --project src\LoadGenerator\LoadGenerator.csproj -- --db postgresql --host localhost --database netledger --username netledger --password password --from 2026-06-01 --to 2026-07-01 --records 50000 --seed 42
```

## Common Options

- `--db sqlite|mysql|postgresql|sqlserver`
- `--file <path>` for SQLite
- `--host <host> --port <port> --database <name> --username <user> --password <password>` for external databases
- `--schema <name>` for PostgreSQL
- `--from <timestamp> --to <timestamp>`
- `--density tiny|low|medium|high|extreme`
- `--records <count>` to override preset entry volume
- `--tenants <count> --users-per-tenant <count> --accounts-per-user <count>`
- `--entries-per-account-day <count>`
- `--commit-ratio <0-1>`
- `--request-history-per-account-day <count>` to tune request-history density independently
- `--request-history-ratio <multiplier>` to scale request-history density up or down
- `--request-history-records <count>` to override request-history row volume
- `--seed <int>` for repeatable randomness
- `--prefix <text>` for generated names
- `--no-request-history` to skip request-history traffic

The generator never clears existing data. Use a fresh database when producing screenshot or marketing fixtures.
