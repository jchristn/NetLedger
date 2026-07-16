# NetLedger v3.0.0 Multi-Tenancy Plan

NetLedger v3.0.0 is a breaking product release. The work is not limited to adding a tenant column. The ledger library, REST server, dashboard, SDKs, documentation, Postman collection, database providers, authentication model, and tests all need to move to the same tenant-aware contract.

Progress convention:

- [ ] Not started
- [~] In progress
- [x] Complete
- [!] Blocked or needs a product decision

Status note: the implementation progress and human-intervention checklist at the bottom of this document are the authoritative current status for this branch. Earlier detailed checklist sections are retained as the original planning baseline and design traceability.

## Product Decisions Needed

The requirements are clear enough to start implementation. A few choices should be decided before the API is frozen:

- [x] Keep all paths under `/v1/...`.
- [x] Canonical tenant hint: support `/v1/tenants/{tenantId}/...` and `x-tenant-id`; reject requests when both are present and disagree.
- [x] Password login creates revocable server-side sessions in v3.0.0.
- [x] Migrate API keys to credentials.
- [x] Support many-to-many user/account mapping with `accountusermaps`.
- [x] Use these metadata limits: labels max 64 labels per object, each label 1-128 chars; tags max 128 pairs, keys 1-128 chars, values 0-1024 chars; reject nested JSON in tags because `Dictionary<string,string>` is required.

## Current State Audit

The current codebase is v2-oriented and mostly single-tenant. The core shapes are useful, but they sit below the new security boundary and use identifiers that v3 must replace.

| Area | Current implementation | v3 requirement | Gap |
| --- | --- | --- | --- |
| IDs | `Account`, `Entry`, `ApiKey`, `Balance`, queries, routes, SDKs, docs, and Postman use `int Id` plus `Guid GUID`. | Remove database integer IDs from model contracts and replace `public Guid GUID` with `public string Id` using PrettyId K-sortable IDs, total length 32. | Breaking model, API, DB, SDK, docs, tests, and route changes required. |
| Tenants | No tenant model or tenant column exists in `accounts`, `entries`, or `apikeys`. | Every account, ledger entry, and object must be tenant scoped. | All schema, indexes, interfaces, queries, and service calls need tenant scoping. |
| Users | No user table or user-facing identity model exists. | Users must belong to tenants, have email/password, active/protected flags, and system/tenant admin flags. | Add models, storage, APIs, auth, dashboard pages, tests. |
| Credentials | `ApiKey` is a bearer token with `IsAdmin`. | Credential objects must be tenant- and user-scoped with access key and secret key; secrets shown once, not used for signatures in v3.0.0. | Replace or migrate API keys to credentials and add secret lifecycle. |
| Auth flow | `AuthService` accepts `Authorization: Bearer <api-key>`. `RequestContext.Auth` exists but `PreRoutingHandler` does not attach it to the request used by handlers. | Every request resolves principal, tenant, resource, operation, and authorization result. Dashboard must support tenant-specific login and tenant selection. | Auth needs redesign and request context propagation. |
| Authorization | API key admin check exists, but handlers do not consistently enforce admin-only behavior or tenant/user access. | System admins see all tenants; tenant admins see tenant-local objects; regular users see themselves and mapped accounts only. Explicit deny beats permit. | Add authorization service, operation mapping, query filters, denial audit. |
| Metadata | No `Labels` or `Tags` on accounts, entries, batch inputs, operations, SDK models, or docs. | Accounts, ledger entries, and operations support `List<string> Labels` and `Dictionary<string,string> Tags`; filters are all-must-match AND filters. | Add fields, validation, storage, filtering, UI editors, SDK support, docs. |
| Enumeration | `EnumerationQuery` supports GUID continuation and amount/balance/date/search filters. | Add labels/tags filters, `CreditMinimum`, `CreditMaximum`, `DebitMinimum`, `DebitMaximum`, nullable values, value clamping, reasonable defaults. | Query model, filter builder, DB provider SQL, API parsing, SDKs, tests need updates. |
| Database architecture | Provider-specific folders exist for SQLite, MySQL, PostgreSQL, and SQL Server. SQL is manually built. No `DatabaseDriverFactory`. | Provider-neutral abstractions, net-new v3 schema setup, first-boot seeding, tenant-aware queries, PrettyId IDs, provider methods such as tenants/users/credentials. | Add setup/schema interfaces; keep manual SQL style while enforcing tenant filters. |
| Server architecture | Server runs on Watson 7 with existing route registration shape. | Requirements call for Watson 7, instance-based host, feature route registrars, typed request context, health/OpenAPI where practical. | Watson 7 is complete; any deeper route-composition cleanup is now code organization, not a v3 functional blocker. |
| Dashboard | React dashboard uses API key login, stores key in localStorage, and has pages for home, accounts, entries, and API keys. No i18n. | Tenant-aware login, tenant picker for multi-tenant emails, role-aware navigation, labels/tags editors, admin pages for tenants/users/credentials/roles/audit, i18n, richer operational shell. | Full product dashboard update required. |
| SDKs | C#, JS/TS, and Python SDKs use GUID-oriented account and entry models. | SDKs must expose tenant, user, credential, session, labels/tags, string IDs, new filters, and changed auth. | Breaking SDK major version updates plus test harnesses. |
| Docs | README explicitly says no auth/authorization and no multi-tenant isolation. REST API uses GUID examples. CHANGELOG is v2-focused. | README, CHANGELOG, REST_API, and Postman must describe v3.0.0 behavior and request bodies. | Full docs rewrite/update needed. |
| Tests | `src/Test`, `src/Test.Automated`, and `src/Test.ServerAutomated` are console-style projects. | Touchstone migration with `Test.Shared`, `Test.Automated`, `Test.Xunit`, and `Test.Nunit`. | Rebuild test strategy and add broad coverage. |

## Requirements Compliance Matrix

### Authentication Requirements

