namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger;
    using NetLedger.Archive;
    using NetLedger.Archive.Catalog.Sql;
    using NetLedger.Archive.Models;
    using NetLedger.Archive.Server.Services;
    using NetLedger.Archive.Settings;
    using NetLedger.Archive.Storage;
    using NetLedger.Database;
    using NetLedger.Server.Authentication;
    using NetLedger.Server.Models;
    using NetLedger.Server.Services;
    using NetLedger.Server.Settings;
    using NetLedger.Telemetry;
    using SyslogLogging;
    using Touchstone.Core;

    /// <summary>
    /// Shared Touchstone suites for NetLedger: telemetry emission.
    /// </summary>
    public static partial class NetLedgerSuites
    {
        private static TestSuiteDescriptor TelemetrySuite()
        {
            string suiteId = "telemetry";
            return new TestSuiteDescriptor(
                suiteId,
                "Telemetry emission (metrics, spans, propagation)",
                new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(suiteId, "telemetry_names_are_stable", "Meter, activity source, and every instrument name follow the documented contract", _ =>
                    {
                        Assert(NetLedgerTelemetry.Meter.Name == "NetLedger", "Meter name changed.");
                        Assert(NetLedgerTelemetry.ActivitySource.Name == "NetLedger", "Activity source name changed.");
                        Assert(TelemetryNames.WatsonSourceName == "Watson", "Watson source name changed.");

                        int instruments = 0;
                        foreach (FieldInfo field in typeof(TelemetryNames).GetFields(BindingFlags.Public | BindingFlags.Static))
                        {
                            string? value = field.GetValue(null) as string;
                            if (value == null || !value.StartsWith("netledger.", StringComparison.Ordinal)) continue;
                            if (value.StartsWith("netledger.tenant", StringComparison.Ordinal) ||
                                value.StartsWith("netledger.account", StringComparison.Ordinal) ||
                                value.StartsWith("netledger.entry", StringComparison.Ordinal) ||
                                value.StartsWith("netledger.archive.migration.id", StringComparison.Ordinal) ||
                                value.StartsWith("netledger.archive.batch", StringComparison.Ordinal) ||
                                value.StartsWith("netledger.archive.manifest", StringComparison.Ordinal) ||
                                value.StartsWith("netledger.principal", StringComparison.Ordinal) ||
                                value == TelemetryNames.AttributeRowCount ||
                                value == TelemetryNames.AttributeByteCount ||
                                value == TelemetryNames.AttributeAttempt)
                            {
                                continue;
                            }

                            instruments++;
                            Assert(value == value.ToLowerInvariant(), "Instrument name is not lower case: " + value);
                            Assert(!value.Contains(' '), "Instrument name contains a space: " + value);
                        }

                        Assert(instruments >= 40, "Expected the full instrument catalog in TelemetryNames, found " + instruments + ".");
                        return Task.CompletedTask;
                    }),
                    new TestCaseDescriptor(suiteId, "telemetry_no_listener_is_safe", "Telemetry helpers never throw and return an inert scope when nothing listens", _ =>
                    {
                        Histogram<double> unobserved = new Meter("NetLedger.Tests.Unobserved." + UniqueSuffix(8)).CreateHistogram<double>("unobserved");
                        using (TelemetryScope scope = TelemetryScope.Start("unobserved", ActivityKind.Internal, unobserved, null, default))
                        {
                            scope.SetTag("key", "value");
                            scope.AddLabel("label", "value");
                            scope.SetOutcome(TelemetryNames.OutcomeNoRows);
                            scope.Fail(new InvalidOperationException("ignored"));
                            scope.Fail("ignored", null);
                            Assert(scope.GetElapsedSeconds() >= 0, "Elapsed seconds was negative.");
                        }

                        NetLedgerTelemetry.RecordException(null, null);
                        NetLedgerTelemetry.RecordError(TelemetryNames.ComponentServer, (Exception?)null);
                        NetLedgerTelemetry.RecordStorageBytes(TelemetryNames.ServiceFilesystem, "write", -1);
                        NetLedgerTelemetry.InjectTraceContext(null, new Dictionary<string, string>(), (c, n, v) => throw new InvalidOperationException("setter must not run without a span"));
                        Assert(NetLedgerTelemetry.GetDbOperationName(null) == "OTHER", "Null query did not map to OTHER.");
                        Assert(NetLedgerTelemetry.GetDbOperationName("  select * from x") == "SELECT", "SELECT verb was not parsed.");
                        Assert(NetLedgerTelemetry.GetDbOperationName("VACUUM") == "OTHER", "Unknown verb was not bounded.");
                        Assert(NetLedgerTelemetry.BoundLabel("Account", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Account" }) == "account", "Allowed label was not normalized.");
                        Assert(NetLedgerTelemetry.BoundLabel("acct_123", new HashSet<string> { "Account" }) == "other", "Unbounded label was not collapsed to other.");
                        return Task.CompletedTask;
                    }),
                    new TestCaseDescriptor(suiteId, "telemetry_ledger_operations_emit_metrics_and_spans", "Ledger operations, entries, commits, verification, locks, and database round trips emit metrics and nested spans", async token =>
                    {
                        using (TelemetryCapture capture = new TelemetryCapture())
                        {
                            await using Ledger ledger = CreateLedger();
                            string dbSystem = NetLedgerTelemetry.GetDbSystemName(ledger.Driver.Settings.Type);
                            string tenantId = ScopedTenantId("telemetry");
                            string accountId = await ledger.CreateAccountAsync("telemetry-" + UniqueSuffix(8), 10m, new List<string>(), new Dictionary<string, string>(), tenantId, token).ConfigureAwait(false);
                            await ledger.AddCreditAsync(accountId, 5m, "credit", null, false, null, null, tenantId, token).ConfigureAwait(false);
                            await ledger.AddDebitAsync(accountId, 2m, "debit", null, false, null, null, tenantId, token).ConfigureAwait(false);
                            await ledger.CommitEntriesAsync(accountId, new List<string>(), true, token).ConfigureAwait(false);
                            bool valid = await ledger.VerifyBalanceChainAsync(accountId, token).ConfigureAwait(false);
                            Assert(valid, "Balance chain was not valid.");

                            Assert(capture.Count(TelemetryNames.LedgerOperations, TelemetryNames.LabelOperation, "AddCredit", TelemetryNames.LabelOutcome, TelemetryNames.OutcomeSuccess) >= 1, capture.Describe(TelemetryNames.LedgerOperations));
                            Assert(capture.Count(TelemetryNames.LedgerOperationDuration, TelemetryNames.LabelOperation, "CommitEntries", TelemetryNames.LabelOutcome, TelemetryNames.OutcomeSuccess) >= 1, capture.Describe(TelemetryNames.LedgerOperationDuration));
                            Assert(capture.Sum(TelemetryNames.LedgerEntriesCreated, TelemetryNames.LabelEntryType, "credit") >= 1, capture.Describe(TelemetryNames.LedgerEntriesCreated));
                            Assert(capture.Sum(TelemetryNames.LedgerEntriesCreated, TelemetryNames.LabelEntryType, "debit") >= 1, capture.Describe(TelemetryNames.LedgerEntriesCreated));
                            Assert(capture.Sum(TelemetryNames.LedgerEntriesCommitted) >= 2, capture.Describe(TelemetryNames.LedgerEntriesCommitted));
                            Assert(capture.Count(TelemetryNames.LedgerCommitSize) >= 1, capture.Describe(TelemetryNames.LedgerCommitSize));
                            Assert(capture.Sum(TelemetryNames.LedgerBalanceChainVerifications, TelemetryNames.LabelResult, "valid") >= 1, capture.Describe(TelemetryNames.LedgerBalanceChainVerifications));
                            Assert(capture.Count(TelemetryNames.LedgerLockWaitDuration, TelemetryNames.LabelLock, "process") >= 1, capture.Describe(TelemetryNames.LedgerLockWaitDuration));
                            Assert(capture.Count(TelemetryNames.LedgerLockWaitDuration, TelemetryNames.LabelLock, "database", TelemetryNames.LabelOutcome, TelemetryNames.OutcomeSuccess) >= 1, capture.Describe(TelemetryNames.LedgerLockWaitDuration));
                            Assert(capture.Count(TelemetryNames.DbOperations, TelemetryNames.LabelComponent, TelemetryNames.ComponentActiveDatabase, TelemetryNames.LabelDbSystem, dbSystem, TelemetryNames.LabelDbOperation, "INSERT", TelemetryNames.LabelOutcome, TelemetryNames.OutcomeSuccess) >= 1, capture.Describe(TelemetryNames.DbOperations));
                            Assert(capture.Count(TelemetryNames.DbOperationDuration, TelemetryNames.LabelDbOperation, "SELECT") >= 1, capture.Describe(TelemetryNames.DbOperationDuration));
                            Assert(capture.Sum(TelemetryNames.DbOperationsActive, TelemetryNames.LabelComponent, TelemetryNames.ComponentActiveDatabase) == 0, "In-flight database counter was not balanced after all operations completed.");

                            capture.CollectObservables();
                            Assert(capture.Count(TelemetryNames.LedgerLocksActive, TelemetryNames.LabelLock, "process") >= 1, capture.Describe(TelemetryNames.LedgerLocksActive));

                            Activity? creditSpan = capture.Spans("ledger AddCredit").FirstOrDefault(a => Equals(a.GetTagItem(TelemetryNames.AttributeAccountId), accountId));
                            Assert(creditSpan != null, "Missing ledger AddCredit span.");
                            Assert(creditSpan!.Status == ActivityStatusCode.Ok, "AddCredit span status was not Ok.");
                            Assert(creditSpan.GetTagItem(TelemetryNames.AttributeEntryId) != null, "AddCredit span did not carry the entry id.");
                            bool hasDbChild = capture.Activities.Any(a => a.Kind == ActivityKind.Client && a.TraceId == creditSpan.TraceId && a.DisplayName.StartsWith(dbSystem + " ", StringComparison.Ordinal));
                            Assert(hasDbChild, "AddCredit span had no nested database client span.");
                            bool dbSpanHasQuery = capture.Activities.Any(a => a.Kind == ActivityKind.Client && a.Tags.Any(t => t.Value != null && t.Value.Contains("INSERT INTO", StringComparison.OrdinalIgnoreCase)));
                            Assert(!dbSpanHasQuery, "Database span leaked SQL text.");
                            Assert(capture.Spans(TelemetryNames.SpanLockWait).Any(a => a.TraceId == creditSpan.TraceId), "Lock wait span was not nested under the ledger operation.");
                        }
                    }),
                    new TestCaseDescriptor(suiteId, "telemetry_ledger_failure_records_error", "A failing ledger operation records outcome failure, error.type, and an Error span with an exception event", async token =>
                    {
                        using (TelemetryCapture capture = new TelemetryCapture())
                        {
                            await using Ledger ledger = CreateLedger();
                            string missingAccountId = NetLedgerId.Generate(IdentifierPrefixes.Account);
                            bool threw = false;
                            try
                            {
                                await ledger.AddCreditAsync(missingAccountId, 1m, "missing", null, false, null, null, null, token).ConfigureAwait(false);
                            }
                            catch (KeyNotFoundException)
                            {
                                threw = true;
                            }

                            Assert(threw, "AddCredit on a missing account did not throw.");
                            Assert(capture.Count(TelemetryNames.LedgerOperations, TelemetryNames.LabelOperation, "AddCredit", TelemetryNames.LabelOutcome, TelemetryNames.OutcomeFailure, TelemetryNames.LabelErrorType, "KeyNotFoundException") >= 1, capture.Describe(TelemetryNames.LedgerOperations));
                            Activity? span = capture.Spans("ledger AddCredit").FirstOrDefault(a => Equals(a.GetTagItem(TelemetryNames.AttributeAccountId), missingAccountId));
                            Assert(span != null, "Missing failed AddCredit span.");
                            Assert(span!.Status == ActivityStatusCode.Error, "Failed span status was not Error.");
                            Assert(span.Events.Any(e => e.Name == "exception"), "Failed span had no exception event.");
                        }
                    }),
                    new TestCaseDescriptor(suiteId, "telemetry_archive_export_pipeline_and_propagation", "Automatic archival emits job, per-stage, integration, worker, and gauge telemetry and propagates W3C trace context to the archive server", async token =>
                    {
                        using (TelemetryCapture capture = new TelemetryCapture())
                        {
                            await using Ledger ledger = CreateLedger();
                            using (TestArchiveServer archiveServer = new TestArchiveServer())
                            {
                                string tenantId = ScopedTenantId("telexport");
                                string accountId = await ledger.CreateAccountAsync("telemetry-export-" + UniqueSuffix(8), 0m, null, null, tenantId, token).ConfigureAwait(false);
                                await CreateOldCommittedCreditAsync(ledger, tenantId, accountId, DateTime.UtcNow.AddDays(-10), token).ConfigureAwait(false);
                                await UpsertAutomaticAccountOverrideAsync(ledger, tenantId, accountId, true, token).ConfigureAwait(false);

                                ServerSettings settings = CreateAutomaticArchiveServerSettings(archiveServer.Endpoint, false);
                                using (ArchiveExportService exportService = new ArchiveExportService(settings, ledger, new LoggingModule()))
                                using (AutomaticArchiveService worker = new AutomaticArchiveService(settings, ledger, exportService, new LoggingModule()))
                                {
                                    AutomaticArchiveRunResult result = await worker.RunOnceAsync(token).ConfigureAwait(false);
                                    Assert(result.EntryExportsSucceeded >= 1, "Automatic archive export did not succeed.");
                                }

                                string[] job = { TelemetryNames.LabelEntity, "entries", TelemetryNames.LabelTrigger, "automatic", TelemetryNames.LabelOutcome, TelemetryNames.OutcomeSuccess };
                                Assert(capture.Count(TelemetryNames.ArchiveExportJobs, job) >= 1, capture.Describe(TelemetryNames.ArchiveExportJobs));
                                Assert(capture.Count(TelemetryNames.ArchiveExportDuration, job) >= 1, capture.Describe(TelemetryNames.ArchiveExportDuration));
                                foreach (string stage in new[] { TelemetryNames.StageValidate, TelemetryNames.StageEnumerate, TelemetryNames.StageCreateMigration, TelemetryNames.StageUploadBatch, TelemetryNames.StageSeal, TelemetryNames.StageCommit })
                                {
                                    Assert(capture.Count(TelemetryNames.ArchiveExportStageEvents, TelemetryNames.LabelStage, stage, TelemetryNames.LabelOutcome, TelemetryNames.OutcomeSuccess) >= 1, "Missing stage " + stage + ". " + capture.Describe(TelemetryNames.ArchiveExportStageEvents));
                                    Assert(capture.Count(TelemetryNames.ArchiveExportStageDuration, TelemetryNames.LabelStage, stage) >= 1, "Missing stage duration " + stage + ".");
                                    Assert(capture.Spans(TelemetryNames.SpanStagePrefix + stage).Count >= 1, "Missing span for stage " + stage + ".");
                                }

                                foreach (string operation in new[] { "create_migration", "create_batch", "upload_batch_content", "seal_migration", "commit_migration" })
                                {
                                    Assert(capture.Count(TelemetryNames.IntegrationRequests, TelemetryNames.LabelService, TelemetryNames.ServiceArchiveServer, TelemetryNames.LabelOperation, operation, TelemetryNames.LabelOutcome, TelemetryNames.OutcomeSuccess) >= 1, "Missing integration " + operation + ". " + capture.Describe(TelemetryNames.IntegrationRequests));
                                    Assert(capture.Count(TelemetryNames.IntegrationRequestDuration, TelemetryNames.LabelOperation, operation) >= 1, "Missing integration duration " + operation + ".");
                                }

                                Assert(capture.Sum(TelemetryNames.ArchiveExportRows, TelemetryNames.LabelEntity, "entries") >= 1, capture.Describe(TelemetryNames.ArchiveExportRows));
                                Assert(capture.Sum(TelemetryNames.ArchiveExportBytes, TelemetryNames.LabelEntity, "entries") > 0, capture.Describe(TelemetryNames.ArchiveExportBytes));
                                Assert(capture.Count(TelemetryNames.AutomaticArchiveRuns, TelemetryNames.LabelOutcome, TelemetryNames.OutcomeSuccess) >= 1, capture.Describe(TelemetryNames.AutomaticArchiveRuns));
                                Assert(capture.Count(TelemetryNames.AutomaticArchiveRunDuration) >= 1, capture.Describe(TelemetryNames.AutomaticArchiveRunDuration));
                                Assert(capture.Sum(TelemetryNames.AutomaticArchiveAccounts, TelemetryNames.LabelResult, "exported") >= 1, capture.Describe(TelemetryNames.AutomaticArchiveAccounts));

                                capture.CollectObservables();
                                Assert(capture.Measurements(TelemetryNames.ArchiveExportLastSuccess, TelemetryNames.LabelEntity, "entries").Any(m => m.Value > 0), capture.Describe(TelemetryNames.ArchiveExportLastSuccess));
                                Assert(capture.Measurements(TelemetryNames.AutomaticArchiveLastSuccess).Any(m => m.Value > 0), capture.Describe(TelemetryNames.AutomaticArchiveLastSuccess));
                                Assert(capture.Measurements(TelemetryNames.AutomaticArchiveLastRun).Any(m => m.Value > 0), capture.Describe(TelemetryNames.AutomaticArchiveLastRun));
                                Assert(capture.Count(TelemetryNames.AutomaticArchiveRunning) >= 1, capture.Describe(TelemetryNames.AutomaticArchiveRunning));

                                Activity? run = capture.Spans(TelemetryNames.SpanAutomaticArchiveRun).LastOrDefault();
                                Assert(run != null, "Missing automatic archival run span.");
                                Activity? accountSpan = capture.Spans(TelemetryNames.SpanAutomaticArchiveAccount).FirstOrDefault(a => Equals(a.GetTagItem(TelemetryNames.AttributeAccountId), accountId));
                                Assert(accountSpan != null && accountSpan.TraceId == run!.TraceId, "Account span was not part of the run trace.");
                                Activity? exportSpan = capture.Spans(TelemetryNames.SpanArchiveExportPrefix + "entries").FirstOrDefault(a => a.TraceId == run!.TraceId);
                                Assert(exportSpan != null, "Export job span was not part of the run trace.");
                                Activity? upload = capture.Spans(TelemetryNames.ServiceArchiveServer + " upload_batch_content").FirstOrDefault(a => a.TraceId == run!.TraceId);
                                Assert(upload != null && upload.Kind == ActivityKind.Client, "Upload client span missing from the run trace.");

                                string? traceParent = archiveServer.LastTraceParent;
                                Assert(!String.IsNullOrEmpty(traceParent), "Archive server did not receive a traceparent header.");
                                Assert(traceParent!.Contains(run!.TraceId.ToHexString(), StringComparison.Ordinal), "Propagated traceparent did not carry the run trace id: " + traceParent);
                            }
                        }
                    }),
                    new TestCaseDescriptor(suiteId, "telemetry_archive_export_failure_paths", "Archive server failures record failed integrations, failed stages and jobs, retries, failed accounts, and errors", async token =>
                    {
                        using (TelemetryCapture capture = new TelemetryCapture())
                        {
                            await using Ledger ledger = CreateLedger();
                            using (TestArchiveServer archiveServer = new TestArchiveServer(10))
                            {
                                string tenantId = ScopedTenantId("telfail");
                                string accountId = await ledger.CreateAccountAsync("telemetry-fail-" + UniqueSuffix(8), 0m, null, null, tenantId, token).ConfigureAwait(false);
                                await CreateOldCommittedCreditAsync(ledger, tenantId, accountId, DateTime.UtcNow.AddDays(-10), token).ConfigureAwait(false);
                                AccountArchivalSettings overrides = await UpsertAutomaticAccountOverrideAsync(ledger, tenantId, accountId, true, token).ConfigureAwait(false);
                                overrides.RetryMaxAttempts = 2;
                                await ledger.Driver.AccountArchivalSettings.UpsertAsync(overrides, token).ConfigureAwait(false);

                                ServerSettings settings = CreateAutomaticArchiveServerSettings(archiveServer.Endpoint, false);
                                using (ArchiveExportService exportService = new ArchiveExportService(settings, ledger, new LoggingModule()))
                                using (AutomaticArchiveService worker = new AutomaticArchiveService(settings, ledger, exportService, new LoggingModule()))
                                {
                                    AutomaticArchiveRunResult result = await worker.RunOnceAsync(token).ConfigureAwait(false);
                                    Assert(result.EntryExportsFailed >= 1, "Injected archive server failure did not fail the export.");
                                }

                                Assert(capture.Count(TelemetryNames.IntegrationRequests, TelemetryNames.LabelService, TelemetryNames.ServiceArchiveServer, TelemetryNames.LabelOperation, "create_migration", TelemetryNames.LabelOutcome, TelemetryNames.OutcomeFailure, TelemetryNames.LabelErrorType, "http_503") >= 2, capture.Describe(TelemetryNames.IntegrationRequests));
                                Assert(capture.Count(TelemetryNames.ArchiveExportStageEvents, TelemetryNames.LabelStage, TelemetryNames.StageCreateMigration, TelemetryNames.LabelOutcome, TelemetryNames.OutcomeFailure) >= 1, capture.Describe(TelemetryNames.ArchiveExportStageEvents));
                                Assert(capture.Count(TelemetryNames.ArchiveExportJobs, TelemetryNames.LabelEntity, "entries", TelemetryNames.LabelOutcome, TelemetryNames.OutcomeFailure, TelemetryNames.LabelErrorType, "InvalidOperationException") >= 1, capture.Describe(TelemetryNames.ArchiveExportJobs));
                                Assert(capture.Sum(TelemetryNames.AutomaticArchiveRetries) >= 1, capture.Describe(TelemetryNames.AutomaticArchiveRetries));
                                Assert(capture.Sum(TelemetryNames.AutomaticArchiveAccounts, TelemetryNames.LabelResult, "failed") >= 1, capture.Describe(TelemetryNames.AutomaticArchiveAccounts));
                                Assert(capture.Count(TelemetryNames.AutomaticArchiveRuns, TelemetryNames.LabelOutcome, TelemetryNames.OutcomeFailure) + capture.Count(TelemetryNames.AutomaticArchiveRuns, TelemetryNames.LabelOutcome, TelemetryNames.OutcomePartial) >= 1, capture.Describe(TelemetryNames.AutomaticArchiveRuns));
                                Assert(capture.Sum(TelemetryNames.Errors, TelemetryNames.LabelComponent, TelemetryNames.ComponentArchiveExport) >= 1, capture.Describe(TelemetryNames.Errors));
                                Assert(capture.Sum(TelemetryNames.Errors, TelemetryNames.LabelComponent, TelemetryNames.ComponentAutomaticArchive) >= 1, capture.Describe(TelemetryNames.Errors));

                                Activity? failedCall = capture.Spans(TelemetryNames.ServiceArchiveServer + " create_migration").FirstOrDefault(a => a.Status == ActivityStatusCode.Error);
                                Assert(failedCall != null, "Failed integration span did not have Error status.");
                                Assert(Equals(failedCall!.GetTagItem(TelemetryNames.AttributeHttpStatusCode), 503), "Failed integration span did not carry the HTTP status.");
                                Activity? failedJob = capture.Spans(TelemetryNames.SpanArchiveExportPrefix + "entries").FirstOrDefault(a => a.Status == ActivityStatusCode.Error);
                                Assert(failedJob != null && failedJob.Events.Any(e => e.Name == "exception"), "Failed export job span had no exception event.");
                            }
                        }
                    }),
                    new TestCaseDescriptor(suiteId, "telemetry_auth_and_authz_decisions", "Authorization permit and deny decisions and rejected logins are counted with bounded labels", async token =>
                    {
                        using (TelemetryCapture capture = new TelemetryCapture())
                        {
                            await using Ledger ledger = CreateLedger();
                            LoggingModule logging = new LoggingModule();
                            logging.Settings.EnableConsole = false;
                            AuthorizationService authorization = new AuthorizationService(ledger.Driver, logging);
                            string tenantId = ScopedTenantId("telauthz");

                            AuthorizationDecision denied = await authorization.AuthorizeAsync(CreateUnauthenticatedRequest(tenantId), "Account", "Read", NetLedgerId.Generate(IdentifierPrefixes.Account), token).ConfigureAwait(false);
                            Assert(!denied.Permitted, "Unauthenticated request was permitted.");
                            RequestContext notRequired = new RequestContext { TenantId = tenantId, Auth = AuthContext.NotRequired() };
                            AuthorizationDecision permitted = await authorization.AuthorizeAsync(notRequired, "Account", "Read", null, token).ConfigureAwait(false);
                            Assert(permitted.Permitted, "Authentication-not-required request was denied.");
                            await authorization.AuthorizeAsync(notRequired, "acct_unbounded_resource_" + UniqueSuffix(6), "Read", null, token).ConfigureAwait(false);

                            Assert(capture.Sum(TelemetryNames.AuthzDecisions, TelemetryNames.LabelComponent, TelemetryNames.ComponentServer, TelemetryNames.LabelResource, "account", TelemetryNames.LabelOperation, "read", TelemetryNames.LabelDecision, "deny") >= 1, capture.Describe(TelemetryNames.AuthzDecisions));
                            Assert(capture.Sum(TelemetryNames.AuthzDecisions, TelemetryNames.LabelResource, "account", TelemetryNames.LabelDecision, "permit") >= 1, capture.Describe(TelemetryNames.AuthzDecisions));
                            Assert(capture.Sum(TelemetryNames.AuthzDecisions, TelemetryNames.LabelResource, "other") >= 1, "Unbounded resource type was not collapsed to other.");
                            Assert(capture.Measurements(TelemetryNames.AuthzDecisions).All(m => !m.Tags.Values.Any(v => v.StartsWith("acct_", StringComparison.Ordinal))), "Authorization metrics leaked an identifier.");
                            Assert(capture.Count(TelemetryNames.AuthzDuration, TelemetryNames.LabelDecision, "deny") >= 1, capture.Describe(TelemetryNames.AuthzDuration));
                            Assert(capture.Spans(TelemetryNames.SpanAuthorize).Count >= 2, "Missing authorization spans.");

                            using (AuthService authService = new AuthService(new ServerSettings(), logging, ledger.Driver))
                            {
                                bool rejected = false;
                                try
                                {
                                    await authService.LoginAsync(tenantId, "nobody@example.com", "wrong-password", token).ConfigureAwait(false);
                                }
                                catch (UnauthorizedAccessException)
                                {
                                    rejected = true;
                                }

                                Assert(rejected, "Login with an unknown tenant was not rejected.");
                            }

                            Assert(capture.Sum(TelemetryNames.AuthLogins, TelemetryNames.LabelOutcome, TelemetryNames.OutcomeRejected) >= 1, capture.Describe(TelemetryNames.AuthLogins));
                            Assert(capture.Spans(TelemetryNames.SpanLogin).Any(a => a.Tags.All(t => t.Value == null || !t.Value.Contains("wrong-password", StringComparison.Ordinal))), "Login span leaked the password.");
                        }
                    }),
                    new TestCaseDescriptor(suiteId, "telemetry_request_history_background_write", "Background request history writes join the request trace and record success and failure", async token =>
                    {
                        using (TelemetryCapture capture = new TelemetryCapture())
                        {
                            await using Ledger ledger = CreateLedger();
                            RequestHistoryService service = new RequestHistoryService(ledger.Driver, new RequestHistorySettings(), new LoggingModule());
                            RequestHistoryEntry entry = new RequestHistoryEntry
                            {
                                TenantId = ScopedTenantId("telhistory"),
                                Method = "GET",
                                Path = "/v1/accounts",
                                Url = "/v1/accounts",
                                StatusCode = 200,
                                CreatedUtc = DateTime.UtcNow,
                                CompletedUtc = DateTime.UtcNow
                            };

                            ActivityTraceId requestTrace;
                            using (Activity request = new Activity("test-request"))
                            {
                                request.SetIdFormat(ActivityIdFormat.W3C);
                                request.Start();
                                requestTrace = request.TraceId;
                                await service.CaptureEntryAsync(entry).ConfigureAwait(false);
                                await service.CaptureEntryAsync(entry).ConfigureAwait(false);
                            }

                            Assert(capture.Sum(TelemetryNames.RequestHistoryWrites, TelemetryNames.LabelOutcome, TelemetryNames.OutcomeSuccess) >= 1, capture.Describe(TelemetryNames.RequestHistoryWrites));
                            Assert(capture.Sum(TelemetryNames.RequestHistoryWrites, TelemetryNames.LabelOutcome, TelemetryNames.OutcomeFailure) >= 1, "Duplicate request history write did not record a failure. " + capture.Describe(TelemetryNames.RequestHistoryWrites));
                            Assert(capture.Count(TelemetryNames.RequestHistoryWriteDuration) >= 2, capture.Describe(TelemetryNames.RequestHistoryWriteDuration));
                            Assert(capture.Sum(TelemetryNames.Errors, TelemetryNames.LabelComponent, TelemetryNames.ComponentRequestHistory) >= 1, capture.Describe(TelemetryNames.Errors));
                            Assert(capture.Spans(TelemetryNames.SpanRequestHistoryWrite).Count(a => a.TraceId == requestTrace) >= 2, "Background writes did not join the request trace.");
                            capture.CollectObservables();
                            Assert(capture.Count(TelemetryNames.RequestHistoryPending) >= 1, capture.Describe(TelemetryNames.RequestHistoryPending));
                        }
                    }),
                    new TestCaseDescriptor(suiteId, "telemetry_object_store_and_catalog", "Archive object storage calls and archive catalog SQL emit integration and database telemetry, including failures", async token =>
                    {
                        using (TelemetryCapture capture = new TelemetryCapture())
                        {
                            string directory = Path.Combine(Path.GetTempPath(), "netledger-telemetry-store-" + UniqueSuffix(16));
                            try
                            {
                                IArchiveObjectStore store = ArchiveObjectStoreFactory.Create(new ArchiveStoragePoolSettings
                                {
                                    Id = "asp_telemetry",
                                    Name = "telemetry",
                                    Type = ArchiveStoragePoolType.FileSystem,
                                    BasePath = directory
                                });
                                Assert(store is InstrumentedArchiveObjectStore, "Factory did not return an instrumented store.");

                                byte[] bytes = Encoding.UTF8.GetBytes("telemetry payload");
                                using (MemoryStream write = new MemoryStream(bytes))
                                {
                                    await store.WriteTemporaryAsync("_tmp/t.jsonl.gz", write, token).ConfigureAwait(false);
                                }

                                await store.CommitAsync("_tmp/t.jsonl.gz", "committed/t.jsonl.gz", token).ConfigureAwait(false);
                                using (Stream read = await store.ReadAsync("committed/t.jsonl.gz", token).ConfigureAwait(false))
                                {
                                }

                                ArchiveObjectMetadata metadata = await store.ReadMetadataAsync("committed/missing.jsonl.gz", token).ConfigureAwait(false);
                                bool rejected = false;
                                try
                                {
                                    using (MemoryStream traversal = new MemoryStream(bytes))
                                    {
                                        await store.WriteTemporaryAsync("../escape.json", traversal, token).ConfigureAwait(false);
                                    }
                                }
                                catch (InvalidOperationException)
                                {
                                    rejected = true;
                                }

                                Assert(rejected, "Traversal write was not rejected.");
                                Assert(capture.Count(TelemetryNames.IntegrationRequests, TelemetryNames.LabelService, TelemetryNames.ServiceFilesystem, TelemetryNames.LabelOperation, "write_temporary", TelemetryNames.LabelOutcome, TelemetryNames.OutcomeSuccess) >= 1, capture.Describe(TelemetryNames.IntegrationRequests));
                                Assert(capture.Count(TelemetryNames.IntegrationRequests, TelemetryNames.LabelService, TelemetryNames.ServiceFilesystem, TelemetryNames.LabelOperation, "write_temporary", TelemetryNames.LabelOutcome, TelemetryNames.OutcomeFailure, TelemetryNames.LabelErrorType, "InvalidOperationException") >= 1, capture.Describe(TelemetryNames.IntegrationRequests));
                                Assert(capture.Count(TelemetryNames.IntegrationRequests, TelemetryNames.LabelService, TelemetryNames.ServiceFilesystem, TelemetryNames.LabelOperation, "commit") >= 1, capture.Describe(TelemetryNames.IntegrationRequests));
                                Assert(capture.Count(TelemetryNames.IntegrationRequests, TelemetryNames.LabelService, TelemetryNames.ServiceFilesystem, TelemetryNames.LabelOperation, "read") >= 1, capture.Describe(TelemetryNames.IntegrationRequests));
                                Assert(capture.Count(TelemetryNames.IntegrationRequests, TelemetryNames.LabelOperation, "read_metadata", TelemetryNames.LabelOutcome, TelemetryNames.OutcomeNotFound) >= 1 || metadata.Exists, capture.Describe(TelemetryNames.IntegrationRequests));
                                Assert(capture.Sum(TelemetryNames.StorageBytes, TelemetryNames.LabelService, TelemetryNames.ServiceFilesystem, TelemetryNames.LabelDirection, "write") >= bytes.Length, capture.Describe(TelemetryNames.StorageBytes));
                                Assert(capture.Sum(TelemetryNames.Errors, TelemetryNames.LabelComponent, TelemetryNames.ComponentArchiveStorage) >= 1, capture.Describe(TelemetryNames.Errors));
                                Assert(capture.Spans(TelemetryNames.ServiceFilesystem + " commit").Any(a => a.Kind == ActivityKind.Client), "Missing filesystem commit client span.");
                                (store as IDisposable)?.Dispose();
                            }
                            finally
                            {
                                if (Directory.Exists(directory))
                                {
                                    foreach (string file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                                    {
                                        File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
                                    }

                                    Directory.Delete(directory, true);
                                }
                            }

                            await using (ArchiveSqlCatalog catalog = new ArchiveSqlCatalog(CreateArchiveCatalogSettings()))
                            {
                                await catalog.InitializeAsync(token).ConfigureAwait(false);
                                await catalog.StoragePools.ReadByIdAsync("asp_missing_" + UniqueSuffix(8), token).ConfigureAwait(false);
                            }

                            Assert(capture.Count(TelemetryNames.DbOperations, TelemetryNames.LabelComponent, TelemetryNames.ComponentArchiveCatalog, TelemetryNames.LabelDbOperation, "SELECT", TelemetryNames.LabelOutcome, TelemetryNames.OutcomeSuccess) >= 1, capture.Describe(TelemetryNames.DbOperations));
                        }
                    }),
                    new TestCaseDescriptor(suiteId, "telemetry_archive_server_recorders", "Archive server migration, workflow-stage, verification, cache, and authorization recorders emit bounded metrics", async _ =>
                    {
                        using (TelemetryCapture capture = new TelemetryCapture())
                        {
                            ArchiveServerTelemetry.RecordMigrationEvent(ArchiveServerTelemetry.EventCommitted, ArchiveServerTelemetry.EntityLabel(NetLedger.Archive.ArchiveEntityType.Entries));
                            ArchiveServerTelemetry.RecordUploadBytes(128);
                            ArchiveServerTelemetry.RecordQueryRows("entries", 3);
                            ArchiveServerTelemetry.RecordVerification(false);
                            ArchiveServerTelemetry.RecordCacheLookup(true);
                            ArchiveServerTelemetry.RecordCacheLookup(false);
                            ArchiveServerTelemetry.RecordAuthorizationDecision("ArchiveMigration", "Update", false);
                            ArchiveServerTelemetry.SetCacheSizeProvider(() => 7);
                            using (TelemetryScope stage = ArchiveServerTelemetry.StartWorkflowStage(TelemetryNames.WorkflowCommit, TelemetryNames.StageCreateManifest))
                            {
                                stage.Fail(new InvalidDataException("bad manifest"));
                            }

                            using (TelemetryScope stage = ArchiveServerTelemetry.StartWorkflowStage(TelemetryNames.WorkflowUpload, TelemetryNames.StageReceive))
                            {
                                await Task.Yield();
                            }

                            capture.CollectObservables();
                            ArchiveServerTelemetry.SetCacheSizeProvider(null);

                            Assert(capture.Sum(TelemetryNames.ArchiveMigrationEvents, TelemetryNames.LabelEvent, "committed", TelemetryNames.LabelEntity, "entries") >= 1, capture.Describe(TelemetryNames.ArchiveMigrationEvents));
                            Assert(capture.Sum(TelemetryNames.ArchiveUploadBytes) >= 128, capture.Describe(TelemetryNames.ArchiveUploadBytes));
                            Assert(capture.Sum(TelemetryNames.ArchiveQueryRows, TelemetryNames.LabelEntity, "entries") >= 3, capture.Describe(TelemetryNames.ArchiveQueryRows));
                            Assert(capture.Sum(TelemetryNames.ArchiveVerifications, TelemetryNames.LabelResult, "invalid") >= 1, capture.Describe(TelemetryNames.ArchiveVerifications));
                            Assert(capture.Sum(TelemetryNames.IntrospectionCacheLookups, TelemetryNames.LabelResult, "hit") >= 1, capture.Describe(TelemetryNames.IntrospectionCacheLookups));
                            Assert(capture.Sum(TelemetryNames.IntrospectionCacheLookups, TelemetryNames.LabelResult, "miss") >= 1, capture.Describe(TelemetryNames.IntrospectionCacheLookups));
                            Assert(capture.Measurements(TelemetryNames.IntrospectionCacheSize).Any(m => m.Value == 7), capture.Describe(TelemetryNames.IntrospectionCacheSize));
                            Assert(capture.Sum(TelemetryNames.AuthzDecisions, TelemetryNames.LabelComponent, TelemetryNames.ComponentArchiveServer, TelemetryNames.LabelResource, "archivemigration", TelemetryNames.LabelDecision, "deny") >= 1, capture.Describe(TelemetryNames.AuthzDecisions));
                            Assert(capture.Count(TelemetryNames.ArchiveWorkflowStageDuration, TelemetryNames.LabelWorkflow, "commit", TelemetryNames.LabelStage, "create_manifest", TelemetryNames.LabelOutcome, TelemetryNames.OutcomeFailure) >= 1, capture.Describe(TelemetryNames.ArchiveWorkflowStageDuration));
                            Assert(capture.Count(TelemetryNames.ArchiveWorkflowStageDuration, TelemetryNames.LabelWorkflow, "upload", TelemetryNames.LabelStage, "receive", TelemetryNames.LabelOutcome, TelemetryNames.OutcomeSuccess) >= 1, capture.Describe(TelemetryNames.ArchiveWorkflowStageDuration));
                            Assert(capture.Spans(TelemetryNames.SpanStagePrefix + "create_manifest").Any(a => a.Status == ActivityStatusCode.Error), "Failed workflow stage span was not Error.");
                        }
                    }),
                    new TestCaseDescriptor(suiteId, "telemetry_build_info_config_and_propagation", "Service registration emits build info, uptime, and safe config gauges; trace context injects a W3C traceparent", _ =>
                    {
                        using (TelemetryCapture capture = new TelemetryCapture())
                        {
                            string component = "test_component_" + UniqueSuffix(6).ToLowerInvariant();
                            NetLedgerTelemetry.RegisterService(component, "9.9.9", () => new Dictionary<string, double> { ["archive.enabled"] = 1, ["broken"] = 2 });
                            NetLedgerTelemetry.RegisterService(component + "_throws", "1.0.0", () => throw new InvalidOperationException("config provider failure"));
                            try
                            {
                                capture.CollectObservables();
                                Assert(capture.Measurements(TelemetryNames.BuildInfo, TelemetryNames.LabelComponent, component, TelemetryNames.LabelVersion, "9.9.9").Any(m => m.Value == 1), capture.Describe(TelemetryNames.BuildInfo));
                                Assert(capture.Measurements(TelemetryNames.Uptime, TelemetryNames.LabelComponent, component).Any(m => m.Value >= 0), capture.Describe(TelemetryNames.Uptime));
                                Assert(capture.Measurements(TelemetryNames.ConfigValue, TelemetryNames.LabelComponent, component, TelemetryNames.LabelSetting, "archive.enabled").Any(m => m.Value == 1), capture.Describe(TelemetryNames.ConfigValue));
                            }
                            finally
                            {
                                NetLedgerTelemetry.UnregisterService(component);
                                NetLedgerTelemetry.UnregisterService(component + "_throws");
                            }

                            Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            using (TelemetryScope scope = NetLedgerTelemetry.StartIntegration(TelemetryNames.ServiceArchiveServer, "test"))
                            {
                                Assert(scope.Activity != null, "Integration span was not created while listening.");
                                NetLedgerTelemetry.InjectTraceContext(scope.Activity, headers, (carrier, name, value) => ((Dictionary<string, string>)carrier)[name] = value);
                                Assert(headers.TryGetValue("traceparent", out string? traceParent) && traceParent.Contains(scope.Activity!.TraceId.ToHexString(), StringComparison.Ordinal), "traceparent was not injected.");
                            }
                        }

                        return Task.CompletedTask;
                    })
                });
        }
    }
}
