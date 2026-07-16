namespace NetLedger
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Database;
    using Padlocks;

    /// <summary>
    /// NetLedger.
    /// </summary>
    public class Ledger : IAsyncDisposable
    {
        #region Public-Members

        /// <summary>
        /// Event fired when an account is created.
        /// </summary>
        public event EventHandler<AccountEventArgs> AccountCreated;

        /// <summary>
        /// Event fired when an account is deleted.
        /// </summary>
        public event EventHandler<AccountEventArgs> AccountDeleted;

        /// <summary>
        /// Event fired when a credit is added.
        /// </summary>
        public event EventHandler<EntryEventArgs> CreditAdded;

        /// <summary>
        /// Event fired when a debit is added.
        /// </summary>
        public event EventHandler<EntryEventArgs> DebitAdded;

        /// <summary>
        /// Event fired when an entry is canceled.
        /// </summary>
        public event EventHandler<EntryEventArgs> EntryCanceled;

        /// <summary>
        /// Event fired when entries are committed successfully.
        /// </summary>
        public event EventHandler<CommitEventArgs> EntriesCommitted;

        /// <summary>
        /// Database driver providing direct access to database operations.
        /// </summary>
        public DatabaseDriverBase Driver
        {
            get { return _Driver; }
        }

        #endregion

        #region Private-Members

        private DatabaseDriverBase _Driver = null;
        private DatabaseSettings _Settings = null;
        private readonly Padlock<string> _AccountLocks = new Padlock<string>();
        private const int MaxBalanceReadConcurrency = 8;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the ledger using default SQLite with specified filename.
        /// </summary>
        /// <param name="filename">SQLite database filename.</param>
        /// <exception cref="ArgumentNullException">Thrown when filename is null or empty.</exception>
        public Ledger(string filename) : this(new DatabaseSettings
        {
            Type = DatabaseTypeEnum.Sqlite,
            Filename = filename ?? "./netledger.db"
        })
        {
        }

        /// <summary>
        /// Instantiate the ledger using specified database settings.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when settings is null.</exception>
        /// <exception cref="ArgumentException">Thrown when database type is unsupported.</exception>
        public Ledger(DatabaseSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _Settings = settings;

            _Driver = DatabaseDriverFactory.Create(settings);
        }

        #endregion

        #region Public-Account-Methods

        /// <summary>
        /// Creates an account with the specified name.
        /// </summary>
        /// <param name="name">Name of the account.</param>
        /// <param name="initialBalance">Initial balance of the account.</param>
        /// <param name="labels">Account labels.</param>
        /// <param name="tags">Account tags.</param>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>string of the newly-created account.</returns>
        /// <exception cref="ArgumentNullException">Thrown when name is null or empty.</exception>
        public async Task<string> CreateAccountAsync(
            string name,
            decimal? initialBalance = null,
            List<string>? labels = null,
            Dictionary<string, string>? tags = null,
            string? tenantId = null,
            CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

            Account a = new Account(name);
            a.TenantId = tenantId ?? String.Empty;
            a.Labels = labels ?? new List<string>();
            a.Tags = tags ?? new Dictionary<string, string>();
            a = await _Driver.Accounts.CreateAsync(a, token).ConfigureAwait(false);
            string accountId = a.Id;

            try
            {
                IDisposable lockReleaser = await _AccountLocks.LockAsync(a.Id, token).ConfigureAwait(false);
                await using IAsyncDisposable dbLockReleaser = await _Driver.AcquireAccountLockAsync(a.Id, token).ConfigureAwait(false);
                using (lockReleaser)
                {
                    Entry balance = new Entry();
                    balance.Id = NetLedgerId.Generate(IdentifierPrefixes.Entry);
                    balance.TenantId = a.TenantId;
                    balance.AccountId = a.Id;
                    balance.Type = EntryType.Balance;
                    balance.Amount = initialBalance ?? 0m;
                    balance.Description = "Initial balance";
                    balance.IsCommitted = true;
                    balance.CommittedUtc = DateTime.Now.ToUniversalTime();

                    await _Driver.Entries.CreateAsync(balance, token).ConfigureAwait(false);
                }
            }
            finally
            {
                Task.Run(() => AccountCreated?.Invoke(this, new AccountEventArgs(a)));
            }

            return accountId;
        }

        /// <summary>
        /// Delete an account and associated entries by account name.
        /// </summary>
        /// <param name="name">Name of the account.</param>
        /// <param name="token">Cancellation token.</param>
        /// <exception cref="ArgumentNullException">Thrown when name is null or empty.</exception>
        public async Task DeleteAccountByNameAsync(string name, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

            Account a = await _Driver.Accounts.ReadByNameAsync(name, token).ConfigureAwait(false);
            if (a != null)
            {
                try
                {
                    IDisposable lockReleaser = await _AccountLocks.LockAsync(a.Id, token).ConfigureAwait(false);
                    await using IAsyncDisposable dbLockReleaser = await _Driver.AcquireAccountLockAsync(a.Id, token).ConfigureAwait(false);
                    using (lockReleaser)
                    {
                        await _Driver.Entries.DeleteByAccountIdAsync(a.Id, token).ConfigureAwait(false);
                        await _Driver.Accounts.DeleteByIdAsync(a.Id, token).ConfigureAwait(false);
                    }
                }
                finally
                {
                    Task.Run(() => AccountDeleted?.Invoke(this, new AccountEventArgs(a)), token);
                }
            }
        }

        /// <summary>
        /// Delete an account and associated entries by account identifier.
        /// </summary>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <exception cref="ArgumentNullException">Thrown when accountId is empty.</exception>
        public async Task DeleteAccountByIdAsync(string accountId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(accountId)) throw new ArgumentNullException(nameof(accountId));

            Account a = await _Driver.Accounts.ReadByIdAsync(accountId, token).ConfigureAwait(false);
            if (a != null)
            {
                try
                {
                    IDisposable lockReleaser = await _AccountLocks.LockAsync(a.Id, token).ConfigureAwait(false);
                    await using IAsyncDisposable dbLockReleaser = await _Driver.AcquireAccountLockAsync(a.Id, token).ConfigureAwait(false);
                    using (lockReleaser)
                    {
                        await _Driver.Entries.DeleteByAccountIdAsync(a.Id, token).ConfigureAwait(false);
                        await _Driver.Accounts.DeleteByIdAsync(a.Id, token).ConfigureAwait(false);
                    }
                }
                finally
                {
                    Task.Run(() => AccountDeleted?.Invoke(this, new AccountEventArgs(a)));
                }
            }
        }

        /// <summary>
        /// Retrieve an account by name.
        /// </summary>
        /// <param name="name">Name of the account.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Account or null if it does not exist.</returns>
        /// <exception cref="ArgumentNullException">Thrown when name is null or empty.</exception>
        public async Task<Account> GetAccountByNameAsync(string name, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            return await _Driver.Accounts.ReadByNameAsync(name, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieve an account by identifier.
        /// </summary>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Account or null if it does not exist.</returns>
        /// <exception cref="ArgumentNullException">Thrown when accountId is empty.</exception>
        public async Task<Account> GetAccountByIdAsync(string accountId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(accountId)) throw new ArgumentNullException(nameof(accountId));
            return await _Driver.Accounts.ReadByIdAsync(accountId, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieve all accounts.
        /// </summary>
        /// <param name="searchTerm">Term to search within account names.</param>
        /// <param name="skip">Number of records to skip.</param>
        /// <param name="take">Number of records to take.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of Account objects.</returns>
        public async Task<List<Account>> GetAllAccountsAsync(string searchTerm = null, int? skip = null, int? take = null, CancellationToken token = default)
        {
            List<Account> accounts;

            if (!String.IsNullOrEmpty(searchTerm))
            {
                accounts = await _Driver.Accounts.SearchByNameAsync(searchTerm, token).ConfigureAwait(false);
            }
            else
            {
                accounts = await _Driver.Accounts.ReadAllAsync(token).ConfigureAwait(false);
            }

            if (skip.HasValue && skip.Value > 0)
            {
                accounts = accounts.Skip(skip.Value).ToList();
            }

            if (take.HasValue && take.Value > 0)
            {
                accounts = accounts.Take(take.Value).ToList();
            }

            return accounts;
        }

        /// <summary>
        /// Enumerate accounts in a paginated way.
        /// </summary>
        /// <param name="query">Enumeration query containing pagination parameters and filters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result containing the page of accounts and metadata for continuing the enumeration.</returns>
        /// <exception cref="ArgumentNullException">Thrown when query is null.</exception>
        /// <exception cref="ArgumentException">Thrown when skip and continuation token are both specified, or when AmountMinimum/AmountMaximum ordering is used.</exception>
        public async Task<EnumerationResult<Account>> EnumerateAccountsAsync(
            EnumerationQuery query,
            CancellationToken token = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (query.ContinuationToken != null && query.Skip > 0)
                throw new ArgumentException("Skip count and enumeration tokens cannot be used in the same enumeration request.");
            if (query.Ordering == EnumerationOrderEnum.AmountAscending || query.Ordering == EnumerationOrderEnum.AmountDescending)
                throw new ArgumentException("Amount ordering is not supported for account enumeration.");

            return await _Driver.Accounts.EnumerateAsync(query, token).ConfigureAwait(false);
        }

        #endregion

        #region Public-Entry-Methods

        /// <summary>
        /// Add a credit.
        /// </summary>
        /// <param name="accountId">string of the account.</param>
        /// <param name="amount">Amount of the credit.</param>
        /// <param name="notes">Notes for the entry.</param>
        /// <param name="summarizedBy">string of the entry that summarized this entry.</param>
        /// <param name="isCommitted">Indicates if the entry should be immediately committed.</param>
        /// <param name="labels">Entry labels.</param>
        /// <param name="tags">Entry tags.</param>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>string of the newly-created entry.</returns>
        /// <exception cref="ArgumentNullException">Thrown when accountId is empty.</exception>
        /// <exception cref="ArgumentException">Thrown when amount is negative.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when account is not found.</exception>
        public async Task<string> AddCreditAsync(
            string accountId,
            decimal amount,
            string notes = null,
            string? summarizedBy = null,
            bool isCommitted = false,
            List<string>? labels = null,
            Dictionary<string, string>? tags = null,
            string? tenantId = null,
            CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(accountId)) throw new ArgumentNullException(nameof(accountId));
            if (amount < 0) throw new ArgumentException("Amount must be zero or greater.");

            Account a = await _Driver.Accounts.ReadByIdAsync(accountId, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with string " + accountId + ".");

            Entry entry = null;

            try
            {
                IDisposable lockReleaser = await _AccountLocks.LockAsync(accountId, token).ConfigureAwait(false);
                await using IAsyncDisposable dbLockReleaser = await _Driver.AcquireAccountLockAsync(accountId, token).ConfigureAwait(false);
                using (lockReleaser)
                {
                    entry = new Entry(accountId, EntryType.Credit, amount, notes, summarizedBy, false);
                    entry.TenantId = tenantId ?? a.TenantId;
                    entry.Labels = labels ?? new List<string>();
                    entry.Tags = tags ?? new Dictionary<string, string>();
                    entry = await _Driver.Entries.CreateAsync(entry, token).ConfigureAwait(false);

                    string entryId = entry.Id;

                    if (isCommitted)
                    {
                        List<string> entryIdsToCommit = new List<string> { entryId };
                        await CommitEntriesAsync(accountId, entryIdsToCommit, false, token).ConfigureAwait(false);
                    }

                    return entryId;
                }
            }
            finally
            {
                if (entry != null) Task.Run(() => CreditAdded?.Invoke(this, new EntryEventArgs(a, entry)));
            }
        }

        /// <summary>
        /// Add a debit.
        /// </summary>
        /// <param name="accountId">string of the account.</param>
        /// <param name="amount">Amount of the debit.</param>
        /// <param name="notes">Notes for the entry.</param>
        /// <param name="summarizedBy">string of the entry that summarized this entry.</param>
        /// <param name="isCommitted">Indicates if the entry should be immediately committed.</param>
        /// <param name="labels">Entry labels.</param>
        /// <param name="tags">Entry tags.</param>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>string of the newly-created entry.</returns>
        /// <exception cref="ArgumentNullException">Thrown when accountId is empty.</exception>
        /// <exception cref="ArgumentException">Thrown when amount is negative.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when account is not found.</exception>
        public async Task<string> AddDebitAsync(
            string accountId,
            decimal amount,
            string notes = null,
            string? summarizedBy = null,
            bool isCommitted = false,
            List<string>? labels = null,
            Dictionary<string, string>? tags = null,
            string? tenantId = null,
            CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(accountId)) throw new ArgumentNullException(nameof(accountId));
            if (amount < 0) throw new ArgumentException("Amount must be zero or greater.");

            Account a = await _Driver.Accounts.ReadByIdAsync(accountId, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with string " + accountId + ".");

            Entry entry = null;

            try
            {
                IDisposable lockReleaser = await _AccountLocks.LockAsync(accountId, token).ConfigureAwait(false);
                await using IAsyncDisposable dbLockReleaser = await _Driver.AcquireAccountLockAsync(accountId, token).ConfigureAwait(false);
                using (lockReleaser)
                {
                    entry = new Entry(accountId, EntryType.Debit, amount, notes, summarizedBy, false);
                    entry.TenantId = tenantId ?? a.TenantId;
                    entry.Labels = labels ?? new List<string>();
                    entry.Tags = tags ?? new Dictionary<string, string>();
                    entry = await _Driver.Entries.CreateAsync(entry, token).ConfigureAwait(false);

                    string entryId = entry.Id;

                    if (isCommitted)
                    {
                        List<string> entryIdsToCommit = new List<string> { entryId };
                        await CommitEntriesAsync(accountId, entryIdsToCommit, false, token).ConfigureAwait(false);
                    }

                    return entryId;
                }
            }
            finally
            {
                if (entry != null) Task.Run(() => DebitAdded?.Invoke(this, new EntryEventArgs(a, entry)));
            }
        }

        /// <summary>
        /// Add multiple credits in batch.
        /// </summary>
        /// <param name="accountId">string of the account.</param>
        /// <param name="credits">List of batch entry inputs containing amount and notes for each credit.</param>
        /// <param name="isCommitted">Indicates if transactions should be immediately committed.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of identifiers for the newly-created entries.</returns>
        /// <exception cref="ArgumentNullException">Thrown when accountId is empty.</exception>
        /// <exception cref="ArgumentException">Thrown when credits list is null or empty.</exception>
        public async Task<List<string>> AddCreditsAsync(string accountId, List<BatchEntryInput> credits, bool isCommitted = false, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(accountId)) throw new ArgumentNullException(nameof(accountId));
            if (credits == null || credits.Count == 0) throw new ArgumentException("Credits list cannot be null or empty.");

            return await AddEntriesAsync(accountId, credits, EntryType.Credit, isCommitted, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Add multiple debits in batch.
        /// </summary>
        /// <param name="accountId">string of the account.</param>
        /// <param name="debits">List of batch entry inputs containing amount and notes for each debit.</param>
        /// <param name="isCommitted">Indicates if transactions should be immediately committed.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of identifiers for the newly-created entries.</returns>
        /// <exception cref="ArgumentNullException">Thrown when accountId is empty.</exception>
        /// <exception cref="ArgumentException">Thrown when debits list is null or empty.</exception>
        public async Task<List<string>> AddDebitsAsync(string accountId, List<BatchEntryInput> debits, bool isCommitted = false, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(accountId)) throw new ArgumentNullException(nameof(accountId));
            if (debits == null || debits.Count == 0) throw new ArgumentException("Debits list cannot be null or empty.");

            return await AddEntriesAsync(accountId, debits, EntryType.Debit, isCommitted, token).ConfigureAwait(false);
        }

        private async Task<List<string>> AddEntriesAsync(
            string accountId,
            List<BatchEntryInput> inputs,
            EntryType type,
            bool isCommitted,
            CancellationToken token)
        {
            Account a = await _Driver.Accounts.ReadByIdAsync(accountId, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with string " + accountId + ".");

            List<Entry>? createdEntries = null;

            try
            {
                IDisposable lockReleaser = await _AccountLocks.LockAsync(accountId, token).ConfigureAwait(false);
                await using IAsyncDisposable dbLockReleaser = await _Driver.AcquireAccountLockAsync(accountId, token).ConfigureAwait(false);
                using (lockReleaser)
                {
                    List<Entry> entries = inputs.Select(input =>
                    {
                        Entry entry = new Entry(accountId, type, input.Amount, input.Notes, null, false);
                        entry.TenantId = a.TenantId;
                        entry.Labels = input.Labels;
                        entry.Tags = input.Tags;
                        return entry;
                    }).ToList();

                    createdEntries = await _Driver.Entries.CreateManyAsync(entries, token).ConfigureAwait(false);
                    List<string> entryIds = createdEntries.Select(entry => entry.Id).ToList();

                    if (isCommitted)
                    {
                        await CommitEntriesAsync(accountId, entryIds, false, token).ConfigureAwait(false);
                    }

                    return entryIds;
                }
            }
            finally
            {
                if (createdEntries != null)
                {
                    foreach (Entry entry in createdEntries)
                    {
                        if (type == EntryType.Credit) Task.Run(() => CreditAdded?.Invoke(this, new EntryEventArgs(a, entry)));
                        else if (type == EntryType.Debit) Task.Run(() => DebitAdded?.Invoke(this, new EntryEventArgs(a, entry)));
                    }
                }
            }
        }

        /// <summary>
        /// Cancel a pending entry.
        /// </summary>
        /// <param name="accountId">string of the account.</param>
        /// <param name="entryId">string of the entry.</param>
        /// <param name="token">Cancellation token.</param>
        /// <exception cref="ArgumentNullException">Thrown when accountId or entryId is empty.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when account is not found.</exception>
        /// <exception cref="InvalidOperationException">Thrown when entry is not found or already committed.</exception>
        public async Task CancelPendingAsync(string accountId, string entryId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(accountId)) throw new ArgumentNullException(nameof(accountId));
            if (String.IsNullOrEmpty(entryId)) throw new ArgumentNullException(nameof(entryId));

            Account a = await _Driver.Accounts.ReadByIdAsync(accountId, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with string " + accountId + ".");

            Entry entry = null;

            try
            {
                IDisposable lockReleaser = await _AccountLocks.LockAsync(accountId, token).ConfigureAwait(false);
                await using IAsyncDisposable dbLockReleaser = await _Driver.AcquireAccountLockAsync(accountId, token).ConfigureAwait(false);
                using (lockReleaser)
                {
                    entry = await _Driver.Entries.ReadByIdAsync(entryId, token).ConfigureAwait(false);
                    if (entry == null) throw new KeyNotFoundException("Unable to find entry with string " + entryId + ".");
                    if (entry.IsCommitted) throw new InvalidOperationException("Entry has already been committed.");
                    if (entry.AccountId != accountId) throw new InvalidOperationException("Entry does not belong to this account.");

                    await _Driver.Entries.DeleteByIdAsync(entryId, token).ConfigureAwait(false);
                }
            }
            finally
            {
                if (entry != null) Task.Run(() => EntryCanceled?.Invoke(this, new EntryEventArgs(a, entry)));
            }
        }

        /// <summary>
        /// Get an entry by its identifier.
        /// </summary>
        /// <param name="entryId">string of the entry.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Entry or null if not found.</returns>
        /// <exception cref="ArgumentNullException">Thrown when entryId is empty.</exception>
        public async Task<Entry> GetEntryAsync(string entryId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(entryId)) throw new ArgumentNullException(nameof(entryId));
            return await _Driver.Entries.ReadByIdAsync(entryId, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get entries for an account with optional filtering.
        /// </summary>
        /// <param name="accountId">string of the account.</param>
        /// <param name="startTimeUtc">Start time UTC filter.</param>
        /// <param name="endTimeUtc">End time UTC filter.</param>
        /// <param name="amountMin">Minimum amount filter.</param>
        /// <param name="amountMax">Maximum amount filter.</param>
        /// <param name="searchTerm">Search term for description.</param>
        /// <param name="entryType">Entry type filter.</param>
        /// <param name="skip">Number of records to skip.</param>
        /// <param name="take">Number of records to take.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of matching entries.</returns>
        /// <exception cref="ArgumentNullException">Thrown when accountId is empty.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when account is not found.</exception>
        public async Task<List<Entry>> GetEntriesAsync(
            string accountId,
            DateTime? startTimeUtc = null,
            DateTime? endTimeUtc = null,
            decimal? amountMin = null,
            decimal? amountMax = null,
            string searchTerm = null,
            EntryType? entryType = null,
            int? skip = null,
            int? take = null,
            CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(accountId)) throw new ArgumentNullException(nameof(accountId));
            if (startTimeUtc.HasValue && endTimeUtc.HasValue && startTimeUtc.Value > endTimeUtc.Value)
                throw new ArgumentException("Start time must be less than or equal to end time.");

            Account a = await _Driver.Accounts.ReadByIdAsync(accountId, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with string " + accountId + ".");

            FilterBuilder filter = new FilterBuilder
            {
                StartTimeUtc = startTimeUtc,
                EndTimeUtc = endTimeUtc,
                AmountMinimum = amountMin,
                AmountMaximum = amountMax,
                SearchTerm = searchTerm,
                EntryType = entryType,
                Skip = skip ?? 0,
                MaxResults = take ?? 1000,
                ExcludeBalanceEntries = true
            };

            return await _Driver.Entries.ReadWithFilterAsync(accountId, filter, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Search for entries within an account.
        /// </summary>
        /// <param name="accountId">string of the account.</param>
        /// <param name="startTimeUtc">Start time UTC.</param>
        /// <param name="endTimeUtc">End time UTC.</param>
        /// <param name="amountMin">Minimum amount.</param>
        /// <param name="amountMax">Maximum amount.</param>
        /// <param name="searchTerm">Search term for description.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of matching entries.</returns>
        /// <exception cref="ArgumentNullException">Thrown when accountId is empty.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when account is not found.</exception>
        public async Task<List<Entry>> SearchEntriesAsync(
            string accountId,
            DateTime? startTimeUtc = null,
            DateTime? endTimeUtc = null,
            decimal? amountMin = null,
            decimal? amountMax = null,
            string searchTerm = null,
            CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(accountId)) throw new ArgumentNullException(nameof(accountId));

            Account a = await _Driver.Accounts.ReadByIdAsync(accountId, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with string " + accountId + ".");

            FilterBuilder filter = new FilterBuilder
            {
                StartTimeUtc = startTimeUtc,
                EndTimeUtc = endTimeUtc,
                AmountMinimum = amountMin,
                AmountMaximum = amountMax,
                SearchTerm = searchTerm,
                ExcludeBalanceEntries = true
            };

            return await _Driver.Entries.ReadWithFilterAsync(accountId, filter, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Enumerate entries in a paginated way.
        /// </summary>
        /// <param name="query">Enumeration query containing pagination parameters and filters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result containing the page of entries and metadata for continuing the enumeration.</returns>
        /// <exception cref="ArgumentNullException">Thrown when query is null or accountId is not specified.</exception>
        /// <exception cref="ArgumentException">Thrown when skip and continuation token are both specified.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when account is not found.</exception>
        public async Task<EnumerationResult<Entry>> EnumerateEntriesAsync(
            EnumerationQuery query,
            CancellationToken token = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (String.IsNullOrEmpty(query.AccountId)) throw new ArgumentNullException(nameof(query.AccountId), "accountId must be specified for entry enumeration.");
            if (query.ContinuationToken != null && query.Skip > 0)
                throw new ArgumentException("Skip count and enumeration tokens cannot be used in the same enumeration request.");
            if (query.BalanceMinimum.HasValue || query.BalanceMaximum.HasValue)
                throw new ArgumentException("Balance filters (BalanceMinimum/BalanceMaximum) are not supported for entry enumeration. Use account enumeration instead.");

            Account a = await _Driver.Accounts.ReadByIdAsync(query.AccountId, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with string " + query.AccountId + ".");

            return await _Driver.Entries.EnumerateAsync(query.AccountId, query, token).ConfigureAwait(false);
        }

        #endregion

        #region Public-Balance-Methods

        /// <summary>
        /// Get the current balance for an account.
        /// </summary>
        /// <param name="accountId">string of the account.</param>
        /// <param name="includePendingEntries">Whether to include pending entries in the balance calculation.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Balance object containing committed and pending balances.</returns>
        /// <exception cref="ArgumentNullException">Thrown when accountId is empty.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when account is not found.</exception>
        public async Task<Balance> GetBalanceAsync(string accountId, bool includePendingEntries = true, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(accountId)) throw new ArgumentNullException(nameof(accountId));

            IDisposable lockReleaser = await _AccountLocks.LockAsync(accountId, token).ConfigureAwait(false);
            await using IAsyncDisposable dbLockReleaser = await _Driver.AcquireAccountLockAsync(accountId, token).ConfigureAwait(false);
            using (lockReleaser)
            {
                return await GetBalanceWithoutLockAsync(accountId, includePendingEntries, token).ConfigureAwait(false);
            }
        }

        private async Task<Balance> GetBalanceWithoutLockAsync(string accountId, bool includePendingEntries = true, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(accountId)) throw new ArgumentNullException(nameof(accountId));

            Account a = await _Driver.Accounts.ReadByIdAsync(accountId, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with string " + accountId + ".");

            return await GetBalanceWithoutLockAsync(a, includePendingEntries, token).ConfigureAwait(false);
        }

        private async Task<Balance> GetBalanceWithoutLockAsync(Account a, bool includePendingEntries = true, CancellationToken token = default)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));

            Balance balance = new Balance();
            balance.AccountId = a.Id;
            balance.Name = a.Name;

            Entry latestBalance = await _Driver.Entries.ReadLatestBalanceAsync(a.Id, token).ConfigureAwait(false);
            balance.CommittedBalance = latestBalance?.Amount ?? 0m;

            if (includePendingEntries)
            {
                List<Entry> pendingCredits = await _Driver.Entries.ReadPendingByAccountIdAsync(a.Id, EntryType.Credit, token).ConfigureAwait(false);
                List<Entry> pendingDebits = await _Driver.Entries.ReadPendingByAccountIdAsync(a.Id, EntryType.Debit, token).ConfigureAwait(false);

                balance.PendingCredits = new PendingTransactionSummary
                {
                    Count = pendingCredits.Count,
                    Total = pendingCredits.Sum(e => e.Amount),
                    Entries = pendingCredits
                };

                balance.PendingDebits = new PendingTransactionSummary
                {
                    Count = pendingDebits.Count,
                    Total = pendingDebits.Sum(e => e.Amount),
                    Entries = pendingDebits
                };

                balance.PendingBalance = balance.CommittedBalance + balance.PendingCredits.Total - balance.PendingDebits.Total;
            }
            else
            {
                balance.PendingBalance = balance.CommittedBalance;
                balance.PendingCredits = new PendingTransactionSummary();
                balance.PendingDebits = new PendingTransactionSummary();
            }

            return balance;
        }

        /// <summary>
        /// Get the balance for an account as of a specific timestamp.
        /// </summary>
        /// <param name="accountId">string of the account.</param>
        /// <param name="asOfUtc">Timestamp in UTC.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Balance as of the specified timestamp.</returns>
        /// <exception cref="ArgumentNullException">Thrown when accountId is empty.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when account is not found.</exception>
        public async Task<decimal> GetBalanceAsOfAsync(string accountId, DateTime asOfUtc, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(accountId)) throw new ArgumentNullException(nameof(accountId));

            Account a = await _Driver.Accounts.ReadByIdAsync(accountId, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with string " + accountId + ".");

            Entry balanceEntry = await _Driver.Entries.ReadBalanceAsOfAsync(accountId, asOfUtc, token).ConfigureAwait(false);
            return balanceEntry?.Amount ?? 0m;
        }

        /// <summary>
        /// Commit pending entries for an account.
        /// </summary>
        /// <param name="accountId">string of the account.</param>
        /// <param name="entryIds">Optional list of specific entry identifiers to commit. If null, all pending entries are committed.</param>
        /// <param name="acquireLock">Whether to acquire the account lock.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Balance after the commit operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when accountId is empty.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when account is not found.</exception>
        public async Task<Balance> CommitEntriesAsync(string accountId, List<string> entryIds = null, bool acquireLock = true, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(accountId)) throw new ArgumentNullException(nameof(accountId));

            Account a = await _Driver.Accounts.ReadByIdAsync(accountId, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with string " + accountId + ".");

            IDisposable? lockReleaser = acquireLock ? await _AccountLocks.LockAsync(accountId, token).ConfigureAwait(false) : null;
            IAsyncDisposable? dbLockReleaser = acquireLock ? await _Driver.AcquireAccountLockAsync(accountId, token).ConfigureAwait(false) : null;
            try
            {
                using (lockReleaser)
                {
                    Balance balanceBefore = await GetBalanceWithoutLockAsync(accountId, true, token).ConfigureAwait(false);
                    Entry balanceOld = await _Driver.Entries.ReadLatestBalanceAsync(accountId, token).ConfigureAwait(false);

                    return await CommitEntriesInternalAsync(accountId, entryIds, balanceBefore, balanceOld, a, token).ConfigureAwait(false);
                }
            }
            finally
            {
                if (dbLockReleaser != null)
                {
                    await dbLockReleaser.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Enumerate entries for an account (alias for EnumerateEntriesAsync).
        /// </summary>
        /// <param name="query">Enumeration query containing pagination parameters and filters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result containing the page of entries and metadata for continuing the enumeration.</returns>
        /// <exception cref="ArgumentNullException">Thrown when query is null or accountId is not specified.</exception>
        /// <exception cref="ArgumentException">Thrown when skip and continuation token are both specified.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when account is not found.</exception>
        public async Task<EnumerationResult<Entry>> EnumerateTransactionsAsync(
            EnumerationQuery query,
            CancellationToken token = default)
        {
            return await EnumerateEntriesAsync(query, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get all pending entries for an account.
        /// </summary>
        /// <param name="accountId">string of the account.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of pending entries.</returns>
        /// <exception cref="ArgumentNullException">Thrown when accountId is empty.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when account is not found.</exception>
        public async Task<List<Entry>> GetPendingEntriesAsync(string accountId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(accountId)) throw new ArgumentNullException(nameof(accountId));

            Account a = await _Driver.Accounts.ReadByIdAsync(accountId, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with string " + accountId + ".");

            return await _Driver.Entries.ReadPendingByAccountIdAsync(accountId, null, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get all pending credit entries for an account.
        /// </summary>
        /// <param name="accountId">string of the account.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of pending credit entries.</returns>
        /// <exception cref="ArgumentNullException">Thrown when accountId is empty.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when account is not found.</exception>
        public async Task<List<Entry>> GetPendingCreditsAsync(string accountId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(accountId)) throw new ArgumentNullException(nameof(accountId));

            Account a = await _Driver.Accounts.ReadByIdAsync(accountId, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with string " + accountId + ".");

            return await _Driver.Entries.ReadPendingByAccountIdAsync(accountId, EntryType.Credit, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get all pending debit entries for an account.
        /// </summary>
        /// <param name="accountId">string of the account.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of pending debit entries.</returns>
        /// <exception cref="ArgumentNullException">Thrown when accountId is empty.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when account is not found.</exception>
        public async Task<List<Entry>> GetPendingDebitsAsync(string accountId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(accountId)) throw new ArgumentNullException(nameof(accountId));

            Account a = await _Driver.Accounts.ReadByIdAsync(accountId, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with string " + accountId + ".");

            return await _Driver.Entries.ReadPendingByAccountIdAsync(accountId, EntryType.Debit, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get balances for all accounts as a dictionary.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dictionary of account string to Balance objects.</returns>
        public async Task<Dictionary<string, Balance>> GetAllBalancesAsync(CancellationToken token = default)
        {
            return await GetAllBalancesAsync(true, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get balances for all accounts as a dictionary.
        /// </summary>
        /// <param name="includePendingEntries">Whether to include pending entries in the balance calculation.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dictionary of account string to Balance objects.</returns>
        public async Task<Dictionary<string, Balance>> GetAllBalancesAsync(bool includePendingEntries, CancellationToken token = default)
        {
            List<Account> accounts = await _Driver.Accounts.ReadAllAsync(token).ConfigureAwait(false);
            return await GetBalancesForAccountsAsync(accounts, includePendingEntries, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get balances for the specified accounts as a dictionary.
        /// </summary>
        /// <param name="accounts">Accounts to summarize.</param>
        /// <param name="includePendingEntries">Whether to include pending entries in the balance calculation.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dictionary of account string to Balance objects.</returns>
        public async Task<Dictionary<string, Balance>> GetBalancesForAccountsAsync(IEnumerable<Account> accounts, bool includePendingEntries = true, CancellationToken token = default)
        {
            if (accounts == null) throw new ArgumentNullException(nameof(accounts));

            List<Account> accountList = accounts
                .Where(account => account != null && !String.IsNullOrEmpty(account.Id))
                .GroupBy(account => account.Id)
                .Select(group => group.First())
                .ToList();

            Dictionary<string, Balance> balances = new Dictionary<string, Balance>();
            using SemaphoreSlim semaphore = new SemaphoreSlim(MaxBalanceReadConcurrency);

            List<Task<KeyValuePair<string, Balance>>> tasks = accountList.Select(async account =>
            {
                await semaphore.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    Balance balance = await GetBalanceWithoutLockAsync(account, includePendingEntries, token).ConfigureAwait(false);
                    return new KeyValuePair<string, Balance>(account.Id, balance);
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToList();

            KeyValuePair<string, Balance>[] results = await Task.WhenAll(tasks).ConfigureAwait(false);
            foreach (KeyValuePair<string, Balance> result in results)
            {
                balances[result.Key] = result.Value;
            }

            return balances;
        }

        /// <summary>
        /// Verify the balance chain for an account.
        /// </summary>
        /// <param name="accountId">string of the account.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the balance chain is valid, false otherwise.</returns>
        /// <exception cref="ArgumentNullException">Thrown when accountId is empty.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when account is not found.</exception>
        public async Task<bool> VerifyBalanceChainAsync(string accountId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(accountId)) throw new ArgumentNullException(nameof(accountId));

            Account a = await _Driver.Accounts.ReadByIdAsync(accountId, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with string " + accountId + ".");

            Entry currentBalance = await _Driver.Entries.ReadLatestBalanceAsync(accountId, token).ConfigureAwait(false);
            if (currentBalance == null) return true;

            HashSet<string> visited = new HashSet<string>();
            while (currentBalance != null && !String.IsNullOrEmpty(currentBalance.Replaces))
            {
                if (visited.Contains(currentBalance.Id))
                    return false;

                visited.Add(currentBalance.Id);
                currentBalance = await _Driver.Entries.ReadByIdAsync(currentBalance.Replaces, token).ConfigureAwait(false);
            }

            return true;
        }

        #endregion

        #region Public-Disposal-Methods

        /// <summary>
        /// Dispose of the ledger.
        /// </summary>
        /// <returns>Task.</returns>
        public async ValueTask DisposeAsync()
        {
            if (_Disposed) return;

            if (_Driver != null)
            {
                await _Driver.DisposeAsync().ConfigureAwait(false);
            }

            _Disposed = true;
        }

        #endregion

        #region Private-Commit-Methods

        private async Task<Balance> CommitEntriesInternalAsync(
            string accountId,
            List<string> entryIds,
            Balance balanceBefore,
            Entry balanceOld,
            Account account,
            CancellationToken token)
        {
            List<string> summarized = new List<string>();

            // Commit credits
            decimal committedCreditsTotal = 0m;
            if (balanceBefore.PendingCredits.Entries != null && balanceBefore.PendingCredits.Entries.Count > 0)
            {
                foreach (Entry entry in balanceBefore.PendingCredits.Entries)
                {
                    if (entryIds != null && entryIds.Count > 0 && !entryIds.Contains(entry.Id)) continue;
                    summarized.Add(entry.Id);
                    entry.IsCommitted = true;
                    entry.CommittedUtc = DateTime.Now.ToUniversalTime();
                    committedCreditsTotal += entry.Amount;
                }
            }

            // Commit debits
            decimal committedDebitsTotal = 0m;
            if (balanceBefore.PendingDebits.Entries != null && balanceBefore.PendingDebits.Entries.Count > 0)
            {
                foreach (Entry entry in balanceBefore.PendingDebits.Entries)
                {
                    if (entryIds != null && entryIds.Count > 0 && !entryIds.Contains(entry.Id)) continue;
                    summarized.Add(entry.Id);
                    entry.IsCommitted = true;
                    entry.CommittedUtc = DateTime.Now.ToUniversalTime();
                    committedDebitsTotal += entry.Amount;
                }
            }

            if (summarized.Count > 0)
            {
                // Create new balance entry
                decimal newBalance = balanceBefore.CommittedBalance + committedCreditsTotal - committedDebitsTotal;
                Entry balanceNew = new Entry();
                balanceNew.Id = NetLedgerId.Generate(IdentifierPrefixes.Entry);
                balanceNew.TenantId = account.TenantId;
                balanceNew.AccountId = accountId;
                balanceNew.Type = EntryType.Balance;
                balanceNew.Amount = newBalance;
                balanceNew.Description = "Balance after commit";
                balanceNew.IsCommitted = true;
                balanceNew.CommittedUtc = DateTime.Now.ToUniversalTime();

                if (balanceOld != null)
                    balanceNew.Replaces = balanceOld.Id;

                List<Entry> committedEntries = new List<Entry>();
                foreach (string entryId in summarized)
                {
                    Entry committedEntry = await _Driver.Entries.ReadByIdAsync(entryId, token).ConfigureAwait(false);
                    if (committedEntry != null)
                    {
                        committedEntry.IsCommitted = true;
                        committedEntry.CommittedUtc = DateTime.Now.ToUniversalTime();
                        committedEntry.CommittedById = balanceNew.Id;
                        committedEntries.Add(committedEntry);
                    }
                }

                await _Driver.Entries.ApplyCommitAsync(committedEntries, balanceNew, token).ConfigureAwait(false);
            }

            Balance balanceAfter = await GetBalanceWithoutLockAsync(accountId, true, token).ConfigureAwait(false);
            balanceAfter.Committed = summarized;
            Task.Run(() => EntriesCommitted?.Invoke(this, new CommitEventArgs(account, balanceBefore, balanceAfter)));

            return balanceAfter;
        }

        #endregion
    }
}