- [ ] Add `Tenant`, `User`, `Credential`, `AuthSession`, `UserRole`, `UserRoleAssignment`, `UserRoleMap`, `Permission`, `RolePermissionMap`, `CredentialScopeAssignment`, and `AuditRecord` models.
- [ ] Use `string Id` on every model and remove public integer row IDs from model contracts.
- [ ] Add `TenantId`, `CreatedUtc`, `LastUpdateUtc`, `Active`, and `IsProtected` where applicable.
- [ ] Implement tenant resolution from route, `x-tenant-id`, and authenticated material.
- [ ] Reject requests when tenant hints conflict or when tenant cannot be resolved for a tenant-scoped operation.
- [ ] Treat email addresses as unique only within a tenant.
- [ ] Add system admin and tenant admin bypass rules exactly as documented, with audit records for privileged bypass.
- [ ] Implement RBAC with tenant-scoped and resource-scoped assignments.
- [ ] Enforce explicit deny before permit and implicit deny when no permission matches.
- [ ] Add first-boot built-in roles: `TenantAdmin`, `SecurityAdmin`, `Auditor`, `ResourceAdmin`, `Editor`, `Viewer`, `TenantMember`, and `Custom`.
- [ ] Protect seeded roles from normal tenant mutation.
- [ ] Add password hashing and constant-time comparisons. The requirements mention SHA-256; evaluate whether to follow that literally or wrap it with salt/iteration policy before implementation.
- [ ] Add access key and secret key generation. Show raw secret only once, persist encrypted or verifier material plus `SecretKeyLast4`.
- [ ] Support direct-header access key authentication in v3.0.0; reserve signed requests for a later release.
- [ ] Add session token issue, validate, refresh where needed, revoke, and logout endpoints.
- [ ] Persist authorization failures in an append-only audit table.
- [ ] Add effective permission inspection endpoint for dashboard gating.
- [ ] Include request ID, correlation ID, trace ID, tenant ID, principal ID, auth scheme, resource type, operation type, and resource ID in request context and logs.

### Backend Architecture Requirements

- [x] Upgrade server package from Watson 6.5.0 to Watson 7 after confirming exact package version and API shape.
- [ ] Convert `NetLedgerServer` from static orchestration to an instance host with explicit composition.
- [ ] Split route registration into feature route registrars: tenants, users, credentials, sessions, accounts, entries, balances, operations, roles, permissions, audit, request history, service info.
- [ ] Add `DatabaseDriverFactory`.
- [x] Treat v3 schema as net-new/manual-migration only; no automated v2-to-v3 migration is required.
- [ ] Keep provider-specific database code under the existing provider folders.
- [ ] Add provider interfaces for every new domain area instead of generic repositories.
- [ ] Add request history capture and API endpoints if the product keeps the shared dashboard observability standard.
- [ ] Use typed request/response models for all fixed API contracts.
- [ ] Avoid tuples in route and service responses.
- [ ] Pass cancellation tokens through public async APIs and database calls.
- [ ] Add health and OpenAPI exposure where Watson 7 support is available.

### Code Style Requirements

- [ ] Keep namespace declarations at the top with using statements inside namespace blocks.
- [ ] Use explicit types; do not introduce `var`.
- [ ] Add XML documentation to public members, constructors, public methods, and documented exceptions.
- [ ] Keep one class or enum per file. Move nested private DTOs in handlers into request model files.
- [ ] Rename private fields to `_PascalCase` where touched; the current code uses `_Header`, `_Settings`, and similar, which is aligned.
- [ ] Keep manual SQL if desired, but add shared escaping/conversion helpers per provider and never omit tenant predicates.
- [ ] Add `ConfigureAwait(false)` on awaited library/server calls where appropriate.
- [ ] Use specific exception types and context-rich messages.
- [ ] Implement full dispose pattern for disposable services and drivers touched by the refactor.
- [ ] Enable nullable reference types consistently and remove broad nullable warning suppressions over time.

### Dashboard, Frontend, And I18N Requirements

- [ ] Add an i18n foundation and move visible strings into locale resources.
- [ ] Add protected routes that do not flash private content during session restore.
- [ ] Replace API key login with tenant-aware authentication:
  - [ ] Server URL entry.
  - [ ] Email and password entry.
  - [ ] Tenant lookup when email maps to more than one tenant.
  - [ ] Tenant picker with clear role/context labels.
  - [ ] Session restore and logout.
- [ ] Add shell context chips for server URL, tenant, principal, and role.
- [ ] Add navigation groups for Ledger, Security, Operations, and Settings.
- [ ] Add admin pages for tenants, users, credentials, roles, permissions, assignments, sessions, audit, and effective permissions.
- [ ] Add labels and tags editing controls for accounts and entries.
- [ ] Add all-must-match label/tag filters to account and entry tables.
- [ ] Add copy controls for IDs, access keys, endpoint URLs, and generated secrets.
- [ ] Add empty, loading, error, and permission-denied states on every record page.
- [ ] Keep tables horizontally scrollable, with stable pagination/filter toolbars.
- [ ] Add first-run setup wizard for initial tenant and system admin creation.
- [ ] Persist setup/tour dismissal per server, tenant, and user where applicable.

### Repository Requirements

- [ ] Add `.dockerignore` if Docker builds remain in the release workflow.
- [ ] Keep source under `src/`, SDKs under `sdk/`, Docker under `docker/`, and documentation at root.
- [ ] Add thorough SDK test harnesses for C#, JS/TS, and Python.
- [ ] Keep Docker compose files as `.yaml`, matching the current repository.

## Target Domain Model

All IDs are strings generated through `PrettyId.IdGenerator.GenerateKSortable(prefix, 32)`. The 32-character limit includes the prefix. Prefixes should stay short because PrettyId includes a timestamp segment after the prefix.

