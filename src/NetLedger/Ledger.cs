namespace NetLedger
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Database;
    using NetLedger.Database.Mysql;
    using NetLedger.Database.Postgresql;
    using NetLedger.Database.Sqlite;
    using NetLedger.Database.SqlServer;

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
        private ConcurrentDictionary<Guid, SemaphoreSlim> _AccountLocks = new ConcurrentDictionary<Guid, SemaphoreSlim>();
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

            switch (settings.Type)
            {
                case DatabaseTypeEnum.Sqlite:
                    _Driver = new SqliteDatabaseDriver(settings);
                    break;
                case DatabaseTypeEnum.Mysql:
                    _Driver = new MysqlDatabaseDriver(settings);
                    break;
                case DatabaseTypeEnum.Postgresql:
                    _Driver = new PostgresqlDatabaseDriver(settings);
                    break;
                case DatabaseTypeEnum.SqlServer:
                    _Driver = new SqlServerDatabaseDriver(settings);
                    break;
                default:
                    throw new ArgumentException("Unsupported database type: " + settings.Type);
            }
        }

        #endregion

        #region Public-Account-Methods

        /// <summary>
        /// Creates an account with the specified name.
        /// </summary>
        /// <param name="name">Name of the account.</param>
        /// <param name="initialBalance">Initial balance of the account.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>GUID of the newly-created account.</returns>
        /// <exception cref="ArgumentNullException">Thrown when name is null or empty.</exception>
        public async Task<Guid> CreateAccountAsync(string name, decimal? initialBalance = null, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

            Account a = new Account(name);
            a = await _Driver.Accounts.CreateAsync(a, token).ConfigureAwait(false);
            Guid accountGuid = a.GUID;

            SemaphoreSlim accountLock = await LockAccountAsync(a.GUID, token).ConfigureAwait(false);

            try
            {
                Entry balance = new Entry();
                balance.GUID = Guid.NewGuid();
                balance.AccountGUID = a.GUID;
                balance.Type = EntryType.Balance;
                balance.Amount = initialBalance ?? 0m;
                balance.Description = "Initial balance";
                balance.IsCommitted = true;
                balance.CommittedUtc = DateTime.Now.ToUniversalTime();

                await _Driver.Entries.CreateAsync(balance, token).ConfigureAwait(false);
            }
            finally
            {
                UnlockAccount(a.GUID, accountLock);
                Task.Run(() => AccountCreated?.Invoke(this, new AccountEventArgs(a)));
            }

            return accountGuid;
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
                SemaphoreSlim accountLock = await LockAccountAsync(a.GUID, token).ConfigureAwait(false);

                try
                {
                    await _Driver.Entries.DeleteByAccountGuidAsync(a.GUID, token).ConfigureAwait(false);
                    await _Driver.Accounts.DeleteByGuidAsync(a.GUID, token).ConfigureAwait(false);
                }
                finally
                {
                    UnlockAccount(a.GUID, accountLock);
                    Task.Run(() => AccountDeleted?.Invoke(this, new AccountEventArgs(a)), token);
                }
            }
        }

        /// <summary>
        /// Delete an account and associated entries by account GUID.
        /// </summary>
        /// <param name="guid">GUID of the account.</param>
        /// <param name="token">Cancellation token.</param>
        /// <exception cref="ArgumentNullException">Thrown when guid is empty.</exception>
        public async Task DeleteAccountByGuidAsync(Guid guid, CancellationToken token = default)
        {
            if (guid == Guid.Empty) throw new ArgumentNullException(nameof(guid));

            Account a = await _Driver.Accounts.ReadByGuidAsync(guid, token).ConfigureAwait(false);
            if (a != null)
            {
                SemaphoreSlim accountLock = await LockAccountAsync(a.GUID, token).ConfigureAwait(false);

                try
                {
                    await _Driver.Entries.DeleteByAccountGuidAsync(a.GUID, token).ConfigureAwait(false);
                    await _Driver.Accounts.DeleteByGuidAsync(a.GUID, token).ConfigureAwait(false);
                }
                finally
                {
                    UnlockAccount(a.GUID, accountLock);
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
        /// Retrieve an account by GUID.
        /// </summary>
        /// <param name="guid">GUID of the account.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Account or null if it does not exist.</returns>
        /// <exception cref="ArgumentNullException">Thrown when guid is empty.</exception>
        public async Task<Account> GetAccountByGuidAsync(Guid guid, CancellationToken token = default)
        {
            if (guid == Guid.Empty) throw new ArgumentNullException(nameof(guid));
            return await _Driver.Accounts.ReadByGuidAsync(guid, token).ConfigureAwait(false);
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
        /// <param name="accountGuid">GUID of the account.</param>
        /// <param name="amount">Amount of the credit.</param>
        /// <param name="notes">Notes for the entry.</param>
        /// <param name="summarizedBy">GUID of the entry that summarized this entry.</param>
        /// <param name="isCommitted">Indicates if the entry should be immediately committed.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>GUID of the newly-created entry.</returns>
        /// <exception cref="ArgumentNullException">Thrown when accountGuid is empty.</exception>
        /// <exception cref="ArgumentException">Thrown when amount is negative.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when account is not found.</exception>
        public async Task<Guid> AddCreditAsync(Guid accountGuid, decimal amount, string notes = null, Guid? summarizedBy = null, bool isCommitted = false, CancellationToken token = default)
        {
            if (accountGuid == Guid.Empty) throw new ArgumentNullException(nameof(accountGuid));
            if (amount < 0) throw new ArgumentException("Amount must be zero or greater.");

            Account a = await _Driver.Accounts.ReadByGuidAsync(accountGuid, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with GUID " + accountGuid + ".");

            Entry entry = null;
            SemaphoreSlim accountLock = await LockAccountAsync(accountGuid, token).ConfigureAwait(false);

            try
            {
                entry = new Entry(accountGuid, EntryType.Credit, amount, notes, summarizedBy, false);
                entry = await _Driver.Entries.CreateAsync(entry, token).ConfigureAwait(false);

                Guid entryGuid = entry.GUID;

                if (isCommitted)
                {
                    List<Guid> guidsToCommit = new List<Guid> { entryGuid };
                    await CommitEntriesAsync(accountGuid, guidsToCommit, false, token).ConfigureAwait(false);
                }

                return entryGuid;
            }
            finally
            {
                UnlockAccount(accountGuid, accountLock);
                if (entry != null) Task.Run(() => CreditAdded?.Invoke(this, new EntryEventArgs(a, entry)));
            }
        }

        /// <summary>
        /// Add a debit.
        /// </summary>
        /// <param name="accountGuid">GUID of the account.</param>
        /// <param name="amount">Amount of the debit.</param>
        /// <param name="notes">Notes for the entry.</param>
        /// <param name="summarizedBy">GUID of the entry that summarized this entry.</param>
        /// <param name="isCommitted">Indicates if the entry should be immediately committed.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>GUID of the newly-created entry.</returns>
        /// <exception cref="ArgumentNullException">Thrown when accountGuid is empty.</exception>
        /// <exception cref="ArgumentException">Thrown when amount is negative.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when account is not found.</exception>
        public async Task<Guid> AddDebitAsync(Guid accountGuid, decimal amount, string notes = null, Guid? summarizedBy = null, bool isCommitted = false, CancellationToken token = default)
        {
            if (accountGuid == Guid.Empty) throw new ArgumentNullException(nameof(accountGuid));
            if (amount < 0) throw new ArgumentException("Amount must be zero or greater.");

            Account a = await _Driver.Accounts.ReadByGuidAsync(accountGuid, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with GUID " + accountGuid + ".");

            Entry entry = null;
            SemaphoreSlim accountLock = await LockAccountAsync(accountGuid, token).ConfigureAwait(false);

            try
            {
                entry = new Entry(accountGuid, EntryType.Debit, amount, notes, summarizedBy, false);
                entry = await _Driver.Entries.CreateAsync(entry, token).ConfigureAwait(false);

                Guid entryGuid = entry.GUID;

                if (isCommitted)
                {
                    List<Guid> guidsToCommit = new List<Guid> { entryGuid };
                    await CommitEntriesAsync(accountGuid, guidsToCommit, false, token).ConfigureAwait(false);
                }

                return entryGuid;
            }
            finally
            {
                UnlockAccount(accountGuid, accountLock);
                if (entry != null) Task.Run(() => DebitAdded?.Invoke(this, new EntryEventArgs(a, entry)));
            }
        }

        /// <summary>
        /// Add multiple credits in batch.
        /// </summary>
        /// <param name="accountGuid">GUID of the account.</param>
        /// <param name="credits">List of batch entry inputs containing amount and notes for each credit.</param>
        /// <param name="isCommitted">Indicates if transactions should be immediately committed.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of GUIDs for the newly-created entries.</returns>
        /// <exception cref="ArgumentNullException">Thrown when accountGuid is empty.</exception>
        /// <exception cref="ArgumentException">Thrown when credits list is null or empty.</exception>
        public async Task<List<Guid>> AddCreditsAsync(Guid accountGuid, List<BatchEntryInput> credits, bool isCommitted = false, CancellationToken token = default)
        {
            if (accountGuid == Guid.Empty) throw new ArgumentNullException(nameof(accountGuid));
            if (credits == null || credits.Count == 0) throw new ArgumentException("Credits list cannot be null or empty.");

            List<Guid> guids = new List<Guid>();
            foreach (BatchEntryInput credit in credits)
            {
                Guid guid = await AddCreditAsync(accountGuid, credit.Amount, credit.Notes, null, isCommitted, token).ConfigureAwait(false);
                guids.Add(guid);
            }
            return guids;
        }

        /// <summary>
        /// Add multiple debits in batch.
        /// </summary>
        /// <param name="accountGuid">GUID of the account.</param>
        /// <param name="debits">List of batch entry inputs containing amount and notes for each debit.</param>
        /// <param name="isCommitted">Indicates if transactions should be immediately committed.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of GUIDs for the newly-created entries.</returns>
        /// <exception cref="ArgumentNullException">Thrown when accountGuid is empty.</exception>
        /// <exception cref="ArgumentException">Thrown when debits list is null or empty.</exception>
        public async Task<List<Guid>> AddDebitsAsync(Guid accountGuid, List<BatchEntryInput> debits, bool isCommitted = false, CancellationToken token = default)
        {
            if (accountGuid == Guid.Empty) throw new ArgumentNullException(nameof(accountGuid));
            if (debits == null || debits.Count == 0) throw new ArgumentException("Debits list cannot be null or empty.");

            List<Guid> guids = new List<Guid>();
            foreach (BatchEntryInput debit in debits)
            {
                Guid guid = await AddDebitAsync(accountGuid, debit.Amount, debit.Notes, null, isCommitted, token).ConfigureAwait(false);
                guids.Add(guid);
            }
            return guids;
        }

        /// <summary>
        /// Cancel a pending entry.
        /// </summary>
        /// <param name="accountGuid">GUID of the account.</param>
        /// <param name="entryGuid">GUID of the entry.</param>
        /// <param name="token">Cancellation token.</param>
        /// <exception cref="ArgumentNullException">Thrown when accountGuid or entryGuid is empty.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when account is not found.</exception>
        /// <exception cref="InvalidOperationException">Thrown when entry is not found or already committed.</exception>
        public async Task CancelPendingAsync(Guid accountGuid, Guid entryGuid, CancellationToken token = default)
        {
            if (accountGuid == Guid.Empty) throw new ArgumentNullException(nameof(accountGuid));
            if (entryGuid == Guid.Empty) throw new ArgumentNullException(nameof(entryGuid));

            Account a = await _Driver.Accounts.ReadByGuidAsync(accountGuid, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with GUID " + accountGuid + ".");

            Entry entry = null;
            SemaphoreSlim accountLock = await LockAccountAsync(accountGuid, token).ConfigureAwait(false);

            try
            {
                entry = await _Driver.Entries.ReadByGuidAsync(entryGuid, token).ConfigureAwait(false);
                if (entry == null) throw new KeyNotFoundException("Unable to find entry with GUID " + entryGuid + ".");
                if (entry.IsCommitted) throw new InvalidOperationException("Entry has already been committed.");
                if (entry.AccountGUID != accountGuid) throw new InvalidOperationException("Entry does not belong to this account.");

                await _Driver.Entries.DeleteByGuidAsync(entryGuid, token).ConfigureAwait(false);
            }
            finally
            {
                UnlockAccount(accountGuid, accountLock);
                if (entry != null) Task.Run(() => EntryCanceled?.Invoke(this, new EntryEventArgs(a, entry)));
            }
        }

        /// <summary>
        /// Get an entry by its GUID.
        /// </summary>
        /// <param name="entryGuid">GUID of the entry.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Entry or null if not found.</returns>
        /// <exception cref="ArgumentNullException">Thrown when entryGuid is empty.</exception>
        public async Task<Entry> GetEntryAsync(Guid entryGuid, CancellationToken token = default)
        {
            if (entryGuid == Guid.Empty) throw new ArgumentNullException(nameof(entryGuid));
            return await _Driver.Entries.ReadByGuidAsync(entryGuid, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get entries for an account with optional filtering.
        /// </summary>
        /// <param name="accountGuid">GUID of the account.</param>
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
        /// <exception cref="ArgumentNullException">Thrown when accountGuid is empty.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when account is not found.</exception>
        public async Task<List<Entry>> GetEntriesAsync(
            Guid accountGuid,
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
            if (accountGuid == Guid.Empty) throw new ArgumentNullException(nameof(accountGuid));
            if (startTimeUtc.HasValue && endTimeUtc.HasValue && startTimeUtc.Value > endTimeUtc.Value)
                throw new ArgumentException("Start time must be less than or equal to end time.");

            Account a = await _Driver.Accounts.ReadByGuidAsync(accountGuid, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with GUID " + accountGuid + ".");

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

            return await _Driver.Entries.ReadWithFilterAsync(accountGuid, filter, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Search for entries within an account.
        /// </summary>
        /// <param name="accountGuid">GUID of the account.</param>
        /// <param name="startTimeUtc">Start time UTC.</param>
        /// <param name="endTimeUtc">End time UTC.</param>
        /// <param name="amountMin">Minimum amount.</param>
        /// <param name="amountMax">Maximum amount.</param>
        /// <param name="searchTerm">Search term for description.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of matching entries.</returns>
        /// <exception cref="ArgumentNullException">Thrown when accountGuid is empty.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when account is not found.</exception>
        public async Task<List<Entry>> SearchEntriesAsync(
            Guid accountGuid,
            DateTime? startTimeUtc = null,
            DateTime? endTimeUtc = null,
            decimal? amountMin = null,
            decimal? amountMax = null,
            string searchTerm = null,
            CancellationToken token = default)
        {
            if (accountGuid == Guid.Empty) throw new ArgumentNullException(nameof(accountGuid));

            Account a = await _Driver.Accounts.ReadByGuidAsync(accountGuid, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with GUID " + accountGuid + ".");

            FilterBuilder filter = new FilterBuilder
            {
                StartTimeUtc = startTimeUtc,
                EndTimeUtc = endTimeUtc,
                AmountMinimum = amountMin,
                AmountMaximum = amountMax,
                SearchTerm = searchTerm,
                ExcludeBalanceEntries = true
            };

            return await _Driver.Entries.ReadWithFilterAsync(accountGuid, filter, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Enumerate entries in a paginated way.
        /// </summary>
        /// <param name="query">Enumeration query containing pagination parameters and filters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result containing the page of entries and metadata for continuing the enumeration.</returns>
        /// <exception cref="ArgumentNullException">Thrown when query is null or AccountGUID is not specified.</exception>
        /// <exception cref="ArgumentException">Thrown when skip and continuation token are both specified.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when account is not found.</exception>
        public async Task<EnumerationResult<Entry>> EnumerateEntriesAsync(
            EnumerationQuery query,
            CancellationToken token = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (!query.AccountGUID.HasValue) throw new ArgumentNullException(nameof(query.AccountGUID), "AccountGUID must be specified for entry enumeration.");
            if (query.ContinuationToken != null && query.Skip > 0)
                throw new ArgumentException("Skip count and enumeration tokens cannot be used in the same enumeration request.");
            if (query.BalanceMinimum.HasValue || query.BalanceMaximum.HasValue)
                throw new ArgumentException("Balance filters (BalanceMinimum/BalanceMaximum) are not supported for entry enumeration. Use account enumeration instead.");

            Account a = await _Driver.Accounts.ReadByGuidAsync(query.AccountGUID.Value, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with GUID " + query.AccountGUID.Value + ".");

            return await _Driver.Entries.EnumerateAsync(query.AccountGUID.Value, query, token).ConfigureAwait(false);
        }

        #endregion

        #region Public-Balance-Methods

        /// <summary>
        /// Get the current balance for an account.
        /// </summary>
        /// <param name="accountGuid">GUID of the account.</param>
        /// <param name="includePendingEntries">Whether to include pending entries in the balance calculation.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Balance object containing committed and pending balances.</returns>
        /// <exception cref="ArgumentNullException">Thrown when accountGuid is empty.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when account is not found.</exception>
        public async Task<Balance> GetBalanceAsync(Guid accountGuid, bool includePendingEntries = true, CancellationToken token = default)
        {
            if (accountGuid == Guid.Empty) throw new ArgumentNullException(nameof(accountGuid));

            Account a = await _Driver.Accounts.ReadByGuidAsync(accountGuid, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with GUID " + accountGuid + ".");

            Balance balance = new Balance();
            balance.AccountGUID = accountGuid;
            balance.Name = a.Name;

            Entry latestBalance = await _Driver.Entries.ReadLatestBalanceAsync(accountGuid, token).ConfigureAwait(false);
            balance.CommittedBalance = latestBalance?.Amount ?? 0m;

            if (includePendingEntries)
            {
                List<Entry> pendingCredits = await _Driver.Entries.ReadPendingByAccountGuidAsync(accountGuid, EntryType.Credit, token).ConfigureAwait(false);
                List<Entry> pendingDebits = await _Driver.Entries.ReadPendingByAccountGuidAsync(accountGuid, EntryType.Debit, token).ConfigureAwait(false);

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
        /// <param name="accountGuid">GUID of the account.</param>
        /// <param name="asOfUtc">Timestamp in UTC.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Balance as of the specified timestamp.</returns>
        /// <exception cref="ArgumentNullException">Thrown when accountGuid is empty.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when account is not found.</exception>
        public async Task<decimal> GetBalanceAsOfAsync(Guid accountGuid, DateTime asOfUtc, CancellationToken token = default)
        {
            if (accountGuid == Guid.Empty) throw new ArgumentNullException(nameof(accountGuid));

            Account a = await _Driver.Accounts.ReadByGuidAsync(accountGuid, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with GUID " + accountGuid + ".");

            Entry balanceEntry = await _Driver.Entries.ReadBalanceAsOfAsync(accountGuid, asOfUtc, token).ConfigureAwait(false);
            return balanceEntry?.Amount ?? 0m;
        }

        /// <summary>
        /// Commit pending entries for an account.
        /// </summary>
        /// <param name="accountGuid">GUID of the account.</param>
        /// <param name="guids">Optional list of specific entry GUIDs to commit. If null, all pending entries are committed.</param>
        /// <param name="acquireLock">Whether to acquire the account lock.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Balance after the commit operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when accountGuid is empty.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when account is not found.</exception>
        public async Task<Balance> CommitEntriesAsync(Guid accountGuid, List<Guid> guids = null, bool acquireLock = true, CancellationToken token = default)
        {
            if (accountGuid == Guid.Empty) throw new ArgumentNullException(nameof(accountGuid));

            Account a = await _Driver.Accounts.ReadByGuidAsync(accountGuid, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with GUID " + accountGuid + ".");

            SemaphoreSlim accountLock = null;
            if (acquireLock)
            {
                accountLock = await LockAccountAsync(accountGuid, token).ConfigureAwait(false);
            }

            try
            {
                Balance balanceBefore = await GetBalanceAsync(accountGuid, true, token).ConfigureAwait(false);
                Entry balanceOld = await _Driver.Entries.ReadLatestBalanceAsync(accountGuid, token).ConfigureAwait(false);

                return await CommitEntriesInternalAsync(accountGuid, guids, balanceBefore, balanceOld, a, token).ConfigureAwait(false);
            }
            finally
            {
                if (acquireLock && accountLock != null)
                {
                    UnlockAccount(accountGuid, accountLock);
                }
            }
        }

        /// <summary>
        /// Enumerate entries for an account (alias for EnumerateEntriesAsync).
        /// </summary>
        /// <param name="query">Enumeration query containing pagination parameters and filters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result containing the page of entries and metadata for continuing the enumeration.</returns>
        /// <exception cref="ArgumentNullException">Thrown when query is null or AccountGUID is not specified.</exception>
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
        /// <param name="accountGuid">GUID of the account.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of pending entries.</returns>
        /// <exception cref="ArgumentNullException">Thrown when accountGuid is empty.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when account is not found.</exception>
        public async Task<List<Entry>> GetPendingEntriesAsync(Guid accountGuid, CancellationToken token = default)
        {
            if (accountGuid == Guid.Empty) throw new ArgumentNullException(nameof(accountGuid));

            Account a = await _Driver.Accounts.ReadByGuidAsync(accountGuid, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with GUID " + accountGuid + ".");

            return await _Driver.Entries.ReadPendingByAccountGuidAsync(accountGuid, null, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get all pending credit entries for an account.
        /// </summary>
        /// <param name="accountGuid">GUID of the account.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of pending credit entries.</returns>
        /// <exception cref="ArgumentNullException">Thrown when accountGuid is empty.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when account is not found.</exception>
        public async Task<List<Entry>> GetPendingCreditsAsync(Guid accountGuid, CancellationToken token = default)
        {
            if (accountGuid == Guid.Empty) throw new ArgumentNullException(nameof(accountGuid));

            Account a = await _Driver.Accounts.ReadByGuidAsync(accountGuid, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with GUID " + accountGuid + ".");

            return await _Driver.Entries.ReadPendingByAccountGuidAsync(accountGuid, EntryType.Credit, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get all pending debit entries for an account.
        /// </summary>
        /// <param name="accountGuid">GUID of the account.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of pending debit entries.</returns>
        /// <exception cref="ArgumentNullException">Thrown when accountGuid is empty.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when account is not found.</exception>
        public async Task<List<Entry>> GetPendingDebitsAsync(Guid accountGuid, CancellationToken token = default)
        {
            if (accountGuid == Guid.Empty) throw new ArgumentNullException(nameof(accountGuid));

            Account a = await _Driver.Accounts.ReadByGuidAsync(accountGuid, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with GUID " + accountGuid + ".");

            return await _Driver.Entries.ReadPendingByAccountGuidAsync(accountGuid, EntryType.Debit, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get balances for all accounts as a dictionary.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dictionary of account GUID to Balance objects.</returns>
        public async Task<Dictionary<Guid, Balance>> GetAllBalancesAsync(CancellationToken token = default)
        {
            return await GetAllBalancesAsync(true, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get balances for all accounts as a dictionary.
        /// </summary>
        /// <param name="includePendingEntries">Whether to include pending entries in the balance calculation.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dictionary of account GUID to Balance objects.</returns>
        public async Task<Dictionary<Guid, Balance>> GetAllBalancesAsync(bool includePendingEntries, CancellationToken token = default)
        {
            List<Account> accounts = await _Driver.Accounts.ReadAllAsync(token).ConfigureAwait(false);
            Dictionary<Guid, Balance> balances = new Dictionary<Guid, Balance>();

            foreach (Account a in accounts)
            {
                Balance balance = await GetBalanceAsync(a.GUID, includePendingEntries, token).ConfigureAwait(false);
                balances[a.GUID] = balance;
            }

            return balances;
        }

        /// <summary>
        /// Verify the balance chain for an account.
        /// </summary>
        /// <param name="accountGuid">GUID of the account.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the balance chain is valid, false otherwise.</returns>
        /// <exception cref="ArgumentNullException">Thrown when accountGuid is empty.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when account is not found.</exception>
        public async Task<bool> VerifyBalanceChainAsync(Guid accountGuid, CancellationToken token = default)
        {
            if (accountGuid == Guid.Empty) throw new ArgumentNullException(nameof(accountGuid));

            Account a = await _Driver.Accounts.ReadByGuidAsync(accountGuid, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with GUID " + accountGuid + ".");

            Entry currentBalance = await _Driver.Entries.ReadLatestBalanceAsync(accountGuid, token).ConfigureAwait(false);
            if (currentBalance == null) return true;

            HashSet<Guid> visited = new HashSet<Guid>();
            while (currentBalance != null && currentBalance.Replaces.HasValue)
            {
                if (visited.Contains(currentBalance.GUID))
                    return false;

                visited.Add(currentBalance.GUID);
                currentBalance = await _Driver.Entries.ReadByGuidAsync(currentBalance.Replaces.Value, token).ConfigureAwait(false);
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

            foreach (SemaphoreSlim semaphore in _AccountLocks.Values)
            {
                semaphore?.Dispose();
            }

            _AccountLocks.Clear();

            if (_Driver != null)
            {
                await _Driver.DisposeAsync().ConfigureAwait(false);
            }

            _Disposed = true;
        }

        #endregion

        #region Private-Account-Lock-Methods

        private async Task<SemaphoreSlim> LockAccountAsync(Guid accountGuid, CancellationToken token = default)
        {
            SemaphoreSlim accountLock = _AccountLocks.GetOrAdd(accountGuid, _ => new SemaphoreSlim(1, 1));
            await accountLock.WaitAsync(token).ConfigureAwait(false);
            return accountLock;
        }

        private void UnlockAccount(Guid accountGuid, SemaphoreSlim accountLock)
        {
            accountLock?.Release();
        }

        #endregion

        #region Private-Commit-Methods

        private async Task<Balance> CommitEntriesInternalAsync(
            Guid accountGuid,
            List<Guid> guids,
            Balance balanceBefore,
            Entry balanceOld,
            Account account,
            CancellationToken token)
        {
            List<Guid> summarized = new List<Guid>();

            // Commit credits
            decimal committedCreditsTotal = 0m;
            if (balanceBefore.PendingCredits.Entries != null && balanceBefore.PendingCredits.Entries.Count > 0)
            {
                foreach (Entry entry in balanceBefore.PendingCredits.Entries)
                {
                    if (guids != null && guids.Count > 0 && !guids.Contains(entry.GUID)) continue;
                    summarized.Add(entry.GUID);
                    entry.IsCommitted = true;
                    entry.CommittedUtc = DateTime.Now.ToUniversalTime();
                    await _Driver.Entries.UpdateAsync(entry, token).ConfigureAwait(false);
                    committedCreditsTotal += entry.Amount;
                }
            }

            // Commit debits
            decimal committedDebitsTotal = 0m;
            if (balanceBefore.PendingDebits.Entries != null && balanceBefore.PendingDebits.Entries.Count > 0)
            {
                foreach (Entry entry in balanceBefore.PendingDebits.Entries)
                {
                    if (guids != null && guids.Count > 0 && !guids.Contains(entry.GUID)) continue;
                    summarized.Add(entry.GUID);
                    entry.IsCommitted = true;
                    entry.CommittedUtc = DateTime.Now.ToUniversalTime();
                    await _Driver.Entries.UpdateAsync(entry, token).ConfigureAwait(false);
                    committedDebitsTotal += entry.Amount;
                }
            }

            if (summarized.Count > 0)
            {
                // Create new balance entry
                decimal newBalance = balanceBefore.CommittedBalance + committedCreditsTotal - committedDebitsTotal;
                Entry balanceNew = new Entry();
                balanceNew.GUID = Guid.NewGuid();
                balanceNew.AccountGUID = accountGuid;
                balanceNew.Type = EntryType.Balance;
                balanceNew.Amount = newBalance;
                balanceNew.Description = "Balance after commit";
                balanceNew.IsCommitted = true;
                balanceNew.CommittedUtc = DateTime.Now.ToUniversalTime();

                if (balanceOld != null)
                    balanceNew.Replaces = balanceOld.GUID;

                balanceNew = await _Driver.Entries.CreateAsync(balanceNew, token).ConfigureAwait(false);

                // Update committed entries with CommittedByGUID
                foreach (Guid guid in summarized)
                {
                    Entry committedEntry = await _Driver.Entries.ReadByGuidAsync(guid, token).ConfigureAwait(false);
                    if (committedEntry != null)
                    {
                        committedEntry.CommittedByGUID = balanceNew.GUID;
                        await _Driver.Entries.UpdateAsync(committedEntry, token).ConfigureAwait(false);
                    }
                }
            }

            Balance balanceAfter = await GetBalanceAsync(accountGuid, true, token).ConfigureAwait(false);
            balanceAfter.Committed = summarized;
            Task.Run(() => EntriesCommitted?.Invoke(this, new CommitEventArgs(account, balanceBefore, balanceAfter)));

            return balanceAfter;
        }

        #endregion
    }
}
