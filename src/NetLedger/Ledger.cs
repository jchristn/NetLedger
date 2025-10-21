namespace NetLedger
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Durable;
    using Durable.Sqlite;
    using Microsoft.Data.Sqlite;

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

        #endregion

        #region Private-Members

        private string _Filename = null;
        private SqliteConnectionFactory _ConnectionFactory = null;
        private SqliteRepository<Account> _AccountRepository = null;
        private SqliteRepository<Entry> _EntryRepository = null;
        private ConcurrentDictionary<Guid, SemaphoreSlim> _AccountLocks = new ConcurrentDictionary<Guid, SemaphoreSlim>();
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the ledger using Sqlite.
        /// </summary>
        /// <param name="filename">Sqlite database filename.</param>
        public Ledger(string filename)
        {
            if (String.IsNullOrEmpty(filename)) throw new ArgumentNullException(nameof(filename));

            _Filename = filename;

            ConnectionPoolOptions poolOptions = new ConnectionPoolOptions
            {
                MaxPoolSize = 500,
                ConnectionTimeout = TimeSpan.FromSeconds(120)
            };

            _ConnectionFactory = new SqliteConnectionFactory(
                $"Data Source={_Filename}",
                poolOptions
            );

            _AccountRepository = new SqliteRepository<Account>(_ConnectionFactory);
            _EntryRepository = new SqliteRepository<Entry>(_ConnectionFactory);

            InitializeDatabaseAsync(CancellationToken.None).Wait();
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
        public async Task<Guid> CreateAccountAsync(string name, decimal? initialBalance = null, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

            ITransaction transaction = null;
            Account a = null;
            Guid accountGuid = Guid.Empty;

            try
            {
                transaction = await _AccountRepository.BeginTransactionAsync(token).ConfigureAwait(false);

                a = new Account(name);
                a = await _AccountRepository.CreateAsync(a, transaction, token).ConfigureAwait(false);
                accountGuid = a.GUID;

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

                    await _EntryRepository.CreateAsync(balance, transaction, token).ConfigureAwait(false);

                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
                catch
                {
                    if (transaction != null)
                        await transaction.RollbackAsync(token).ConfigureAwait(false);
                    throw;
                }
                finally
                {
                    transaction?.Dispose();
                    UnlockAccount(a.GUID, accountLock);
                    Task.Run(() => AccountCreated?.Invoke(this, new AccountEventArgs(a)));
                }

                return accountGuid;
            }
            catch
            {
                if (transaction != null)
                    await transaction.RollbackAsync(token).ConfigureAwait(false);
                throw;
            }
            finally
            {
                transaction?.Dispose();
            }
        }

        /// <summary>
        /// Delete an account and associated entries by account name.
        /// </summary>
        /// <param name="name">Name of the account.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task DeleteAccountByNameAsync(string name, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

            Account a = await GetAccountByNameInternalAsync(name, token).ConfigureAwait(false);
            if (a != null)
            {
                ITransaction transaction = null;
                SemaphoreSlim accountLock = await LockAccountAsync(a.GUID, token).ConfigureAwait(false);

                try
                {
                    transaction = await _EntryRepository.BeginTransactionAsync(token).ConfigureAwait(false);

                    List<Entry> entries = (await _EntryRepository.Query(transaction)
                        .Where(e => e.AccountGUID == a.GUID)
                        .ExecuteAsync(token).ConfigureAwait(false)).ToList();

                    foreach (Entry entry in entries)
                    {
                        await _EntryRepository.DeleteAsync(entry, transaction, token).ConfigureAwait(false);
                    }

                    await _AccountRepository.DeleteAsync(a, transaction, token).ConfigureAwait(false);

                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
                catch
                {
                    if (transaction != null)
                        await transaction.RollbackAsync(token).ConfigureAwait(false);
                    throw;
                }
                finally
                {
                    transaction?.Dispose();
                    UnlockAccount(a.GUID, accountLock);
                    Task.Run(() => AccountDeleted?.Invoke(this, new AccountEventArgs(a)));
                }
            }
        }

        /// <summary>
        /// Delete an account and associated entries by account GUID.
        /// </summary>
        /// <param name="guid">GUID of the account.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task DeleteAccountByGuidAsync(Guid guid, CancellationToken token = default)
        {
            if (guid == Guid.Empty) throw new ArgumentNullException(nameof(guid));

            Account a = await GetAccountByGuidInternalAsync(guid, token).ConfigureAwait(false);
            if (a != null)
            {
                ITransaction transaction = null;
                SemaphoreSlim accountLock = await LockAccountAsync(a.GUID, token).ConfigureAwait(false);

                try
                {
                    transaction = await _EntryRepository.BeginTransactionAsync(token).ConfigureAwait(false);

                    List<Entry> entries = (await _EntryRepository.Query(transaction)
                        .Where(e => e.AccountGUID == a.GUID)
                        .ExecuteAsync(token).ConfigureAwait(false)).ToList();

                    foreach (Entry entry in entries)
                    {
                        await _EntryRepository.DeleteAsync(entry, transaction, token).ConfigureAwait(false);
                    }

                    await _AccountRepository.DeleteAsync(a, transaction, token).ConfigureAwait(false);

                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
                catch
                {
                    if (transaction != null)
                        await transaction.RollbackAsync(token).ConfigureAwait(false);
                    throw;
                }
                finally
                {
                    transaction?.Dispose();
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
        public async Task<Account> GetAccountByNameAsync(string name, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            return await GetAccountByNameInternalAsync(name, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieve an account by GUID.
        /// </summary>
        /// <param name="guid">GUID of the account.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Account or null if it does not exist.</returns>
        public async Task<Account> GetAccountByGuidAsync(Guid guid, CancellationToken token = default)
        {
            if (guid == Guid.Empty) throw new ArgumentNullException(nameof(guid));
            return await GetAccountByGuidInternalAsync(guid, token).ConfigureAwait(false);
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
            return await GetAllAccountsInternalAsync(searchTerm, skip, take, token).ConfigureAwait(false);
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
        public async Task<Guid> AddCreditAsync(Guid accountGuid, decimal amount, string notes = null, Guid? summarizedBy = null, bool isCommitted = false, CancellationToken token = default)
        {
            if (accountGuid == Guid.Empty) throw new ArgumentNullException(nameof(accountGuid));
            if (amount < 0) throw new ArgumentException("Amount must be zero or greater.");

            Account a = await GetAccountByGuidInternalAsync(accountGuid, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with GUID " + accountGuid + ".");

            Entry entry = null;
            ITransaction transaction = null;
            SemaphoreSlim accountLock = await LockAccountAsync(accountGuid, token).ConfigureAwait(false);

            try
            {
                transaction = await _EntryRepository.BeginTransactionAsync(token).ConfigureAwait(false);

                entry = new Entry(accountGuid, EntryType.Credit, amount, notes, summarizedBy, isCommitted);
                entry = await _EntryRepository.CreateAsync(entry, transaction, token).ConfigureAwait(false);

                await transaction.CommitAsync(token).ConfigureAwait(false);

                return entry.GUID;
            }
            catch
            {
                if (transaction != null)
                    await transaction.RollbackAsync(token).ConfigureAwait(false);
                throw;
            }
            finally
            {
                transaction?.Dispose();
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
        public async Task<Guid> AddDebitAsync(Guid accountGuid, decimal amount, string notes = null, Guid? summarizedBy = null, bool isCommitted = false, CancellationToken token = default)
        {
            if (accountGuid == Guid.Empty) throw new ArgumentNullException(nameof(accountGuid));
            if (amount < 0) throw new ArgumentException("Amount must be zero or greater.");

            Account a = await GetAccountByGuidInternalAsync(accountGuid, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with GUID " + accountGuid + ".");

            Entry entry = null;
            ITransaction transaction = null;
            SemaphoreSlim accountLock = await LockAccountAsync(accountGuid, token).ConfigureAwait(false);

            try
            {
                transaction = await _EntryRepository.BeginTransactionAsync(token).ConfigureAwait(false);

                entry = new Entry(accountGuid, EntryType.Debit, amount, notes, summarizedBy, isCommitted);
                entry = await _EntryRepository.CreateAsync(entry, transaction, token).ConfigureAwait(false);

                await transaction.CommitAsync(token).ConfigureAwait(false);

                return entry.GUID;
            }
            catch
            {
                if (transaction != null)
                    await transaction.RollbackAsync(token).ConfigureAwait(false);
                throw;
            }
            finally
            {
                transaction?.Dispose();
                UnlockAccount(accountGuid, accountLock);
                if (entry != null) Task.Run(() => DebitAdded?.Invoke(this, new EntryEventArgs(a, entry)));
            }
        }

        /// <summary>
        /// Add multiple credits in batch.
        /// </summary>
        /// <param name="accountGuid">GUID of the account.</param>
        /// <param name="credits">List of tuples containing (amount, notes) for each credit.</param>
        /// <param name="isCommitted">Indicates if transactions should be immediately committed.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of GUIDs for the newly-created entries.</returns>
        public async Task<List<Guid>> AddCreditsAsync(Guid accountGuid, List<(decimal amount, string notes)> credits, bool isCommitted = false, CancellationToken token = default)
        {
            if (accountGuid == Guid.Empty) throw new ArgumentNullException(nameof(accountGuid));
            if (credits == null || credits.Count == 0) throw new ArgumentException("Credits list cannot be null or empty.");

            List<Guid> guids = new List<Guid>();
            foreach ((decimal amount, string notes) credit in credits)
            {
                Guid guid = await AddCreditAsync(accountGuid, credit.amount, credit.notes, null, isCommitted, token).ConfigureAwait(false);
                guids.Add(guid);
            }
            return guids;
        }

        /// <summary>
        /// Add multiple debits in batch.
        /// </summary>
        /// <param name="accountGuid">GUID of the account.</param>
        /// <param name="debits">List of tuples containing (amount, notes) for each debit.</param>
        /// <param name="isCommitted">Indicates if transactions should be immediately committed.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of GUIDs for the newly-created entries.</returns>
        public async Task<List<Guid>> AddDebitsAsync(Guid accountGuid, List<(decimal amount, string notes)> debits, bool isCommitted = false, CancellationToken token = default)
        {
            if (accountGuid == Guid.Empty) throw new ArgumentNullException(nameof(accountGuid));
            if (debits == null || debits.Count == 0) throw new ArgumentException("Debits list cannot be null or empty.");

            List<Guid> guids = new List<Guid>();
            foreach ((decimal amount, string notes) debit in debits)
            {
                Guid guid = await AddDebitAsync(accountGuid, debit.amount, debit.notes, null, isCommitted, token).ConfigureAwait(false);
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
        public async Task CancelPendingAsync(Guid accountGuid, Guid entryGuid, CancellationToken token = default)
        {
            if (accountGuid == Guid.Empty) throw new ArgumentNullException(nameof(accountGuid));
            if (entryGuid == Guid.Empty) throw new ArgumentNullException(nameof(entryGuid));

            Account a = await GetAccountByGuidInternalAsync(accountGuid, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with GUID " + accountGuid + ".");

            Entry entry = null;
            ITransaction transaction = null;
            SemaphoreSlim accountLock = await LockAccountAsync(accountGuid, token).ConfigureAwait(false);

            try
            {
                transaction = await _EntryRepository.BeginTransactionAsync(token).ConfigureAwait(false);

                entry = await GetPendingEntryInternalAsync(accountGuid, entryGuid, transaction, token).ConfigureAwait(false);
                if (entry == null) throw new KeyNotFoundException("Unable to find pending entry with GUID " + entryGuid + ".");

                await _EntryRepository.DeleteAsync(entry, transaction, token).ConfigureAwait(false);

                await transaction.CommitAsync(token).ConfigureAwait(false);
            }
            catch
            {
                if (transaction != null)
                    await transaction.RollbackAsync(token).ConfigureAwait(false);
                throw;
            }
            finally
            {
                transaction?.Dispose();
                UnlockAccount(accountGuid, accountLock);
                if (entry != null) Task.Run(() => EntryCanceled?.Invoke(this, new EntryEventArgs(a, entry)));
            }
        }

        /// <summary>
        /// Retrieve a list of pending entries for a given account.
        /// </summary>
        /// <param name="accountGuid">GUID of the account.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of pending entries.</returns>
        public async Task<List<Entry>> GetPendingEntriesAsync(Guid accountGuid, CancellationToken token = default)
        {
            if (accountGuid == Guid.Empty) throw new ArgumentNullException(nameof(accountGuid));

            Account a = await GetAccountByGuidInternalAsync(accountGuid, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with GUID " + accountGuid + ".");

            SemaphoreSlim accountLock = await LockAccountAsync(accountGuid, token).ConfigureAwait(false);

            try
            {
                return await GetPendingEntriesInternalAsync(accountGuid, token).ConfigureAwait(false);
            }
            finally
            {
                UnlockAccount(accountGuid, accountLock);
            }
        }

        /// <summary>
        /// Retrieve a list of pending credits for a given account.
        /// </summary>
        /// <param name="accountGuid">GUID of the account.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of pending credits.</returns>
        public async Task<List<Entry>> GetPendingCreditsAsync(Guid accountGuid, CancellationToken token = default)
        {
            if (accountGuid == Guid.Empty) throw new ArgumentNullException(nameof(accountGuid));

            Account a = await GetAccountByGuidInternalAsync(accountGuid, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with GUID " + accountGuid + ".");

            SemaphoreSlim accountLock = await LockAccountAsync(accountGuid, token).ConfigureAwait(false);

            try
            {
                int creditTypeInt = (int)EntryType.Credit;

                List<Entry> results = (await _EntryRepository.Query()
                    .Where(e => e.AccountGUID == accountGuid)
                    .Where(e => !e.IsCommitted)
                    .Where(e => (int)e.Type == creditTypeInt)
                    .OrderByDescending(e => e.CreatedUtc)
                    .ExecuteAsync(token).ConfigureAwait(false)).ToList();

                return results;
            }
            finally
            {
                UnlockAccount(accountGuid, accountLock);
            }
        }

        /// <summary>
        /// Retrieve a list of pending debits for a given account.
        /// </summary>
        /// <param name="accountGuid">GUID of the account.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of pending debits.</returns>
        public async Task<List<Entry>> GetPendingDebitsAsync(Guid accountGuid, CancellationToken token = default)
        {
            if (accountGuid == Guid.Empty) throw new ArgumentNullException(nameof(accountGuid));

            Account a = await GetAccountByGuidInternalAsync(accountGuid, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with GUID " + accountGuid + ".");

            SemaphoreSlim accountLock = await LockAccountAsync(accountGuid, token).ConfigureAwait(false);

            try
            {
                int debitTypeInt = (int)EntryType.Debit;

                List<Entry> results = (await _EntryRepository.Query()
                    .Where(e => e.AccountGUID == accountGuid)
                    .Where(e => !e.IsCommitted)
                    .Where(e => (int)e.Type == debitTypeInt)
                    .OrderByDescending(e => e.CreatedUtc)
                    .ExecuteAsync(token).ConfigureAwait(false)).ToList();

                return results;
            }
            finally
            {
                UnlockAccount(accountGuid, accountLock);
            }
        }

        /// <summary>
        /// Retrieve a list of entries matching the specified conditions.
        /// </summary>
        /// <param name="accountGuid">GUID of the account.</param>
        /// <param name="startTimeUtc">Start time UTC.</param>
        /// <param name="endTimeUtc">End time UTC.</param>
        /// <param name="searchTerm">Search term that must appear in the entry description.</param>
        /// <param name="entryType">The type of entry.</param>
        /// <param name="amountMin">Minimum amount.</param>
        /// <param name="amountMax">Maximum amount.</param>
        /// <param name="skip">Number of records to skip.</param>
        /// <param name="take">Number of records to take.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of matching entries.</returns>
        public async Task<List<Entry>> GetEntriesAsync(
            Guid accountGuid,
            DateTime? startTimeUtc = null,
            DateTime? endTimeUtc = null,
            string searchTerm = null,
            EntryType? entryType = null,
            decimal? amountMin = null,
            decimal? amountMax = null,
            int? skip = null,
            int? take = null,
            CancellationToken token = default)
        {
            if (startTimeUtc != null && endTimeUtc != null)
            {
                if (DateTime.Compare(Convert.ToDateTime(endTimeUtc), Convert.ToDateTime(startTimeUtc)) < 0)
                {
                    throw new ArgumentException("Specified end time must be later than the specified start time.");
                }
            }

            if (amountMin != null && amountMin.Value < 0) throw new ArgumentException("Minimum amount must be zero or greater.");
            if (amountMax != null && amountMax.Value < 0) throw new ArgumentException("Maximum amount must be zero or greater.");

            if (accountGuid == Guid.Empty) throw new ArgumentNullException(nameof(accountGuid));

            Account a = await GetAccountByGuidInternalAsync(accountGuid, token).ConfigureAwait(false);
            if (a == null) throw new KeyNotFoundException("Unable to find account with GUID " + accountGuid + ".");

            SemaphoreSlim accountLock = await LockAccountAsync(accountGuid, token).ConfigureAwait(false);

            try
            {
                IQueryBuilder<Entry> query = _EntryRepository.Query()
                    .Where(e => e.AccountGUID == accountGuid);

                if (startTimeUtc != null)
                    query = query.Where(e => e.CreatedUtc >= startTimeUtc.Value);

                if (endTimeUtc != null)
                    query = query.Where(e => e.CreatedUtc <= endTimeUtc.Value);

                if (!String.IsNullOrEmpty(searchTerm))
                    query = query.Where(e => e.Description.Contains(searchTerm));

                if (amountMin != null)
                    query = query.Where(e => e.Amount >= amountMin.Value);

                if (amountMax != null)
                    query = query.Where(e => e.Amount <= amountMax.Value);

                if (entryType != null)
                {
                    int entryTypeInt = (int)entryType.Value;
                    query = query.Where(e => (int)e.Type == entryTypeInt);
                }
                else
                {
                    int balanceTypeInt = (int)EntryType.Balance;
                    query = query.Where(e => (int)e.Type != balanceTypeInt);
                }

                query = query.OrderByDescending(e => e.CreatedUtc);

                if (skip != null)
                    query = query.Skip(skip.Value);

                if (take != null)
                    query = query.Take(take.Value);

                List<Entry> results = (await query.ExecuteAsync(token).ConfigureAwait(false)).ToList();
                return results;
            }
            finally
            {
                UnlockAccount(accountGuid, accountLock);
            }
        }

        /// <summary>
        /// Enumerate transactions in a paginated way.
        /// </summary>
        /// <param name="query">Enumeration query containing account GUID, pagination parameters, and ordering.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumeration result containing the page of entries and metadata for continuing the enumeration.</returns>
        /// <exception cref="ArgumentNullException">Thrown when query is null.</exception>
        /// <exception cref="ArgumentException">Thrown when skip and continuation token are both specified.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the specified account does not exist.</exception>
        public async Task<EnumerationResult<Entry>> EnumerateTransactionsAsync(
            EnumerationQuery query,
            CancellationToken token = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (query.AccountGUID == Guid.Empty) throw new ArgumentNullException(nameof(query.AccountGUID));
            if (query.ContinuationToken != null && query.Skip > 0)
                throw new ArgumentException("Skip count and enumeration tokens cannot be used in the same enumeration request.");

            Account account = await GetAccountByGuidInternalAsync(query.AccountGUID, token).ConfigureAwait(false);
            if (account == null) throw new KeyNotFoundException("Unable to find account with GUID " + query.AccountGUID + ".");

            EnumerationResult<Entry> result = new EnumerationResult<Entry>
            {
                MaxResults = query.MaxResults,
                Skip = query.Skip
            };

            SemaphoreSlim accountLock = await LockAccountAsync(query.AccountGUID, token).ConfigureAwait(false);

            try
            {
                // Build base query for total count
                IQueryBuilder<Entry> countQuery = _EntryRepository.Query()
                    .Where(e => e.AccountGUID == query.AccountGUID);

                // Exclude balance entries from enumeration
                int balanceTypeInt = (int)EntryType.Balance;
                countQuery = countQuery.Where(e => (int)e.Type != balanceTypeInt);

                // Apply date filters
                if (query.CreatedAfter != null)
                    countQuery = countQuery.Where(e => e.CreatedUtc >= query.CreatedAfter.Value);

                if (query.CreatedBefore != null)
                    countQuery = countQuery.Where(e => e.CreatedUtc <= query.CreatedBefore.Value);

                List<Entry> totalEntries = (await countQuery.ExecuteAsync(token).ConfigureAwait(false)).ToList();
                result.TotalRecords = totalEntries.Count;

                // Build query for this page
                IQueryBuilder<Entry> pageQuery = _EntryRepository.Query()
                    .Where(e => e.AccountGUID == query.AccountGUID)
                    .Where(e => (int)e.Type != balanceTypeInt);

                // Apply date filters
                if (query.CreatedAfter != null)
                    pageQuery = pageQuery.Where(e => e.CreatedUtc >= query.CreatedAfter.Value);

                if (query.CreatedBefore != null)
                    pageQuery = pageQuery.Where(e => e.CreatedUtc <= query.CreatedBefore.Value);

                // Handle continuation token
                DateTime? lastCreatedOrAmount = await GetCreatedUtcFromEntryGuidAsync(query.ContinuationToken, token).ConfigureAwait(false);
                if (lastCreatedOrAmount != null)
                {
                    if (query.Ordering == EnumerationOrderEnum.CreatedDescending || query.Ordering == EnumerationOrderEnum.AmountDescending)
                    {
                        if (query.Ordering == EnumerationOrderEnum.CreatedDescending)
                            pageQuery = pageQuery.Where(e => e.CreatedUtc < lastCreatedOrAmount.Value);
                        else
                        {
                            decimal? lastAmount = await GetAmountFromEntryGuidAsync(query.ContinuationToken.Value, token).ConfigureAwait(false);
                            if (lastAmount != null)
                                pageQuery = pageQuery.Where(e => e.Amount < lastAmount.Value);
                        }
                    }
                    else
                    {
                        if (query.Ordering == EnumerationOrderEnum.CreatedAscending)
                            pageQuery = pageQuery.Where(e => e.CreatedUtc > lastCreatedOrAmount.Value);
                        else
                        {
                            decimal? lastAmount = await GetAmountFromEntryGuidAsync(query.ContinuationToken.Value, token).ConfigureAwait(false);
                            if (lastAmount != null)
                                pageQuery = pageQuery.Where(e => e.Amount > lastAmount.Value);
                        }
                    }
                }

                // Apply ordering
                switch (query.Ordering)
                {
                    case EnumerationOrderEnum.CreatedAscending:
                        pageQuery = pageQuery.OrderBy(e => e.CreatedUtc);
                        break;
                    case EnumerationOrderEnum.CreatedDescending:
                        pageQuery = pageQuery.OrderByDescending(e => e.CreatedUtc);
                        break;
                    case EnumerationOrderEnum.AmountAscending:
                        pageQuery = pageQuery.OrderBy(e => e.Amount);
                        break;
                    case EnumerationOrderEnum.AmountDescending:
                        pageQuery = pageQuery.OrderByDescending(e => e.Amount);
                        break;
                }

                // Handle skip
                if (query.Skip > 0)
                {
                    IQueryBuilder<Entry> skipQuery = _EntryRepository.Query()
                        .Where(e => e.AccountGUID == query.AccountGUID)
                        .Where(e => (int)e.Type != balanceTypeInt);

                    // Apply date filters to skip query
                    if (query.CreatedAfter != null)
                        skipQuery = skipQuery.Where(e => e.CreatedUtc >= query.CreatedAfter.Value);

                    if (query.CreatedBefore != null)
                        skipQuery = skipQuery.Where(e => e.CreatedUtc <= query.CreatedBefore.Value);

                    // Apply same ordering to skip query
                    switch (query.Ordering)
                    {
                        case EnumerationOrderEnum.CreatedAscending:
                            skipQuery = skipQuery.OrderBy(e => e.CreatedUtc);
                            break;
                        case EnumerationOrderEnum.CreatedDescending:
                            skipQuery = skipQuery.OrderByDescending(e => e.CreatedUtc);
                            break;
                        case EnumerationOrderEnum.AmountAscending:
                            skipQuery = skipQuery.OrderBy(e => e.Amount);
                            break;
                        case EnumerationOrderEnum.AmountDescending:
                            skipQuery = skipQuery.OrderByDescending(e => e.Amount);
                            break;
                    }

                    skipQuery = skipQuery.Take(query.Skip);
                    List<Entry> skippedResults = (await skipQuery.ExecuteAsync(token).ConfigureAwait(false)).ToList();

                    if (skippedResults != null && skippedResults.Count == query.Skip)
                    {
                        if (query.Ordering == EnumerationOrderEnum.CreatedDescending || query.Ordering == EnumerationOrderEnum.CreatedAscending)
                        {
                            DateTime skipCreated = query.Ordering == EnumerationOrderEnum.CreatedDescending
                                ? skippedResults.Min(r => r.CreatedUtc)
                                : skippedResults.Max(r => r.CreatedUtc);

                            if (query.Ordering == EnumerationOrderEnum.CreatedDescending)
                                pageQuery = pageQuery.Where(e => e.CreatedUtc < skipCreated);
                            else
                                pageQuery = pageQuery.Where(e => e.CreatedUtc > skipCreated);
                        }
                        else
                        {
                            decimal skipAmount = query.Ordering == EnumerationOrderEnum.AmountDescending
                                ? skippedResults.Min(r => r.Amount)
                                : skippedResults.Max(r => r.Amount);

                            if (query.Ordering == EnumerationOrderEnum.AmountDescending)
                                pageQuery = pageQuery.Where(e => e.Amount < skipAmount);
                            else
                                pageQuery = pageQuery.Where(e => e.Amount > skipAmount);
                        }
                    }
                }

                // Count remaining records after applying filters
                List<Entry> remainingEntries = (await pageQuery.ExecuteAsync(token).ConfigureAwait(false)).ToList();
                result.RecordsRemaining = remainingEntries.Count;

                // Get the actual page
                pageQuery = pageQuery.Take(query.MaxResults);
                result.Objects = (await pageQuery.ExecuteAsync(token).ConfigureAwait(false)).ToList();
                result.IterationsRequired = 1;

                if (result.Objects == null) result.Objects = new List<Entry>();
                result.RecordsRemaining -= result.Objects.Count;

                if (result.Objects != null
                    && result.Objects.Count > 0
                    && result.RecordsRemaining > 0)
                {
                    result.EndOfResults = false;
                    result.ContinuationToken = result.Objects.Last().GUID;
                }

                return result;
            }
            finally
            {
                UnlockAccount(query.AccountGUID, accountLock);
            }
        }

        #endregion

        #region Public-Ledgering-Methods

        /// <summary>
        /// Retrieve balance details for a given account.
        /// </summary>
        /// <param name="accountGuid">GUID of the account.</param>
        /// <param name="applyLock">Indicate whether or not the account should be locked during retrieval of balance details. Leave this value as 'true'.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Balance details.</returns>
        public async Task<Balance> GetBalanceAsync(Guid accountGuid, bool applyLock = true, CancellationToken token = default)
        {
            if (accountGuid == Guid.Empty) throw new ArgumentNullException(nameof(accountGuid));

            Account account = null;
            Balance balance = new Balance();
            balance.AccountGUID = accountGuid;

            SemaphoreSlim accountLock = null;
            if (applyLock)
                accountLock = await LockAccountAsync(accountGuid, token).ConfigureAwait(false);

            try
            {
                account = await GetAccountByGuidInternalAsync(accountGuid, token).ConfigureAwait(false);
                if (account == null) throw new KeyNotFoundException("Unable to find account with GUID " + accountGuid + ".");

                balance.Name = account.Name;
                balance.CreatedUtc = account.CreatedUtc;

                Entry balanceEntry = await GetLatestBalanceEntryInternalAsync(accountGuid, token).ConfigureAwait(false);
                if (balanceEntry != null)
                {
                    balance.EntryGUID = balanceEntry.GUID;
                    balance.CommittedBalance = balanceEntry.Amount;
                    balance.BalanceTimestampUtc = balanceEntry.CreatedUtc;
                }

                List<Entry> pendingEntries = await GetPendingEntriesInternalAsync(accountGuid, token).ConfigureAwait(false);

                if (pendingEntries != null && pendingEntries.Count > 0)
                {
                    foreach (Entry entry in pendingEntries)
                    {
                        if (entry.Type == EntryType.Credit)
                        {
                            balance.PendingCredits.Count++;
                            balance.PendingCredits.Total += entry.Amount;
                            balance.PendingCredits.Entries.Add(entry);
                        }
                        else if (entry.Type == EntryType.Debit)
                        {
                            balance.PendingDebits.Count++;
                            balance.PendingDebits.Total += entry.Amount;
                            balance.PendingDebits.Entries.Add(entry);
                        }
                    }
                }

                balance.PendingBalance = balance.CommittedBalance - balance.PendingDebits.Total + balance.PendingCredits.Total;
                return balance;
            }
            finally
            {
                if (applyLock && accountLock != null) UnlockAccount(accountGuid, accountLock);
            }
        }

        /// <summary>
        /// Commit pending entries to the balance.
        /// Specify entries to commit using the guids property, or leave it null to commit all pending entries.
        /// </summary>
        /// <param name="accountGuid">GUID of the account.</param>
        /// <param name="guids">List of entry GUIDs to commit. Leave null to commit all pending entries.</param>
        /// <param name="applyLock">Indicate whether or not the account should be locked during retrieval of balance details. Leave this value as 'true'.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Balance details.</returns>
        public async Task<Balance> CommitEntriesAsync(Guid accountGuid, List<Guid> guids = null, bool applyLock = true, CancellationToken token = default)
        {
            if (accountGuid == Guid.Empty) throw new ArgumentNullException(nameof(accountGuid));

            Account account = null;
            Balance balanceBefore = null;
            Balance balanceAfter = null;
            List<Guid> summarized = new List<Guid>();

            SemaphoreSlim accountLock = null;
            if (applyLock)
                accountLock = await LockAccountAsync(accountGuid, token).ConfigureAwait(false);

            try
            {
                account = await GetAccountByGuidInternalAsync(accountGuid, token).ConfigureAwait(false);
                balanceBefore = await GetBalanceAsync(accountGuid, false, token).ConfigureAwait(false);
                Entry balanceOld = await GetLatestBalanceEntryInternalAsync(accountGuid, token).ConfigureAwait(false);

                // validate requested GUIDs
                if (guids != null && guids.Count > 0)
                {
                    List<Entry> requestedEntries = await GetEntriesByGuidsAsync(accountGuid, guids, token).ConfigureAwait(false);
                    if (requestedEntries == null || requestedEntries.Count != guids.Count)
                    {
                        throw new KeyNotFoundException("One or more requested entries to commit were not found.");
                    }

                    foreach (Entry entry in requestedEntries)
                    {
                        if (entry.Type != EntryType.Debit && entry.Type != EntryType.Credit)
                        {
                            throw new InvalidOperationException("Commit cannot be performed on entry with GUID " + entry.GUID + " because it is not of type Debit or Credit.");
                        }

                        if (entry.IsCommitted)
                        {
                            throw new InvalidOperationException("Commit cannot be performed on entry with GUID " + entry.GUID + " because it is already committed.");
                        }

                        if (entry.CommittedUtc != null)
                        {
                            throw new InvalidOperationException("Commit cannot be performed on entry with GUID " + entry.GUID + " because it already contains a committed timestamp.");
                        }
                    }

                    foreach (Guid guid in guids)
                    {
                        if (!requestedEntries.Any(e => e.GUID == guid))
                        {
                            throw new KeyNotFoundException("No entry found with GUID " + guid + ".");
                        }
                    }
                }

                return await CommitEntriesInternalAsync(accountGuid, guids, balanceBefore, balanceOld, token).ConfigureAwait(false);
            }
            finally
            {
                if (applyLock && accountLock != null) UnlockAccount(accountGuid, accountLock);
            }
        }

        /// <summary>
        /// Get the balance of an account as of a specific timestamp.
        /// </summary>
        /// <param name="accountGuid">GUID of the account.</param>
        /// <param name="asOfUtc">Timestamp UTC.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Balance as of the specified timestamp.</returns>
        public async Task<decimal> GetBalanceAsOfAsync(Guid accountGuid, DateTime asOfUtc, CancellationToken token = default)
        {
            if (accountGuid == Guid.Empty) throw new ArgumentNullException(nameof(accountGuid));

            SemaphoreSlim accountLock = await LockAccountAsync(accountGuid, token).ConfigureAwait(false);

            try
            {
                List<Entry> balanceEntries = (await _EntryRepository.Query()
                    .Where(e => e.AccountGUID == accountGuid)
                    .Where(e => (int)e.Type == (int)EntryType.Balance)
                    .Where(e => e.CreatedUtc <= asOfUtc)
                    .OrderByDescending(e => e.CreatedUtc)
                    .Take(1)
                    .ExecuteAsync(token).ConfigureAwait(false)).ToList();

                if (balanceEntries == null || balanceEntries.Count == 0)
                    return 0m;

                return balanceEntries[0].Amount;
            }
            finally
            {
                UnlockAccount(accountGuid, accountLock);
            }
        }

        /// <summary>
        /// Get balances for all accounts.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dictionary mapping account GUID to Balance object.</returns>
        public async Task<Dictionary<Guid, Balance>> GetAllBalancesAsync(CancellationToken token = default)
        {
            List<Account> accounts = await GetAllAccountsAsync(null, null, null, token).ConfigureAwait(false);

            Dictionary<Guid, Balance> balances = new Dictionary<Guid, Balance>();

            if (accounts != null && accounts.Count > 0)
            {
                foreach (Account account in accounts)
                {
                    Balance balance = await GetBalanceAsync(account.GUID, true, token).ConfigureAwait(false);
                    balances[account.GUID] = balance;
                }
            }

            return balances;
        }

        /// <summary>
        /// Verify the balance chain for an account.
        /// </summary>
        /// <param name="accountGuid">GUID of the account.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the chain is valid.</returns>
        public async Task<bool> VerifyBalanceChainAsync(Guid accountGuid, CancellationToken token = default)
        {
            if (accountGuid == Guid.Empty) throw new ArgumentNullException(nameof(accountGuid));

            SemaphoreSlim accountLock = await LockAccountAsync(accountGuid, token).ConfigureAwait(false);

            try
            {
                int balanceTypeInt = (int)EntryType.Balance;

                List<Entry> balanceEntries = (await _EntryRepository.Query()
                    .Where(e => e.AccountGUID == accountGuid)
                    .Where(e => (int)e.Type == balanceTypeInt)
                    .OrderByDescending(e => e.CreatedUtc)
                    .ExecuteAsync(token).ConfigureAwait(false)).ToList();

                if (balanceEntries == null || balanceEntries.Count == 0) return true;
                if (balanceEntries.Count == 1) return true;

                for (int i = 0; i < balanceEntries.Count - 1; i++)
                {
                    Entry current = balanceEntries[i];
                    Entry next = balanceEntries[i + 1];

                    if (current.Replaces == null) return false;
                    if (current.Replaces.Value != next.GUID) return false;
                }

                return true;
            }
            finally
            {
                UnlockAccount(accountGuid, accountLock);
            }
        }

        #endregion

        #region Private-Initialization-Methods

        private async Task InitializeDatabaseAsync(CancellationToken token)
        {
            using (SqliteConnection connection = (SqliteConnection)_ConnectionFactory.GetConnection())
            {
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS accounts (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            guid TEXT NOT NULL,
                            name TEXT NOT NULL,
                            notes TEXT,
                            createdutc TEXT NOT NULL
                        )";
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);

                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS entries (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            guid TEXT NOT NULL,
                            accountguid TEXT NOT NULL,
                            type INTEGER NOT NULL,
                            amount REAL NOT NULL,
                            description TEXT,
                            replaces TEXT,
                            committed INTEGER NOT NULL,
                            committedbyguid TEXT,
                            committedutc TEXT,
                            createdutc TEXT NOT NULL
                        )";
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        #endregion

        #region Private-Locking-Methods

        private async Task<SemaphoreSlim> LockAccountAsync(Guid accountGuid, CancellationToken token)
        {
            SemaphoreSlim semaphore = _AccountLocks.GetOrAdd(accountGuid, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync(token).ConfigureAwait(false);
            return semaphore;
        }

        private void UnlockAccount(Guid accountGuid, SemaphoreSlim semaphore)
        {
            semaphore.Release();
        }

        #endregion

        #region Private-Query-Methods

        private async Task<List<Account>> GetAllAccountsInternalAsync(string searchTerm = null, int? skip = null, int? take = null, CancellationToken token = default)
        {
            IQueryBuilder<Account> query = _AccountRepository.Query()
                .Where(a => a.Id > 0)
                .OrderByDescending(a => a.CreatedUtc);

            if (!String.IsNullOrEmpty(searchTerm))
                query = query.Where(a => a.Name.Contains(searchTerm));

            if (skip != null)
                query = query.Skip(skip.Value);

            if (take != null)
                query = query.Take(take.Value);

            List<Account> results = (await query.ExecuteAsync(token).ConfigureAwait(false)).ToList();
            return results;
        }

        private async Task<Account> GetAccountByNameInternalAsync(string name, CancellationToken token = default)
        {
            List<Account> results = (await _AccountRepository.Query()
                .Where(a => a.Name == name)
                .Take(1)
                .ExecuteAsync(token).ConfigureAwait(false)).ToList();

            return results.FirstOrDefault();
        }

        private async Task<Account> GetAccountByGuidInternalAsync(Guid guid, CancellationToken token = default)
        {
            List<Account> results = (await _AccountRepository.Query()
                .Where(a => a.GUID == guid)
                .Take(1)
                .ExecuteAsync(token).ConfigureAwait(false)).ToList();

            return results.FirstOrDefault();
        }

        private async Task<Entry> GetPendingEntryInternalAsync(Guid accountGuid, Guid entryGuid, ITransaction transaction = null, CancellationToken token = default)
        {
            IQueryBuilder<Entry> query = transaction != null
                ? _EntryRepository.Query(transaction)
                : _EntryRepository.Query();

            List<Entry> results = (await query
                .Where(e => e.AccountGUID == accountGuid)
                .Where(e => e.GUID == entryGuid)
                .Where(e => !e.IsCommitted)
                .Take(1)
                .ExecuteAsync(token).ConfigureAwait(false)).ToList();

            return results.FirstOrDefault();
        }

        private async Task<List<Entry>> GetPendingEntriesInternalAsync(Guid accountGuid, CancellationToken token = default)
        {
            int balanceTypeInt = (int)EntryType.Balance;

            List<Entry> results = (await _EntryRepository.Query()
                .Where(e => e.AccountGUID == accountGuid)
                .Where(e => !e.IsCommitted)
                .Where(e => (int)e.Type != balanceTypeInt)
                .OrderByDescending(e => e.CreatedUtc)
                .ExecuteAsync(token).ConfigureAwait(false)).ToList();

            return results;
        }

        private async Task<Entry> GetLatestBalanceEntryInternalAsync(Guid accountGuid, CancellationToken token = default)
        {
            int balanceTypeInt = (int)EntryType.Balance;

            List<Entry> results = (await _EntryRepository.Query()
                .Where(e => e.AccountGUID == accountGuid)
                .Where(e => (int)e.Type == balanceTypeInt)
                .OrderByDescending(e => e.CreatedUtc)
                .Take(1)
                .ExecuteAsync(token).ConfigureAwait(false)).ToList();

            return results.FirstOrDefault();
        }

        private async Task<List<Entry>> GetEntriesByGuidsAsync(Guid accountGuid, List<Guid> guids, CancellationToken token = default)
        {
            List<Entry> results = new List<Entry>();

            foreach (Guid guid in guids)
            {
                List<Entry> entries = (await _EntryRepository.Query()
                    .Where(e => e.AccountGUID == accountGuid)
                    .Where(e => e.GUID == guid)
                    .ExecuteAsync(token).ConfigureAwait(false)).ToList();

                if (entries != null && entries.Count > 0)
                    results.AddRange(entries);
            }

            return results;
        }

        private async Task<DateTime?> GetCreatedUtcFromEntryGuidAsync(Guid? guid, CancellationToken token = default)
        {
            if (guid == null) return null;

            List<Entry> entries = (await _EntryRepository.Query()
                .Where(e => e.GUID == guid.Value)
                .Take(1)
                .ExecuteAsync(token).ConfigureAwait(false)).ToList();

            Entry entry = entries.FirstOrDefault();
            return entry != null ? entry.CreatedUtc : null;
        }

        private async Task<decimal?> GetAmountFromEntryGuidAsync(Guid guid, CancellationToken token = default)
        {
            List<Entry> entries = (await _EntryRepository.Query()
                .Where(e => e.GUID == guid)
                .Take(1)
                .ExecuteAsync(token).ConfigureAwait(false)).ToList();

            Entry entry = entries.FirstOrDefault();
            return entry != null ? entry.Amount : null;
        }

        #endregion

        #region Private-Commit-Methods

        private async Task<Balance> CommitEntriesInternalAsync(
            Guid accountGuid,
            List<Guid> guids,
            Balance balanceBefore,
            Entry balanceOld,
            CancellationToken token)
        {
            List<Guid> summarized = new List<Guid>();
            ITransaction transaction = null;

            try
            {
                transaction = await _EntryRepository.BeginTransactionAsync(token).ConfigureAwait(false);

                // Pre-fetch account using transaction connection
                Account account = null;
                List<Account> accountResults = (await _AccountRepository.Query(transaction)
                    .Where(a => a.GUID == accountGuid)
                    .Take(1)
                    .ExecuteAsync(token).ConfigureAwait(false)).ToList();
                account = accountResults.FirstOrDefault();

                // commit credits
                decimal committedCreditsTotal = 0m;
                if (balanceBefore.PendingCredits.Entries != null && balanceBefore.PendingCredits.Entries.Count > 0)
                {
                    foreach (Entry entry in balanceBefore.PendingCredits.Entries)
                    {
                        if (guids != null && guids.Count > 0 && !guids.Contains(entry.GUID)) continue;
                        summarized.Add(entry.GUID);
                        entry.IsCommitted = true;
                        entry.CommittedUtc = DateTime.Now.ToUniversalTime();
                        await _EntryRepository.UpdateAsync(entry, transaction, token).ConfigureAwait(false);
                        committedCreditsTotal += entry.Amount;
                    }
                }

                // commit debits
                decimal committedDebitsTotal = 0m;
                if (balanceBefore.PendingDebits.Entries != null && balanceBefore.PendingDebits.Entries.Count > 0)
                {
                    foreach (Entry entry in balanceBefore.PendingDebits.Entries)
                    {
                        if (guids != null && guids.Count > 0 && !guids.Contains(entry.GUID)) continue;
                        summarized.Add(entry.GUID);
                        entry.IsCommitted = true;
                        entry.CommittedUtc = DateTime.Now.ToUniversalTime();
                        await _EntryRepository.UpdateAsync(entry, transaction, token).ConfigureAwait(false);
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

                    balanceNew = await _EntryRepository.CreateAsync(balanceNew, transaction, token).ConfigureAwait(false);

                    // Update committed entries
                    foreach (Guid guid in summarized)
                    {
                        List<Entry> committedEntryList = (await _EntryRepository.Query(transaction)
                            .Where(e => e.GUID == guid)
                            .ExecuteAsync(token).ConfigureAwait(false)).ToList();

                        if (committedEntryList != null && committedEntryList.Count > 0)
                        {
                            Entry committedEntry = committedEntryList[0];
                            committedEntry.CommittedByGUID = balanceNew.GUID;
                            await _EntryRepository.UpdateAsync(committedEntry, transaction, token).ConfigureAwait(false);
                        }
                    }
                }

                await transaction.CommitAsync(token).ConfigureAwait(false);

                Balance balanceAfter = await GetBalanceAsync(accountGuid, false, token).ConfigureAwait(false);
                Task.Run(() => EntriesCommitted?.Invoke(this, new CommitEventArgs(account, balanceBefore, balanceAfter)));

                return balanceAfter;
            }
            catch
            {
                if (transaction != null)
                    await transaction.RollbackAsync(token).ConfigureAwait(false);
                throw;
            }
            finally
            {
                transaction?.Dispose();
            }
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

            if (_ConnectionFactory != null)
            {
                _ConnectionFactory.Dispose();
            }

            _Disposed = true;
            await Task.CompletedTask;
        }

        #endregion
    }
}
