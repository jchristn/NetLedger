# ArchivalValidation

`ArchivalValidation` is a live smoke-test console application for NetLedger archival behavior. It starts disposable NetLedger Server and NetLedger Archive Server processes on random localhost ports, writes temporary SQLite databases and filesystem archive objects, and validates hot/cold retrieval behavior through the public SDK.

Run from the repository root:

```bash
dotnet run --project src/ArchivalValidation/ArchivalValidation.csproj --framework net8.0
```

The app validates active-server export to archive, Archive Server metadata, cold entry search and filters, active retention boundary rejection for cold data, Archive Server rejection for uncovered hot and partial ranges, archived balance checkpoint absence for entry-only exports, and the raw Archive Server migration lifecycle exposed through the SDK.
