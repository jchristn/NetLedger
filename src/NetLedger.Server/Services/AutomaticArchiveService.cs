namespace NetLedger.Server.Services
{
    using System;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Server.Authentication;
    using NetLedger.Server.Models;
    using NetLedger.Server.Settings;
    using SyslogLogging;

    internal sealed class AutomaticArchiveService : IDisposable
    {
        private readonly string _Header = "[AutomaticArchiveService] ";
        private readonly ServerSettings _Settings;
        private readonly Ledger _Ledger;
        private readonly ArchiveExportService _ArchiveExportService;
        private readonly LoggingModule _Logging;
        private readonly SemaphoreSlim _RunLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource? _TokenSource = null;
        private Task? _WorkerTask = null;
        private bool _Disposed = false;

        internal AutomaticArchiveService(
            ServerSettings settings,
            Ledger ledger,
            ArchiveExportService archiveExportService,
            LoggingModule logging)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            _ArchiveExportService = archiveExportService ?? throw new ArgumentNullException(nameof(archiveExportService));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        internal void Start()
        {
            if (!ArchiveIntegrationEnabled())
            {
                _Logging.Debug(_Header + "not started because archive integration is disabled.");
                return;
            }

            if (_WorkerTask != null)
            {
                return;
            }

            _TokenSource = new CancellationTokenSource();
            _WorkerTask = Task.Run(() => RunLoopAsync(_TokenSource.Token));
            _Logging.Info(_Header + "started.");
        }

        internal async Task StopAsync()
        {
            CancellationTokenSource? tokenSource = _TokenSource;
            Task? workerTask = _WorkerTask;
            if (tokenSource == null || workerTask == null)
            {
                return;
            }

            tokenSource.Cancel();
            try
            {
                await workerTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }

            tokenSource.Dispose();
            _TokenSource = null;
            _WorkerTask = null;
            _Logging.Debug(_Header + "stopped.");
        }

        internal async Task<AutomaticArchiveRunResult> RunOnceAsync(CancellationToken token = default)
        {
            AutomaticArchiveRunResult result = new AutomaticArchiveRunResult
            {
                ArchiveEnabled = ArchiveIntegrationEnabled(),
                AutomaticEnabled = GlobalAutomaticEnabled()
            };

            if (!ArchiveIntegrationEnabled())
            {
                result.CompletedUtc = DateTime.UtcNow;
                return result;
            }

            bool lockTaken = await _RunLock.WaitAsync(0, token).ConfigureAwait(false);
            if (!lockTaken)
            {
                result.AccountsSkipped++;
                result.CompletedUtc = DateTime.UtcNow;
                return result;
            }

            try
            {
                await ProcessAccountsAsync(result, token).ConfigureAwait(false);
                result.CompletedUtc = DateTime.UtcNow;
                return result;
            }
            finally
            {
                _RunLock.Release();
            }
        }

        public void Dispose()
        {
            if (_Disposed)
            {
                return;
            }

            StopAsync().GetAwaiter().GetResult();
            _RunLock.Dispose();
            _Disposed = true;
        }

        private async Task RunLoopAsync(CancellationToken token)
        {
            ArchiveAutomaticSettings automatic = GetAutomaticSettings();
            if (automatic.InitialDelaySeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(automatic.InitialDelaySeconds), token).ConfigureAwait(false);
            }

            while (!token.IsCancellationRequested)
            {
                try
                {
                    AutomaticArchiveRunResult result = await RunOnceAsync(token).ConfigureAwait(false);
                    if (result.EntryExportsAttempted > 0 || result.Errors.Count > 0)
                    {
                        _Logging.Info(_Header + "run completed accounts=" +
                            result.AccountsScanned.ToString(CultureInfo.InvariantCulture) +
                            " exports=" + result.EntryExportsSucceeded.ToString(CultureInfo.InvariantCulture) +
                            " failures=" + result.EntryExportsFailed.ToString(CultureInfo.InvariantCulture) +
                            " rows=" + result.RowsExported.ToString(CultureInfo.InvariantCulture) + ".");
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    _Logging.Warn(_Header + "run failed: " + e.Message);
                }

                automatic = GetAutomaticSettings();
                await Task.Delay(TimeSpan.FromSeconds(automatic.IntervalSeconds), token).ConfigureAwait(false);
            }
        }

        private async Task ProcessAccountsAsync(AutomaticArchiveRunResult result, CancellationToken token)
        {
            ArchiveAutomaticSettings automatic = GetAutomaticSettings();
            int remaining = automatic.MaxAccountsPerRun;
            int skip = 0;

            while (remaining > 0)
            {
                token.ThrowIfCancellationRequested();

                int pageSize = Math.Min(remaining, 1000);
                EnumerationResult<Account> page = await _Ledger.EnumerateAccountsAsync(new EnumerationQuery
                {
                    MaxResults = pageSize,
                    Skip = skip,
                    Ordering = EnumerationOrderEnum.CreatedAscending
                }, token).ConfigureAwait(false);

                if (page.Objects == null || page.Objects.Count == 0)
                {
                    break;
                }

                foreach (Account account in page.Objects)
                {
                    token.ThrowIfCancellationRequested();
                    result.AccountsScanned++;
                    await ProcessAccountAsync(account, result, token).ConfigureAwait(false);
                    remaining--;
                    if (remaining == 0)
                    {
                        break;
                    }
                }

                skip += page.Objects.Count;
                if (page.EndOfResults || page.Objects.Count < pageSize)
                {
                    break;
                }
            }
        }

        private async Task ProcessAccountAsync(Account account, AutomaticArchiveRunResult result, CancellationToken token)
        {
            AccountArchivalSettings? existing = await _Ledger.Driver.AccountArchivalSettings
                .ReadByAccountAsync(account.TenantId, account.Id, token)
                .ConfigureAwait(false);
            AccountArchivalSettings state = existing ?? new AccountArchivalSettings
            {
                TenantId = account.TenantId,
                AccountId = account.Id
            };

            AutomaticArchivePolicy policy = BuildPolicy(state);
            if (!policy.Enabled)
            {
                result.AccountsSkipped++;
                return;
            }

            DateTime now = DateTime.UtcNow;
            if (state.NextAttemptUtc.HasValue && state.NextAttemptUtc.Value.ToUniversalTime() > now)
            {
                result.AccountsSkipped++;
                return;
            }

            if (state.LastAttemptUtc.HasValue && state.LastAttemptUtc.Value.ToUniversalTime().AddSeconds(policy.IntervalSeconds) > now)
            {
                result.AccountsSkipped++;
                return;
            }

            DateTime toUtc = now.AddDays(-policy.MaxRetentionDays);
            DateTime fromUtc = state.LastArchivedThroughUtc.HasValue
                ? state.LastArchivedThroughUtc.Value.ToUniversalTime().AddTicks(10)
                : DateTime.UnixEpoch;

            if (toUtc < fromUtc)
            {
                state.NextAttemptUtc = now.AddSeconds(policy.IntervalSeconds);
                await _Ledger.Driver.AccountArchivalSettings.UpsertAsync(state, token).ConfigureAwait(false);
                result.AccountsSkipped++;
                return;
            }

            state.LastAttemptUtc = now;
            await _Ledger.Driver.AccountArchivalSettings.UpsertAsync(state, token).ConfigureAwait(false);

            result.EntryExportsAttempted++;
            ArchiveExportRequest request = new ArchiveExportRequest
            {
                TenantId = account.TenantId,
                AccountId = account.Id,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                StoragePoolId = policy.StoragePoolId,
                MaxBatchRows = policy.MaxBatchRows,
                DeleteAfterCommit = policy.DeleteAfterCommit,
                IdempotencyKey = BuildIdempotencyKey(account, fromUtc, toUtc),
                ActiveDataRetentionDaysOverride = policy.MaxRetentionDays
            };

            try
            {
                ArchiveExportResponse response = await ExportWithRetryAsync(account, request, policy, token).ConfigureAwait(false);
                result.EntryExportsSucceeded++;
                result.RowsExported += response.RowsExported;
                result.BytesUploaded += response.BytesUploaded;
                result.ActiveRowsDeleted += response.ActiveCleanupRowsDeleted;

                state.LastSuccessUtc = DateTime.UtcNow;
                if (response.RowsExported > 0)
                {
                    state.LastArchivedThroughUtc = toUtc;
                }

                state.FailureCount = 0;
                state.LastError = null;
                state.NextAttemptUtc = state.LastSuccessUtc.Value.AddSeconds(policy.IntervalSeconds);
                await _Ledger.Driver.AccountArchivalSettings.UpsertAsync(state, token).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                result.EntryExportsFailed++;
                string error = "Account " + account.Id + " archival failed: " + e.Message;
                result.Errors.Add(error);
                _Logging.Warn(_Header + error);

                state.LastFailureUtc = DateTime.UtcNow;
                state.FailureCount++;
                state.LastError = Truncate(e.Message, 2048);
                state.NextAttemptUtc = state.LastFailureUtc.Value.AddSeconds(CalculateFailureDelaySeconds(policy, state.FailureCount));
                await _Ledger.Driver.AccountArchivalSettings.UpsertAsync(state, token).ConfigureAwait(false);
            }
        }

        private async Task<ArchiveExportResponse> ExportWithRetryAsync(
            Account account,
            ArchiveExportRequest request,
            AutomaticArchivePolicy policy,
            CancellationToken token)
        {
            Exception? lastException = null;
            for (int attempt = 1; attempt <= policy.RetryMaxAttempts; attempt++)
            {
                try
                {
                    RequestContext req = new RequestContext
                    {
                        TenantId = account.TenantId,
                        AccountId = account.Id,
                        Auth = AuthContext.NotRequired(),
                        Url = "/v1/archive/automatic/entries",
                        SourceIp = "background"
                    };

                    return await _ArchiveExportService.ExportEntriesAsync(req, request, BuildServiceHeaders(account.TenantId), token).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    lastException = e;
                    if (attempt >= policy.RetryMaxAttempts)
                    {
                        break;
                    }

                    int delaySeconds = CalculateAttemptDelaySeconds(policy, attempt);
                    if (delaySeconds > 0)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds), token).ConfigureAwait(false);
                    }
                }
            }

            throw lastException ?? new InvalidOperationException("Automatic archive export failed.");
        }

        private AutomaticArchivePolicy BuildPolicy(AccountArchivalSettings settings)
        {
            ArchiveAutomaticSettings automatic = GetAutomaticSettings();
            ArchiveRetrySettings retry = automatic.Retry ?? new ArchiveRetrySettings();

            return new AutomaticArchivePolicy
            {
                Enabled = settings.Enabled ?? automatic.Enabled,
                MaxRetentionDays = settings.MaxRetentionDays ?? automatic.MaxRetentionDays,
                IntervalSeconds = settings.IntervalSeconds ?? automatic.IntervalSeconds,
                MaxBatchRows = settings.MaxBatchRows ?? automatic.MaxBatchRows,
                DeleteAfterCommit = settings.DeleteAfterCommit ?? automatic.DeleteAfterCommit,
                StoragePoolId = !String.IsNullOrWhiteSpace(settings.StoragePoolId) ? settings.StoragePoolId : automatic.StoragePoolId,
                RetryMaxAttempts = settings.RetryMaxAttempts ?? retry.MaxAttempts,
                RetryInitialDelaySeconds = settings.RetryInitialDelaySeconds ?? retry.InitialDelaySeconds,
                RetryMaxDelaySeconds = settings.RetryMaxDelaySeconds ?? retry.MaxDelaySeconds
            };
        }

        private NameValueCollection BuildServiceHeaders(string tenantId)
        {
            NameValueCollection headers = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
            headers.Add("x-tenant-id", tenantId);

            ArchiveSettings archive = _Settings.Archive;
            if (!String.IsNullOrWhiteSpace(archive.ServiceAccessKey))
            {
                headers.Add("x-access-key", archive.ServiceAccessKey);
            }

            if (!String.IsNullOrWhiteSpace(archive.ServiceSecretKey))
            {
                headers.Add("x-secret-key", archive.ServiceSecretKey);
            }

            return headers;
        }

        private string BuildIdempotencyKey(Account account, DateTime fromUtc, DateTime toUtc)
        {
            return "netledger-automatic-entry-export:" +
                account.TenantId + ":" +
                account.Id + ":" +
                fromUtc.ToString("O", CultureInfo.InvariantCulture) + ":" +
                toUtc.ToString("O", CultureInfo.InvariantCulture);
        }

        private int CalculateAttemptDelaySeconds(AutomaticArchivePolicy policy, int completedAttempt)
        {
            if (policy.RetryInitialDelaySeconds <= 0)
            {
                return 0;
            }

            long delay = policy.RetryInitialDelaySeconds;
            for (int i = 1; i < completedAttempt; i++)
            {
                delay *= 2;
                if (delay >= policy.RetryMaxDelaySeconds)
                {
                    return Math.Max(0, policy.RetryMaxDelaySeconds);
                }
            }

            return Convert.ToInt32(Math.Min(delay, policy.RetryMaxDelaySeconds));
        }

        private int CalculateFailureDelaySeconds(AutomaticArchivePolicy policy, int failureCount)
        {
            if (policy.RetryInitialDelaySeconds <= 0)
            {
                return 0;
            }

            long delay = policy.RetryInitialDelaySeconds;
            for (int i = 1; i < failureCount; i++)
            {
                delay *= 2;
                if (delay >= policy.RetryMaxDelaySeconds)
                {
                    return Math.Max(0, policy.RetryMaxDelaySeconds);
                }
            }

            return Convert.ToInt32(Math.Min(delay, policy.RetryMaxDelaySeconds));
        }

        private ArchiveAutomaticSettings GetAutomaticSettings()
        {
            if (_Settings.Archive.Automatic == null)
            {
                _Settings.Archive.Automatic = new ArchiveAutomaticSettings();
            }

            if (_Settings.Archive.Automatic.Retry == null)
            {
                _Settings.Archive.Automatic.Retry = new ArchiveRetrySettings();
            }

            return _Settings.Archive.Automatic;
        }

        private bool ArchiveIntegrationEnabled()
        {
            return _Settings.Archive != null && _Settings.Archive.Enabled;
        }

        private bool GlobalAutomaticEnabled()
        {
            return _Settings.Archive != null &&
                _Settings.Archive.Automatic != null &&
                _Settings.Archive.Automatic.Enabled;
        }

        private string Truncate(string value, int maxLength)
        {
            if (String.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength);
        }
    }
}
