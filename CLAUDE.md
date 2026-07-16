# CLAUDE.md

This file provides guidance to Claude Code when working with this repository.

## Project Overview

NetLedger is a multi-tenant ledgering platform. The repository contains:

- `src/NetLedger`: core ledger library, database abstractions, provider implementations, tenant/user/credential/RBAC models, and PrettyId-backed identifiers.
- `src/NetLedger.Server`: Watson 7 REST API server with authentication, authorization, credential management, identity routes, account routes, entry routes, and balance routes.
- `src/NetLedger.Dashboard`: React/Vite dashboard.
- `src/Test.Shared`, `src/Test.Automated`, `src/Test.Xunit`, `src/Test.Nunit`: Touchstone shared test suites and runner front ends.
- `sdk/`: C#, JavaScript, and Python SDKs.
- `docker/`: local deployment, PostgreSQL initialization, update scripts, and factory reset scripts.

## Build And Test Commands

```powershell
dotnet build src\NetLedger.sln
dotnet run --project src\Test.Automated\Test.Automated.csproj --framework net8.0
dotnet run --project src\Test.Automated\Test.Automated.csproj --framework net10.0
dotnet test src\Test.Xunit\Test.Xunit.csproj
dotnet test src\Test.Nunit\Test.Nunit.csproj
```

Dashboard commands:

```powershell
cd src\NetLedger.Dashboard
npm install
npm run build
```

## Architecture

The backend uses Watson 7 for HTTP hosting and explicit route handlers under `src/NetLedger.Server/API`. Request handling normalizes HTTP state into `RequestContext`, authenticates through `AuthService`, and authorizes with `AuthorizationService` or credential-specific access checks.

The database layer uses provider-neutral interfaces under `src/NetLedger/Database/Interfaces` and provider-specific implementations for SQLite, MySQL, PostgreSQL, and SQL Server. Keep provider-specific SQL under the corresponding provider folder. Use typed models and request DTOs rather than JSON DOM navigation for fixed contracts.

All primary domain identifiers are strings generated through `NetLedgerId`, which wraps PrettyId K-sortable IDs with stable entity prefixes from `IdentifierPrefixes`.

## Security Requirements

Tenant boundaries are mandatory:

- System admins can access anything.
- Tenant admins can access only resources in their tenant.
- Regular users can read their own tenant, read their own user record, and access resources explicitly mapped to them.
- Credentials are tenant-bound and user-bound.
- Raw credential secrets are returned only once at creation.
- Passwords and secret verifier material must not be logged or returned.
- Password-equivalent comparisons must use constant-time comparison.
- Authorization denies by default and records security-relevant audit events.

Security changes must include positive and negative tests around system admin, tenant admin, regular user, cross-tenant, same-tenant-unmapped, and own-resource cases.

## Code Style

Follow `C:\Code\agents\requirements\CODE_STYLE.md` strictly:

- Namespace declaration at the top; `using` statements inside the namespace.
- System/Microsoft usings first in alphabetical order, then other usings in alphabetical order.
- One class or one enum per file. Do not nest DTOs inside handlers.
- Do not use `var`.
- Do not use tuples unless there is no reasonable alternative.
- Public members, constructors, and public methods require XML documentation.
- Do not document private members or private methods.
- Async calls should use `.ConfigureAwait(false)` where appropriate.
- Async methods should accept a `CancellationToken` unless the class owns cancellation state.
- Validate inputs at method entry with guard clauses.
- Prefer specific exceptions with useful messages.
- Enable nullable reference types in project files.
- Do not add `Console.WriteLine` to library code.

## Tests

Shared behavior tests live in `src/Test.Shared` as Touchstone descriptors. The console, xUnit, and NUnit projects consume the same suite list. Do not write console output from `Test.Shared`; throw exceptions on assertion failures.

When adding data access, authorization, or tenant-scoped behavior, add tests that prove both allowed and denied paths. Enumeration tests must prove that cross-tenant and unmapped same-tenant records do not leak.

## Docker

Use `.yaml` files, not `.yml`. The local Docker surface lives in `docker/`, including PostgreSQL initialization and factory reset scripts. Compose files should either use explicit image tags or build contexts.