| Entity | Prefix | Example |
| --- | --- | --- |
| Tenant | `ten_` | `ten_...` |
| User | `usr_` | `usr_...` |
| Credential | `cred_` | `cred_...` |
| Session | `sess_` | `sess_...` |
| Account | `acct_` | `acct_...` |
| Entry | `ent_` | `ent_...` |
| Operation | `op_` | `op_...` |
| Role | `role_` | `role_...` |
| Permission | `perm_` | `perm_...` |
| Assignment | `asn_` | `asn_...` |
| Audit record | `aud_` | `aud_...` |
| Request history | `req_` | `req_...` |

Core model changes:

- [ ] `Account`: `Id`, `TenantId`, `OwnerUserId?`, `Name`, `Notes`, `Labels`, `Tags`, `CreatedUtc`, `LastUpdateUtc`, `Active`.
- [ ] `Entry`: `Id`, `TenantId`, `AccountId`, `Type`, `Amount`, `Description`, `Labels`, `Tags`, `ReplacesEntryId?`, `IsCommitted`, `CommittedByEntryId?`, `CommittedUtc?`, `CreatedUtc`, `LastUpdateUtc`.
- [ ] `Operation`: `Id`, `TenantId`, `AccountId`, `Type`, `EntryIds`, `Labels`, `Tags`, `CreatedByUserId?`, `CreatedByCredentialId?`, `CreatedUtc`, `LastUpdateUtc`.
- [ ] `Balance`: replace `AccountGUID`, `EntryGUID`, and committed GUID lists with string IDs.
- [ ] `PendingTransactionSummary`: entries carry string IDs and metadata.
- [ ] `BatchEntryInput`: add `Labels` and `Tags`.
- [ ] `EnumerationQuery`: replace `AccountGUID` and `ContinuationToken` GUIDs with string IDs, add `Labels`, `Tags`, `CreditMinimum`, `CreditMaximum`, `DebitMinimum`, `DebitMaximum`.
- [ ] `EnumerationResult<T>`: replace continuation token type with string.

## Database Plan

The v3 database must make tenant isolation impossible to forget. Every provider implementation should use composite tenant predicates in read, update, delete, enumerate, and balance queries. A method that reads by resource ID must either accept `tenantId` or be explicitly marked system-only.

### Schema

- [ ] Add migrations table:
  - [ ] `id`, `name`, `appliedutc`, `checksum`, `success`.
- [ ] Add `tenants` with `id`, `parentid`, `name`, `region`, `restbasedomain`, `s3basedomain`, `active`, `isprotected`, timestamps.
- [ ] Add `users` with `id`, `tenantid`, name fields, email, password hash fields, `isadmin`, `istenantadmin`, `active`, `isprotected`, timestamps.
- [ ] Add `credentials` with `id`, `tenantid`, `userid`, `name`, `accesskey`, `secretkeyencrypted` or verifier material, `secretkeylast4`, `authmode`, `lastusedutc`, `expiresutc`, flags, timestamps.
- [ ] Add `authsessions` with session/principal fields, auth scheme, token identifier, source IP, user agent, expiry/revocation fields, flags, timestamps.
- [ ] Add `userroles`, `permissions`, `rolepermissionmaps`, `userroleassignments`, `userrolemaps`, and `credentialscopeassignments`.
- [ ] Add `accountusermaps` for regular-user account visibility.
- [ ] Add `operations` and `operationentries` if operations become first-class objects beyond commit records.
- [ ] Add `auditrecords` for auth/authorization/security events.
- [ ] Add `requesthistory` if implementing the dashboard observability requirement.
- [ ] Replace `accounts.guid` with `accounts.id` string and add `tenantid`, `owneruserid`, `labels`, `tags`, `lastupdateutc`, `active`.
- [ ] Replace `entries.guid`, `accountguid`, `replaces`, and `committedbyguid` with string ID columns and add `tenantid`, `labels`, `tags`, `lastupdateutc`.
- [ ] Replace or migrate `apikeys` into `credentials`.

### Indexing

- [ ] Unique `tenants.id`.
- [ ] Unique tenant name if product requires it; otherwise indexed name.
- [ ] Unique `(tenantid, email)` for users.
- [ ] Unique `credentials.accesskey`.
- [ ] Unique `(tenantid, accounts.id)`.
- [ ] Unique `(tenantid, accounts.name)` unless duplicate account names are allowed.
- [ ] Unique `(tenantid, entries.id)`.
- [ ] Indexed `(tenantid, entries.accountid, entries.id)` for K-sortable pagination.
- [ ] Indexed `(tenantid, entries.accountid, entries.type, entries.committed)`.
- [ ] Indexed `(tenantid, entries.accountid, entries.createdutc)`.
- [ ] Provider-appropriate JSON or text indexes for labels/tags where available. If JSON indexing is not portable enough, add normalized metadata tables.

### Labels And Tags Storage

Recommendation: store labels and tags as JSON columns for response hydration, plus normalized tables for filtering:

- [ ] `accountlabels(tenantid, accountid, label)`.
- [ ] `accounttags(tenantid, accountid, tagkey, tagvalue)`.
- [ ] `entrylabels(tenantid, entryid, label)`.
- [ ] `entrytags(tenantid, entryid, tagkey, tagvalue)`.
- [ ] `operationlabels(tenantid, operationid, label)`.
- [ ] `operationtags(tenantid, operationid, tagkey, tagvalue)`.

All label/tag filters are AND filters. A query for labels `["debit","blue"]` must only return objects with both labels. A query for tags `{ "user": "foo", "source": "api" }` must only return objects with both key/value pairs.

### Deployment Strategy

- [x] Treat v3.0.0 as a breaking schema/API release.
- [x] Support net-new v3 deployments and manually migrated deployments only.
- [x] Do not ship automated v2-to-v3 data migration or legacy GUID lookup routes.
- [x] Replace API key management with credential management endpoints.

## Core Library Plan

### Identifier Refactor

