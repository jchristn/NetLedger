# NetLedger Performance and Scalability Findings

Review date: 2026-07-16

Scope: server, dashboard, and SDKs. The recommendations below favor low-risk changes that preserve the current API surface and avoid replacing foundational libraries.

## Findings

### High impact, low to medium effort

1. Account entries need keyed serialization, but database providers currently serialize all queries.

   Evidence: the MySQL driver enables pooling (`MaximumPoolSize`) but wraps each `ExecuteQueryAsync` and `ExecuteQueriesAsync` call in `_Lock.LockAsync` (`src/NetLedger/Database/Mysql/MysqlDatabaseDriver.cs:23`, `:52`, `:89`, `:162`). SQL Server and PostgreSQL follow the same pattern.

   Impact: the correctness goal is to serialize ledger mutations for a given account, not to serialize unrelated SQL work. Today one `DatabaseDriverBase` instance can execute only one SQL call at a time, so pooled MySQL/PostgreSQL/SQL Server connections cannot provide parallel read or write throughput. Dashboard fan-out calls and concurrent API traffic queue behind unrelated queries.

   Recommendation: replace the provider-wide query lock with keyed serialization at the ledger/account boundary. A keyed in-process lock such as `Padlock<string>` keyed by account identifier is a good fit for single-process serialization because it provides exclusive async locking per key without blocking unrelated accounts. Keep SQLite-specific protection and schema initialization locking as needed, and keep transaction batches atomic on their own connection/transaction. For multi-instance deployments, an in-process keyed lock is not sufficient by itself; retain the database-backed account lock or replace it with database-native per-account locking so two server instances cannot commit entries to the same account concurrently.

2. Balance enumeration is an N+1 path and now performs distributed lock writes for read requests.

   Evidence: `Ledger.GetAllBalancesAsync` reads all accounts and then calls `GetBalanceAsync` for each account (`src/NetLedger/Ledger.cs:859-867`). Tenant-scoped API balance enumeration repeats the same per-account loop (`src/NetLedger.Server/API/Agnostic/BalanceHandler.cs:137-146`). `GetBalanceAsync` acquires an in-process lock plus a database-backed account lock (`src/NetLedger/Ledger.cs:658-666`), and every database lock acquisition deletes expired locks, inserts a lock row, and later deletes it (`src/NetLedger/Database/DatabaseDriverBase.cs:168-176`, `:241-248`).

   Impact: listing balances scales with account count and generates write pressure on `accountlocks` for read-only dashboard/API calls. A dashboard refresh across 1,000 accounts can become thousands of SQL operations plus 1,000 lock acquire/release cycles.

   Recommendation: add an internal bulk balance summary path that computes latest balance and pending credit/debit totals for a set of accounts in grouped SQL. Keep the public API response shape unchanged. Consider using the distributed account lock only for mutating/commit flows, while read-only summaries use database isolation and deterministic queries.

3. Batch credit/debit APIs are implemented as per-entry loops.

   Evidence: `AddCreditsAsync` and `AddDebitsAsync` loop over inputs and call `AddCreditAsync`/`AddDebitAsync` one item at a time (`src/NetLedger/Ledger.cs:438-470`). Each single-entry path reads the account, acquires account locks, inserts one entry, and optionally commits (`src/NetLedger/Ledger.cs:330-352`, `:394-416`).

   Impact: batch calls do not get batch performance. Immediate-commit batches are especially expensive because they repeatedly lock and commit.

   Recommendation: implement the existing batch methods as one account read, one account lock, one `CreateManyAsync`, and at most one commit. This preserves the public API and should materially improve write throughput.

4. Dashboard pages request all balances when they only need a page or filtered subset.

   Evidence: the Accounts page requests paged accounts but all balances (`src/NetLedger.Dashboard/src/views/Accounts.jsx:72-79`). Home requests up to 1,000 accounts and all balances (`src/NetLedger.Dashboard/src/views/Home.jsx:293-304`) and then matches balances client-side.

   Impact: UI pages pay the cost of the full balance N+1 path even when displaying a small page or selected tenant/account subset.

   Recommendation: once a bulk balance internal path exists, use it behind `getAllBalances`. In the dashboard, avoid fetching balances for accounts outside the current tenant/filter/page where possible. If the API remains unchanged, a short-term UI improvement is to fetch account lists first and only request per-account balances for visible accounts.