- [ ] Add `IdGenerator` helper in `src/NetLedger/Helpers/IdGenerator.cs`.
- [ ] Add PrettyId NuGet package to `src/NetLedger/NetLedger.csproj`.
- [ ] Generate IDs through one helper so prefixes and length are not duplicated.
- [ ] Replace public `Guid` properties with `string` IDs.
- [ ] Remove public model `int Id` fields. Provider-specific row IDs may exist internally only where a provider needs stable tie-breakers, but they must not leak into contracts.
- [ ] Replace `AsyncKeyedLocker<Guid>` with `AsyncKeyedLocker<string>`.
- [ ] Rename methods:
  - [ ] `GetAccountByGuidAsync` -> `GetAccountByIdAsync`.
  - [ ] `DeleteAccountByGuidAsync` -> `DeleteAccountByIdAsync`.
  - [ ] `ReadByGuidAsync` -> `ReadByIdAsync`.
  - [ ] `DeleteByGuidAsync` -> `DeleteByIdAsync`.
  - [ ] `ExistsByGuidAsync` -> `ExistsByIdAsync`.

### Tenant-Aware Ledger API

- [ ] Add overloads or new service methods that require `tenantId`.
- [ ] Require `tenantId` on account creation, read, update, delete, enumerate, balance, entry creation, commit, cancel, and verification.
- [ ] Require `accountId` to be checked against the same `tenantId` before entry or balance operations.
- [ ] Add `userId` or principal context when creating accounts and entries so ownership/audit data can be recorded.
- [ ] Return string IDs from creation methods.
- [ ] Add account user mapping methods.
- [ ] Add operation metadata to single and batch entry creation.
- [ ] Keep balance chain verification tenant-scoped.

### Metadata And Filtering

- [ ] Add `Labels` and `Tags` to account constructors, entry constructors, batch inputs, and create/update requests.
- [ ] Normalize labels on write:
  - [ ] trim whitespace.
  - [ ] reject null/empty labels.
  - [ ] de-duplicate case-insensitively or choose case-sensitive behavior and document it.
- [ ] Normalize tags on write:
  - [ ] trim keys.
  - [ ] reject null/empty keys.
  - [ ] reject null values or convert to empty string, then document the behavior.
- [ ] Add labels/tags to `FilterBuilder`.
- [ ] Add type-aware debit/credit amount filters:
  - [ ] `CreditMinimum` and `CreditMaximum` match `Type == Credit`.
  - [ ] `DebitMinimum` and `DebitMaximum` match `Type == Debit`.
  - [ ] Existing `AmountMinimum` and `AmountMaximum` remain generic amount filters if retained.
- [ ] Clamp `MaxResults` to 1-1000 instead of throwing when parsing API query values; keep constructor setters strict only if existing behavior is intentionally preserved.

## REST API Plan

### Route Shape

All v3 behavior remains under `/v1/...` routes:

- [ ] `GET /v1/service`
- [ ] `POST /v1/auth/tenants`
- [ ] `POST /v1/auth/login`
- [ ] `POST /v1/auth/logout`
- [ ] `POST /v1/auth/sessions/{sessionId}/revoke`
- [ ] `GET /v1/tenants`
- [ ] `PUT /v1/tenants`
- [ ] `GET /v1/tenants/{tenantId}`
- [ ] `PATCH /v1/tenants/{tenantId}`
- [ ] `DELETE /v1/tenants/{tenantId}`
- [ ] `GET /v1/tenants/{tenantId}/users`
- [ ] `PUT /v1/tenants/{tenantId}/users`
- [ ] `GET /v1/tenants/{tenantId}/credentials`
- [ ] `PUT /v1/tenants/{tenantId}/users/{userId}/credentials`
- [ ] `GET /v1/tenants/{tenantId}/accounts`
- [ ] `PUT /v1/tenants/{tenantId}/accounts`
- [ ] `GET /v1/tenants/{tenantId}/accounts/{accountId}`
- [ ] `PATCH /v1/tenants/{tenantId}/accounts/{accountId}`
- [ ] `DELETE /v1/tenants/{tenantId}/accounts/{accountId}`
- [ ] `GET /v1/tenants/{tenantId}/accounts/{accountId}/entries`
- [ ] `POST /v1/tenants/{tenantId}/accounts/{accountId}/entries/enumerate`
- [ ] `PUT /v1/tenants/{tenantId}/accounts/{accountId}/credits`
- [ ] `PUT /v1/tenants/{tenantId}/accounts/{accountId}/debits`
- [ ] `DELETE /v1/tenants/{tenantId}/accounts/{accountId}/entries/{entryId}`
- [ ] `GET /v1/tenants/{tenantId}/accounts/{accountId}/balance`
- [ ] `POST /v1/tenants/{tenantId}/accounts/{accountId}/commit`
- [ ] `GET /v1/tenants/{tenantId}/audit`
- [ ] `GET /v1/tenants/{tenantId}/permissions/effective`

### Request Bodies

- [ ] Account create:

```json
{
  "name": "Operating Account",
  "notes": "Primary account",
  "ownerUserId": "usr_...",
  "initialBalance": 1000.0,
  "labels": ["operating", "usd"],
  "tags": { "department": "finance", "region": "west" }
}
```

- [ ] Credit/debit create:

```json
{
  "amount": 25.0,
  "notes": "Customer payment",
  "isCommitted": false,
  "labels": ["credit", "blue"],
  "tags": { "user": "foo", "source": "dashboard" }
}
```

- [ ] Batch create:

```json
{
  "isCommitted": false,
  "operationLabels": ["batch", "import"],
  "operationTags": { "file": "july-ledger.csv" },
  "entries": [
    {
      "amount": 25.0,
      "notes": "Customer payment",
      "labels": ["credit"],
      "tags": { "user": "foo" }
    }
  ]
}
```

- [ ] Enumeration:

```json
{
  "maxResults": 50,
  "continuationToken": null,
  "ordering": "CreatedDescending",
  "searchTerm": "payment",
  "labels": ["credit", "blue"],
  "tags": { "user": "foo" },
  "creditMinimum": 10.0,
  "creditMaximum": null,
  "debitMinimum": null,
  "debitMaximum": null
}
```

### Authorization Per Route

- [ ] Map each route to `ResourceType`, `OperationType`, and `ResourceId`.
- [ ] System admin:
  - [ ] Can list/read/manage any tenant, user, account, credential, role, permission, audit record, and entry.
- [ ] Tenant admin:
  - [ ] Can list/read/manage users, credentials, accounts, roles, permissions, audit records, and entries only inside their tenant.
- [ ] Regular user:
  - [ ] Can read self.
  - [ ] Can list/read accounts mapped to self.
  - [ ] Can create entries only against mapped accounts if granted by role or explicit account mapping policy.
  - [ ] Cannot list other users or tenant-wide credentials.
- [ ] Credentials:
  - [ ] Inherit no more than owning user permissions and credential scope assignments.
  - [ ] Cannot cross tenant boundaries.

## Dashboard Plan

The dashboard should become an operations console for tenants and ledgers. Keep the current accounts and entries pages, but add security administration and metadata-aware workflows.

### Foundation

- [ ] Add route metadata with title, summary, required permission, and nav group.
- [ ] Add auth context with `serverUrl`, `sessionToken`, `tenantId`, `tenantName`, `user`, `roles`, `effectivePermissions`, and `isRestoring`.
- [ ] Store session data by server and tenant; do not store raw password or secret keys.
- [ ] Add API client support for session token and `x-tenant-id`.
- [ ] Add i18n resource files and migrate visible strings.
- [ ] Add role-aware navigation hiding only as a UX aid; server remains authoritative.

### Login And Setup

- [ ] Replace API key field with email/password flow.
- [ ] Add tenant discovery by email:
  - [ ] If one tenant matches, continue automatically after showing tenant context.
  - [ ] If multiple tenants match, require selection.
  - [ ] If none match, show a safe generic error.
- [ ] Add local development helper text only when server reports first-run or dev mode.
- [ ] Add first-run setup wizard:
  - [ ] Create initial tenant.
  - [ ] Create protected system admin.
  - [ ] Optionally create first tenant admin and sample account.
- [ ] Add logout and session revocation.

### Ledger Pages

- [ ] Accounts page:
  - [ ] Show ID, tenant, name, owner, active state, balance summary, labels, tags, created/updated timestamps.
  - [ ] Add create/edit modal with labels/tags controls.
  - [ ] Add filters for search, labels, tags, balance min/max, owner, active.
  - [ ] Add account-user mapping drawer for admins.
- [ ] Entries page:
  - [ ] Show ID, account ID/name, type, amount, commit state, labels, tags, created/committed timestamps.
  - [ ] Add credit/debit modal with labels/tags controls.
  - [ ] Add batch entry editor with per-entry metadata and operation metadata.
  - [ ] Add filters for labels, tags, credit/debit min/max, amount min/max, date, commit state.
- [ ] Balance page:
  - [ ] Keep committed/pending summaries.
  - [ ] Add tenant/account context and metadata-aware pending-entry views.
- [ ] Operations page:
  - [ ] Show commits, imports, batches, and their labels/tags if operations become first-class.

### Admin Pages

- [ ] Tenants: list/create/edit/disable, protected flag handling, tenant context switching for system admins.
- [ ] Users: list/create/edit/disable, tenant admin/system admin flags, account mappings.
- [ ] Credentials: create access/secret key, show secret once, revoke, rotate, expire, show last used.
- [ ] Roles and permissions: built-in roles read-only, custom roles editable, assignment workflows.
- [ ] Sessions: active sessions, revoke, expiry, source IP/user agent.
- [ ] Audit: authorization failures, login attempts, admin bypass events, credential operations.
- [ ] Effective permissions: inspect current user or selected principal.

## SDK Plan

All SDKs must ship a v3 major-compatible surface. Do not hide the breaking change behind GUID aliases.

### C# SDK

- [ ] Replace `Guid` model properties with string IDs.
- [ ] Add tenant/session/auth client methods.
- [ ] Add user, credential, role, permission, audit, and operation methods.
- [ ] Add labels/tags on account and entry models.
- [ ] Add new enumeration filters.
- [ ] Add `NetLedgerClient` constructor overloads for access key, session token, and tenant ID.
- [ ] Add test harness coverage for auth, tenant scoping, metadata, and negative authorization cases.
- [ ] Update README and XML docs.

### JavaScript/TypeScript SDK

- [ ] Update `src/models/index.ts` to string IDs and metadata fields.
- [ ] Add tenant/session-aware HTTP client headers.
- [ ] Add auth, tenant, user, credential, role, permission, audit, and operation methods.
- [ ] Add type-safe enumeration query and result models.
- [ ] Regenerate `dist/` after source changes.
- [ ] Update README and test harness.

### Python SDK

- [ ] Update models to string IDs and metadata fields.
- [ ] Add tenant/session-aware HTTP client headers.
- [ ] Add auth, tenant, user, credential, role, permission, audit, and operation methods.
- [ ] Update setup/package version to 3.0.0.
- [ ] Add negative and positive harness tests.
- [ ] Update README.

## Documentation And Postman Plan

- [ ] README:
  - [ ] Update version and product positioning for v3.0.0.
  - [ ] Remove claims that authentication/authorization and multi-tenancy are unsupported.
  - [ ] Add quickstart for first tenant, first user, login, account creation, labels/tags, debit/credit, and commit.
  - [x] Document v3 as net-new/manual migration only and describe the v3 PrettyId string-ID model.
- [ ] CHANGELOG:
  - [ ] Add v3.0.0 breaking changes.
  - [ ] List model/route/auth/schema/SDK changes.