5. Home chart loading fans out one entry enumeration request per visible account with unbounded concurrency.

   Evidence: `loadChart` maps every visible account to `api.listEntries(...)` and executes the full set via `Promise.all` (`src/NetLedger.Dashboard/src/views/Home.jsx:367-388`).

   Impact: a broad filter can launch hundreds or thousands of simultaneous HTTP requests from one browser, which can overload the server, amplify the provider-level database lock, and make the UI appear unstable.

   Recommendation: add a small client-side concurrency limiter and request cancellation for stale filter/range changes. A larger follow-up would be a server-side chart aggregation endpoint, but concurrency limiting is a low-risk improvement with no API change.

6. Request history summaries scan and bucket matching rows in application memory.

   Evidence: `SummarizeAsync` selects `statuscode`, `durationms`, and `createdutc` for all matching rows, orders them, then buckets in C# (`src/NetLedger/Database/Portable/PortableSqlRequestHistoryMethods.cs:155-181`). The dashboard calls the list and summary endpoints in parallel for the same filter (`src/NetLedger.Dashboard/src/views/RequestHistory.jsx:161-167`).

   Impact: large request-history ranges transfer and allocate far more data than needed for a 48-ish bucket chart.

   Recommendation: push summary aggregation into SQL using database-specific bucket expressions, returning only bucket rows. This keeps the REST response unchanged.

### Medium impact, low effort

7. Common entry query shapes would benefit from composite indexes.

   Evidence: setup currently creates separate indexes for `accountguid`, `createdutc`, `type`, `iscommitted`, plus some two-column indexes (`src/NetLedger/Database/Mysql/Queries/SetupQueries.cs:267-276`; mirrored in the other providers). Enumeration filters combine `accountguid`, time, type/committed state, amount, and order (`src/NetLedger/Database/Portable/PortableSqlMethods.cs:397-420`).

   Impact: the database may need extra scans/sorts for common searches like account plus time range ordered by created/id, pending entries by account/type/committed, and amount-ordered account queries.

   Recommendation: add cross-provider composite indexes for hot shapes, such as `(accountguid, createdutc)`, `(accountguid, id)`, `(accountguid, type, iscommitted, createdutc)`, and `(tenantid, accountguid, createdutc)`. Validate with each supported database's query plan before adding every index.

### SDK improvements

8. The C# SDK buffers every response body and owns its own `HttpClient`.

    Evidence: `NetLedgerClient` constructs `new HttpClient()` internally (`sdk/sdk-csharp/NetLedger.Sdk/NetLedgerClient.cs:176`) and `SendAsync` reads the full response string before deserialization (`sdk/sdk-csharp/NetLedger.Sdk/NetLedgerClient.cs:251-278`).

    Recommendation: add a backwards-compatible constructor overload accepting an external `HttpClient`, and use `HttpCompletionOption.ResponseHeadersRead` plus stream deserialization for large enumeration/request-history responses.

9. The JavaScript SDK uses raw Node HTTP requests without keep-alive agents and concatenates response chunks into a string.

    Evidence: `HttpClient` creates each request with `http`/`https` and no configured agent (`sdk/sdk-js/src/http-client.ts:100-123`), then appends chunks to `data` (`:122-156`).

    Recommendation: add keep-alive `http.Agent`/`https.Agent` instances and optional max response size guards. This is compatible with the current SDK API and reduces connection setup cost under repeated calls.

10. The Python SDK uses `requests.Session`, but does not tune adapter pool sizes.

    Evidence: the SDK correctly reuses a `requests.Session` (`sdk/sdk-python/netledger_sdk/http_client.py:36`), but uses default adapter settings for all traffic.

    Recommendation: expose optional pool sizing/retry settings or install a configured `HTTPAdapter` internally. Keep defaults unchanged for compatibility.

## Suggested Order

1. Replace provider-global locks with keyed per-account serialization while preserving cross-instance account locking.
2. Add an internal bulk balance summary query and wire `getAllBalances`/dashboard calls through it.
3. Rework ledger batch create paths to use one lock and one database batch.
4. Add dashboard concurrency limiting/cancellation for Home chart fan-out.
5. Push request-history summary bucketing into SQL.
6. Add measured composite indexes for the top entry enumeration shapes.