- [ ] REST_API:
  - [ ] Replace GUID examples with PrettyId examples.
  - [ ] Add tenant/user/credential/session/RBAC/audit endpoints.
  - [ ] Add labels/tags examples and filters.
  - [ ] Document auth schemes and headers.
  - [ ] Document role behavior and response codes.
- [ ] Postman:
  - [ ] Add collection variables for `tenantId`, `userId`, `credentialId`, `sessionToken`, `accountId`, `entryId`, `roleId`, `permissionId`.
  - [ ] Add first-run setup folder.
  - [ ] Add login tenant-discovery and tenant-selection flows.
  - [ ] Add account and entry metadata request bodies.
  - [ ] Add admin folders for users, credentials, roles, permissions, sessions, and audit.
  - [ ] Add tests that capture PrettyId string IDs rather than GUIDs.

## Touchstone Test Plan

Migrate tests before the largest refactors land or at the same time as the ID model changes. The shared test descriptors should become the source of truth for library, server, provider, and SDK expectations.

### Project Migration

- [ ] Add `src/Test.Shared/Test.Shared.csproj` with `Touchstone.Core`.
- [ ] Move existing core behavioral tests into shared descriptors.
- [ ] Update `src/Test.Automated` to use `Touchstone.Cli`.
- [ ] Add `src/Test.Xunit` with `Touchstone.XunitAdapter`.
- [ ] Add `src/Test.Nunit` with `Touchstone.NunitAdapter`.
- [ ] Keep `Test.ServerAutomated` only if it becomes a Touchstone-backed server harness; otherwise replace it.
- [ ] Add CI commands for console, xUnit, and NUnit runners.

### Coverage Inventory

- [ ] Identifier tests:
  - [ ] IDs use correct prefixes.
  - [ ] IDs are exactly 32 characters.
  - [ ] IDs sort chronologically by string order for same prefix.
  - [x] Old GUID compatibility endpoints are not shipped for v3 management paths.
- [ ] Tenant tests:
  - [ ] Tenant creation, read, update, disable.
  - [ ] Every account and entry requires tenant ID.
  - [ ] Same account name can exist in different tenants if product allows it.
  - [ ] Cross-tenant reads, writes, deletes, commits, and enumeration fail.
- [ ] User tests:
  - [ ] Email uniqueness within tenant.
  - [ ] Same email can exist in multiple tenants.
  - [ ] Tenant selection required for multi-tenant email login.
  - [ ] Disabled user cannot authenticate.
- [ ] Credential tests:
  - [ ] Access and secret keys are generated.
  - [ ] Raw secret appears once.
  - [ ] Secret is redacted thereafter.
  - [ ] Disabled, expired, wrong-tenant, and revoked credentials fail.
- [ ] Auth/session tests:
  - [ ] Password login success and failure.
  - [ ] Session token validation.
  - [ ] Session revocation.
  - [ ] Conflicting tenant hints rejected.
  - [ ] Constant-time comparison helper covered at unit level.
- [ ] Authorization tests:
  - [ ] System admin can access all tenants.
  - [ ] Tenant admin can access only tenant-local objects.
  - [ ] Regular user sees only self and mapped accounts.
  - [ ] Explicit deny overrides permit.
  - [ ] Implicit deny rejects.
  - [ ] Authorization failure creates audit record.
- [ ] Account tests:
  - [ ] Create with labels/tags.
  - [ ] Update labels/tags.
  - [ ] Enumerate by all-must-match labels/tags.
  - [ ] Pagination by K-sortable continuation token.
  - [ ] Delete cascades tenant-scoped entries and metadata.
- [ ] Entry tests:
  - [ ] Credit/debit create with labels/tags.
  - [ ] Batch create with per-entry and operation metadata.
  - [ ] Pending, commit, cancel, and balance chain behavior.
  - [ ] `CreditMinimum`, `CreditMaximum`, `DebitMinimum`, and `DebitMaximum`.
  - [ ] Labels/tags all-must-match filters.
- [ ] Provider matrix:
  - [ ] SQLite.
  - [ ] MySQL.
  - [ ] PostgreSQL.
  - [ ] SQL Server.
- [ ] REST tests:
  - [ ] Every endpoint success path.
  - [ ] Bad request, unauthorized, forbidden, not found, conflict, and validation errors.
  - [ ] Response shape and redaction.
- [ ] Dashboard tests:
  - [ ] Build succeeds.
  - [ ] Login restore and tenant picker workflows.
  - [ ] Role-aware navigation.
  - [ ] Labels/tags controls and filters.
  - [ ] i18n missing-key detection.
- [ ] SDK tests:
  - [ ] C#, JS/TS, and Python can authenticate, create tenant/account, create entries with metadata, enumerate by filters, and handle authorization failures.

## Implementation Sequence

### Phase 0: Lock The Contract

- [ ] Resolve the product decisions at the top of this document.
- [ ] Write v3 API examples into REST_API draft before implementation starts.
- [ ] Confirm exact NuGet versions for PrettyId, Touchstone, and Watson 7.
- [x] Create `feature/v3.0.0` branch and remove automated v2-to-v3 migration as a release requirement.

### Phase 1: Test Harness And ID Foundation

- [ ] Add Touchstone project structure.
- [ ] Add initial descriptor suites for IDs, tenant isolation, metadata filters, and core ledger behavior.
- [ ] Add PrettyId package and central ID helper.
- [ ] Update models to string IDs behind failing tests.
- [ ] Update core ledger methods and database interfaces to string IDs.

### Phase 2: Schema And Provider Setup

- [x] Add v3 schema setup for net-new/manual-migration deployments.
- [ ] Add v3 tables and indexes in SQLite.
- [x] Add v3 table and index declarations to MySQL, PostgreSQL, and SQL Server.
- [ ] Update provider CRUD for accounts and entries with tenant predicates.
- [ ] Add metadata persistence and filters.
- [x] Automated v2 data migration and validation are out of scope by product decision.

### Phase 3: Identity, Credentials, Sessions, RBAC

- [ ] Add identity/security models and provider interfaces.
- [ ] Add authentication service with tenant resolution.
- [ ] Add authorization service and operation mapping.
- [ ] Add seeded roles and permissions.
- [ ] Add audit logging for denials and admin bypasses.
- [ ] Add session lifecycle endpoints.
- [ ] Migrate API keys to credentials.

### Phase 4: REST API Refactor

- [ ] Upgrade to Watson 7.
- [ ] Convert host to instance-based composition.
- [ ] Add feature route registrars.
- [ ] Add v3 typed request/response DTOs.
- [ ] Add tenant/account/entry/security/admin endpoints.
- [ ] Add request history and health/OpenAPI if approved.
- [ ] Verify CORS, request context propagation, response codes, and redaction.

### Phase 5: Dashboard

- [ ] Update API client and auth context.
- [ ] Build tenant-aware login and tenant picker.
- [ ] Build first-run setup wizard.
- [ ] Add role-aware shell, route metadata, and i18n.
- [ ] Update accounts, entries, balances, and API key pages.
- [ ] Add tenants, users, credentials, sessions, roles, permissions, audit, and effective permissions pages.
- [ ] Add labels/tags controls and filters.
- [ ] Run build, lint, and browser smoke tests.

### Phase 6: SDKs

- [ ] Update C# SDK models, methods, tests, and README.
- [ ] Update JS/TS SDK source, generated dist, tests, and README.
- [ ] Update Python SDK models, methods, tests, and README.
- [ ] Run SDK test harnesses against the v3 server.

### Phase 7: Docs, Postman, Packaging

- [ ] Update README, CHANGELOG, REST_API, and SDK READMEs.
- [ ] Update Postman collection and examples.
- [ ] Update Docker config and sample settings.
- [ ] Set package versions to 3.0.0.
- [ ] Build all target frameworks.
- [ ] Run full Touchstone matrix.
- [x] No automated migration test is required for v3; release assumes net-new or manually migrated deployments.

## Release Acceptance Criteria

- [x] Every persisted v3 domain object implemented in this release is tenant-scoped. SQLite-backed accounts, entries, credentials, tenants, users, sessions, account-user maps, audit records, roles, permissions, and assignments are tenant-scoped where applicable.
- [x] Every public account, entry, tenant, user, credential, session, account-user map, and audit model ID is a 32-character PrettyId string with the approved prefix.
- [x] No API response exposes public integer row IDs or retrievable raw secret keys. Credential creation uses a dedicated one-time `SecretKey` response and later responses expose only `SecretKeyLast4`.
- [!] Tenant isolation is enforced by authorization and by database query predicates for the automated SQLite path. Full provider certification outside SQLite requires live provider infrastructure.
- [x] Conflicting tenant hints are rejected.
- [x] System admin, tenant admin, and regular user access match the product requirements for implemented resources. System and tenant admin bypasses, regular user self-read, mapped account access, RBAC assignments, explicit deny precedence, denials, and audit records are implemented.
- [!] Accounts and entries can set and retrieve labels/tags. First-class operation metadata requires a product decision because operations are not currently a persisted first-class object separate from entries/commits.
- [x] Enumeration supports all-must-match label/tag filters for accounts and entries.
- [x] Enumeration supports credit/debit min/max filters.
- [x] Dashboard login supports tenant selection for multi-tenant emails.
- [x] Dashboard can create and display labels and tags on accounts and entries; API filters are available for account and entry metadata.
- [x] SDKs expose the v3 tenant/auth/metadata contract for C#, JavaScript/TypeScript, and Python.
- [x] REST_API, README, CHANGELOG, and Postman reflect v3.0.0.
- [x] Touchstone runners pass for automated, xUnit, and NUnit projects.
- [!] Provider tests pass for SQLite, MySQL, PostgreSQL, and SQL Server or release notes explicitly identify any provider not certified. SQLite is covered by Touchstone; other providers require live database infrastructure for certification.
- [!] Migration preserves account counts, entry counts, committed balances, pending entries, and balance chain validity. This requires a representative v2 fixture supplied by a human.

## Risk Register

- [!] ID replacement is a whole-product breaking change. Mitigation: perform it early and update all tests and docs before broad feature work.
- [!] Tenant isolation bugs are high severity. Mitigation: require tenant ID in database interfaces, add negative cross-tenant tests for every method, and review every SQL statement.
- [!] Metadata filters can become slow with JSON-only storage. Mitigation: use normalized metadata tables for portable filtering.
- [!] Dashboard scope can grow beyond v3.0.0. Mitigation: prioritize tenant-aware login, ledger metadata, credentials, users, and tenant switching before advanced RBAC editors.
- [!] Existing API key behavior may conflict with credential/session requirements. Mitigation: replace management routes with credential routes and keep bearer access-key authentication explicit.
- [!] Watson 7 upgrade may force route API changes. Mitigation: isolate it in the route refactor phase and avoid mixing it with database setup work.

## Developer Notes

Keep the implementation boring where the requirements allow it. Tenant scope belongs in method signatures, indexes, and tests, not in comments or conventions. Metadata should be simple to write and predictable to filter. The dashboard should show the tenant and principal everywhere an operator could make a destructive choice.

## Implementation Progress

Last updated on this branch: v3.0.0 implementation pass.

- [x] Created and checked out `feature/v3.0.0`.
- [x] Added PrettyId package and central ID helpers.
- [x] Added domain models for tenants, users, credentials, sessions, and account-user maps.
- [x] Added audit record and effective permission models.
- [x] Converted core public account/entry/balance/query identifiers to string IDs while retaining JSON-hidden aliases only where existing internal method names still use GUID terminology.
- [x] Added account and entry `TenantId`, `Labels`, `Tags`, `LastUpdateUtc`, and active-state model fields.
- [x] Added metadata normalization and serializer helpers with the approved label/tag limits.
- [x] Added SQLite v3 schema fields and startup setup for net-new/manual-migration deployments.
- [x] Added SQLite storage for tenants, users, auth sessions, account-user maps, and audit records.
- [x] Persisted account and entry metadata/tenant fields in SQLite.
- [x] Added label/tag, tenant, credit min/max, and debit min/max filters to `FilterBuilder`.
- [x] Added mapped-user account enumeration filtering so regular users only list accounts mapped to themselves.
- [x] Added `/v1/tenants/{tenantId}/...` account, entry, balance, commit, and verify route aliases.
- [x] Added `x-tenant-id` parsing and conflict rejection when route/header tenant hints disagree.
- [x] Added tenant discovery, tenant-scoped password login, logout, session revocation, tenant, user, account-user map, audit, and effective permission endpoints.
- [x] Added `/v1/credentials` and `/v1/tenants/{tenantId}/credentials` routes and removed `/v1/apikeys` management routes.
- [x] Persisted SQLite credential tenant, user, access key, secret verifier, and secret last-four fields.
- [x] Added session-token authentication while preserving bearer access-key compatibility.
- [x] Added authorization service with system-admin, tenant-admin, regular-user, mapped-account, denial, and privileged-bypass audit behavior.
- [x] Added MySQL, PostgreSQL, and SQL Server v3 schema declarations for identity/security tables and tenant/metadata columns.
- [x] Wired MySQL, PostgreSQL, and SQL Server account, entry, and credential methods to a shared v3 SQL provider implementation for tenant, labels/tags, credit/debit filters, and credential scope persistence.
- [x] Wired MySQL, PostgreSQL, and SQL Server tenant, user, auth session, account-user map, audit, RBAC role/permission, user role assignment, and credential scope assignment methods to portable SQL implementations.
- [x] Added Docker compose services for MySQL, PostgreSQL, and SQL Server certification on non-default host ports.
- [x] Added Touchstone provider matrix tests for full v3 workflows across SQLite, MySQL, PostgreSQL, and SQL Server using `NETLEDGER_PROVIDER_MATRIX=1` for live SQL providers.
- [x] Certified SQLite, MySQL, PostgreSQL, and SQL Server provider workflows for tenants, users, sessions, accounts, entries, credentials, account-user maps, audit records, RBAC roles/permissions, user role assignments, and credential scope assignments.
- [x] Upgraded server dependency to Watson `7.0.15` and updated transitive `Timestamps` dependency.
- [x] Added `DatabaseDriverFactory`.
- [x] Added `SchemaMigration` model and `schemamigrations` table declarations for all providers.
- [x] Added RBAC models, SQLite RBAC storage, built-in role/permission seeding, role assignment persistence, and authorization evaluation with explicit deny precedence.
- [x] Added REST endpoints for roles, permissions, role-permission maps, and user role assignments.
- [x] Added `.dockerignore`.
- [x] Added labels/tags to account create, entry create, batch entry, and enumeration request paths.
- [x] Updated credential creation to return the raw secret key exactly once through a dedicated response shape.
- [x] Updated dashboard login to use email/password tenant discovery, tenant selection, session token storage, and access-key compatibility mode.
- [x] Added dashboard security console for tenants, users, roles, permissions, sessions, audit, and effective permissions.
- [x] Updated dashboard account and entry creation forms to set labels/tags.
- [x] Updated dashboard account and entry tables to display labels and string IDs.
- [x] Updated C#, JavaScript/TypeScript, and Python SDK models for string IDs and metadata fields.
- [x] Added tenant header support to C#, JavaScript/TypeScript, and Python SDK HTTP clients.
- [x] Added identity/security SDK methods for tenant discovery, login, tenants, users, roles, permissions, sessions, audit, and effective permissions.
- [x] Updated C#, JavaScript/TypeScript, and Python SDK credential management calls to use `/v1/credentials`.
- [x] Regenerated JavaScript SDK `dist/` output.
- [x] Added Touchstone project structure: `Test.Shared`, `Test.Automated`, `Test.Xunit`, and `Test.Nunit`.
- [x] Added shared Touchstone suites for ID, metadata validation, SQLite metadata round-trip, credential scope, tenant/user/session persistence, account-user mapping, mapped account enumeration, audit persistence, and RBAC built-in assignment persistence.
- [x] Removed legacy console/server/SDK harness projects from `NetLedger.sln` so solution test execution uses Touchstone-backed projects.
- [x] Updated README, CHANGELOG, REST_API, and Postman with v3 tenant/metadata examples.
- [x] Set core library, server, dashboard, C# SDK, JavaScript SDK, and Python SDK package versions to `3.0.0`.
- [x] Verified Postman collection JSON and raw JSON request bodies parse successfully.
- [x] Verified `dotnet build src\NetLedger.sln`.
- [x] Verified `npm.cmd run build` for `src\NetLedger.Dashboard`.
- [x] Verified `npm.cmd run build` for `sdk\sdk-js`.
- [x] Verified `python -m compileall sdk\sdk-python\netledger_sdk`.
- [x] Verified `dotnet test src\NetLedger.sln`.
- [x] Verified `dotnet run --project src\Test.Automated\Test.Automated.csproj --framework net8.0`.
- [x] Verified `NETLEDGER_PROVIDER_MATRIX=1` Touchstone run against SQLite plus Docker MySQL (`3307`), PostgreSQL (`5433`), and SQL Server (`14330`): 18/18 passing.
- [x] Verified `dotnet list src\NetLedger\NetLedger.csproj package --vulnerable --include-transitive` reports no vulnerable packages.

Human-intervention checklist:

- [ ] Dashboard and RBAC workflows are implemented to the point of buildable product surfaces; final role taxonomy, first-run seed policy, and operator UX acceptance require product/security review.
- [x] Touchstone console, xUnit, and NUnit runners build and execute the shared suite.
